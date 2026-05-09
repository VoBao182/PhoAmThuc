# PRD – VinhKhanhTour: Hệ thống hướng dẫn du lịch phố ẩm thực Vĩnh Khánh

> **Phiên bản:** 1.3 | **Ngày:** 09/05/2026
>
> **Lưu ý phiên bản 1.3:** Bổ sung chi tiết kỹ thuật cho F01–F05 theo cùng format F06–F10: sơ đồ liên quan, phân lớp triển khai, method names và quy tắc nghiệp vụ chính. Đồng bộ cả bản Markdown và DOCX.
>
> **Lưu ý phiên bản 1.2:** Đồng bộ với các sequence diagrams F01–F05 đã được chuẩn hoá theo cùng convention UML stereotype như F06–F10 (`AppDbContext <<persistence>>`, `database "PostgreSQL"`, message ở cấp method thay vì SQL).
>
> **Lưu ý phiên bản 1.1:** Đánh số lại F06–F10 để khớp với diagrams (`docs/diagrams/06`–`10`); tách luồng thanh toán app thành F05 (phía App) và F08 (phía Admin); bổ sung F10 Dashboard; cập nhật chi tiết kỹ thuật theo các sequence diagrams (UI / PageModel / Persistence).

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

**Mô tả:** Mỗi lần mở app, kiểm tra quyền sử dụng của thiết bị, cho phép kích hoạt dùng thử hoặc khôi phục gói cũ bằng recovery code/QR.
**Diagrams:** `docs/diagrams/01-subscription-gate-activity.puml`, `01-subscription-gate-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI (boundary) | `LaunchPage.xaml`, `SubscriptionPage.xaml`, `QrScannerPage.xaml` |
| Page/Control | `LaunchPage.OnAppearing / RouteAsync`, `MainPage.OnAppearing / EnsureSubscriptionGateAsync`, `SubscriptionPage.UpdateRecoveryCard`, `OnMuaGoiClicked`, `OnPasteRecoveryCodeClicked`, `OnEnterRecoveryCodeClicked`, `OnScanRecoveryQrClicked`, `RestoreFromRecoveryCodeAsync`, `RestoreSubscriptionStateAsync`, `ActivateFreeTrialAsync` |
| Domain/helper | `SubscriptionState.IsSubscriptionActive`, `HasStoredSubscriptionRecord`, `CalculateRemainingDays`, `DeviceIdentity.GetDeviceId`, `BuildRecoveryPayload`, `BuildQrCodeUrl`, `TrySetDeviceIdOverride`, `ApiConnectionPrompt.EnsureConnectedApiBaseUrlAsync` |
| Controller (control) | `SubscriptionController.GetStatus` (`GET /api/subscription/status/{maThietBi}`), `Purchase` (`POST /api/subscription/purchase`) |
| Persistence | `Preferences` (`sub_ngay_het_han`, `da_dung_thu`, `device_id`, `device_id_override`) + `AppDbContext` → PostgreSQL bảng `dangkyapp` |

**Luồng chính:**
1. `LaunchPage.RouteAsync()` kiểm tra `SubscriptionState.IsSubscriptionActive()`.
2. Nếu local còn hạn → mở `MainPage`; nếu không → mở `SubscriptionPage(HasStoredSubscriptionRecord())`.
3. Khi `MainPage.OnAppearing()`, app gọi `EnsureSubscriptionGateAsync()`; nếu local hết hạn, app thử `RestoreSubscriptionStateAsync()` qua API trước khi hiện modal.
4. Trên `SubscriptionPage`, gói `thu` gọi `ActivateFreeTrialAsync()` → `POST /api/subscription/purchase`; gói trả phí mở `PaymentPage` (F05).
5. Recovery code/QR đi qua `DeviceIdentity.TrySetDeviceIdOverride()` rồi gọi `GET /api/subscription/status/{deviceId}` để đồng bộ lại hạn gói.

**Quy tắc nghiệp vụ chính:**
- Gói miễn phí `thu` chỉ dùng 1 lần/thiết bị; API kiểm tra `DangKyApp.LoaiGoi == "thu"`.
- Ngày hết hạn được lưu dạng ISO round-trip vào `Preferences["sub_ngay_het_han"]`.
- Nếu không kết nối được API, `ApiConnectionPrompt` cho phép nhập base URL thủ công trước khi kích hoạt/khôi phục.

---

### F02 – Danh sách & Tìm kiếm POI

**Mô tả:** Hiển thị danh sách quán/điểm tham quan, hỗ trợ tìm kiếm, render card/map và đồng bộ lịch sử trải nghiệm của thiết bị.
**Diagrams:** `docs/diagrams/02-poi-explore-activity.puml`, `02-poi-explore-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI (boundary) | `MainPage.xaml` (tab Khám phá/Bản đồ/Cài đặt, search box, POI cards), `QrScannerPage.xaml` |
| Page/Control | `MainPage.OnAppearing`, `LoadPoisFromApi`, `RefreshPoisInBackgroundAsync`, `RenderPoiCards`, `OnSearchTextChanged`, `OpenPoiDetailAsync`, `RestorePoiHistoryAsync`, `SyncPoiHistoryAsync`, `UpdateCaiDatUI`, `RecordPoiViewAsync` |
| Domain/helper | `DeviceIdentity.GetDeviceId`, `GetSavedPoiIds`, `MergeSavedPoiIds`, `FoodImageCatalog.GetPoiImageSource`, `AppConfig.EnsureApiBaseUrlAsync` |
| Controller (control) | `PoiController.GetAll` (`GET /api/poi`), `HeartbeatController.GetExperienceProfile` (`GET /api/heartbeat/profile/{maThietBi}`), `SyncHistory` (`POST /api/heartbeat/sync-history`), `RecordView` (`POST /api/heartbeat/view`) |
| Persistence | `Preferences` (viewed/visited POI ids, device id) + `AppDbContext` → PostgreSQL bảng `poi`, `lichsuphat` |

**Điều kiện lọc API:** `PoiController.GetAll()` chỉ trả POI có `TrangThai = true`, `NgayHetHanDuyTri.HasValue` và `NgayHetHanDuyTri >= DateTime.UtcNow`, sắp xếp theo `MucUuTien`.

**Luồng chính:**
1. `MainPage.OnAppearing()` qua được subscription gate thì gọi `LoadPoisFromApi()`.
2. `LoadPoisFromApi()` gọi `GET /api/poi`; nếu lỗi thì dùng `CreateFallbackPois()` để vẫn test được UI.
3. Sau khi tải dữ liệu, app gọi `RestorePoiHistoryAsync()` và `SyncPoiHistoryAsync()` để đồng bộ viewed/visited/XP.
4. `RenderPoiCards()` tính khoảng cách theo vị trí hiện tại, hiển thị ảnh, badge, trạng thái gần/đang phát và nút mở chi tiết.
5. `OnSearchTextChanged()` cập nhật `_searchText` và render lại danh sách tức thời.
6. Nền GPS gọi `RefreshPoisInBackgroundAsync()` định kỳ để nhận POI mới/sửa/ẩn từ CMS.

**Quy tắc nghiệp vụ chính:**
- POI quá hạn duy trì bị ẩn khỏi app ngay tại API.
- Lịch sử xem/ghe được lưu local trước, sau đó sync lên server để CMS/XP không lệch khi có request fire-and-forget bị rớt.
- `FoodImageCatalog` cung cấp ảnh fallback khi `AnhDaiDien` thiếu hoặc URL không dùng được.

---

### F03 – Tự động phát thuyết minh (Geofence + Audio)

**Mô tả:** Khi khách bước vào vùng bán kính của quán và đứng đủ thời gian xác nhận, app tự động đưa POI vào hàng đợi phát thuyết minh.
**Diagrams:** `docs/diagrams/03-geofence-audio-activity.puml`, `03-geofence-audio-sequence.puml`; sub-flow queue: `03b-queue-playback-activity.puml`, `03b-queue-playback-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI (boundary) | `MainPage.xaml` (GPS status, map WebView, now-playing banner, POI highlight) |
| Page/Control | `EnsureGpsTrackingAsync`, `StartGpsTracking`, `CheckGeofence`, `SelectPoiForLocation`, `QueuePoiPlayback`, `ProcessSpeakQueueAsync`, `SpeakPoiAsync`, `SendHeartbeatAsync`, `RecordPoiVisitAsync`, `LogPlaybackAsync` |
| Domain/helper | `Location.CalculateDistance`, `_lastSpokenTime`, `_speakQueue`, `_queuedSpeakPoiIds`, `_speakLock`, `AppText.LanguageCode`, `TextToSpeech.Default` |
| Controller (control) | `HeartbeatController.SendHeartbeat` (`POST /api/heartbeat`), `RecordVisit` (`POST /api/heartbeat/visit`), `ThuyetMinhController.GetByPoi`, `LogController.Post` (`POST /api/log`) |
| Persistence | `Preferences` (visited ids) + `AppDbContext` → PostgreSQL bảng `vitrikhach`, `lichsuphat`, `thuyetminh`, `bandich` |

**Luồng geofence:**
1. `EnsureGpsTrackingAsync()` xin `Permissions.LocationWhenInUse`, sau đó gọi `StartGpsTracking()`.
2. `StartGpsTracking()` lấy GPS mỗi 5 giây bằng `Geolocation.Default.GetLocationAsync()`.
3. `CheckGeofence()` gọi `SelectPoiForLocation()` để chọn candidate trong bán kính, ưu tiên `MucUuTien`, sau đó khoảng cách và tên.
4. POI phải dwell đủ `DwellSecondsToConfirm` trước khi được commit thành `_currentPoi`.
5. Nếu là POI mới và qua cooldown, app ghi visited local, gọi `RecordPoiVisitAsync()`, cập nhật UI và `QueuePoiPlayback()`.
6. `ProcessSpeakQueueAsync()` gọi `SpeakPoiAsync()` tuần tự; `SpeakPoiAsync()` lấy nội dung qua `/api/thuyet-minh/{poiId}?lang=...`, đọc TTS và gọi `LogPlaybackAsync()`.

**Heartbeat GPS:**
- `SendHeartbeatAsync()` gửi `POST /api/heartbeat` với `{MaThietBi, Lat, Lng, PoiIdHienTai, TenPoiHienTai}` theo chu kỳ tick của GPS.
- Server `VerifyCurrentPoiAsync()` re-check tọa độ nằm trong `BanKinh + 5m` trước khi tin POI app báo.
- `RecordVisit` dedup server trong 10 phút cho cùng thiết bị/POI.

**Quy tắc nghiệp vụ chính:**
- Cooldown audio 10 phút/POI trên app bằng `_lastSpokenTime`.
- Hysteresis giữ POI hiện tại khi hai POI sát nhau, cùng `MucUuTien` và chênh khoảng cách nhỏ, tránh nháy do GPS jitter.
- Hàng đợi audio chống trùng bằng 3 lớp: `_playingPoi`, `_queuedSpeakPoiIds`, `_speakLock`.

---

### F04 – Chi tiết POI

**Mô tả:** Xem thông tin đầy đủ một quán: ảnh bìa, thuyết minh, thực đơn, audio guide, audio controls và chỉ đường.
**Diagrams:** `docs/diagrams/04-poi-detail-activity.puml`, `04-poi-detail-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI (boundary) | `DetailPage.xaml` (cover, info, menu, audio WebView, play/pause/stop/slider, nút chỉ đường) |
| Page/Control | `DetailPage.LoadDetail`, `ConfigureAudioPlayer`, `RenderMenu`, `UseFallback`, `OnNgheClicked`, `OnAudioWebViewNavigated`, `OnAudioWebViewNavigating`, `OnPlayPauseClicked`, `OnStopAudioClicked`, `OnAudioSliderDragCompleted`, `OnMapClicked` |
| Domain/helper | `FoodImageCatalog.GetPoiImageSource`, `TextToSpeech.Default`, `Browser.Default.OpenAsync`, `AppText.LanguageCode` |
| Controller (control) | `PoiController.GetById` (`GET /api/poi/{id}?lang={lang}`), `HeartbeatController.RecordView` được gọi từ luồng mở chi tiết ở `MainPage.RecordPoiViewAsync` |
| Persistence | `AppDbContext` → PostgreSQL bảng `poi`, `monan`, `thuyetminh`, `bandich`, `lichsuphat` |

**Dữ liệu trả về:** `PoiController.GetById()` trả thông tin POI + `MonAns` còn `TinhTrang` + `ThuyetMinhs` đang `TrangThai` với bản dịch theo `lang`, fallback về `vi`.

**Luồng chính:**
1. `OpenPoiDetailAsync()` ở `MainPage` ghi viewed local, gọi `RecordPoiViewAsync()` và mở `DetailPage`.
2. `DetailPage.LoadDetail()` gọi `GET /api/poi/{id}?lang={_lang}`.
3. Nếu thành công: set ảnh bìa, tên, địa chỉ, số điện thoại, nội dung thuyết minh; sau đó `ConfigureAudioPlayer()` và `RenderMenu()`.
4. Nếu API lỗi/null: `UseFallback(tenPoi)` hiển thị dữ liệu cơ bản, ảnh fallback và ẩn menu/audio controls không đủ dữ liệu.
5. `OnNgheClicked()` ưu tiên phát `FileAudio` qua WebView; nếu không có audio file thì dùng TTS đọc `NoiDungThuyetMinh`.
6. `OnMapClicked()` mở Google Maps bằng tọa độ POI.

**Quy tắc nghiệp vụ chính:**
- Menu nhóm theo `PhanLoai`; không có món thì ẩn section menu.
- Audio file có các control `playAudio`, `pauseAudio`, `stopAudio`, `seekAudio`; trạng thái trả về qua URL scheme `audiostate://`.
- `POST /api/heartbeat/view` dedup server 5 phút cho cùng thiết bị/POI.

---

### F05 – Thanh toán gói trả phí (App side)

**Mô tả:** Luồng đặt yêu cầu thanh toán gói trả phí qua QR VietQR MBBank, app polling chờ admin duyệt.
**Diagrams:** `docs/diagrams/05-paid-plan-activity.puml`, `05-paid-plan-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI (boundary) | `SubscriptionPage.xaml`, `PaymentPage.xaml`, `PaymentStatusPage.xaml` |
| Page/Control | `SubscriptionPage.OnMuaGoiClicked`, `PaymentPage.SetupUi`, `OnCopyNoiDungClicked`, `OnDaChuyenKhoanClicked`, `OnConfigureApiClicked`, `PaymentStatusPage.OnAppearing`, `StartPollingAsync`, `PollStatusAsync`, `ShowSuccess`, `ShowRejected`, `ClosePaymentFlowAsync` |
| Domain/helper | `DeviceIdentity.GetDeviceId`, `ApiConnectionPrompt.EnsureConnectedApiBaseUrlAsync`, `SubscriptionState.CalculateRemainingDays`, `AppConfig.EnsureApiBaseUrlAsync` |
| Controller (control) | `SubscriptionController.CreateRequest` (`POST /api/subscription/request`), `GetRequestStatus` (`GET /api/subscription/request/{yeuCauId}`) |
| Persistence | `Preferences["sub_ngay_het_han"]` + `AppDbContext` → PostgreSQL bảng `yeucauthanhtoan`, `dangkyapp` |

**Luồng khách (App):**
1. `SubscriptionPage.OnMuaGoiClicked()` nhận `CommandParameter` gói trả phí và mở `PaymentPage(loaiGoi)`.
2. `PaymentPage.SetupUi()` lấy `DeviceIdentity.GetDeviceId()`, tạo nội dung chuyển khoản `VKT <GOI> <SHORTID>` và URL QR VietQR MBBank.
3. Khách chuyển khoản rồi bấm "Đã chuyển" → `OnDaChuyenKhoanClicked()` đảm bảo API kết nối được, sau đó gọi `POST /api/subscription/request`.
4. API `CreateRequest` chuẩn hóa `MaThietBi`, tạo `YeuCauThanhToan` (`TrangThai=cho_duyet`) và trả `{YeuCauId, SoTien, Ten, NoiDungChuyen, TrangThai}`.
5. App mở `PaymentStatusPage(yeuCauId, loaiGoi, noiDung)` và `StartPollingAsync()` gọi `PollStatusAsync()` mỗi 10 giây.
6. Khi `TrangThai=da_duyet`, `ShowSuccess()` lưu `NgayHetHan` vào `Preferences`, hiển thị số ngày còn lại và `ClosePaymentFlowAsync(closeSubscriptionPage:true)` đưa khách vào `MainPage`.
7. Khi `TrangThai=tu_choi`, `ShowRejected()` hiển thị `GhiChuAdmin`, cho phép thử lại hoặc đóng luồng.

**Quy tắc nghiệp vụ chính:**
- Gói hợp lệ phía app: `ngay`, `tuan`, `thang`, `nam`; gói `thu` đi qua F01 (`/purchase`), không tạo yêu cầu duyệt.
- Nội dung chuyển khoản được sinh từ device id đã normalize, ví dụ `VKT THANG A1B2C3`.
- Polling bỏ qua lỗi mạng tạm thời và tiếp tục chờ chu kỳ sau.

**State machine `YeuCauThanhToan`:** xem `docs/diagrams/11-yeucauthanhtoan-state.puml` (`cho_duyet → da_duyet | tu_choi`).

> Phía Admin duyệt/từ chối được tách thành **F08** để khớp với diagram F08.

---

### F06 – Quản lý POI, ảnh, geofence, thuyết minh và menu (CMS)

**Mô tả:** CRUD quán ăn, ảnh, vùng geofence, thuyết minh đa ngôn ngữ và thực đơn qua CMS.
**Diagrams:** `docs/diagrams/06-cms-poi-management-activity.puml`, `06-cms-poi-management-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI (boundary) | `Pages/Poi/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Pages/ThuyetMinh/Edit.cshtml` |
| PageModel (control) | `IndexModel.OnGetAsync / OnPostToggleAsync`, `CreateModel.OnPostAsync`, `EditModel.OnPostAsync`, helper `ImageUrlHelper.ResolvePoi/ResolveDish` |
| Controller (control) | `UploadController.Upload` (`POST /api/upload`) |
| Persistence | `AppDbContext` (EF Core) → PostgreSQL; collection `wwwroot/uploads` |

**Quy tắc nghiệp vụ chính:**
- POI tạo mới mặc định được **free trial 30 ngày**: nếu `NgayHetHanDuyTri` null thì gán `UtcNow + 30 ngày`.
- `MonAns`: lọc bỏ dòng trống (không có `TenMonAn`); `DonGia` null → 0.
- Khi sửa POI: load lại entity graph (`POI.Include(MonAns).Include(ThuyetMinhs)`), upsert `BanDich`, deactivate những `MonAn` đã bị xoá khỏi form.
- Bật/tắt hiển thị POI: `IndexModel.OnPostToggleAsync` đảo `POI.TrangThai`.

**Upload ảnh:**
- `POST /api/upload` — giới hạn **5MB**, định dạng: `.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`.
- Lưu tại `VinhKhanhTour.API/wwwroot/uploads/`; URL trả về dạng `/uploads/{filename}`.

**Xử lý lỗi:**
- `DbUpdateConcurrencyException` khi sửa: log warning, set `TempData["Error"]`, redirect reload trang Edit.
- Lỗi DB khác: log error, hiển thị `TempData["Error"]`, render lại form.

---

### F07 – Ghi nhận phí duy trì và lịch sử hóa đơn POI (CMS)

**Mô tả:** Admin ghi nhận thanh toán phí duy trì hàng tháng của chủ quán và xem lịch sử hóa đơn.
**Diagrams:** `docs/diagrams/07-maintenance-payment-activity.puml`, `07-maintenance-payment-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI | `Pages/ThanhToan/Index.cshtml`, `GhiNhan.cshtml`, `LichSu.cshtml` |
| PageModel | `ThanhToan/IndexModel.OnGetAsync`, `GhiNhanModel.OnGetAsync / OnPostAsync / LoadPoiAsync`, `LichSuModel.OnGetAsync` |
| Persistence | `AppDbContext` → PostgreSQL (bảng `poi`, `dangkydichvu`, `hoadon`) |

**Luồng ghi nhận thanh toán (`GhiNhanModel.OnPostAsync`):**
1. `LoadPoiAsync(poiId)` → đọc POI + gói `DangKyDichVu` + 5 hóa đơn gần nhất.
2. Tính **mốc gia hạn**: nếu POI còn hạn → `mocGiaHan = NgayHetHanDuyTri hiện tại`; nếu đã quá hạn → `mocGiaHan = now`.
3. Cập nhật `POI.NgayHetHanDuyTri = mocGiaHan + soThangGiaHan tháng`, upsert `DangKyDichVu` (phí, ghi chú).
4. Tạo dòng `HoaDon` cho **từng kỳ `yyyy-MM`** trong khoảng gia hạn (mỗi tháng một dòng).
5. `SaveChangesAsync()` → redirect `/ThanhToan?msg=...`.

**Lịch sử (`LichSuModel.OnGetAsync(poiId)`):** đọc `HoaDons` theo POI, sắp xếp giảm dần `NgayThanhToan`, render bảng.

**Cảnh báo quá hạn:** Dashboard (F10) hiển thị `SoQuanQuaHan`; POI quá hạn bị **ẩn khỏi app** (xem F02 điều kiện lọc).

---

### F08 – Duyệt hoặc từ chối thanh toán gói app (CMS)

**Mô tả:** Admin xem và xử lý yêu cầu thanh toán gói app do khách gửi từ F05.
**Diagrams:** `docs/diagrams/08-app-payment-approval-activity.puml`, `08-app-payment-approval-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI | `Pages/DuyetThanhToan/Index.cshtml` (3 tab: `cho_duyet`, `da_duyet`, `tu_choi`) + JS polling |
| PageModel | `DuyetThanhToan.IndexModel.OnGetAsync(tab)`, `OnGetPendingSnapshotAsync`, `OnPostApproveAsync(yeuCauId)`, `OnPostRejectAsync(yeuCauId, lyDo)` |
| Persistence | `AppDbContext` → PostgreSQL (bảng `yeucauthanhtoan`, `dangkyapp`) |

**Polling snapshot (JS → `OnGetPendingSnapshotAsync`):**
- Trả về `{ Count, LatestId, LatestTransferContent }` để cập nhật badge và toast khi có yêu cầu mới mà không cần reload.

**Duyệt (`OnPostApproveAsync`):**
1. Đọc `YeuCauThanhToan` + `DangKyApp` còn hạn của `MaThietBi`.
2. Tính `mocBatDau`: nếu còn hạn → `NgayHetHan` hiện tại; nếu hết hạn → `now`.
3. Tính `NgayHetHan` mới = `mocBatDau + thời hạn của LoaiGoi`.
4. Tạo `DangKyApp` mới + đổi `YeuCauThanhToan.TrangThai = da_duyet`, set `NgayDuyet`.
5. Redirect `?tab=cho_duyet`.

**Từ chối (`OnPostRejectAsync`):**
1. Đọc `YeuCauThanhToan`.
2. Cập nhật `TrangThai = tu_choi`, `NgayDuyet = now`, `GhiChuAdmin = lyDo`.
3. Redirect `?tab=tu_choi`.

---

### F09 – Bản đồ Live & Theo dõi khách hàng (CMS)

**Mô tả:** CMS render server-side trang theo dõi khách hàng — bảng danh sách + summary tiles tổng hợp từ `dangkyapp`, `vitrikhach`, `lichsuphat`.
**Diagrams:** `docs/diagrams/09-live-map-activity.puml`, `09-live-map-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI | `Pages/BanDo/Index.cshtml` — tham số `search`, `filter`, `sort`, `dir` |
| PageModel | `BanDo.IndexModel.OnGetAsync`, `LoadSubscriptionsAsync`, `LoadLocationsAsync`, `LoadPoiActivityCountsAsync`, `BuildCustomerRows`, `ApplySearch`, `ApplyFilter`, `ApplySort`, helper `DescribeRemaining`, `SubscriptionBadgeClass`, `ActivityBadgeClass`, `ActivityBadgeText` |
| Persistence helper | `ExecuteRawReadAsync` (raw read helper trên `Database.GetConnectionString()`) → PostgreSQL |

**Mốc thời gian tính toán trong `OnGetAsync`:**
- `now` = `DateTime.UtcNow`.
- `onlineCutoff = now - 2 phút` → tiêu chí khách đang online.
- `expiringSoonCutoff = now + 7 ngày` → tiêu chí gói sắp hết hạn.

**4 nguồn dữ liệu (đọc qua `ExecuteRawReadAsync`):**

| Helper | Tổng hợp |
|---|---|
| `LoadSubscriptionsAsync()` | Theo `MaThietBi`: `max(NgayHetHan)`, số gói `paid`, tổng tiền đã chi |
| `LoadLocationsAsync()` | Snapshot vị trí từ `vitrikhach` — **chỉ lấy `lat/lng` trong vùng phục vụ và khác (0,0)** |
| `LoadPoiActivityCountsAsync(VisitedSourceValues)` | `count DISTINCT POIId` theo `Nguon ∈ {GPS, APP-GEOFENCE, APP_GEOFENCE, GEOFENCE}` |
| `LoadPoiActivityCountsAsync(ViewedSourceValues)` | `count DISTINCT POIId` theo `Nguon ∈ {VIEW}` |

**`BuildCustomerRows()`:** hợp nhất `MaThietBi` từ 4 nguồn, tính `expiresAt`, `remainingDays/Hours`, `currentPoi`, `isOnline`, `viewedCount`, `visitedCount`, `paidPackagesCount`, `totalSpent`, `experiencePoints`, `level`.

**Summary tiles:** `TotalCustomers`, `OnlineCustomers`, `ActiveSubscriptionCustomers`, `ExpiringSoonCustomers`.

**Tìm kiếm / Lọc / Sắp xếp:** `ApplySearch` (theo `MaThietBi` rút gọn / POI hiện tại), `ApplyFilter` (online, paid, expiring_soon...), `ApplySort` (theo last activity / XP / spent).

> **Lưu ý cập nhật v1.1:** Trang BanDo trong v1.0 là Leaflet map + JS fetch `HeartbeatController`. Phiên bản hiện tại đã chuyển hoàn toàn sang **server-side rendering** + bảng + summary tiles, **không còn** JS polling `/api/heartbeat/active`. Các API heartbeat (mục 8.3) vẫn phục vụ mobile app.

---

### F10 – Dashboard tổng quan và monitoring CMS

**Mô tả:** Trang chủ CMS (`/`) hiển thị tổng quan POI, monitoring hoạt động khách, doanh thu theo nhiều phạm vi thời gian.
**Diagrams:** `docs/diagrams/10-dashboard-activity.puml`, `10-dashboard-sequence.puml`.

**Phân lớp triển khai:**

| Lớp | Thành phần |
|---|---|
| UI | `Pages/Index.cshtml` — tham số `mode`, `date`, `week`, `month`, `year`, `from`, `to` |
| PageModel | `CMS.IndexModel.OnGetAsync`, `ResolveRange`, `LoadPoiSectionAsync`, `LoadAnalyticsWithRetryAsync`, `FillMissingBuckets`, helper `ImageUrlHelper.ResolvePoi` |
| Analytics read helper | `LoadAnalyticsAsync`, `LoadActivityAsync`, `LoadGeoAsync`, `LoadTopPoiAsync`, `LoadRevenueAsync`, `LoadSummaryAsync` (chia sẻ chung 1 `DbConnection` từ `AppDbContext.Database.GetDbConnection()`) |
| Persistence | `AppDbContext` → PostgreSQL |

**`ResolveRange()` — 11 chế độ phạm vi thời gian:** `today`, `yesterday`, `this_week`, `last_week`, `this_month`, `last_month`, `this_year`, `day`, `week`, `month`, `year`, `custom` (trong đó `day/week/month/year` ghép cặp với `date/week/month/year`; `custom` dùng `from/to`).

**2 pha tải dữ liệu:**
1. **POI section (`LoadPoiSectionAsync`):** đọc `POIs.Include(MonAns)` theo `MucUuTien` → tính `TongPOI`, `POIDangHoatDong`, `SoQuanQuaHan`.
2. **Analytics (`LoadAnalyticsWithRetryAsync`):** mở `DbConnection` chia sẻ → gọi 5 nhóm helper bên dưới. Khi lỗi pool, `ClearNpgsqlPoolsQuietly` rồi retry một lần.

**5 nhóm analytics (mỗi nhóm 1 helper):**

| Helper | Đầu ra | Mô tả |
|---|---|---|
| `LoadActivityAsync` | `ActivityBuckets[]` | Chuỗi thời gian: active users / views / visits theo bucket (`granularity`) |
| `LoadGeoAsync` | `GeoPoints[]` | Toạ độ POI để vẽ heatmap, **chỉ lấy điểm nằm trong vùng phục vụ** |
| `LoadTopPoiAsync` | `TopPoi[]` | Top POI theo `views` / `visits` |
| `LoadRevenueAsync` | `RevenueBuckets[]` | Doanh thu app (`dangkyapp`) + duy trì (`hoadon`) theo bucket |
| `LoadSummaryAsync` | `summary` | `TotalActiveDevices`, `TotalViews`, `TotalVisits`, `TotalRevenue` |

**`FillMissingBuckets`:** chèn bucket trống để chart hiển thị liên tục. Sau cùng, IndexModel serialize `ActivityJson`, `GeoJson`, `RevenueJson` cho client render.

**Stats grid (3 thẻ thống kê chính):**

| Thẻ | Công thức |
|---|---|
| `TongPOI` | `POIs.Count()` — tất cả POI, **kể cả POI chưa gia hạn** |
| `POIDangHoatDong` | `POIs.Count(p => p.TrangThai && p.NgayHetHanDuyTri >= now)` |
| `Ngôn ngữ` | Hard-code = **3** (vi/en/zh) |

Ngoài ra còn `SoQuanQuaHan = POIs.Count(p => p.TrangThai && (p.NgayHetHanDuyTri == null || p.NgayHetHanDuyTri < now))` để cảnh báo cho F07.

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

- **Xác thực CMS:** đăng nhập admin qua `POST /api/auth/login` (kiểm tra `TenDangNhap` + `MatKhau` trong bảng `taikhoan`); đăng ký qua `POST /api/auth/register`. *Phiên bản demo dùng plain-text password — sản phẩm thực tế cần BCrypt + JWT.* (Tính năng phụ trợ, không có sequence diagram riêng.)
- CMS chạy nội bộ (không public internet) — không cần auth phức tạp cho demo.
- App không lưu thông tin cá nhân — chỉ Device UUID (tự sinh).
- Supabase connection string không commit vào repository công khai.

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
    ├── Index.cshtml(.cs)           ← Dashboard tổng quan + monitoring (F10)
    ├── Poi/                        ← CRUD quán ăn (F06)
    ├── ThuyetMinh/                 ← Quản lý thuyết minh (F06)
    ├── ThanhToan/                  ← Phí duy trì POI (F07)
    ├── DuyetThanhToan/             ← Duyệt thanh toán app (F08)
    └── BanDo/                      ← Theo dõi khách hàng / live customer tracking (F09)
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
  - Quản lý quán ăn (CRUD, upload ảnh) — F06
  - Quản lý nội dung thuyết minh + bản dịch đa ngôn ngữ — F06
  - Ghi nhận phí duy trì quán hàng tháng + xem lịch sử hóa đơn — F07
  - Duyệt/từ chối yêu cầu thanh toán app — F08
  - Theo dõi khách hàng & trạng thái sử dụng (online, gói, XP, POI đã ghé/xem) — F09
  - Xem dashboard tổng quan + monitoring (POI, hoạt động, geo, top POI, doanh thu) — F10
```

---

## 11. GIỚI HẠN & PHẠM VI ĐỒ ÁN

| Tính năng | Trạng thái |
|---|---|
| Hướng dẫn du lịch tự động (audio guide) | ✅ Hoàn thành |
| Đa ngôn ngữ (vi/en/zh) | ✅ Hoàn thành |
| Geofence tự động phát thuyết minh | ✅ Hoàn thành |
| Subscription & QR payment (App, F05) | ✅ Hoàn thành |
| CMS quản lý POI / thuyết minh / menu (F06) | ✅ Hoàn thành |
| Phí duy trì POI (F07) | ✅ Hoàn thành |
| Duyệt thanh toán app từ CMS (F08) | ✅ Hoàn thành |
| Theo dõi khách hàng & trạng thái sử dụng (F09) | ✅ Hoàn thành |
| Dashboard tổng quan & monitoring CMS (F10) | ✅ Hoàn thành |
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
