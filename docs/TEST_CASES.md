# Test Cases - VinhKhanhTour

Tai lieu nay bao phu test case chuc nang, bien, loi va hoi quy cho 3 thanh phan:

- Mobile App MAUI: subscription gate, POI, geofence, audio, thanh toan QR, ngon ngu, khoi phuc thiet bi.
- REST API ASP.NET Core: POI, thuyet minh, subscription, heartbeat, payment, upload, auth, log, health.
- CMS Razor Pages: dashboard, quan ly POI, duyet thanh toan, phi duy tri, ban do live.

## Quy Uoc

| Truong | Y nghia |
|---|---|
| ID | Ma test case duy nhat |
| Muc do | P0 = bat buoc, P1 = quan trong, P2 = mo rong |
| Loai | Positive, Negative, Boundary, Integration, UI, Performance, Security |
| Tien dieu kien | Du lieu/trang thai can co truoc khi test |
| Buoc thuc hien | Cac thao tac kiem thu |
| Ket qua mong doi | Ket qua dung can xac nhan |

## Du Lieu Test Chuan

| Nhom | Du lieu |
|---|---|
| Device moi | `TEST-DEVICE-NEW-001`, chua co ban ghi `dangkyapp` |
| Device da dung thu | `TEST-DEVICE-TRIAL-001`, da co `dangkyapp.LoaiGoi = thu` |
| Device dang active | `TEST-DEVICE-ACTIVE-001`, `NgayHetHan > now` |
| Device het han | `TEST-DEVICE-EXPIRED-001`, `NgayHetHan < now` |
| POI active | `TrangThai = true`, `NgayHetHanDuyTri >= now`, co toa do trong TP.HCM |
| POI bi an | `TrangThai = false` |
| POI het han | `TrangThai = true`, `NgayHetHanDuyTri < now` |
| POI chua dong phi | `NgayHetHanDuyTri = null` |
| File hop le | `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`, kich thuoc <= 5MB |
| File khong hop le | `.exe`, `.pdf`, file rong, file > 5MB |
| Toa do trong vung phuc vu | Lat 10.65-10.9, Lng 106.55-106.9 |
| Toa do ngoai vung phuc vu | Lat/Lng = 0 hoac ngoai khoang tren |

## Muc Tieu Kiem Thu Theo Diagram

Bo test case nay duoc cai thien theo cac so do trong `docs/diagrams`. Muc tieu khong chi la xac nhan chuc nang chay duoc, ma la tim loi do lech luong, sai trang thai, sai dieu huong, sai dong bo, sai giao dien, sai du lieu va sai cach he thong phan ung khi co loi.

| Diagram | Luong can bat loi | Test case chinh |
|---|---|---|
| `00-overall-usecase.puml` | Khong bo sot actor/use case: khach, admin, GPS, Maps, VietQR | `E2E-*`, `REG-*`, `DIAG-000-*` |
| `01-subscription-gate-activity/sequence.puml` | Startup, retry, gate subscription, dung thu, recovery code/QR, thoat modal stack | `APP-SUB-*`, `DIAG-001-*` |
| `02-poi-explore-activity/sequence.puml` | Tai POI, fallback seed data, tim kiem, map tab, view log, sync history/XP, recovery trong MainPage | `POI-*`, `APP-*`, `DIAG-002-*` |
| `03-geofence-audio-activity/sequence.puml` | GPS poll 5s, dwell 5s, heartbeat 10s, refresh POI 20s, POI priority/distance, cooldown 10 phut | `GPS-*`, `DIAG-003-*` |
| `04-poi-detail-activity/sequence.puml` | Detail fallback, audio WebView bridge, play/pause/stop/seek, TTS fallback, Maps | `TM-*`, `DIAG-004-*` |
| `05-paid-plan-activity/sequence.puml` | QR VietQR, copy noi dung, tao request sau nut da chuyen, polling 10s, approve/reject modal stack | `APP-SUB-*`, `CMS-PAY-*`, `DIAG-005-*` |
| `06-cms-poi-management-activity/sequence.puml` | Search/filter/sort POI, upload anh, free trial POI 30 ngay, mon an rong, concurrency | `CMS-POI-*`, `UP-*`, `DIAG-006-*` |
| `07-maintenance-payment-activity/sequence.puml` | Gia han tu han cu/now, tao hoa don tung ky, cap nhat goi dich vu | `FEE-*`, `DIAG-007-*` |
| `08-app-payment-approval-activity/sequence.puml` | PendingSnapshot, stats, filter, approve/reject trang thai hop le, retry Npgsql pool | `CMS-PAY-*`, `DIAG-008-*` |
| `09-live-map-activity/sequence.puml` | BanDo server-side, raw SQL timeout, toa do trong vung phuc vu, filter/sort, XP/level, badge | `MAP-*`, `DIAG-009-*` |
| `10-dashboard-activity/sequence.puml` | 11 mode range, 2 pha load POI/analytics, raw SQL chart/heatmap/revenue, retry/reset khi loi | `DASH-*`, `DIAG-010-*` |

## Diem Rui Ro Can Uu Tien Tim Loi

| ID | Muc do | Khu vuc | Dau hieu/rui ro | Cach bat loi bang test |
|---|---|---|---|---|
| RISK-001 | P1 | App GPS | Comment trong `MainPage.xaml.cs` con ghi heartbeat 15s/refresh 30s, trong khi constant va diagram la 10s/20s | Test `DIAG-003-004` va `DIAG-003-005` do thoi gian request thuc te; cap nhat tai lieu/code comment neu can |
| RISK-002 | P0 | Subscription | Lech modal stack sau khi approve/reject co the lam app quay lai Payment/SubscriptionPage sai | Test `DIAG-005-006`, `DIAG-005-007`, `APP-SUB-013` |
| RISK-003 | P0 | Payment approval | Admin bam approve/reject nhieu lan, 2 tab CMS mo dong thoi co the tao goi lap | Test `SUB-019`, `CMS-PAY-009`, `DIAG-008-005` |
| RISK-004 | P0 | Geofence | GPS jitter hoac 2 POI gan nhau co the phat sai quan/sai log visit | Test `GPS-007`, `GPS-010`, `DIAG-003-001..003` |
| RISK-005 | P1 | Offline/sync | App da luu viewed/visited local nhung sync-history bi fail lam CMS XP bi lech | Test `GPS-027`, `DIAG-002-005`, `DIAG-003-009` |
| RISK-006 | P1 | Dashboard/BanDo | Raw SQL timeout/disposed pool khong duoc hien thi ro tren UI | Test `MAP-010`, `DASH-012`, `DIAG-009-008`, `DIAG-010-010` |
| RISK-007 | P1 | CMS POI | POI moi khong co `NgayHetHanDuyTri` se bi an khoi app neu free trial 30 ngay khong duoc set | Test `CMS-POI-008`, `DIAG-006-004` |
| RISK-008 | P1 | Detail/audio | Audio WebView chua ready nhung user bam nghe ngay co the khong play | Test `DIAG-004-003`, `DIAG-004-004` |
| RISK-009 | P1 | Upload | Upload fail trong CMS co the lam mat form POI dang nhap | Test `UP-010`, `DIAG-006-002` |
| RISK-010 | P1 | Locale/UI | Text VI/EN/ZH dai, so tien/gia mon lon, badge map/dashboard co the tran layout | Test `APP-SUB-018`, `TM-016`, `DIAG-009-011`, `DIAG-010-012` |
| RISK-011 | P0 | Subscription API | Endpoint `/api/subscription/purchase` co the bi goi truc tiep voi goi tra phi, bo qua QR/admin duyet | Test `SUB-025`, `SEC-001`, doi chieu PRD F05 |
| RISK-012 | P0 | Rollover data | Khi noi tiep goi, `NgayHetHan` dung nhung `NgayBatDau` ban ghi moi van la `now`, lech PRD | Test `SUB-026`, `CMS-PAY-010`, `DIAG-008-003` |
| RISK-013 | P1 | Payment polling | PaymentStatusPage im lang khi API request status tra 404/500, co the cho vo han | Test `APP-SUB-019`, `DIAG-005-008` |
| RISK-014 | P1 | Recovery | Recovery code duoc ghi override truoc khi server xac nhan co goi active/lich su | Test `APP-016`, `DIAG-001-008`, `DIAG-002-008` |
| RISK-015 | P1 | Tai lieu | PRD/comment con lech voi code: heartbeat 15s vs 10s, POI null expiry, visit dedup, CMS refresh | Test `DOC-*`, cap nhat tai lieu truoc khi bao cao |

## A. Health, Cau Hinh Va Khoi Dong

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| ENV-001 | P0 | Positive | API dang chay | GET `/health` | HTTP 200, body co `status = ok` |
| ENV-002 | P0 | Positive | API ket noi DB dung | GET `/health/db` | HTTP 200, body co `database = connected` |
| ENV-003 | P0 | Negative | Sai hoac thieu connection string | Khoi dong API/CMS | Ung dung dung voi loi ro rang ve `SUPABASE_CONNECTION_STRING` |
| ENV-004 | P1 | Negative | DB khong truy cap duoc | GET `/health/db` | HTTP 503, title `Database connection failed` |
| ENV-005 | P1 | Positive | Bien moi truong `PORT` duoc set | Khoi dong API/CMS tren Render/local | Service bind dung `0.0.0.0:{PORT}` |
| ENV-006 | P1 | Positive | Supabase pooler port khac 5432 | Khoi dong API/CMS | Connection string duoc chinh ve port 5432, max pool size <= 6 |
| ENV-007 | P1 | Positive | Thu muc `wwwroot/uploads` chua ton tai | Khoi dong API/CMS | Thu muc upload duoc tao, static file hoat dong |
| ENV-008 | P0 | Positive | App cai moi, API reachable | Mo app | App vao `LaunchPage`, sau do dieu huong theo trang thai subscription |
| ENV-009 | P1 | Negative | App khong ket noi API | Mo app/chuyen API URL | App hien prompt cau hinh API, khong crash |
| ENV-010 | P1 | Positive | Da luu API URL hop le | Mo app lai | App dung URL da luu, khong hoi lai |

## B. Auth API

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| AUTH-001 | P0 | Positive | Co tai khoan active | POST `/api/auth/login` voi dung `TenDangNhap`, `MatKhau` | HTTP 200, tra ve user va `VaiTro` |
| AUTH-002 | P0 | Negative | Khong can | POST login thieu username | HTTP 400, message yeu cau nhap day du |
| AUTH-003 | P0 | Negative | Khong can | POST login thieu password | HTTP 400 |
| AUTH-004 | P0 | Negative | Username khong ton tai | POST login | HTTP 401, message username khong ton tai |
| AUTH-005 | P0 | Negative | Username ton tai | POST login sai password | HTTP 401, message password khong dung |
| AUTH-006 | P1 | Negative | Tai khoan `TrangThai = false` | POST login dung mat khau | HTTP 401 |
| AUTH-007 | P1 | Positive | Username moi | POST `/api/auth/register` mat khau >= 6 | HTTP 200, user moi co `VaiTro = khach` |
| AUTH-008 | P1 | Negative | Khong can | POST register thieu username/password | HTTP 400 |
| AUTH-009 | P1 | Boundary | Khong can | POST register password 5 ky tu | HTTP 400 |
| AUTH-010 | P1 | Negative | Username da ton tai | POST register trung username | HTTP 409 |

## C. Subscription API

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| SUB-001 | P0 | Positive | API dang chay | GET `/api/subscription/plans` | Tra ve 5 goi: `thu`, `ngay`, `tuan`, `thang`, `nam` dung gia va so ngay |
| SUB-002 | P0 | Positive | Device moi | GET `/api/subscription/status/{device}` | `CoDangKy = false`, `DaDungThu = false` |
| SUB-003 | P0 | Positive | Device co goi active | GET status | `CoDangKy = true`, co `NgayHetHan`, `SoNgayConLai >= 1` |
| SUB-004 | P0 | Positive | Device het han | GET status | `CoDangKy = false`, `DaDungThu` dung theo lich su |
| SUB-005 | P0 | Positive | Device moi | POST `/api/subscription/purchase` voi `LoaiGoi = thu` | HTTP 200, tao `dangkyapp`, het han sau 3 ngay, `MienPhi = true` |
| SUB-006 | P0 | Negative | Device da dung thu | POST purchase `thu` lan 2 | HTTP 400, khong tao them ban ghi |
| SUB-007 | P0 | Negative | Khong can | POST purchase thieu `MaThietBi` | HTTP 400 |
| SUB-008 | P0 | Negative | Khong can | POST purchase `LoaiGoi` khong ton tai | HTTP 400 |
| SUB-009 | P1 | Positive | Device co goi active het han sau 5 ngay | POST purchase goi tra phi `ngay` | Goi moi het han = han cu + 1 ngay |
| SUB-010 | P1 | Positive | Device het han | POST purchase goi tra phi | Het han = now + so ngay cua goi |
| SUB-011 | P0 | Positive | Device moi | POST `/api/subscription/request` voi `ngay` | Tao `yeucauthanhtoan`, `TrangThai = cho_duyet`, `NoiDungChuyen = VKT NGAY {shortId}` |
| SUB-012 | P0 | Positive | Device moi | POST request voi `tuan`, `thang`, `nam` | Tra dung so tien tung goi |
| SUB-013 | P0 | Negative | Khong can | POST request voi `LoaiGoi = thu` | HTTP 400, vi goi mien phi khong qua QR |
| SUB-014 | P0 | Negative | Khong can | POST request thieu `MaThietBi` | HTTP 400 |
| SUB-015 | P0 | Positive | Co yeu cau `cho_duyet` | GET `/api/subscription/request/{id}` | HTTP 200, trang thai `cho_duyet`, `NgayHetHan = null` |
| SUB-016 | P0 | Negative | GUID khong ton tai | GET request status | HTTP 404 |
| SUB-017 | P0 | Positive | Co yeu cau `cho_duyet` | POST `/api/subscription/approve/{id}` | Tao `DangKyApp`, yeu cau chuyen `da_duyet`, co `NgayDuyet` |
| SUB-018 | P0 | Boundary | Device dang co goi active | Approve yeu cau goi moi | Het han moi duoc noi tiep tu han cu, khong mat ngay con lai |
| SUB-019 | P0 | Negative | Y/c da duyet | POST approve lai cung id | HTTP 400, khong tao dang ky lan 2 |
| SUB-020 | P0 | Positive | Co yeu cau `cho_duyet` | POST `/api/subscription/reject/{id}` co ghi chu | Trang thai `tu_choi`, luu `GhiChuAdmin`, khong tao `DangKyApp` |
| SUB-021 | P0 | Negative | Y/c da duyet hoac da tu choi | POST reject | HTTP 400 |
| SUB-022 | P1 | Positive | Co nhieu yeu cau | GET `/api/subscription/requests` | Sap xep moi nhat truoc |
| SUB-023 | P1 | Positive | Co yeu cau 3 trang thai | GET `/api/subscription/requests?trangthai=cho_duyet` | Chi tra ve dung trang thai |
| SUB-024 | P1 | Boundary | `MaThietBi` co khoang trang/chu thuong | Tao request/status | Device id duoc normalize nhat quan |
| SUB-025 | P0 | Security/Negative | Device moi, khong co request QR | POST `/api/subscription/purchase` voi `LoaiGoi = thang` bang Postman/curl | API phai tu choi goi tra phi hoac chi cho `thu`; khong tao `DangKyApp` bo qua admin |
| SUB-026 | P0 | Data/Boundary | Device co goi active het han sau 5 ngay | Approve request goi `ngay` | Ban ghi moi co `NgayBatDau = han cu`, `NgayHetHan = han cu + 1 ngay`; khong dung `NgayBatDau = now` |
| SUB-027 | P2 | UX/Message | Khong can | POST purchase voi `LoaiGoi` sai | Message goi hop le phai liet ke du `thu, ngay, tuan, thang, nam` |

## D. Mobile App - Subscription Gate Va Thanh Toan

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| APP-SUB-001 | P0 | Positive | App cai moi, chua co `sub_ngay_het_han` | Mo app | Hien `SubscriptionPage` |
| APP-SUB-002 | P0 | Positive | `sub_ngay_het_han > now` | Mo app | Vao MainPage, tai POI va bat dau tracking |
| APP-SUB-003 | P0 | Boundary | `sub_ngay_het_han < now` | Mo app | Hien SubscriptionPage voi trang thai het han |
| APP-SUB-004 | P0 | Boundary | `sub_ngay_het_han` dung thoi diem hien tai | Mo app | Neu khong con lon hon now thi bi xem la het han |
| APP-SUB-005 | P1 | Negative | Preference ngay het han sai format | Mo app | Khong crash, chuyen sang SubscriptionPage |
| APP-SUB-006 | P0 | Positive | Device moi, API reachable | Bam dung thu | Goi purchase `thu`, luu het han va flag da dung thu, thoat gate |
| APP-SUB-007 | P0 | Negative | `da_dung_thu = true` | Mo SubscriptionPage | Nut dung thu bi disable hoac API tu choi, khong active lai |
| APP-SUB-008 | P0 | Positive | Chon goi tra phi | Bam goi `ngay/tuan/thang/nam` | Dieu huong sang `PaymentPage` |
| APP-SUB-009 | P0 | Positive | PaymentPage mo | Kiem tra QR va thong tin | Hien so tien, noi dung chuyen khoan, device short, QR VietQR |
| APP-SUB-010 | P0 | Positive | Da tao request QR | Bam "Da chuyen" | Mo `PaymentStatusPage`, bat dau polling moi 10 giay |
| APP-SUB-011 | P0 | Positive | Admin duyet yeu cau | Cho app polling | App luu `sub_ngay_het_han`, hien thanh cong, dong flow thanh toan |
| APP-SUB-012 | P0 | Negative | Admin tu choi yeu cau | Cho app polling | App hien trang thai bi tu choi/ghi chu, khong luu het han |
| APP-SUB-013 | P1 | Negative | Mang mat khi polling | PaymentStatusPage dang polling | Hien loi tam thoi, khong crash, co the thu lai |
| APP-SUB-014 | P1 | Positive | App bi dong khi dang cho duyet | Mo lai app | Vao gate neu chua duoc duyet; co the polling/kiem tra status lai |
| APP-SUB-015 | P1 | Positive | Co recovery code cua device active | Quet/nhap ma khoi phuc | App override device id, restore subscription tu API |
| APP-SUB-016 | P1 | Negative | Recovery code sai/khong co goi | Quet/nhap ma | App bao khong tim thay subscription active |
| APP-SUB-017 | P1 | Positive | Co flag da dung thu tren server | Restore status | App luu `da_dung_thu = true` de chan dung thu lai |
| APP-SUB-018 | P1 | UI | App ngon ngu EN/ZH | Mo SubscriptionPage/PaymentPage | Text doi theo ngon ngu, khong tran layout |
| APP-SUB-019 | P1 | Negative/UX | Request thanh toan bi xoa hoac API tra 404/500 | O PaymentStatusPage qua 2-3 chu ky polling | UI hien loi/cho retry hoac quay lai thanh toan; khong cho vo han im lang |
| APP-SUB-020 | P1 | Recovery/Negative | Recovery code dung format nhung device khong co goi active | Nhap tren SubscriptionPage | Khong ghi de device id cu vinh vien; thong bao ro khong tim thay goi active |

## E. POI API Va Danh Sach App

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| POI-001 | P0 | Positive | Co POI active con han | GET `/api/poi` | POI xuat hien trong danh sach |
| POI-002 | P0 | Negative | Co POI `TrangThai = false` | GET `/api/poi` | POI khong xuat hien |
| POI-003 | P0 | Negative | Co POI het han duy tri | GET `/api/poi` | POI khong xuat hien |
| POI-004 | P0 | Boundary | Co POI `NgayHetHanDuyTri = null` | GET `/api/poi` | POI khong xuat hien |
| POI-005 | P0 | Boundary | POI het han dung thoi diem now | GET `/api/poi` | Neu `NgayHetHanDuyTri >= now` thi con xuat hien |
| POI-006 | P0 | Positive | Co nhieu POI active | GET `/api/poi` | Sap xep tang dan theo `MucUuTien` |
| POI-007 | P0 | Positive | POI active co menu va thuyet minh | GET `/api/poi/{id}?lang=vi` | Tra thong tin POI, menu dang active, noi dung thuyet minh VI |
| POI-008 | P0 | Positive | POI co ban dich EN | GET detail `lang=en` | Tra noi dung EN |
| POI-009 | P1 | Boundary | POI khong co EN nhung co VI | GET detail `lang=en` | Fallback noi dung VI |
| POI-010 | P1 | Negative | POI active nhung khong co ban dich | GET detail | `NoiDungThuyetMinh = ""`, khong crash |
| POI-011 | P0 | Negative | ID khong ton tai | GET detail | HTTP 404 |
| POI-012 | P0 | Negative | POI bi an/het han | GET detail | HTTP 404 |
| POI-013 | P1 | Positive | POI co mon `TinhTrang = false` | GET detail | Mon an bi an khong nam trong `MonAns` |
| POI-014 | P1 | UI | App co danh sach POI | Mo MainPage | Hien anh, ten, dia chi, SDT, trang thai gan/xa |
| POI-015 | P1 | Positive | Danh sach co nhieu ten tieng Viet | Tim kiem co dau/khong dau | Loc dung ten POI theo normalize dau |
| POI-016 | P1 | Boundary | Ket qua tim kiem rong | Nhap tu khoa khong khop | Hien trang thai rong phu hop, khong crash |
| POI-017 | P1 | Negative | API `/api/poi` loi | Mo MainPage | Hien loi/tai du lieu mau neu co fallback, app khong crash |
| POI-018 | P1 | Positive | Anh POI la URL tuong doi | Mo danh sach | Anh resolve dung qua API base URL |
| POI-019 | P1 | Positive | Anh POI la URL ngoai | Mo danh sach | Anh hien thi tu URL ngoai |
| POI-020 | P1 | Negative | Anh hong | Mo danh sach | Hien fallback image, layout khong vo |

## F. Thuyet Minh, Audio, Ngon Ngu Va Chi Tiet POI

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| TM-001 | P0 | Positive | POI active co thuyet minh VI | GET `/api/thuyet-minh/{poiId}?lang=vi` | HTTP 200, co `NoiDung`, `NgonNgu = vi` |
| TM-002 | P0 | Positive | Co ban dich EN/ZH | GET voi `lang=en`, `lang=zh` | Tra dung ban dich |
| TM-003 | P1 | Boundary | Gui `lang=en-US` | GET thuyet minh | Normalize ve `en`, tra EN neu co |
| TM-004 | P1 | Boundary | Gui `lang` rong | GET thuyet minh | Mac dinh `vi` |
| TM-005 | P1 | Positive | Khong co ban dich ngon ngu chon nhung co VI | GET thuyet minh | Fallback VI |
| TM-006 | P0 | Negative | POI bi an/het han/null expiry | GET thuyet minh | HTTP 404 |
| TM-007 | P0 | Negative | POI active nhung chua co thuyet minh active | GET thuyet minh | HTTP 404, message chua co noi dung |
| TM-008 | P0 | Negative | Co thuyet minh nhung khong co ban dich | GET thuyet minh | HTTP 404 |
| TM-009 | P1 | Integration | DetailPage mo POI active | Bam POI tren MainPage | Goi detail, hien menu, thuyet minh, nut nghe, nut chi duong |
| TM-010 | P1 | Positive | DetailPage, co noi dung | Bam nut nghe | App phat TTS/audio dung ngon ngu |
| TM-011 | P1 | Negative | Khong co noi dung/audio | Bam nghe | Hien thong bao khong co audio/noi dung, khong crash |
| TM-012 | P1 | Positive | Doi ngon ngu trong DetailPage | Chon EN/ZH | Tai lai thuyet minh va cap nhat UI |
| TM-013 | P1 | Positive | POI co toa do | Bam chi duong | Mo Google Maps voi toa do POI |
| TM-014 | P1 | Positive | Mo DetailPage | Kiem tra log view | App gui `/api/heartbeat/view`, log `Nguon = VIEW` |
| TM-015 | P1 | Boundary | Mo DetailPage lien tiep trong 5 phut | Goi view nhieu lan | Chi ghi 1 log VIEW trong khoang dedup |
| TM-016 | P1 | UI | Menu co gia 0, gia lon, mo ta dai | Mo detail | Format gia/dong menu dung, khong tran layout |

## G. Geofence, GPS, Heartbeat Va Hanh Trinh

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| GPS-001 | P0 | Positive | App active subscription, GPS duoc cap quyen | Mo MainPage | App bat dau lay location dinh ky |
| GPS-002 | P0 | Negative | User tu choi quyen location | Mo MainPage | App hien thong bao/khong crash, cac chuc nang khac van dung |
| GPS-003 | P1 | Negative | GPS tra null | Mo MainPage | App thu last known location hoac bo qua lan do |
| GPS-004 | P0 | Positive | User o ngoai ban kinh moi POI | Cap nhat GPS | Khong tu phat thuyet minh |
| GPS-005 | P0 | Positive | User vao trong `BanKinh` POI | Cap nhat GPS | App goi thuyet minh, phat TTS/audio, ghi visit |
| GPS-006 | P0 | Boundary | User dung dung mep `BanKinh` | Cap nhat GPS | Duoc tinh la trong geofence neu `distance <= BanKinh` |
| GPS-007 | P1 | Boundary | GPS jitter quanh mep POI | Di chuyen vao/ra nhanh | Khong phat lap lai lien tuc nho dedup |
| GPS-008 | P0 | Boundary | Vua phat POI A xong trong 10 phut | Di ra/vao lai POI A | Khong phat lai trong 10 phut |
| GPS-009 | P0 | Positive | Sau hon 10 phut | Vao lai POI A | Duoc phep phat lai |
| GPS-010 | P1 | Positive | Co 2 POI gan nhau | Dung gan ca 2 | App chon POI gan nhat/logic hien tai, khong phat chong cheo |
| GPS-011 | P0 | Positive | App co location hop le | POST `/api/heartbeat` | Upsert `vitrikhach`, HTTP 200 `message = OK` |
| GPS-012 | P0 | Negative | Thieu `MaThietBi` | POST heartbeat | HTTP 400 |
| GPS-013 | P0 | Boundary | Lat/Lng = 0 | POST heartbeat | HTTP 200 `skipped = true`, reason `out_of_service_area`, khong upsert |
| GPS-014 | P0 | Boundary | Toa do ngoai vung phuc vu | POST heartbeat | HTTP 200 skipped |
| GPS-015 | P1 | Negative | Claim `PoiIdHienTai` khong ton tai | POST heartbeat | Upsert location, `PoiIdHienTai = null` |
| GPS-016 | P1 | Negative | Claim POI active nhung toa do ngoai ban kinh + tolerance | POST heartbeat | `PoiIdHienTai = null`, khong gan sai POI |
| GPS-017 | P1 | Positive | Claim POI active va toa do trong ban kinh + 5m tolerance | POST heartbeat | Luu `PoiIdHienTai`, `TenPoiHienTai` |
| GPS-018 | P0 | Positive | Device vua heartbeat | GET `/api/heartbeat/active` | Device nam trong danh sach active |
| GPS-019 | P0 | Boundary | Heartbeat cu hon 2 phut | GET active | Device khong nam trong active |
| GPS-020 | P1 | Positive | Device co subscription active | GET active | Item co `NgayHetHan`, `ConLaiNgay` |
| GPS-021 | P1 | Positive | Device co VIEW/GPS logs | GET active | Co `SoQuanDaXem`, `SoQuanDaGhe`, XP/level |
| GPS-022 | P0 | Positive | POST `/api/heartbeat/visit` lan dau | Gui `MaThietBi`, `PoiId` | HTTP 200 `recorded = true`, tao log |
| GPS-023 | P0 | Boundary | Gui visit cung POI trong 10 phut | POST visit | `recorded = false` |
| GPS-024 | P0 | Negative | Thieu `MaThietBi` hoac `PoiId = Guid.Empty` | POST visit | HTTP 400 |
| GPS-025 | P0 | Positive | POST `/api/heartbeat/view` lan dau | Gui `MaThietBi`, `PoiId` | `recorded = true`, `Nguon = VIEW` |
| GPS-026 | P0 | Boundary | Gui view cung POI trong 5 phut | POST view | `recorded = false` |
| GPS-027 | P1 | Positive | App co viewed/visited local | POST `/api/heartbeat/sync-history` | Chen cac log con thieu, tra `insertedViews`, `insertedVisits` |
| GPS-028 | P1 | Boundary | Sync list co duplicate va `Guid.Empty` | POST sync-history | Bo duplicate/empty, chi chen hop le |
| GPS-029 | P1 | Positive | Device co lich su | GET `/api/heartbeat/profile/{maThietBi}` | Tra viewed/visited ids, count, XP, level |
| GPS-030 | P1 | Positive | Co device active va logs 4h gan nhat | GET `/api/heartbeat/history/{deviceShort}` | Tra lich su sap xep moi truoc, so diem da ghe |
| GPS-031 | P1 | Negative | `deviceShort` khong khop | GET history | HTTP 404 |

## H. Log API

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| LOG-001 | P1 | Positive | Co POI | POST `/api/log` day du | HTTP 200, `success = true`, tao `lichsuphat` |
| LOG-002 | P1 | Boundary | Thieu `MaThietBi` | POST log | Van luu log voi `MaThietBi = null`, neu DB cho phep |
| LOG-003 | P1 | Boundary | Thieu `ThoiGian` | POST log | Dung `DateTime.UtcNow` |
| LOG-004 | P1 | Boundary | `NgonNguDung` rong/sai | POST log | Duoc normalize ve mac dinh hop le |
| LOG-005 | P1 | Boundary | `Nguon` rong/sai format | POST log | Duoc normalize, khong crash |
| LOG-006 | P1 | Negative | DB interrupted/disposed | POST log | HTTP 200 `skipped = true` hoac 500 neu loi khac |

## I. Upload API Va Upload Anh CMS

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| UP-001 | P0 | Positive | File `.jpg` <= 5MB | POST multipart `/api/upload` field `file` | HTTP 200, tra `url = /uploads/{guid}.jpg`, file ton tai |
| UP-002 | P0 | Positive | File `.png`, `.webp`, `.gif`, `.jpeg` <= 5MB | Upload tung file | Tat ca duoc chap nhan |
| UP-003 | P0 | Negative | Khong gui file | POST upload | HTTP 400 |
| UP-004 | P0 | Negative | File rong | POST upload | HTTP 400 |
| UP-005 | P0 | Boundary | File dung 5MB | POST upload | Duoc chap nhan neu `Length <= 5MB` |
| UP-006 | P0 | Boundary | File > 5MB | POST upload | HTTP 400, message file qua lon |
| UP-007 | P0 | Negative | File `.exe`/`.pdf` | POST upload | HTTP 400, message dinh dang khong ho tro |
| UP-008 | P1 | Security | File ten co path traversal | Upload file name `..\a.jpg` | Server bo qua ten goc, luu ten GUID an toan |
| UP-009 | P1 | Positive | CMS Create/Edit POI | Upload anh dai dien | Input anh duoc cap nhat URL upload, preview hien dung |
| UP-010 | P1 | Negative | API upload down | Upload trong CMS | UI bao loi, khong mat du lieu form da nhap |

## J. CMS - Quan Ly POI

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| CMS-POI-001 | P0 | Positive | Co nhieu POI | Mo `/Poi` | Hien danh sach POI, menu count, trang thai, han duy tri |
| CMS-POI-002 | P1 | Positive | Co POI ten/dia chi/SDT khop | Search tren `/Poi` | Loc theo ten, dia chi hoac SDT |
| CMS-POI-003 | P1 | Boundary | Search rong | Submit search rong | Hien tat ca POI |
| CMS-POI-004 | P1 | Positive | Co POI visible/hidden | Filter `status=visible/hidden` | Chi hien dung nhom |
| CMS-POI-005 | P1 | Positive | Co POI active/expired/no-expiry | Filter expiry/status tuong ung | Chi hien dung nhom |
| CMS-POI-006 | P1 | Positive | Co POI sap xep khac nhau | Sort name/expiry/menu/status/priority asc/desc | Thu tu dung, tie-break theo ten |
| CMS-POI-007 | P0 | Positive | Form Create hop le | Tao POI moi voi ten, dia chi, toa do, ban kinh, uu tien | Luu POI, redirect `/Poi`, hien success |
| CMS-POI-008 | P0 | Boundary | Create khong nhap `NgayHetHanDuyTri` | Tao POI | Tu set han dung thu 30 ngay, POI co the xuat hien tren app |
| CMS-POI-009 | P1 | Positive | Create co thuyet minh VI/EN/ZH | Tao POI | Tao `ThuyetMinh` va cac `BanDich` dung ngon ngu |
| CMS-POI-010 | P1 | Boundary | Create bo trong thuyet minh | Tao POI | Van tao POI, thuyet minh khong co ban dich |
| CMS-POI-011 | P1 | Positive | Create co nhieu mon an | Tao POI | Chi luu mon co `TenMonAn`, don gia null thanh 0 |
| CMS-POI-012 | P0 | Negative | Form POI thieu truong required theo model | Submit Create/Edit | O lai trang, hien validation |
| CMS-POI-013 | P0 | Positive | POI ton tai | Mo `/Poi/Edit?id={id}` | Load dung POI, menu active, thuyet minh active |
| CMS-POI-014 | P0 | Positive | Edit POI | Sua ten, dia chi, SDT, toa do, ban kinh, uu tien, trang thai | Luu thay doi, redirect edit, hien success |
| CMS-POI-015 | P1 | Positive | Edit ban dich co san | Sua VI/EN/ZH | Upsert dung ban dich |
| CMS-POI-016 | P1 | Positive | POI chua co thuyet minh | Edit them noi dung | Tao `ThuyetMinh` moi va `BanDich` |
| CMS-POI-017 | P1 | Positive | Menu co mon cu | Xoa dong mon khoi form | Mon cu duoc set `TinhTrang = false` |
| CMS-POI-018 | P1 | Positive | Menu co mon cu | Sua ten/gia/phan loai/hinh | Mon duoc update |
| CMS-POI-019 | P1 | Positive | Menu form co dong moi | Them mon moi co ten | Tao `MonAn` moi active |
| CMS-POI-020 | P1 | Boundary | Menu form co dong moi rong | Submit edit | Bo qua dong rong, khong validation sai |
| CMS-POI-021 | P0 | Positive | POI active | Bam toggle an | `TrangThai = false`, POI bien mat khoi API app |
| CMS-POI-022 | P0 | Positive | POI hidden con han | Bam toggle hien | `TrangThai = true`, POI xuat hien lai tren API app |
| CMS-POI-023 | P0 | Negative | ID edit/toggle khong ton tai | Mo edit/toggle | Edit tra 404; toggle redirect khong crash |
| CMS-POI-024 | P1 | Negative | DB loi khi create/edit | Submit form | Hien thong bao loi ket noi DB, giu input de sua |
| CMS-POI-025 | P1 | Boundary | Concurrency update | Hai admin sua cung POI | Loi concurrency duoc bao va yeu cau reload |

## K. CMS - Duyet Thanh Toan App

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| CMS-PAY-001 | P0 | Positive | Co yeu cau `cho_duyet` | Mo `/DuyetThanhToan` | Tab cho duyet hien yeu cau moi nhat truoc |
| CMS-PAY-002 | P1 | Positive | Co yeu cau 3 trang thai | Chuyen tab `cho_duyet`, `da_duyet`, `tu_choi` | Moi tab chi hien dung trang thai |
| CMS-PAY-003 | P1 | Positive | Co nhieu goi | Filter package `ngay/tuan/thang/nam` | Chi hien dung loai goi |
| CMS-PAY-004 | P1 | Positive | Co device/noi dung CK khop | Search | Loc theo `MaThietBi` hoac `NoiDungChuyen` |
| CMS-PAY-005 | P1 | Positive | Co thong ke du lieu | Mo trang | Stats so cho duyet/da duyet/tu choi/tien dung toan bo DB |
| CMS-PAY-006 | P1 | Positive | Co pending | GET handler `?handler=PendingSnapshot` | JSON co `Count`, `LatestId`, `LatestTransferContent` |
| CMS-PAY-007 | P0 | Positive | Co request `cho_duyet` | Bam Duyet | Tao `DangKyApp`, request sang `da_duyet`, hien message het han |
| CMS-PAY-008 | P0 | Boundary | Device dang active | Duyet goi moi | Han moi noi tiep tu han cu |
| CMS-PAY-009 | P0 | Negative | Request da duyet | Bam Duyet lai bang POST | Redirect voi loi request da o trang thai `da_duyet` |
| CMS-PAY-010 | P0 | Positive | Co request `cho_duyet` | Bam Tu choi, nhap ly do | Trang thai `tu_choi`, luu ly do |
| CMS-PAY-011 | P1 | Boundary | Ly do tu choi rong | Tu choi | Van tu choi, `GhiChuAdmin` rong/null theo form |
| CMS-PAY-012 | P0 | Negative | Request khong ton tai | POST approve/reject | Redirect voi loi khong tim thay |
| CMS-PAY-013 | P1 | Negative | DB disposed transient | Mo trang/snapshot | Trang retry/clear pool hoac tra 503 snapshot, khong crash |
| CMS-PAY-014 | P1 | UI | Danh sach > 200 request | Mo trang | Chi hien toi da 200 request moi nhat |
| CMS-PAY-015 | P1 | Integration | App dang polling request | Admin duyet tren CMS | App nhan `da_duyet` trong lan polling tiep theo |

## L. Payment API Va CMS Phi Duy Tri POI

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| FEE-001 | P0 | Positive | POI ton tai co goi dich vu active | GET `/api/payment/status/{poiId}` | Tra han duy tri, `PhiDuyTriThang`, `PhiConvert` theo goi |
| FEE-002 | P0 | Positive | POI khong co goi dich vu | GET status | Tra default 50,000 va 20,000 |
| FEE-003 | P0 | Negative | POI khong ton tai | GET status | HTTP 404 |
| FEE-004 | P0 | Positive | POI ton tai | GET `/api/payment/history/{poiId}` | Tra hoa don sap xep moi nhat truoc |
| FEE-005 | P0 | Positive | POI het han | POST `/api/payment/maintenance` `SoThangGiaHan = 1` | Tao 1 hoa don, han moi = now + 1 thang, `TrangThai = true` |
| FEE-006 | P0 | Boundary | POI con han 10 ngay | Record 1 thang | Han moi = han cu + 1 thang |
| FEE-007 | P0 | Boundary | `SoThangGiaHan = 3` | Record maintenance | Tao 3 hoa don, tong tien = don gia * 3 |
| FEE-008 | P0 | Negative | `SoThangGiaHan <= 0` | POST maintenance | HTTP 400 |
| FEE-009 | P0 | Negative | POI khong ton tai | POST maintenance | HTTP 404 |
| FEE-010 | P1 | Positive | DangKyDichVu active co don gia custom | POST maintenance | Hoa don dung don gia custom |
| FEE-011 | P1 | Positive | POI con han duy tri | POST `/api/payment/convert/{poiId}` | Tao hoa don `LoaiPhi = convert`, `CanConvert = true` |
| FEE-012 | P0 | Negative | POI het han/null expiry | POST convert | HTTP 400, yeu cau gia han truoc |
| FEE-013 | P0 | Negative | POI khong ton tai | POST convert | HTTP 404 |
| FEE-014 | P1 | Positive | Co POI het han/null expiry | GET `/api/payment/overdue` | Tra danh sach qua han, `SoNgayQuaHan = -1` neu null |
| FEE-015 | P0 | Positive | Mo CMS `/ThanhToan` | Xem danh sach | Hien trang thai han cua tung POI |
| FEE-016 | P0 | Positive | POI ton tai | Mo `/ThanhToan/GhiNhan?poiId={id}` | Load ten, dia chi, han, don gia, lich su gan day |
| FEE-017 | P0 | Positive | Ghi nhan 1 thang tren CMS | Submit form hop le | Tao/Cap nhat `DangKyDichVu`, tao hoa don, redirect voi message |
| FEE-018 | P0 | Boundary | Ghi nhan nhieu thang tren CMS | Submit `soThangGiaHan = 6` | Tao 6 hoa don tung ky `yyyy-MM` |
| FEE-019 | P0 | Negative | Submit `soThangGiaHan = 0` | POST CMS | O lai trang, hien loi so thang |
| FEE-020 | P1 | Positive | Doi `phiDuyTriThang` tren CMS | Submit | Cap nhat don gia goi dich vu va hoa don theo gia moi |
| FEE-021 | P1 | Positive | Co hoa don | Mo `/ThanhToan/LichSu?poiId={id}` | Hien lich su dung POI |

## M. CMS - Ban Do Live Va Dashboard

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| MAP-001 | P0 | Positive | Co POI va device active | Mo `/BanDo` | Hien ban do Leaflet, marker POI va dot khach |
| MAP-002 | P1 | Positive | Co POI active/con han | Mo ban do | Marker POI the hien trang thai active |
| MAP-003 | P1 | Positive | Co POI het han | Mo ban do | Marker/canh bao the hien qua han theo UI hien tai |
| MAP-004 | P0 | Positive | Device heartbeat trong 2 phut | Mo ban do | Khach hien online tai toa do moi nhat |
| MAP-005 | P0 | Boundary | Device khong heartbeat > 2 phut | Mo ban do | Khach khong con hien active |
| MAP-006 | P1 | Positive | Device dang o POI verified | Hover/click marker | Tooltip/popup hien device short, POI hien tai, so da ghe/xem, han con lai |
| MAP-007 | P1 | Positive | Device co history | Click device | Goi history va hien danh sach POI 4h gan nhat |
| MAP-008 | P1 | Negative | History API 404 | Click device khong co history | UI hien rong/thong bao phu hop, khong crash |
| MAP-009 | P1 | Performance | Ban do dang mo | Cho 10-20 giay | Auto-refresh theo `ViewData["AutoRefreshSeconds"] = 10`, marker/bang cap nhat khong nhan doi va khong mat filter |
| DASH-001 | P0 | Positive | Co du lieu demo | Mo `/` dashboard | Hien tong POI, so quan qua han, doanh thu/luot xem theo filter |
| DASH-002 | P1 | Positive | Dung filter ngay/tuan/thang/custom | Doi range tren dashboard | So lieu va bieu do cap nhat dung khoang thoi gian |
| DASH-003 | P1 | Boundary | Custom date rong/sai thu tu | Submit | UI xu ly an toan, khong crash |
| DASH-004 | P1 | Positive | Khong co du lieu | Mo dashboard | Hien 0/empty state hop ly |
| DASH-005 | P1 | Negative | DB loi | Mo dashboard | Hien thong bao loi, khong crash |

## N. App - Cau Hinh, Device Identity, Recovery, UI

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| APP-001 | P0 | Positive | Chua co device id | Mo app | Tao device id moi va luu Preferences |
| APP-002 | P1 | Positive | Co stable platform id | Mo app | Dung stable id neu hop le |
| APP-003 | P1 | Positive | Co override recovery code | Set override | Device id trong app chuyen sang override |
| APP-004 | P1 | Boundary | Recovery code co khoang trang/chu thuong | Nhap/quyet recovery | Normalize thanh id hop le |
| APP-005 | P1 | Negative | Recovery code rong/sai format | Nhap recovery | Bao loi, khong doi id |
| APP-006 | P1 | Positive | Da xem/da ghe local | Mo app co API | Sync/restore profile, cap nhat count va XP |
| APP-007 | P1 | Positive | Doi ngon ngu VI/EN/ZH | Doi setting trong app | Text chinh doi ngon ngu, API thuyet minh dung lang |
| APP-008 | P1 | Boundary | Text EN/ZH dai | Kiem tra cac trang mobile nho | Khong bi cat/tran nut/card |
| APP-009 | P1 | Positive | App background/foreground | Chuyen app ra nen roi mo lai | Tracking/polling tiep tuc dung, khong tao nhieu loop |
| APP-010 | P1 | Negative | Mat mang khi tai POI/detail | Thao tac app | Hien loi/retry/fallback, khong crash |
| APP-011 | P1 | Positive | Nut chi duong tren MainPage/DetailPage | Bam | Mo browser/maps voi toa do hoac query dung |
| APP-012 | P1 | Positive | QR scanner modal | Mo va dong scanner | Modal dong dung, khong ket stack navigation |
| APP-013 | P1 | Boundary | Nhieu modal Payment/Subscription | Dong flow sau approve/cancel | Stack navigation sach, khong con trang payment treo |
| APP-014 | P1 | UI | Android release khong co API baked in | Mo app | Hien canh bao cau hinh ro rang theo release checklist |
| APP-015 | P2 | UI | Windows va Android | Smoke test cung luong | UI va dieu huong nhat quan tren 2 nen tang |
| APP-016 | P1 | Recovery/Negative | Dang dung device A, nhap recovery code dung format cua device B khong ton tai/server down | Nhap/scan recovery trong Cai Dat | App khong bao "da khoi phuc" neu sync fail; device id khong bi doi sang ma chua xac thuc |

## O. Tich Hop End-To-End

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| E2E-001 | P0 | Integration | DB sach cho device moi | Cai app -> dung thu -> vao MainPage | Trial active 3 ngay, app tai POI |
| E2E-002 | P0 | Integration | Device moi | Chon goi thang -> hien QR -> admin duyet -> app polling | App active goi thang, request da duyet, DangKyApp tao dung |
| E2E-003 | P0 | Integration | Device moi | Chon goi ngay -> admin tu choi | App khong active, request co ly do tu choi |
| E2E-004 | P0 | Integration | Co POI active | User vao geofence -> app phat audio -> CMS ban do | Log visit tao, CMS hien device da ghe POI |
| E2E-005 | P0 | Integration | Co POI active | User mo detail -> nghe -> chi duong | Detail load dung, log VIEW tao, maps mo dung |
| E2E-006 | P0 | Integration | Admin tao POI moi tren CMS | Tao POI co menu/thuyet minh/anh | API `/api/poi` va app hien POI moi |
| E2E-007 | P0 | Integration | POI het han | Admin ghi nhan phi duy tri | POI xuat hien lai tren API/app |
| E2E-008 | P1 | Integration | Device cai lai app | Khoi phuc bang recovery code | Subscription va lich su/XP duoc restore |
| E2E-009 | P1 | Integration | API deployed public | Build APK voi `HostedApiBaseUrl` | APK cai tren dien thoai khong can USB/localhost |
| E2E-010 | P1 | Integration | Nhieu device active | 3-5 device gui heartbeat | CMS map hien dung tat ca, khong nham device/POI |

## P. Hoi Quy Chinh Can Chay Truoc Release

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| REG-001 | P0 | Regression | API/CMS/App moi build | Chay health, login/register, POI list/detail | Khong loi P0 |
| REG-002 | P0 | Regression | DB co du lieu | Chay flow trial va paid approval | Subscription van hoat dong |
| REG-003 | P0 | Regression | Co POI active | Test geofence visit va detail view | Log, XP, CMS active dung |
| REG-004 | P0 | Regression | Co POI | Test CMS create/edit/toggle/upload | POI CRUD khong hoi quy |
| REG-005 | P0 | Regression | Co POI het han | Test maintenance payment | Gia han va hoa don dung |
| REG-006 | P1 | Regression | Co request cho duyet | Test CMS pending snapshot | Snapshot dung, khong loi transient |
| REG-007 | P1 | Regression | Android release | Cai APK tren may that | Mo app, cau hinh API, tai POI, thanh toan OK |
| REG-008 | P1 | Regression | Mang yeu/on-off | Thu cac luong tai POI/payment/audio | App khong crash va co thong bao loi |

## Q. Phi Chuc Nang

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| NFR-001 | P1 | Performance | DB co >= 100 POI | GET `/api/poi` 20 lan | p95 < 500ms trong dieu kien local/staging on dinh |
| NFR-002 | P1 | Performance | Co >= 50 active devices | GET `/api/heartbeat/active` | Tra ket qua trong muc chap nhan, khong timeout |
| NFR-003 | P1 | Performance | CMS map mo 10 phut | Theo doi auto-refresh 10s tren `/BanDo` | Khong leak UI ro rang, marker/bang khong nhan doi bat thuong |
| NFR-004 | P1 | Reliability | API bi restart khi app dang polling | Restart API | App phuc hoi sau khi API len lai |
| NFR-005 | P1 | Reliability | DB pool cham/loi transient | Goi CMS DuyetThanhToan | Retry/clear pool theo code, UI khong crash |
| NFR-006 | P1 | Security | Upload file doc hai doi duoi `.exe` | POST upload | Bi chan theo extension |
| NFR-007 | P1 | Security | Thu truy cap `/uploads/{file}` | GET static file | Chi file da upload duoc phuc vu, khong doc file ngoai uploads |
| NFR-008 | P1 | Security | Login/register plain text demo | Review release | Ghi nhan rui ro: production can BCrypt/JWT, demo chap nhan neu trong pham vi |
| NFR-009 | P1 | Privacy | App khong nhap PII | Kiem tra DB/log | Chi luu device id, location, history; khong yeu cau ten/email khach app |
| NFR-010 | P1 | Accessibility | Mobile small screen | Kiem tra cac nut/form quan trong | Text doc duoc, nut bam duoc, khong bi che |

## Q2. Bao Mat Va Sai Lech Tai Lieu

| ID | Muc do | Loai | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|
| SEC-001 | P0 | Security | API public, khong dang nhap | Goi truc tiep `POST /api/subscription/purchase` voi `LoaiGoi = nam` | Khong duoc active goi tra phi; phai yeu cau luong QR/admin duyet |
| SEC-002 | P1 | Security | Co recovery code do nguoi khac cung cap | Nhap code tren app | Chi chap nhan khi server xac nhan device co goi/lich su hop le; khong cho chiem device id bang format doan duoc |
| DOC-001 | P1 | Documentation | PRD va code hien tai | Doi chieu F03/NFR voi constant app | Tai lieu thong nhat heartbeat 10s va refresh POI 20s, hoac code doi ve 15s/30s neu do la yeu cau that |
| DOC-002 | P1 | Documentation | PRD va code hien tai | Doi chieu F02 POI filter | PRD thong nhat viec POI `NgayHetHanDuyTri = null` bi an hay duoc hien |
| DOC-003 | P1 | Documentation | PRD va code hien tai | Doi chieu visit/view dedup | PRD ghi ro visit GPS 10 phut, view detail 5 phut neu giu theo code |
| DOC-004 | P1 | Documentation | PRD va code hien tai | Doi chieu CMS map/dashboard refresh | PRD thong nhat auto-refresh 10s theo code hoac code doi ve 30s |
| DOC-005 | P2 | Documentation | Review model/comment auth | Kiem tra `TaiKhoan.MatKhau` va `AuthController` | Comment khong noi BCrypt hash neu demo dang luu plain text |

## R. Test Case Bo Sung Theo Diagram De Tim Loi An

### R0. Overall Use Case

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-000-001 | P0 | Coverage | `00-overall-usecase` | Khong can | Doi chieu tung use case F01-F10 voi menu/app/API/CMS | Khong co use case nao trong diagram bi thieu man hinh, API hoac test case |
| DIAG-000-002 | P0 | E2E | `00-overall-usecase` | API/CMS/App chay | Chay luong khach: active goi -> xem POI -> vao geofence -> nghe -> xem detail -> maps -> thanh toan gia han | Du lieu di qua dung App/API/DB/CMS, khong co trang thai bi mat |
| DIAG-000-003 | P0 | E2E | `00-overall-usecase` | Admin co quyen CMS | Chay luong admin: tao POI -> upload -> xem app -> ghi phi -> duyet thanh toan -> xem ban do/dashboard | Tat ca module lien ket dung, khong phai sua DB thu cong |

### R1. F01 - Startup, Subscription Gate, Recovery

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-001-001 | P0 | UI/Negative | `01-subscription-gate-activity` | Gay exception trong startup route hoac cau hinh API loi | Mo app | BootIndicator tat, nut Retry hien, bam Retry goi lai route khong crash |
| DIAG-001-002 | P0 | Navigation | `01-subscription-gate-activity` | `sub_ngay_het_han` active | Mo app tu LaunchPage | LaunchPage bi remove, MainPage la trang hien tai, back khong quay ve LaunchPage |
| DIAG-001-003 | P0 | Navigation | `01-subscription-gate-activity` | Subscription het han | Mo app | SubscriptionPage nhan `hetHan = true`, gate khong cho vao F02/F03 |
| DIAG-001-004 | P0 | Recovery | `01-subscription-gate-sequence` | Co recovery payload hop le cua device active | Copy/paste recovery code tren SubscriptionPage | Device override duoc luu, status server duoc restore, gate thoat ve MainPage |
| DIAG-001-005 | P1 | Recovery | `01-subscription-gate-sequence` | Camera permission bi deny | Bam scan QR recovery | Hien thong bao can quyen camera, khong mo scanner rong/khong crash |
| DIAG-001-006 | P1 | Recovery/Negative | `01-subscription-gate-sequence` | Recovery payload sai prefix/sai checksum/sai GUID | Nhap/paste/scan | Hien trang thai ma khong hop le, khong doi `MaThietBi` |
| DIAG-001-007 | P1 | UI | `01-subscription-gate-activity` | Doi ngon ngu VI/EN/ZH | Mo SubscriptionPage | Recovery card, trial button, API guide, loi ket noi hien dung ngon ngu va khong tran |
| DIAG-001-008 | P1 | Recovery/Negative | `01-subscription-gate-sequence` | Recovery code dung format nhung server khong co goi active | Nhap tren SubscriptionPage | Khong doi device override neu restore fail; hien thong bao khong co goi active |

### R2. F02 - POI Explore, Map Tab, Sync History

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-002-001 | P0 | Negative | `02-poi-explore-activity` | API `/api/poi` down | Mo MainPage lan dau | App dung fallback POI, set `_isUsingFallbackData`, UI hien duoc danh sach demo |
| DIAG-002-002 | P1 | Recovery | `02-poi-explore-activity` | API loi lan dau, sau do phuc hoi | Cho refresh nen 20s hoac reload | App chuyen tu fallback sang du lieu API, map/card cap nhat khong nhan doi |
| DIAG-002-003 | P1 | UI | `02-poi-explore-activity` | Co nhieu POI va map tab | Chuyen Explore -> Ban do -> bam marker -> sheet -> xem chi tiet | Sheet dung POI, badge near/current dung, navigation sang DetailPage dung |
| DIAG-002-004 | P0 | Data | `02-poi-explore-sequence` | Device co lich su VIEW/GPS tren server | Mo MainPage | GET profile restore viewed/visited ids, XP/level trong Cai Dat dung |
| DIAG-002-005 | P1 | Data | `02-poi-explore-sequence` | Local Preferences co POI ids chua sync | Mo MainPage/refresh | POST sync-history chen log con thieu, khong chen duplicate |
| DIAG-002-006 | P1 | Recovery | `02-poi-explore-activity` | Dang o MainPage cua device A | Khoi phuc device B tu Cai Dat | Device id, subscription, viewed/visited, QR recovery card cap nhat theo device B |
| DIAG-002-007 | P1 | UI/Boundary | `02-poi-explore-activity` | Tim kiem khong co ket qua | Nhap search khong khop | Card/list/map empty state dung, marker cu khong con hien |
| DIAG-002-008 | P1 | Recovery/Negative | `02-poi-explore-activity` | Dang o MainPage, API profile/status timeout | Nhap recovery code moi | App khong bao da sync thanh cong khi server timeout; giu du lieu device cu hoac rollback duoc |

### R3. F03 - GPS, Dwell, Geofence, Audio

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-003-001 | P0 | Boundary | `03-geofence-audio-activity` | User vao ban kinh POI < 5 giay roi roi di | Gia lap location | Khong set `_currentPoi`, khong visit, khong audio, CMS khong hien dang o quan |
| DIAG-003-002 | P0 | Positive | `03-geofence-audio-activity` | User dung trong ban kinh >= 5 giay | Gia lap location moi 5s | Xac nhan POI, highlight, visit log, sync history, queue audio |
| DIAG-003-003 | P0 | Boundary | `03-geofence-audio-sequence` | 2 POI overlap, cung priority, distance chenh <= 5m | Di chuyen giua 2 POI | App giu POI hien tai, khong nhay audio lien tuc |
| DIAG-003-004 | P1 | Timing | `03-geofence-audio-sequence` | GPS poll dang chay | Do thoi gian POST `/api/heartbeat` | Heartbeat gui moi 10s, khong phai 15s |
| DIAG-003-005 | P1 | Timing | `03-geofence-audio-sequence` | GPS poll dang chay | Do thoi gian refresh `/api/poi` nen | Refresh POI moi 20s, khong phai 30s |
| DIAG-003-006 | P1 | Negative | `03-geofence-audio-sequence` | Location ngoai vung phuc vu | Gui heartbeat | Server `skipped = true`, CMS khong hien toa do rac |
| DIAG-003-007 | P1 | Negative | `03-geofence-audio-activity` | App claim POI A nhung toa do ngoai BanKinh + 5m | POST heartbeat | Server xoa `PoiIdHienTai`, CMS hien dang di chuyen |
| DIAG-003-008 | P1 | Audio | `03-geofence-audio-activity` | Thuyet minh API 404 hoac body rong | Vao geofence | App phat fallback text theo ngon ngu, khong im lang/crash |
| DIAG-003-009 | P1 | Reliability | `03-geofence-audio-sequence` | Mien mang khi visit/log fire-and-forget | Vao geofence | App van phat audio; sync-history lan sau bo sung duoc lich su |

### R4. F04 - Detail, Audio WebView Bridge, Maps

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-004-001 | P0 | Negative | `04-poi-detail-activity` | Detail API loi/POI bi an sau khi mo tu list cu | Mo DetailPage | Dung fallback basic, an audio controls/menu neu khong co data, khong crash |
| DIAG-004-002 | P1 | UI | `04-poi-detail-activity` | POI co cover/mon an anh URL tuong doi/ngoai/hong | Mo DetailPage | Resolve anh dung, fallback dung, layout khong vo |
| DIAG-004-003 | P1 | Audio | `04-poi-detail-sequence` | Co `FileAudio`, WebView chua ready | Bam nghe ngay khi trang vua load | `_playWhenReady` duoc set, audio tu play sau `OnAudioWebViewNavigated` |
| DIAG-004-004 | P1 | Audio/UI | `04-poi-detail-activity` | Audio dang play | Bam pause, play, stop, keo slider | State icon/time/slider dong bo voi `audiostate://` |
| DIAG-004-005 | P1 | Negative | `04-poi-detail-activity` | Khong co FileAudio, NoiDung rong | Bam nghe | Hien alert `NoAudio`, khong goi TTS rong |
| DIAG-004-006 | P1 | Integration | `04-poi-detail-sequence` | POI co toa do | Bam Chi duong | Mo Google Maps `q=lat,lng`; neu browser loi thi app bao loi/khong crash |

### R5. F05 - Paid Plan, VietQR, Polling

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-005-001 | P0 | Positive | `05-paid-plan-activity` | Chon goi tra phi | Mo PaymentPage | Noi dung chuyen khoan local = format API `VKT <GOI> <SHORTID>`, QR hien dung so tien |
| DIAG-005-002 | P1 | UI | `05-paid-plan-activity` | PaymentPage hien | Bam copy noi dung | Clipboard co noi dung CK, UI bao da copy |
| DIAG-005-003 | P0 | Integration | `05-paid-plan-sequence` | API reachable | Bam "Da chuyen khoan" | Chi luc nay moi POST `/api/subscription/request`, tao request `cho_duyet` |
| DIAG-005-004 | P1 | Negative | `05-paid-plan-activity` | API URL sai | Bam "Da chuyen khoan" | Prompt sua API URL, khong tao modal status rong |
| DIAG-005-005 | P1 | Timing | `05-paid-plan-sequence` | Request `cho_duyet` | O PaymentStatusPage | Polling moi 10s, countdown cap nhat, khong spam API |
| DIAG-005-006 | P0 | Navigation | `05-paid-plan-sequence` | Admin duyet | App nhan `da_duyet`, bam Bat dau | Pop het payment modal va SubscriptionPage, stack cuoi la MainPage |
| DIAG-005-007 | P0 | Navigation | `05-paid-plan-sequence` | Admin tu choi | App nhan `tu_choi`, bam Thu lai/Dong | Thu lai quay ve PaymentPage/SubscriptionPage dung, khong luu expiry |
| DIAG-005-008 | P1 | Negative/UX | `05-paid-plan-sequence` | Request status API tra 404/500 | Cho polling | PaymentStatusPage hien loi/retry/back, khong treo countdown vo han |

### R6. F06 - CMS POI Management

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-006-001 | P1 | UI/Data | `06-cms-poi-management-activity` | Danh sach co anh tuong doi/ngoai/hong | Mo `/Poi` | `ImageUrlHelper` resolve/fallback dung |
| DIAG-006-002 | P1 | Negative | `06-cms-poi-management-sequence` | Dang nhap form Create/Edit nhieu truong | Upload anh fail | Form khong mat du lieu da nhap, hien loi upload |
| DIAG-006-003 | P0 | Data | `06-cms-poi-management-activity` | Tao POI voi mon rong xen ke mon hop le | Submit | Chi mon co ten duoc luu, don gia null thanh 0 |
| DIAG-006-004 | P0 | Data | `06-cms-poi-management-activity` | Tao POI khong nhap han duy tri | Submit va GET `/api/poi` | Han mac dinh now + 30 ngay, POI hien tren app |
| DIAG-006-005 | P1 | Data | `06-cms-poi-management-sequence` | Edit POI xoa 1 mon khoi form | Submit | Mon bi xoa set `TinhTrang=false`, detail API khong tra mon do |
| DIAG-006-006 | P1 | Concurrency | `06-cms-poi-management-activity` | Hai admin mo cung EditPage | Admin A luu, Admin B luu sau | Neu concurrency xay ra, hien loi reload; khong ghi de im lang |
| DIAG-006-007 | P1 | Filter/Sort | `06-cms-poi-management-activity` | Co POI active/hidden/expired/no-expiry | Thu tat ca filter/sort/search | Ket qua dung nhu diagram, query string giu state |

### R7. F07 - Maintenance Payment

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-007-001 | P0 | Boundary | `07-maintenance-payment-activity` | POI con han 1 ngay | Gia han 1 thang | Han moi = han cu + 1 thang, khong mat 1 ngay con lai |
| DIAG-007-002 | P0 | Boundary | `07-maintenance-payment-activity` | POI het han 10 ngay | Gia han 1 thang | Han moi = now + 1 thang, `TrangThai=true` |
| DIAG-007-003 | P0 | Data | `07-maintenance-payment-sequence` | Gia han 3 thang | Submit CMS/API | Tao 3 hoa don ky lien tiep `yyyy-MM`, tong tien dung |
| DIAG-007-004 | P1 | Data | `07-maintenance-payment-activity` | Chua co `DangKyDichVu` | Ghi nhan phi | Tao goi dich vu active voi `PhiConvert=20000` |
| DIAG-007-005 | P1 | UI | `07-maintenance-payment-activity` | Co lich su hoa don | Mo GhiNhan va LichSu | GhiNhan hien 5 gan day, LichSu hien day du |

### R8. F08 - CMS App Payment Approval

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-008-001 | P1 | UI/Data | `08-app-payment-approval-activity` | Co request moi | Mo `/DuyetThanhToan`, doi JS polling | Snapshot cap nhat count/latest, co the bao request moi |
| DIAG-008-002 | P1 | Negative | `08-app-payment-approval-sequence` | DB transient disposed | Goi pending snapshot | Tra HTTP 503 JSON `Ok=false`, trang khong crash |
| DIAG-008-003 | P0 | Data | `08-app-payment-approval-activity` | Device co goi active | Duyet request moi | `DangKyApp.NgayHetHan` noi tiep tu han cu |
| DIAG-008-004 | P0 | Negative | `08-app-payment-approval-activity` | Loai goi trong request sai | Bam duyet | Redirect loi loai goi khong hop le, khong tao DangKyApp |
| DIAG-008-005 | P0 | Race | `08-app-payment-approval-sequence` | Hai admin bam duyet cung request gan dong thoi | Submit 2 POST | Chi 1 request tao goi thanh cong; lan sau bao trang thai khong hop le |
| DIAG-008-006 | P1 | Filter | `08-app-payment-approval-activity` | Co >200 request | Filter/search/pkg/tab | Trang chi hien 200 moi nhat, stats van tinh toan bo |
| DIAG-008-007 | P0 | Data/Boundary | `08-app-payment-approval-sequence` | Device active het han sau 5 ngay | Duyet request moi tu CMS va API admin | `NgayBatDau` cua ban ghi moi = han cu, `NgayHetHan` = han cu + so ngay goi |

### R9. F09 - BanDo Live / Customer Tracking

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-009-001 | P0 | Data | `09-live-map-activity` | Device chi co subscription, chua co heartbeat | Mo `/BanDo` | Device van trong bang customer, trang thai chua heartbeat/offline |
| DIAG-009-002 | P0 | Data | `09-live-map-activity` | Device chi co heartbeat, chua mua goi | Mo `/BanDo` | Device van trong bang, filter `no-sub` bat duoc |
| DIAG-009-003 | P0 | Boundary | `09-live-map-sequence` | Location lat/lng 0 hoac ngoai vung | Mo `/BanDo` | Location bi loai, khong hien tren map/bang nhu toa do hop le |
| DIAG-009-004 | P1 | Filter | `09-live-map-activity` | Co online/offline/at-poi/active/expired/no-sub | Thu tat ca filter | Moi filter tra dung device |
| DIAG-009-005 | P1 | Sort | `09-live-map-activity` | Co nhieu device | Sort last/expiry/experience/visited/viewed/spent asc/desc | Thu tu dung, online tie-break dung |
| DIAG-009-006 | P1 | Data | `09-live-map-activity` | Device co 3 VIEW, 2 GPS distinct POI | Mo BanDo | XP = 3*50 + 2*100 = 350, level/progress dung |
| DIAG-009-007 | P1 | UI | `09-live-map-activity` | Goi sap het trong <1 gio, trong 7 ngay, het han | Mo BanDo | Text/badge remaining dung tung trang thai |
| DIAG-009-008 | P1 | Reliability | `09-live-map-sequence` | Raw SQL timeout | Mo BanDo | Hien warning tung nhom du lieu loi, cac nhom khac van render neu co |
| DIAG-009-009 | P1 | UI/Responsive | `09-live-map-activity` | Man hinh nho, device id dai | Mo BanDo mobile | Bang/card khong tran, device short hien ro |

### R10. F10 - Dashboard Monitoring

| ID | Muc do | Loai | Diagram | Tien dieu kien | Buoc thuc hien | Ket qua mong doi |
|---|---|---|---|---|---|---|
| DIAG-010-001 | P1 | Boundary | `10-dashboard-activity` | Khong can | Test mode `today`, `yesterday`, `last7`, `last30`, `thismonth`, `lastmonth`, `last12m` | Since/Until/Granularity/label dung |
| DIAG-010-002 | P1 | Boundary | `10-dashboard-activity` | Khong can | Test mode `day`, `week`, `month`, `year` voi input hop le | Range dung ngay/tuan ISO/thang/nam |
| DIAG-010-003 | P1 | Boundary | `10-dashboard-activity` | `from > to` | Submit custom | Tu swap `from/to`, label dung, khong loi |
| DIAG-010-004 | P1 | Boundary | `10-dashboard-activity` | Custom 1-2 ngay, 3-95 ngay, >95 ngay | Submit | Granularity lan luot `hour`, `day`, `month` |
| DIAG-010-005 | P1 | Negative | `10-dashboard-activity` | Mode/input sai | Submit | Fallback last7 hoac now hop ly, khong crash |
| DIAG-010-006 | P1 | Data | `10-dashboard-sequence` | Co POI active/expired/no-expiry/menu | Mo Dashboard | TongPOI, TongMonAn, SoQuanQuaHan dung |
| DIAG-010-007 | P1 | Data | `10-dashboard-sequence` | Co lichsuphat VIEW/GPS trong range | Mo Dashboard | ActivityBuckets, TopPoi, GeoPoints dung range va nguon |
| DIAG-010-008 | P1 | Data | `10-dashboard-sequence` | Co `dangkyapp` + `hoadon` trong range | Mo Dashboard | RevenueBuckets va TotalRevenue dung |
| DIAG-010-009 | P1 | Data | `10-dashboard-sequence` | Bucket thieu du lieu | Mo chart | `FillMissingBuckets` chen bucket 0 de chart lien mach |
| DIAG-010-010 | P1 | Reliability | `10-dashboard-activity` | DB disposed/timeout trong analytics | Mo Dashboard | Retry 1 lan, neu fail reset analytics va hien AnalyticsError |
| DIAG-010-011 | P1 | Reliability | `10-dashboard-activity` | DB loi o POI section nhung analytics OK | Mo Dashboard | POI stats reset/error, analytics van co neu load duoc |
| DIAG-010-012 | P1 | UI | `10-dashboard-activity` | Nhieu du lieu, label dai | Mo desktop/mobile | Chart, heatmap, top POI, revenue khong bi che/tran |

## Checklist Smoke Test Nhanh

1. API: `/health`, `/health/db`, `/api/poi`, `/api/subscription/plans`.
2. CMS: mo dashboard, `/Poi`, tao/sua/toggle POI, upload anh.
3. App: cai moi, dung thu, tai danh sach POI, mo detail, nghe thuyet minh, chi duong.
4. Thanh toan app: tao request QR, CMS duyet, app nhan approved va luu han.
5. Tracking: app gui heartbeat, CMS map hien device, visit/view tao lich su.
6. Phi duy tri: POI het han bi an tren app, admin gia han, POI hien lai.
