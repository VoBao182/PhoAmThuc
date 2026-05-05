# Test Findings - VinhKhanhTour

Tai lieu nay ghi lai cac loi/sai lech phat hien khi doi chieu diagram, PRD va code. Muc tieu la giup uu tien sua truoc khi bao cao/do an duoc cham.

## Tom Tat Uu Tien

| Muc do | So luong | Nen xu ly |
|---|---:|---|
| P0 | 4 | Can sua hoac giai thich truoc demo/cham do an |
| P1 | 8 | Nen sua de tranh bi hoi khi kiem thu |
| P2 | 2 | Sua tai lieu/comment de sach se hon |

## Danh Sach Phat Hien

| ID | Muc do | Loai | Khu vuc | Phat hien | Bang chung | Tac dong | De xuat | Test lien quan |
|---|---|---|---|---|---|---|---|---|
| FIND-001 | P0 | Security/Business | Subscription API | `POST /api/subscription/purchase` co the active goi tra phi neu client goi truc tiep `ngay/tuan/thang/nam`, bo qua QR va admin duyet | `VinhKhanhTour.API/Controllers/SubscriptionController.cs:90-147`; PRD `docs/PRD_VinhKhanhTour.md:155`, `:336` chi cho purchase dung thu | Nguoi dung co the kich hoat goi tra phi khong thanh toan neu biet API | Gioi han `purchase` chi nhan `thu`; tat ca goi tra phi bat buoc qua `/request` + approve | `SUB-025`, `SEC-001` |
| FIND-002 | P0 | Data | Subscription rollover | Khi approve noi tiep, code tinh `NgayHetHan` tu han cu nhung luu `NgayBatDau = now`, lech PRD | API `SubscriptionController.cs:258-267`; CMS `DuyetThanhToan/Index.cshtml.cs:252-261`; PRD noi `NgayBatDau = NgayHetHan cu` | Bao cao/lich su goi co khoang thoi gian sai, de bi bat loi khi query data | Dat `NgayBatDau = mocBatDau` cho goi noi tiep; them test DB assertion | `SUB-026`, `DIAG-008-007` |
| FIND-003 | P0 | Race condition | Duyet thanh toan app | API/CMS check `TrangThai == cho_duyet` roi add `DangKyApp`, nhung khong thay transaction/concurrency guard | API `SubscriptionController.cs:240-273`; CMS `DuyetThanhToan/Index.cshtml.cs:231-267` | Hai admin/2 request gan dong thoi co the tao 2 goi cho cung yeu cau | Dung transaction, concurrency token, row lock hoac update co dieu kien status | `SUB-019`, `DIAG-008-005` |
| FIND-004 | P0 | Functional parity | Phi duy tri POI | PRD noi CMS `/ThanhToan/GhiNhan` goi `POST /api/payment/maintenance`, nhung CMS dang ghi EF truc tiep; API maintenance khong cap nhat `DangKyDichVu` nhu CMS | PRD `docs/PRD_VinhKhanhTour.md:269-270`; API `PaymentController.cs:117-126`; CMS `ThanhToan/GhiNhan.cshtml.cs:91-100` | Neu dung API maintenance, du lieu goi dich vu/co che phi co the lech voi CMS | Chon 1 source of truth: CMS goi API, hoac cap nhat PRD; dong bo logic `DangKyDichVu` trong API | `FEE-021`, `DIAG-007-004` |
| FIND-005 | P1 | UX/Reliability | Payment polling | `PaymentStatusPage` return im lang khi status API tra non-success, nen request bi xoa/404/500 se cho vo han | `VinhKhanhTourDemo/PaymentStatusPage.xaml.cs:104-106` | User khong biet thanh toan loi hay request mat, de tuong app treo | Dem so lan loi va hien retry/back; 404 nen bao request khong ton tai | `APP-SUB-019`, `DIAG-005-008` |
| FIND-006 | P1 | Recovery/Security | Device identity | Recovery override duoc luu truoc khi server xac nhan co subscription/profile; MainPage co the bao da khoi phuc du sync fail | `DeviceIdentity.cs:53-57`; `SubscriptionPage.xaml.cs:220-241`; `MainPage.xaml.cs:1796-1827` | Nhap nham code dung format co the lam app doi device id sang ma khong dung | Validate voi server truoc, hoac rollback override neu sync/status fail | `APP-016`, `APP-SUB-020`, `SEC-002` |
| FIND-007 | P1 | Documentation mismatch | GPS timing | Diagram/code dung heartbeat 10s va refresh POI 20s, nhung PRD/comment con ghi 15s/30s | PRD `:187`, `:295`; `MainPage.xaml.cs` constants 10s/20s; comment `HeartbeatController.cs:13` ghi 15s | Khi bao cao/co nguoi test bang PRD se ket luan sai | Cap nhat PRD/comment theo code, hoac doi code neu 15s/30s la yeu cau that | `DOC-001`, `DIAG-003-004`, `DIAG-003-005` |
| FIND-008 | P1 | Documentation mismatch | POI expiry filter | PRD noi POI `NgayHetHanDuyTri IS NULL` van hien, code lai an POI null expiry | PRD `docs/PRD_VinhKhanhTour.md:164`; API `PoiController.cs:21-23`, `:49-51` | Du lieu POI chua dong phi/null expiry co ky vong khac nhau giua tai lieu va app | Cap nhat PRD theo rule "null bi an", hoac doi API neu null phai hien | `DOC-002`, `POI-004` |
| FIND-009 | P1 | Documentation mismatch | Visit dedup | PRD ghi visit dedup 5 phut, code visit GPS dung 10 phut; view detail moi dung 5 phut | PRD `:188`, `:303`; `HeartbeatController.cs:194-195`, `:250-251` | Test thuc te theo PRD se thay thieu log visit trong phut 6-10 | PRD tach ro: GPS visit 10 phut, VIEW 5 phut | `DOC-003`, `GPS-024` |
| FIND-010 | P1 | Documentation mismatch | CMS auto-refresh | PRD/NFR ghi CMS map refresh 30s, nhieu page CMS dang set auto-refresh 10s | PRD `:296`; `BanDo/Index.cshtml:7`; `_Layout.cshtml:3-5` | NFR va test performance sai moc thoi gian | Cap nhat PRD/test ve 10s hoac doi ViewData ve 30s | `DOC-004`, `MAP-009`, `NFR-003` |
| FIND-011 | P2 | Message/UX | Subscription API | Message invalid `purchase` thieu goi `tuan` | `SubscriptionController.cs:97` | Loi nho nhung de bi cham la thong bao sai | Sua message thanh `thu, ngay, tuan, thang, nam` | `SUB-027` |
| FIND-012 | P2 | Comment/tai lieu code | Auth model | `TaiKhoan.MatKhau` comment ghi BCrypt hash, nhung AuthController dang so sanh plain text va PRD noi demo plain text | `TaiKhoan.cs:17`; `AuthController.cs:29-30`, `:63`; PRD `:283` | Gay hieu nham ve bao mat khi review code | Doi comment model thanh plain-text demo, hoac chuyen that sang BCrypt | `DOC-005`, `NFR-008` |
| FIND-013 | P1 | Data/API consistency | Payment status | `GET /api/payment/status` co the tra phi mac dinh neu `DangKyDichVu` khong duoc tao/cap nhat boi API maintenance | API `PaymentController.cs`; CMS `ThanhToan/GhiNhan.cshtml.cs` co logic tao/cap nhat goi dich vu rieng | API va CMS hien thi phi/han dich vu khac nhau | Dong bo logic maintenance giua API va CMS | `DIAG-007-004` |
| FIND-014 | P1 | Test/doc risk | BanDo implementation | Mot so mo ta cu de nghi fetch JS tu HeartbeatController, nhung diagram/code hien la server-side raw SQL va page reload | `09-live-map-activity/sequence.puml`; `BanDo/Index.cshtml.cs` raw SQL load | Test sai cach se bo sot loi SQL timeout/filter/sort server-side | Giu test theo server-side: query string, raw SQL timeout, page reload 10s | `DIAG-009-*` |

## Nhung Diem Da Xac Nhan Build

- `dotnet build VinhKhanhTour.API/VinhKhanhTour.API.csproj --no-restore`: thanh cong, 0 warning/error.
- `dotnet build VinhKhanhTour.CMS/VinhKhanhTour.CMS.csproj --no-restore`: thanh cong, 0 warning/error.
- `dotnet build VinhKhanhTourDemo/VinhKhanhTourDemo.csproj -f net10.0-windows10.0.19041.0 --no-restore`: thanh cong, 0 warning/error.

## De Xuat Thu Tu Sua

1. Chan `purchase` voi goi tra phi va sua rollover `NgayBatDau`.
2. Xu ly polling 404/500 va rollback recovery override khi sync fail.
3. Them guard concurrency cho approve/reject request.
4. Dong bo PRD voi code cho heartbeat, POI expiry, visit dedup, CMS refresh.
5. Chon mot luong duy nhat cho phi duy tri POI: CMS truc tiep EF hoac API maintenance.
