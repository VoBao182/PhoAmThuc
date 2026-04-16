# PRD – VinhKhanhTour: Hệ thống hướng dẫn du lịch phố ẩm thực Vĩnh Khánh

> **Phiên bản:** 1.0 | **Ngày:** 17/04/2026  
> **Tác giả:** Cao Hoàng Thịnh | **Email:** caohoangthinh2@gmail.com

---

## 1. TỔNG QUAN DỰ ÁN

### 1.1 Bối cảnh

Phố ẩm thực Vĩnh Khánh (Quận 4, TP.HCM) là một trong những tuyến phố ẩm thực nổi tiếng tại TP.HCM, thu hút hàng nghìn lượt khách mỗi ngày. Tuy nhiên, khách du lịch — đặc biệt khách nước ngoài — gặp nhiều khó khăn trong việc khám phá: không biết quán nào nổi tiếng, không có thông tin tiếng Anh, thiếu hướng dẫn bản đồ, không biết gọi món gì.

### 1.2 Mô tả sản phẩm

**VinhKhanhTour** là hệ thống du lịch thông minh gồm ba thành phần:

| Thành phần | Mô tả | Công nghệ |
|---|---|---|
| **Mobile App** | Ứng dụng cho khách tham quan | .NET MAUI (Android/Windows) |
| **REST API** | Backend trung tâm | ASP.NET Core 10, EF Core, PostgreSQL |
| **CMS Web** | Bảng điều khiển quản trị | ASP.NET Core Razor Pages |

**Cơ sở dữ liệu:** Supabase PostgreSQL (cloud-hosted)

### 1.3 Mục tiêu

1. Cung cấp hướng dẫn tham quan tự động (audio guide) khi khách bước vào khu vực quán.
2. Hỗ trợ đa ngôn ngữ (Việt, Anh, Trung).
3. Cho phép theo dõi vị trí khách thực tế (live map) phục vụ quản lý.
4. Mô hình kinh doanh rõ ràng: gói subscription cho khách, phí duy trì tháng cho chủ quán.

---

## 2. ĐỐI TƯỢNG SỬ DỤNG

### 2.1 Khách du lịch (App User)

- **Đặc điểm:** Người dùng ẩn danh, định danh bằng Device UUID (không đăng nhập).
- **Nhu cầu:** Khám phá quán ăn, nghe thuyết minh tự động, xem thực đơn, chỉ đường.
- **Hành vi:** Cài app → chọn gói → dạo phố → app tự động phát thuyết minh khi đến gần quán.

### 2.2 Quản trị viên (Admin CMS)

- **Đặc điểm:** Nhân viên quản lý hệ thống, truy cập CMS web nội bộ.
- **Nhu cầu:** Quản lý nội dung POI, thuyết minh, thu phí duy trì, duyệt thanh toán, xem bản đồ live.

### 2.3 Chủ quán (POI Owner)

- **Đặc điểm:** Đăng ký dịch vụ hướng dẫn cho quán của mình.
- **Nhu cầu:** Cập nhật thực đơn, nội dung thuyết minh, thanh toán phí duy trì hàng tháng.

---

## 3. KIẾN TRÚC HỆ THỐNG

```
┌──────────────────────────────────────────────────────────┐
│                      CLIENTS                             │
│  ┌─────────────────┐        ┌──────────────────────────┐ │
│  │  MAUI Mobile App│        │   CMS Web (Razor Pages)  │ │
│  │  (Android/Win)  │        │   (Admin Panel)          │ │
│  └────────┬────────┘        └────────────┬─────────────┘ │
└───────────┼──────────────────────────────┼───────────────┘
            │ HTTP/REST                    │ HTTP + EF Core
            ▼                             ▼
┌───────────────────────────────────────────────────────────┐
│               ASP.NET Core 10 REST API                    │
│  AuthController │ PoiController │ SubscriptionController  │
│  HeartbeatController │ PaymentController │ UploadController│
│  ThuyetMinhController │ LogController                     │
└──────────────────────────┬────────────────────────────────┘
                           │ EF Core + Npgsql
                           ▼
                ┌─────────────────────┐
                │  Supabase PostgreSQL │
                │  (11 tables)        │
                └─────────────────────┘
```

---

## 4. CẤU TRÚC DỮ LIỆU

### 4.1 Sơ đồ Entity (ERD mô tả)

```
poi ──────────────── thuyetminh ──── bandich
 │                       │
 ├── monan               └── lichsuphat
 ├── dangkydichvu
 ├── hoadon
 └── vitrikhach (device, 1:1)

dangkyapp ─── yeucauthanhtoan (device-based, không FK trực tiếp)

taikhoan ── hoadon
```

### 4.2 Mô tả bảng

| Bảng | Mô tả | Cột quan trọng |
|---|---|---|
| `poi` | Điểm du lịch/quán ăn | `KinhDo`, `ViDo`, `BanKinh` (geofence), `NgayHetHanDuyTri` |
| `thuyetminh` | Nội dung audio guide của POI | `POIId`, `ThuTu`, `TrangThai` |
| `bandich` | Bản dịch thuyết minh | `NgonNgu` (vi/en/zh), `NoiDung`, `FileAudio` |
| `monan` | Thực đơn quán | `TenMonAn`, `DonGia`, `PhanLoai`, `HinhAnh` |
| `dangkyapp` | Gói đăng ký app của khách | `MaThietBi`, `LoaiGoi`, `NgayBatDau`, `NgayHetHan` |
| `yeucauthanhtoan` | Yêu cầu thanh toán QR | `MaThietBi`, `LoaiGoi`, `SoTien`, `TrangThai` |
| `vitrikhach` | Vị trí GPS real-time | `MaThietBi` (UNIQUE), `Lat`, `Lng`, `LanCuoiHeartbeat` |
| `dangkydichvu` | Gói dịch vụ POI | `POIId`, `PhiDuyTriThang`, `PhiConvert` |
| `hoadon` | Hóa đơn thanh toán POI | `LoaiPhi` (duytri/convert), `KyThanhToan` |
| `lichsuphat` | Log xem/nghe thuyết minh | `Nguon` (GPS/VIEW), `NgonNguDung`, `MaThietBi` |
| `taikhoan` | Tài khoản admin CMS | `TenDangNhap`, `VaiTro` |

---

## 5. MÔ HÌNH GÓI DỊCH VỤ

### 5.1 Gói đăng ký App (khách tham quan)

| Mã gói | Tên | Giá | Thời hạn | Ghi chú |
|---|---|---|---|---|
| `thu` | Dùng thử | 0đ | 3 ngày | 1 lần/thiết bị, kích hoạt ngay |
| `ngay` | 1 ngày | 29.000đ | 1 ngày | Thanh toán qua QR |
| `tuan` | 1 tuần | 99.000đ | 7 ngày | Thanh toán qua QR |
| `thang` | 1 tháng | 199.000đ | 30 ngày | Thanh toán qua QR |
| `nam` | 1 năm | 999.000đ | 365 ngày | Thanh toán qua QR |

- Gói nối tiếp: nếu chưa hết hạn gói cũ → `NgayBatDau = NgayHetHan cũ`
- Gia hạn dễ dàng khi sắp hết hạn

### 5.2 Phí dịch vụ POI (chủ quán)

| Loại phí | Mức phí | Chu kỳ | Ghi chú |
|---|---|---|---|
| Phí duy trì | 50.000đ/tháng | Hàng tháng | Quá hạn → POI bị ẩn khỏi app |
| Phí convert TTS | 20.000đ/lần | Mỗi lần convert | Chuyển text → audio |

---

## 6. YÊU CẦU CHỨC NĂNG

### F01 – Khởi động & Subscription Gate

**Mô tả:** Mỗi lần mở app, kiểm tra xem thiết bị có gói hợp lệ không.

**Luồng chính:**
1. `MainPage.OnAppearing()` → đọc `sub_ngay_het_han` từ `Preferences`
2. Nếu hết hạn hoặc chưa có → điều hướng sang `SubscriptionPage`
3. Nếu còn hạn → tải danh sách POI và bắt đầu GPS tracking

**Điều kiện biên:**
- Gói miễn phí "thu": chỉ được dùng 1 lần/thiết bị (kiểm tra flag `da_dung_thu`)
- Gói trả phí: cần trải qua luồng QR → admin duyệt

---

### F02 – Danh sách & Tìm kiếm POI

**Mô tả:** Hiển thị danh sách quán/điểm tham quan, hỗ trợ tìm kiếm.

**API:** `GET /api/poi`  
**Điều kiện lọc:** `TrangThai = true` AND (`NgayHetHanDuyTri IS NULL` OR `NgayHetHanDuyTri > now()`)

**Tính năng:**
- Tìm kiếm văn bản: lọc theo tên POI (normalize dấu tiếng Việt)
- Sắp xếp theo `MucUuTien`
- Hiển thị ảnh đại diện, tên, địa chỉ, số điện thoại

---

### F03 – Tự động phát thuyết minh (Geofence + Audio)

**Mô tả:** Khi khách bước vào vùng bán kính của quán, app tự động phát audio guide.

**Luồng:**
1. GPS poll mỗi 5 giây
2. Tính khoảng cách đến từng POI bằng Haversine
3. Nếu `khoangCach <= BanKinh (30m)` → đánh dấu "đang trong geofence"
4. Gọi `SpeakPoiAsync()` → GET `/api/thuyet-minh/{poiId}?lang={ngonNgu}`
5. Phát audio file; nếu không có audio → TTS văn bản
6. Ghi log: `POST /api/log` với `Nguon="GPS"`
7. Dedup: không phát lại trong vòng 10 phút/POI

**Heartbeat GPS:**
- Mỗi 15 giây: `POST /api/heartbeat` với `{Lat, Lng, PoiIdHienTai}`
- Khi vào geofence: `POST /api/heartbeat/visit` (dedup 5 phút)

---

### F04 – Chi tiết POI

**Mô tả:** Xem thông tin đầy đủ một quán: thuyết minh, thực đơn, nút nghe audio, chỉ đường.

**API:** `GET /api/poi/{id}`  
**Dữ liệu trả về:** Thông tin POI + `MonAns` + `ThuyetMinhs` với `BanDichs` theo ngôn ngữ hiện tại

**Tính năng:**
- Chọn ngôn ngữ (vi/en/zh) → tải lại thuyết minh
- Nút "Nghe" → phát audio hoặc TTS
- Nút "Chỉ đường" → mở Google Maps với tọa độ POI
- Ghi log: `POST /api/heartbeat/view` khi mở trang (`Nguon="VIEW"`)

---

### F05 – Thanh toán QR & Phê duyệt

**Mô tả:** Luồng thanh toán gói trả phí qua QR VietQR MBBank, admin duyệt thủ công.

**Luồng khách:**
1. `SubscriptionPage` → chọn gói → gọi `POST /api/subscription/request`
2. API tạo `YeuCauThanhToan` (status=`cho_duyet`) → trả về `{YeuCauId, NoiDungChuyen}`
3. `PaymentPage` → hiển thị QR (VietQR) + nội dung CK (VD: `VKT THANG A1B2C3`)
4. Khách chuyển khoản → nhấn "Đã chuyển" → `PaymentStatusPage`
5. Polling `GET /api/subscription/request/{id}` mỗi 10 giây
6. Khi status=`da_duyet` → lưu `NgayHetHan` vào Preferences → đóng luồng

**Luồng Admin CMS (`/DuyetThanhToan`):**
- Tab 3 trạng thái: Chờ duyệt / Đã duyệt / Từ chối
- Nút "Duyệt" → `POST /api/subscription/approve/{id}` → tạo `DangKyApp` + cập nhật status
- Nút "Từ chối" → modal nhập lý do → `POST /api/subscription/reject/{id}`

---

### F06 – Bản đồ Live & Theo dõi khách

**Mô tả:** CMS hiển thị bản đồ Leaflet với vị trí khách đang online và các POI.

**API:** `GET /api/heartbeat/active` → thiết bị heartbeat trong 2 phút qua

**Dữ liệu hiển thị:**
- POI: icon camera màu theo trạng thái duy trì
- Khách online: dot xanh tại tọa độ GPS
- Tooltip: Device ID (rút gọn), POI hiện tại, số quán đã ghé/xem, thời gian còn lại

**Lịch sử hành trình:**
- Click vào thiết bị → `GET /api/heartbeat/history/{deviceShort}`
- Popup hiển thị danh sách POI đã ghé (4 giờ gần nhất), thời điểm, nguồn (GPS/VIEW)

**Auto-refresh:** 30 giây

---

### F07 – Quản lý POI (CMS)

**Mô tả:** CRUD quán ăn, thực đơn, thuyết minh qua CMS.

| Chức năng | URL CMS | API sử dụng |
|---|---|---|
| Danh sách POI | `/Poi` | EF Core trực tiếp |
| Tạo POI mới | `/Poi/Create` | `POST /api/upload` cho ảnh |
| Chỉnh sửa POI | `/Poi/Edit/{id}` | EF Core trực tiếp |
| Quản lý thuyết minh | `/ThuyetMinh/Edit/{id}` | EF Core trực tiếp |
| Xem dashboard | `/` | EF Core: TongPOI, SoQuanQuaHan |

**Upload ảnh:**
- `POST /api/upload` — giới hạn 5MB, định dạng: .jpg, .jpeg, .png, .webp, .gif
- Lưu tại `wwwroot/uploads/`

---

### F08 – Quản lý Phí Duy Trì POI

**Mô tả:** Admin ghi nhận thanh toán phí hàng tháng của chủ quán.

**Luồng:**
1. `/ThanhToan` → danh sách POI với trạng thái hạn
2. `/ThanhToan/GhiNhan/{poiId}` → chọn số tháng → `POST /api/payment/maintenance`
3. API tạo `HoaDon` (mỗi tháng một dòng) + gia hạn `NgayHetHanDuyTri`
4. `/ThanhToan/LichSu/{poiId}` → xem lịch sử hóa đơn

**Cảnh báo quá hạn:** Dashboard hiển thị số quán quá hạn; POI quá hạn bị ẩn khỏi app.

---

### F09 – Xác thực CMS

**Mô tả:** Đăng nhập quản trị viên vào CMS.

**API:** `POST /api/auth/login` → kiểm tra `TenDangNhap` + `MatKhau`

> **Lưu ý:** Phiên bản demo dùng plain-text password. Sản phẩm thực tế cần BCrypt + JWT.

---

## 7. YÊU CẦU PHI CHỨC NĂNG

### 7.1 Hiệu năng

| Chỉ số | Mục tiêu |
|---|---|
| Thời gian phản hồi API | < 500ms (p95) |
| GPS poll interval | 5 giây |
| Heartbeat interval | 15 giây |
| CMS map refresh | 30 giây |
| App polling thanh toán | 10 giây |
| API probe cache | 5 phút (success) / 10 giây (fail) |

### 7.2 Độ tin cậy

- **Geofence dedup:** Không phát lại thuyết minh trong 10 phút/POI
- **Visit dedup:** Không ghi `visit` trong 5 phút/POI
- **Subscription rollover:** Gia hạn nối tiếp, không mất ngày còn lại
- **Image URL fallback:** `ResolveImageUrl` xử lý cả đường dẫn tương đối, localhost, và URL ngoài

### 7.3 Khả năng mở rộng

- Hệ thống ngôn ngữ mở rộng: thêm ngôn ngữ mới chỉ cần thêm bản dịch trong bảng `bandich`
- Gói subscription: thêm gói mới bằng cách mở rộng enum trong `SubscriptionController`
- Multi-platform: MAUI hỗ trợ Android, Windows; iOS sau

### 7.4 Bảo mật

- CMS chạy nội bộ (không public internet) — không cần auth phức tạp cho demo
- App không lưu thông tin cá nhân — chỉ Device UUID (tự sinh)
- Supabase connection string không commit vào repository công khai

---

## 8. API REFERENCE

### 8.1 POI

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/poi` | Danh sách POI đang hoạt động |
| GET | `/api/poi/{id}` | Chi tiết POI (kèm menu + thuyết minh) |

### 8.2 Subscription

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/subscription/plans` | Danh sách gói |
| GET | `/api/subscription/status/{maThietBi}` | Trạng thái gói hiện tại |
| POST | `/api/subscription/purchase` | Mua gói miễn phí (kích hoạt ngay) |
| POST | `/api/subscription/request` | Tạo yêu cầu thanh toán QR |
| GET | `/api/subscription/request/{id}` | Kiểm tra trạng thái yêu cầu |
| POST | `/api/subscription/approve/{id}` | Admin duyệt |
| POST | `/api/subscription/reject/{id}` | Admin từ chối |
| GET | `/api/subscription/requests` | Danh sách yêu cầu (CMS) |

### 8.3 Heartbeat & Tracking

| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/api/heartbeat` | Upsert vị trí + POI hiện tại |
| POST | `/api/heartbeat/visit` | Ghi nhận vào geofence POI |
| POST | `/api/heartbeat/view` | Ghi nhận mở chi tiết POI |
| POST | `/api/heartbeat/sync-history` | Đồng bộ lịch sử (offline catch-up) |
| GET | `/api/heartbeat/active` | Thiết bị online (2 phút qua) |
| GET | `/api/heartbeat/history/{deviceShort}` | Lịch sử POI (4 giờ gần nhất) |

### 8.4 Thanh toán POI

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/payment/status/{poiId}` | Trạng thái phí duy trì |
| GET | `/api/payment/history/{poiId}` | Lịch sử hóa đơn |
| POST | `/api/payment/maintenance` | Ghi nhận phí duy trì (Admin) |
| POST | `/api/payment/convert/{poiId}` | Thanh toán phí convert TTS |
| GET | `/api/payment/overdue` | POI quá hạn |

### 8.5 Khác

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/thuyet-minh/{poiId}?lang=vi` | Nội dung thuyết minh theo ngôn ngữ |
| POST | `/api/log` | Ghi log phát audio |
| POST | `/api/upload` | Upload ảnh (5MB, jpg/png/webp/gif) |
| POST | `/api/auth/login` | Đăng nhập CMS |
| POST | `/api/auth/register` | Đăng ký tài khoản |

---

## 9. CẤU TRÚC THƯ MỤC DỰ ÁN

```
VinhKhanhTourDemo/                  ← MAUI App
├── AppConfig.cs                    ← Cấu hình API URL, image URL resolver
├── MainPage.xaml(.cs)              ← Trang chính: POI list, GPS, geofence, audio
├── DetailPage.xaml(.cs)            ← Chi tiết POI, menu, audio player
├── SubscriptionPage.xaml(.cs)      ← Chọn gói đăng ký
├── PaymentPage.xaml(.cs)           ← Hiển thị QR thanh toán
└── PaymentStatusPage.xaml(.cs)     ← Polling trạng thái thanh toán

VinhKhanhTour.API/
├── Controllers/
│   ├── AuthController.cs
│   ├── PoiController.cs
│   ├── SubscriptionController.cs
│   ├── HeartbeatController.cs
│   ├── PaymentController.cs
│   ├── ThuyetMinhController.cs
│   ├── LogController.cs
│   └── UploadController.cs
└── Models/                         ← Entity classes (EF Core)

VinhKhanhTour.CMS/
└── Pages/
    ├── Index.cshtml(.cs)           ← Dashboard
    ├── Poi/                        ← CRUD quán ăn
    ├── ThuyetMinh/                 ← Quản lý thuyết minh
    ├── ThanhToan/                  ← Phí duy trì POI
    ├── DuyetThanhToan/             ← Duyệt thanh toán app
    └── BanDo/                      ← Bản đồ live
```

---

## 10. USE CASE DIAGRAM (Mô tả)

```
Khách du lịch:
  - Cài app → chọn gói dùng thử/trả phí
  - Dạo phố → nghe thuyết minh tự động khi vào geofence
  - Xem chi tiết quán: thuyết minh, thực đơn, chỉ đường
  - Thanh toán gia hạn gói qua QR

Quản trị viên:
  - Quản lý quán ăn (CRUD, upload ảnh)
  - Quản lý nội dung thuyết minh + bản dịch đa ngôn ngữ
  - Xem bản đồ live + lịch sử hành trình khách
  - Duyệt/từ chối yêu cầu thanh toán app
  - Ghi nhận phí duy trì quán hàng tháng
  - Xem dashboard tổng quan
```

---

## 11. GIỚI HẠN & PHẠM VI ĐỒ ÁN

| Tính năng | Trạng thái |
|---|---|
| Hướng dẫn du lịch tự động (audio guide) | ✅ Hoàn thành |
| Đa ngôn ngữ (vi/en/zh) | ✅ Hoàn thành |
| Geofence tự động phát thuyết minh | ✅ Hoàn thành |
| Subscription & QR payment | ✅ Hoàn thành |
| CMS quản lý nội dung | ✅ Hoàn thành |
| Bản đồ live tracking | ✅ Hoàn thành |
| Duyệt thanh toán CMS | ✅ Hoàn thành |
| Phí duy trì POI | ✅ Hoàn thành |
| Xác thực JWT cho API | ❌ Ngoài phạm vi (demo plain text) |
| Push notification | ❌ Ngoài phạm vi |
| iOS | ❌ Ngoài phạm vi |
| Đặt bàn / Order online | ❌ Ngoài phạm vi |

---

## 12. PHỤ LỤC – LUỒNG THANH TOÁN CHI TIẾT

```
App                          API                         Admin CMS
 │                            │                              │
 ├─ POST /subscription/request ─►                            │
 │                            ├─ Tạo YeuCauThanhToan         │
 │                            │  (status=cho_duyet)          │
 │◄── {yeuCauId, noiDungCK} ──┤                              │
 │                            │                              │
 │  [Hiển thị QR + nội dung]  │                              │
 │  [Khách chuyển khoản]      │                              │
 │                            │                              │
 │  polling (10s)             │      GET /subscription/      │
 ├─ GET /request/{id} ────────►      requests?trangthai=     │
 │◄── {status:cho_duyet} ─────┤      cho_duyet ◄────────────┤
 │                            │                              │
 │  polling (10s)             │      POST /approve/{id} ◄───┤
 ├─ GET /request/{id} ────────►                              │
 │◄── {status:da_duyet} ──────┤                              │
 │                            │                              │
 │  [Lưu NgayHetHan]          │                              │
 │  [Chuyển MainPage]         │                              │
```

---

*Tài liệu này mô tả toàn bộ phạm vi và yêu cầu của đồ án VinhKhanhTour phiên bản 1.0.*
