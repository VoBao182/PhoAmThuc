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
| MAP-009 | P1 | Performance | Ban do dang mo | Cho 30 giay | Auto-refresh du lieu, marker cap nhat khong reload trang toan bo neu UI ho tro |
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
| NFR-003 | P1 | Performance | CMS map mo 10 phut | Theo doi refresh 30s | Khong leak UI ro rang, marker khong nhan doi bat thuong |
| NFR-004 | P1 | Reliability | API bi restart khi app dang polling | Restart API | App phuc hoi sau khi API len lai |
| NFR-005 | P1 | Reliability | DB pool cham/loi transient | Goi CMS DuyetThanhToan | Retry/clear pool theo code, UI khong crash |
| NFR-006 | P1 | Security | Upload file doc hai doi duoi `.exe` | POST upload | Bi chan theo extension |
| NFR-007 | P1 | Security | Thu truy cap `/uploads/{file}` | GET static file | Chi file da upload duoc phuc vu, khong doc file ngoai uploads |
| NFR-008 | P1 | Security | Login/register plain text demo | Review release | Ghi nhan rui ro: production can BCrypt/JWT, demo chap nhan neu trong pham vi |
| NFR-009 | P1 | Privacy | App khong nhap PII | Kiem tra DB/log | Chi luu device id, location, history; khong yeu cau ten/email khach app |
| NFR-010 | P1 | Accessibility | Mobile small screen | Kiem tra cac nut/form quan trong | Text doc duoc, nut bam duoc, khong bi che |

## Checklist Smoke Test Nhanh

1. API: `/health`, `/health/db`, `/api/poi`, `/api/subscription/plans`.
2. CMS: mo dashboard, `/Poi`, tao/sua/toggle POI, upload anh.
3. App: cai moi, dung thu, tai danh sach POI, mo detail, nghe thuyet minh, chi duong.
4. Thanh toan app: tao request QR, CMS duyet, app nhan approved va luu han.
5. Tracking: app gui heartbeat, CMS map hien device, visit/view tao lich su.
6. Phi duy tri: POI het han bi an tren app, admin gia han, POI hien lai.
