-- Migration: Mở rộng CHECK constraint cột "nguon" trong bảng lichsuphat
-- để cho phép giá trị 'VIEW' (ghi nhận khi khách xem chi tiết POI)
-- Chạy trên Supabase SQL Editor

-- ── Bước 1: Xóa constraint cũ (thử theo tên mặc định trước) ──────────
ALTER TABLE lichsuphat DROP CONSTRAINT IF EXISTS lichsuphat_nguon_check;

-- ── Bước 2: Tìm và xóa nếu constraint có tên khác ────────────────────
DO $$
DECLARE
    v_name TEXT;
BEGIN
    SELECT conname INTO v_name
    FROM pg_constraint c
    JOIN pg_class t ON c.conrelid = t.oid
    WHERE t.relname = 'lichsuphat'
      AND c.contype = 'c'
      AND pg_get_constraintdef(c.oid) ILIKE '%nguon%';

    IF v_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE lichsuphat DROP CONSTRAINT %I', v_name);
        RAISE NOTICE 'Đã xóa constraint cũ: %', v_name;
    END IF;
END $$;

-- ── Bước 3: Tạo lại constraint với giá trị mở rộng ───────────────────
ALTER TABLE lichsuphat
    ADD CONSTRAINT lichsuphat_nguon_check
    CHECK (nguon IN ('GPS', 'QR', 'VIEW'));
