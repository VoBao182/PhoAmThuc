# Bo so do cho do an VinhKhanhTour

Bo tep nay duoc trich tu code hien co trong 3 phan he thong:

- `VinhKhanhTourDemo` (MAUI app cho khach du lich)
- `VinhKhanhTour.API` (backend ASP.NET Core Web API)
- `VinhKhanhTour.CMS` (Razor Pages CMS cho quan tri)

## Cach hieu "chuc nang" va "task"

- `Chuc nang`: muc tieu nghiep vu co gia tri doc lap, co actor ro rang va co the dua vao use case.
- `Task`: thao tac nho nam ben trong chuc nang, thuong la cac buoc giao dien, goi ham, goi API, luu DB, polling, upload, render...

Vi du: `Mua goi tra phi` la chuc nang. Cac task con ben trong no gom `SetupUI()`, tao noi dung chuyen khoan, `POST /api/subscription/request`, polling trang thai, luu `Preferences`.

## Cac chuc nang da mo hinh hoa

| ID | Chuc nang | Actor chinh | Task tieu bieu | Code can cu chinh | Tep so do |
| --- | --- | --- | --- | --- | --- |
| F01 | Kich hoat quyen su dung app va mo khoa subscription gate | Khach du lich | `MainPage.OnAppearing()`, `EnsureSubscriptionGateAsync()`, `SubscriptionPage.OnMuaGoiClicked()`, `ActivateFreeTrialAsync()` | `VinhKhanhTourDemo/MainPage.xaml.cs`, `VinhKhanhTourDemo/SubscriptionPage.xaml.cs`, `VinhKhanhTour.API/Controllers/SubscriptionController.cs` | `00-overall-usecase.puml`, `01-subscription-gate-activity.puml`, `01-subscription-gate-sequence.puml` |
| F02 | Tai va kham pha danh sach POI | Khach du lich | `LoadPoisFromApi()`, `RefreshPoisInBackgroundAsync()`, `RenderPoiCards()`, `OnSearchTextChanged()`, `OpenPoiDetailAsync()` | `VinhKhanhTourDemo/MainPage.xaml.cs`, `VinhKhanhTour.API/Controllers/PoiController.cs`, `VinhKhanhTour.API/Controllers/HeartbeatController.cs` | `02-poi-explore-activity.puml`, `02-poi-explore-sequence.puml` |
| F03 | Theo doi GPS, geofence va tu dong phat thuyet minh | Khach du lich | `EnsureGpsTrackingAsync()`, `StartGpsTracking()`, `CheckGeofence()`, `SendHeartbeatAsync()`, `RecordPoiVisitAsync()`, `SpeakPoiAsync()`, `LogPlaybackAsync()` | `VinhKhanhTourDemo/MainPage.xaml.cs`, `VinhKhanhTour.API/Controllers/HeartbeatController.cs`, `ThuyetMinhController.cs`, `LogController.cs` | `03-geofence-audio-activity.puml`, `03-geofence-audio-sequence.puml` |
| F04 | Xem chi tiet POI, thuc don, audio guide va chi duong | Khach du lich | `DetailPage.LoadDetail()`, `RenderMenu()`, `ConfigureAudioPlayer()`, `OnNgheClicked()`, `OnMapClicked()` | `VinhKhanhTourDemo/DetailPage.xaml.cs`, `VinhKhanhTour.API/Controllers/PoiController.cs` | `04-poi-detail-activity.puml`, `04-poi-detail-sequence.puml` |
| F05 | Thanh toan goi tra phi va cho duyet | Khach du lich | `PaymentPage.SetupUI()`, `OnDaChuyenKhoanClicked()`, `PaymentStatusPage.StartPollingAsync()`, `PollStatusAsync()` | `VinhKhanhTourDemo/PaymentPage.xaml.cs`, `PaymentStatusPage.xaml.cs`, `SubscriptionController.cs` | `05-paid-plan-activity.puml`, `05-paid-plan-sequence.puml` |
| F06 | Quan ly POI, anh, geofence, thuyet minh va menu tren CMS | Quan tri vien CMS | `Poi/Index`, `Poi/Create`, `Poi/Edit`, `UpsertBanDich()`, `doUpload()`, `uploadImg()`, `uploadMonAnImg()` | `VinhKhanhTour.CMS/Pages/Poi/*.cshtml*`, `VinhKhanhTour.API/Controllers/UploadController.cs` | `06-cms-poi-management-activity.puml`, `06-cms-poi-management-sequence.puml` |
| F07 | Ghi nhan phi duy tri va xem lich su hoa don POI | Quan tri vien CMS | `ThanhToan/Index.OnGetAsync()`, `GhiNhan.OnGetAsync()`, `GhiNhan.OnPostAsync()`, `LichSu.OnGetAsync()` | `VinhKhanhTour.CMS/Pages/ThanhToan/*.cshtml.cs`, cac model `POI`, `DangKyDichVu`, `HoaDon` | `07-maintenance-payment-activity.puml`, `07-maintenance-payment-sequence.puml` |
| F08 | Duyet hoac tu choi thanh toan goi app | Quan tri vien CMS | `DuyetThanhToan/Index.OnGetAsync()`, `OnPostApproveAsync()`, `OnPostRejectAsync()` | `VinhKhanhTour.CMS/Pages/DuyetThanhToan/Index.cshtml.cs`, model `YeuCauThanhToan`, `DangKyApp` | `08-app-payment-approval-activity.puml`, `08-app-payment-approval-sequence.puml` |
| F09 | Giam sat khach dang online va xem hanh trinh live | Quan tri vien CMS | `BanDo/Index.OnGetAsync()`, JS `loadData()`, `showHistory()`, API `GetActive()`, `GetHistory()` | `VinhKhanhTour.CMS/Pages/BanDo/Index.cshtml*`, `HeartbeatController.cs` | `09-live-map-activity.puml`, `09-live-map-sequence.puml` |
| F10 | Xem dashboard tong quan CMS | Quan tri vien CMS | `Index.OnGetAsync()`, tinh `TongPOI`, `TongMonAn`, `SoQuanQuaHan` | `VinhKhanhTour.CMS/Pages/Index.cshtml.cs` | `10-dashboard-activity.puml`, `10-dashboard-sequence.puml` |

## Tep use case tong the

- `00-overall-usecase.puml`: use case tong quan cua toan bo do an.

## Ghi chu pham vi va trung thuc voi code

- `UploadController` duoc mo hinh nhu task ben trong F06, vi no chi phuc vu thao tac tao/sua POI.
- `AuthController` ton tai trong API, nhung hien chua co man hinh MAUI/CMS goi truc tiep trong repo, nen chua dua thanh use case chinh.
- `PaymentController` ton tai voi cac API `status/history/maintenance/convert/overdue`, nhung luong CMS hien tai chu yeu thao tac truc tiep qua `DbContext` trong Razor PageModel. Vi vay no duoc xem la service backend du phong/chua duoc noi day du vao UI hien tai.
- F05 va F08 lien thong voi nhau: F05 la luong khach tao yeu cau va polling; F08 la luong admin xu ly yeu cau trong CMS.

## Cach render

- Mo cac tep `.puml` bang PlantUML extension trong VS Code/IntelliJ, hoac
- copy noi dung vao bat ky PlantUML renderer nao de xuat PNG/SVG.

