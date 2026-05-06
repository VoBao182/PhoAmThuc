-- Migration: correct POI coordinates that were duplicated on the live map.
-- Safe to run repeatedly.

UPDATE poi
SET
    vido = 10.7614216,
    kinhdo = 106.7028293,
    diachi = '534 Vinh Khanh, Phuong 8, Quan 4, TP.HCM'
WHERE id = '11111111-1111-1111-1111-111111111111'
   OR lower(tenpoi) IN ('quan oc oanh', 'quán ốc oanh');

UPDATE poi
SET
    vido = 10.7607921,
    kinhdo = 106.7045826,
    diachi = '234 Vinh Khanh, Phuong 8, Quan 4, TP.HCM',
    mucuutien = CASE WHEN mucuutien = 1 THEN 8 ELSE mucuutien END
WHERE id = '58189b58-d700-4578-8399-1abb9204ab0a'
   OR lower(tenpoi) IN ('hao ky', 'hào ký');
