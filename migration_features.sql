-- Migration: Tính năng 1 — Yêu cầu thanh toán qua QR + duyệt admin
-- Tính năng 3 — Theo dõi vị trí khách thực tế (heartbeat)
-- Chạy trên Supabase SQL Editor

-- ============================================================
-- 1. Bảng yêu cầu thanh toán gói app (do khách gửi từ app)
-- ============================================================
CREATE TABLE IF NOT EXISTS yeucauthanhtoan (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mathietbi       TEXT NOT NULL,
    loaigoi         TEXT NOT NULL DEFAULT 'thang',   -- 'ngay' | 'thang' | 'nam'
    sotien          NUMERIC(12,0) NOT NULL,
    noidung_chuyen  TEXT NOT NULL,                   -- mã nội dung chuyển khoản để khớp với sao kê
    trangthai       TEXT NOT NULL DEFAULT 'cho_duyet', -- 'cho_duyet' | 'da_duyet' | 'tu_choi'
    ghichu_admin    TEXT,                             -- lý do từ chối hoặc ghi chú của admin
    ngaytao         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ngayduyet       TIMESTAMPTZ                      -- NULL khi chưa xử lý
);

CREATE INDEX IF NOT EXISTS idx_yeucauthanhtoan_mathietbi  ON yeucauthanhtoan(mathietbi);
CREATE INDEX IF NOT EXISTS idx_yeucauthanhtoan_trangthai  ON yeucauthanhtoan(trangthai);
CREATE INDEX IF NOT EXISTS idx_yeucauthanhtoan_ngaytao    ON yeucauthanhtoan(ngaytao DESC);

-- ============================================================
-- 2. Bảng vị trí khách (heartbeat định vị thực tế)
-- ============================================================
CREATE TABLE IF NOT EXISTS vitrikhach (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mathietbi       TEXT NOT NULL UNIQUE,            -- mỗi thiết bị chỉ có 1 dòng (upsert)
    lat             DOUBLE PRECISION NOT NULL,
    lng             DOUBLE PRECISION NOT NULL,
    lancuoi_heartbeat TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    poiid_hientai   UUID,
    ten_poi_hientai TEXT
);

ALTER TABLE vitrikhach
    ADD COLUMN IF NOT EXISTS poiid_hientai UUID,
    ADD COLUMN IF NOT EXISTS ten_poi_hientai TEXT;

CREATE INDEX IF NOT EXISTS idx_vitrikhach_mathietbi ON vitrikhach(mathietbi);
CREATE INDEX IF NOT EXISTS idx_vitrikhach_lancuoi   ON vitrikhach(lancuoi_heartbeat DESC);
