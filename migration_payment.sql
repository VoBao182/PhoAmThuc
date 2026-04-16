-- Migration: Hệ thống phí duy trì và phí convert cho chủ quán
-- Chạy trên Supabase SQL Editor

-- 1. Thêm cột NgayHetHanDuyTri vào bảng poi
ALTER TABLE poi
    ADD COLUMN IF NOT EXISTS ngayhethanduytri TIMESTAMPTZ;

-- 2. Thêm cột poiid vào taikhoan (liên kết tài khoản quản lý với quán)
ALTER TABLE taikhoan
    ADD COLUMN IF NOT EXISTS poiid UUID REFERENCES poi(id) ON DELETE SET NULL;

-- 3. Bảng gói dịch vụ (lưu mức phí của từng POI)
CREATE TABLE IF NOT EXISTS dangkydichvu (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    poiid           UUID NOT NULL REFERENCES poi(id) ON DELETE CASCADE,
    phiduytrithang  NUMERIC(12,0) NOT NULL DEFAULT 50000,
    phiconvert      NUMERIC(12,0) NOT NULL DEFAULT 20000,
    ngaybatdau      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ngayhethan      TIMESTAMPTZ NOT NULL,
    trangthai       BOOLEAN NOT NULL DEFAULT TRUE
);
CREATE INDEX IF NOT EXISTS idx_dangkydichvu_poiid ON dangkydichvu(poiid);

-- 4. Bảng hóa đơn (loaiphi = 'duytri' hoặc 'convert')
CREATE TABLE IF NOT EXISTS hoadon (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    poiid           UUID NOT NULL REFERENCES poi(id) ON DELETE CASCADE,
    taikhoanid      UUID REFERENCES taikhoan(id) ON DELETE SET NULL,
    loaiphi         VARCHAR(20) NOT NULL DEFAULT 'duytri',
    sotien          NUMERIC(12,0) NOT NULL DEFAULT 0,
    ngaythanhtoan   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    kythanhtoan     VARCHAR(7),   -- 'yyyy-MM' cho duy trì; NULL cho convert
    ghichu          TEXT
);
CREATE INDEX IF NOT EXISTS idx_hoadon_poiid ON hoadon(poiid);
CREATE INDEX IF NOT EXISTS idx_hoadon_ky ON hoadon(kythanhtoan);

-- 5. Seed: gia hạn tạm 30 ngày cho tất cả POI hiện có (bỏ comment khi cần)
-- UPDATE poi SET ngayhethanduytri = NOW() + INTERVAL '30 days';
