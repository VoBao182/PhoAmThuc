-- Repair migration for the features added after v1.3.7.
-- Run this once in Supabase SQL Editor before deploying the API/CMS.

-- Keep device IDs consistent across app, payment, heartbeat, and CMS views.
UPDATE dangkyapp
SET mathietbi = lower(trim(regexp_replace(mathietbi, '^VKT-DEVICE:', '', 'i')))
WHERE mathietbi IS NOT NULL;

UPDATE yeucauthanhtoan
SET mathietbi = lower(trim(regexp_replace(mathietbi, '^VKT-DEVICE:', '', 'i')))
WHERE mathietbi IS NOT NULL;

UPDATE vitrikhach
SET mathietbi = lower(trim(regexp_replace(mathietbi, '^VKT-DEVICE:', '', 'i')))
WHERE mathietbi IS NOT NULL;

UPDATE lichsuphat
SET mathietbi = lower(trim(regexp_replace(mathietbi, '^VKT-DEVICE:', '', 'i')))
WHERE mathietbi IS NOT NULL;

-- Columns used by HeartbeatController and the CMS live-map/customer page.
ALTER TABLE vitrikhach
    ADD COLUMN IF NOT EXISTS poiid_hientai UUID,
    ADD COLUMN IF NOT EXISTS ten_poi_hientai TEXT;

ALTER TABLE lichsuphat
    ADD COLUMN IF NOT EXISTS mathietbi TEXT;

-- Older builds may have stored geofence visits with app-specific source names.
UPDATE lichsuphat
SET nguon = 'GPS'
WHERE upper(nguon) IN ('APP-GEOFENCE', 'APP_GEOFENCE', 'GEOFENCE');

-- Recreate the source constraint with the values the app/CMS understand.
ALTER TABLE lichsuphat DROP CONSTRAINT IF EXISTS lichsuphat_nguon_check;

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
    END IF;
END $$;

ALTER TABLE lichsuphat
    ADD CONSTRAINT lichsuphat_nguon_check
    CHECK (nguon IS NULL OR nguon IN ('GPS', 'QR', 'VIEW'));

-- If old heartbeat rows were duplicated before the unique index existed, keep
-- the latest row per normalized device so the API can upsert reliably.
WITH ranked AS (
    SELECT
        ctid,
        row_number() OVER (
            PARTITION BY lower(trim(mathietbi))
            ORDER BY lancuoi_heartbeat DESC, id DESC
        ) AS rn
    FROM vitrikhach
    WHERE mathietbi IS NOT NULL
)
DELETE FROM vitrikhach v
USING ranked r
WHERE v.ctid = r.ctid
  AND r.rn > 1;

CREATE UNIQUE INDEX IF NOT EXISTS idx_vitrikhach_mathietbi_unique
    ON vitrikhach(mathietbi);
CREATE INDEX IF NOT EXISTS idx_lichsuphat_mathietbi ON lichsuphat(mathietbi);
CREATE INDEX IF NOT EXISTS idx_lichsuphat_device_source_poi ON lichsuphat(mathietbi, nguon, poiid);
