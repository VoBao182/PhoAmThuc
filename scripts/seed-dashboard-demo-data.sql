-- Seed demo customer analytics data for the CMS dashboard and BanDo page.
-- Safe to run repeatedly: only rows with mathietbi LIKE 'vkt-demo-%' are removed/recreated.
--
-- What this creates:
--   - 100 demo customers/devices.
--   - 90 customers with subscription/renewal history across thu/ngay/tuan/thang/nam.
--   - 10 customers with no subscription, still useful for the "no-sub" filter.
--   - Mixed active, expiring-soon, expired, online, offline, and at-POI states.
--   - VIEW/GPS activity spread across today, last 7/30 days, and last 12 months.
--   - Demo payment requests to make /DuyetThanhToan less empty while testing.

BEGIN;

DO $$
DECLARE
    active_poi_count integer;
BEGIN
    SELECT count(*)
    INTO active_poi_count
    FROM poi
    WHERE trangthai = true;

    IF active_poi_count = 0 THEN
        RAISE EXCEPTION 'Cannot seed dashboard demo data because table poi has no active rows.';
    END IF;
END $$;

DELETE FROM lichsuphat
WHERE mathietbi LIKE 'vkt-demo-%';

DELETE FROM vitrikhach
WHERE mathietbi LIKE 'vkt-demo-%';

DELETE FROM yeucauthanhtoan
WHERE mathietbi LIKE 'vkt-demo-%';

DELETE FROM dangkyapp
WHERE mathietbi LIKE 'vkt-demo-%';

WITH
poi_ranked AS (
    SELECT
        id,
        tenpoi,
        vido,
        kinhdo,
        row_number() OVER (ORDER BY mucuutien, tenpoi, id) AS rn,
        count(*) OVER () AS poi_count
    FROM poi
    WHERE trangthai = true
),
customers AS (
    SELECT
        n,
        format('vkt-demo-%s-%s', lpad(n::text, 3, '0'), substr(md5('customer-' || n::text), 1, 8)) AS device_id,
        CASE
            WHEN n <= 15 THEN now() + make_interval(days => (1 + (n % 7))::int)
            WHEN n <= 75 THEN now() + make_interval(days => (8 + ((n * 5) % 260))::int)
            WHEN n <= 90 THEN now() - make_interval(days => (1 + ((n * 3) % 120))::int)
            ELSE NULL
        END AS target_expiry,
        CASE
            WHEN n <= 90 THEN 1 + (n % 5)
            ELSE 0
        END AS paid_package_count,
        (SELECT max(poi_count) FROM poi_ranked) AS poi_count
    FROM generate_series(1, 100) AS g(n)
),
subscription_choices AS (
    SELECT
        c.n,
        c.device_id,
        c.target_expiry,
        c.paid_package_count,
        s.idx,
        CASE
            WHEN s.idx = 1 AND c.n % 6 = 0 THEN 'thu'
            WHEN (c.n + s.idx) % 11 = 0 THEN 'nam'
            WHEN (c.n + s.idx) % 5 = 0 THEN 'thang'
            WHEN (c.n + s.idx) % 3 = 0 THEN 'tuan'
            ELSE 'ngay'
        END AS loaigoi
    FROM customers c
    CROSS JOIN LATERAL generate_series(1, c.paid_package_count) AS s(idx)
    WHERE c.target_expiry IS NOT NULL
),
subscription_rows AS (
    SELECT
        sc.*,
        CASE sc.loaigoi
            WHEN 'thu' THEN 0::numeric
            WHEN 'ngay' THEN 29000::numeric
            WHEN 'tuan' THEN 99000::numeric
            WHEN 'thang' THEN 199000::numeric
            WHEN 'nam' THEN 999000::numeric
        END AS sotien,
        CASE sc.loaigoi
            WHEN 'thu' THEN 3
            WHEN 'ngay' THEN 1
            WHEN 'tuan' THEN 7
            WHEN 'thang' THEN 30
            WHEN 'nam' THEN 365
        END AS plan_days,
        CASE
            WHEN sc.idx = sc.paid_package_count AND sc.target_expiry >= now()
                THEN now() - make_interval(days => (1 + (sc.n % 45))::int, hours => (sc.n % 12)::int)
            WHEN sc.idx = sc.paid_package_count
                THEN sc.target_expiry - make_interval(days =>
                    CASE sc.loaigoi
                        WHEN 'thu' THEN 3
                        WHEN 'ngay' THEN 1
                        WHEN 'tuan' THEN 7
                        WHEN 'thang' THEN 30
                        WHEN 'nam' THEN 365
                    END)
            ELSE now() - make_interval(days => (((sc.paid_package_count - sc.idx) * 45) + 10 + ((sc.n + sc.idx) % 18))::int)
        END AS ngaybatdau
    FROM subscription_choices sc
),
insert_subscriptions AS (
    INSERT INTO dangkyapp (id, mathietbi, loaigoi, ngaybatdau, ngayhethan, sotien)
    SELECT
        gen_random_uuid(),
        device_id,
        loaigoi,
        ngaybatdau,
        CASE
            WHEN idx = paid_package_count THEN target_expiry
            ELSE ngaybatdau + (plan_days || ' days')::interval
        END,
        sotien
    FROM subscription_rows
    RETURNING 1
),
insert_locations AS (
    INSERT INTO vitrikhach (id, mathietbi, lat, lng, lancuoi_heartbeat, poiid_hientai, ten_poi_hientai)
    SELECT
        gen_random_uuid(),
        c.device_id,
        p.vido + (((c.n % 9) - 4) * 0.00012),
        p.kinhdo + (((c.n % 7) - 3) * 0.00012),
        CASE
            WHEN c.n <= 28 THEN now() - make_interval(secs => (5 + ((c.n * 3) % 105))::int)
            ELSE now() - make_interval(hours => (1 + (c.n % 96))::int, mins => ((c.n * 7) % 60)::int)
        END,
        CASE WHEN c.n <= 35 THEN p.id ELSE NULL END,
        CASE WHEN c.n <= 35 THEN p.tenpoi ELSE NULL END
    FROM customers c
    JOIN poi_ranked p
      ON p.rn = ((c.n - 1) % c.poi_count) + 1
    ON CONFLICT (mathietbi) DO UPDATE
    SET lat = excluded.lat,
        lng = excluded.lng,
        lancuoi_heartbeat = excluded.lancuoi_heartbeat,
        poiid_hientai = excluded.poiid_hientai,
        ten_poi_hientai = excluded.ten_poi_hientai
    RETURNING 1
),
view_events AS (
    SELECT
        c.n,
        c.device_id,
        p.id AS poi_id,
        k,
        now()
            - make_interval(days => (CASE WHEN k <= 3 THEN ((c.n + k) % 7) ELSE ((c.n * 5 + k * 3) % 360) END)::int)
            - make_interval(hours => ((c.n + k) % 18)::int, mins => ((c.n * k) % 53)::int) AS event_time,
        CASE c.n % 4
            WHEN 0 THEN 'vi'
            WHEN 1 THEN 'en'
            WHEN 2 THEN 'ko'
            ELSE 'zh'
        END AS language_code
    FROM customers c
    CROSS JOIN LATERAL generate_series(1, LEAST(c.poi_count, 1 + ((c.n * 7) % greatest(c.poi_count, 1)))) AS k
    JOIN poi_ranked p
      ON p.rn = ((c.n + k - 2) % c.poi_count) + 1
),
visit_events AS (
    SELECT
        c.n,
        c.device_id,
        p.id AS poi_id,
        k,
        now()
            - make_interval(days => (CASE WHEN k <= 2 THEN ((c.n + k + 2) % 7) ELSE ((c.n * 7 + k * 5) % 360) END)::int)
            - make_interval(hours => ((c.n + k + 4) % 20)::int, mins => ((c.n * k + 11) % 57)::int) AS event_time,
        CASE c.n % 4
            WHEN 0 THEN 'vi'
            WHEN 1 THEN 'en'
            WHEN 2 THEN 'ko'
            ELSE 'zh'
        END AS language_code
    FROM customers c
    CROSS JOIN LATERAL generate_series(1, LEAST(c.poi_count, 1 + ((c.n * 5) % greatest(c.poi_count, 1)))) AS k
    JOIN poi_ranked p
      ON p.rn = ((c.n + k - 1) % c.poi_count) + 1
),
insert_views AS (
    INSERT INTO lichsuphat (id, taikhoanid, poiid, thuyetminhid, thoigian, ngonngudung, nguon, mathietbi)
    SELECT
        gen_random_uuid(),
        NULL,
        poi_id,
        NULL,
        event_time,
        language_code,
        'VIEW',
        device_id
    FROM view_events
    RETURNING 1
),
insert_visits AS (
    INSERT INTO lichsuphat (id, taikhoanid, poiid, thuyetminhid, thoigian, ngonngudung, nguon, mathietbi)
    SELECT
        gen_random_uuid(),
        NULL,
        poi_id,
        NULL,
        event_time,
        language_code,
        'GPS',
        device_id
    FROM visit_events
    RETURNING 1
),
payment_requests AS (
    SELECT
        c.n,
        c.device_id,
        CASE
            WHEN c.n % 5 = 0 THEN 'nam'
            WHEN c.n % 3 = 0 THEN 'thang'
            WHEN c.n % 2 = 0 THEN 'tuan'
            ELSE 'ngay'
        END AS loaigoi,
        CASE
            WHEN c.n <= 8 THEN 'cho_duyet'
            WHEN c.n <= 24 THEN 'da_duyet'
            ELSE 'tu_choi'
        END AS trangthai
    FROM customers c
    WHERE c.n <= 30
),
insert_payment_requests AS (
    INSERT INTO yeucauthanhtoan (
        id,
        mathietbi,
        loaigoi,
        sotien,
        noidung_chuyen,
        trangthai,
        ghichu_admin,
        ngaytao,
        ngayduyet
    )
    SELECT
        gen_random_uuid(),
        device_id,
        loaigoi,
        CASE loaigoi
            WHEN 'ngay' THEN 29000::numeric
            WHEN 'tuan' THEN 99000::numeric
            WHEN 'thang' THEN 199000::numeric
            WHEN 'nam' THEN 999000::numeric
        END,
        'VKT ' || upper(loaigoi) || ' ' || upper(substr(device_id, 10, 6)),
        trangthai,
        CASE
            WHEN trangthai = 'tu_choi' THEN 'Demo: thong tin chuyen khoan khong khop'
            WHEN trangthai = 'da_duyet' THEN 'Demo: da duyet thanh toan'
            ELSE NULL
        END,
        now() - make_interval(days => (n % 28)::int, hours => (n % 12)::int),
        CASE WHEN trangthai = 'cho_duyet' THEN NULL ELSE now() - make_interval(days => (n % 24)::int, hours => (n % 8)::int) END
    FROM payment_requests
    RETURNING 1
)
SELECT
    (SELECT count(*) FROM customers) AS demo_customers,
    (SELECT count(*) FROM insert_subscriptions) AS subscription_rows,
    (SELECT count(*) FROM insert_locations) AS location_rows,
    (SELECT count(*) FROM insert_views) AS view_rows,
    (SELECT count(*) FROM insert_visits) AS visit_rows,
    (SELECT count(*) FROM insert_payment_requests) AS payment_request_rows;

COMMIT;

SELECT 'dangkyapp' AS table_name, count(*) AS rows
FROM dangkyapp
WHERE mathietbi LIKE 'vkt-demo-%'
UNION ALL
SELECT 'vitrikhach', count(*)
FROM vitrikhach
WHERE mathietbi LIKE 'vkt-demo-%'
UNION ALL
SELECT 'lichsuphat', count(*)
FROM lichsuphat
WHERE mathietbi LIKE 'vkt-demo-%'
UNION ALL
SELECT 'yeucauthanhtoan', count(*)
FROM yeucauthanhtoan
WHERE mathietbi LIKE 'vkt-demo-%'
ORDER BY table_name;
