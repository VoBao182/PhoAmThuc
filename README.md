# VinhKhanhTourDemo

Đồ án gồm 3 phần chính:

- `VinhKhanhTour.API`: backend ASP.NET Core Web API, lưu dữ liệu POI, đăng ký, thanh toán, heartbeat GPS.
- `VinhKhanhTour.CMS`: trang quản trị Razor Pages cho admin quản lý POI, thuyết minh, thanh toán và theo dõi khách.
- `VinhKhanhTourDemo`: ứng dụng .NET MAUI cho khách tham quan, tự phát thuyết minh khi đi vào vùng POI.

## 1. Yêu cầu môi trường

Cần cài:

- .NET SDK 10.0, repo đang dùng `global.json`.
- Visual Studio có workload .NET MAUI nếu chạy app mobile/Windows.
- Supabase/Postgres connection string nếu chạy API/CMS thật.
- Node.js không bắt buộc, chỉ cần nếu chạy Appium hoặc công cụ phụ.
- Docker không bắt buộc cho chạy local và automation test. Docker chỉ cần khi muốn kiểm tra Dockerfile giống Render.

## 2. Cấu hình database local

API và CMS thật cần connection string Supabase. Chạy lệnh sau một lần:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\set-supabase-connection.ps1
```

Script sẽ hỏi `SUPABASE_CONNECTION_STRING`, lưu vào user environment và tạo file local bị ignore bởi Git:

- `VinhKhanhTour.API/appsettings.Development.Local.json`
- `VinhKhanhTour.CMS/appsettings.Development.Local.json`

Nếu chỉ chạy automation test mặc định thì không cần Supabase, vì test dùng SQLite/test host riêng.

## 3. Cách chạy đồ án local

Mở 2 terminal riêng cho API và CMS.

### Chạy API

```powershell
dotnet run --project .\VinhKhanhTour.API\VinhKhanhTour.API.csproj --launch-profile http
```

API local chạy tại:

```text
http://localhost:5118
```

Kiểm tra nhanh:

```powershell
Invoke-WebRequest http://localhost:5118/health
```

### Chạy CMS

```powershell
dotnet run --project .\VinhKhanhTour.CMS\VinhKhanhTour.CMS.csproj --launch-profile http
```

CMS local chạy tại:

```text
http://localhost:5213
```

Tài khoản đăng nhập CMS:

```text
Tài khoản: admin
Mật khẩu: admin
```

### Chạy app MAUI

Chạy trên Windows:

```powershell
dotnet run --project .\VinhKhanhTourDemo\VinhKhanhTourDemo.csproj -f net10.0-windows10.0.19041.0
```

Mặc định app ưu tiên API public đã deploy:

```text
https://phoamthuc.onrender.com
```

Nếu muốn ép app dùng API local, build/run với `HostedApiBaseUrl`:

```powershell
dotnet run --project .\VinhKhanhTourDemo\VinhKhanhTourDemo.csproj -f net10.0-windows10.0.19041.0 -p:HostedApiBaseUrl=http://localhost:5118
```

Nếu test trên điện thoại Android qua USB, bật API local trước rồi dùng ADB reverse:

```powershell
adb reverse tcp:5118 tcp:5118
dotnet build .\VinhKhanhTourDemo\VinhKhanhTourDemo.csproj -f net10.0-android -c Debug -p:HostedApiBaseUrl=http://127.0.0.1:5118
```

## 4. Deploy Render

File deploy nằm ở:

```text
render.yaml
```

Render có 2 service:

- `phoamthuc`: API.
- `phoamthuc-cms`: CMS.

Biến môi trường quan trọng:

- `SUPABASE_CONNECTION_STRING`: bắt buộc cho cả API và CMS.
- `ADMIN_API_TOKEN`: dùng cho API admin endpoint.
- `CMS_ADMIN_USERNAME`: hiện đang là `admin`.
- `CMS_ADMIN_PASSWORD`: hiện đang là `admin`.
- `ApiBaseUrl`: CMS gọi tới API public.

Lưu ý: `admin/admin` chỉ phù hợp để demo hoặc nộp đồ án. Nếu đưa hệ thống public lâu dài, nên đổi mật khẩu trên Render Environment.

Sau khi sửa `render.yaml` hoặc Dockerfile, push code rồi chọn Manual Deploy trên Render. Nếu build bị lỗi lạ, chọn Clear build cache & deploy.

## 5. Cách chạy automation test

Chạy toàn bộ baseline automation test:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-all-tests.ps1
```

Kết quả xuất ra:

```text
TestResults/api-tests.trx
TestResults/cms-e2e-tests.trx
TestResults/maui-appium-tests.trx
TestResults/run-all-tests-YYYYMMDD-HHMMSS.log
```

File `.trx` là kết quả test theo format của `dotnet test`. File `.log` là log console đầy đủ của lần chạy, dùng để nộp minh chứng hoặc gửi khi cần debug.

Muốn chỉ định tên file log riêng:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-all-tests.ps1 -LogPath .\TestResults\automation-test.log
```

Automation test mặc định gồm:

- API integration tests.
- CMS Playwright E2E tests chạy Chromium headless.
- MAUI logic tests kiểm tra chọn POI theo geofence, xử lý 2 POI chồng bán kính, hysteresis và hàng đợi phát POI.
- MAUI contract tests kiểm tra `AutomationId` trong XAML.

Đây là functional/integration/E2E/logic automation test, không phải load test đo tải hệ thống. Phần logic test hiện tập trung vào nghiệp vụ quan trọng của app như chọn POI và hàng đợi phát audio.

Không cần bật Docker để chạy automation test mặc định.

### Chạy riêng từng nhóm test

API:

```powershell
dotnet test .\tests\VinhKhanhTour.API.Tests\VinhKhanhTour.API.Tests.csproj
```

CMS E2E:

```powershell
dotnet test .\tests\VinhKhanhTour.CMS.E2ETests\VinhKhanhTour.CMS.E2ETests.csproj
```

MAUI logic/contract/Appium:

```powershell
dotnet test .\tests\VinhKhanhTour.MAUI.AppiumTests\VinhKhanhTour.MAUI.AppiumTests.csproj
```

### Chạy Appium thật trên Android

Phần này không chạy mặc định. Cần emulator hoặc điện thoại Android, Appium server và APK.

Cài Appium:

```powershell
npm install -g appium
appium driver install uiautomator2
```

Build APK:

```powershell
dotnet publish .\VinhKhanhTourDemo\VinhKhanhTourDemo.csproj -f net10.0-android -c Debug
```

Mở emulator hoặc cắm điện thoại, kiểm tra:

```powershell
adb devices
appium --base-path /
```

Terminal khác:

```powershell
$env:RUN_APPIUM_TESTS = "1"
$env:APPIUM_SERVER_URL = "http://127.0.0.1:4723"
$env:APPIUM_APP_PATH = "$env:LOCALAPPDATA\VinhKhanhTourDemo\bin\VinhKhanhTourDemo\Debug\net10.0-android\android-x64\publish\com.companyname.vinhkhanhtourdemo-Signed.apk"

powershell -ExecutionPolicy Bypass -File .\scripts\run-all-tests.ps1 -WithAppium
```

Khi Appium fail, artifact được lưu ở:

```text
TestResults/artifacts/appium
```

## 6. Automation test đang kiểm tra phần nào

### API integration tests

Kiểm tra các luồng backend chính:

- `/health` trả OK.
- Đăng ký dùng thử 3 ngày, mỗi thiết bị chỉ dùng thử một lần.
- Tạo yêu cầu thanh toán gói trả phí.
- Admin duyệt/từ chối yêu cầu thanh toán.
- Chặn duyệt/từ chối nếu thiếu admin token.
- Gia hạn gói đang còn hạn.
- Danh sách POI chỉ trả quán đang hoạt động và còn hạn duy trì.
- Chi tiết POI trả đúng bản dịch theo ngôn ngữ, fallback về tiếng Việt nếu thiếu ngôn ngữ.
- Ẩn món ăn không còn active.
- Ghi nhận phí duy trì và tạo hóa đơn.
- Chặn số tháng gia hạn không hợp lệ hoặc POI không tồn tại.
- Ghi nhận phí convert TTS khi POI còn hạn duy trì.

### CMS Playwright E2E tests

Kiểm tra CMS bằng trình duyệt Chromium headless:

- Trang quản trị redirect về Login khi chưa đăng nhập.
- Đăng nhập admin test.
- Dashboard và danh sách POI hiển thị dữ liệu seed.
- Tìm kiếm POI.
- Tạo POI mới.
- Sửa POI và kiểm tra dữ liệu được lưu vào database.
- Ghi nhận phí duy trì, gia hạn POI và tạo hóa đơn.
- Duyệt và từ chối yêu cầu thanh toán app.

### MAUI logic/contract/Appium tests

Mặc định kiểm tra logic app và contract UI:

- Chọn POI trong bán kính geofence.
- Ưu tiên `MucUuTien` trước khoảng cách.
- Nếu cùng ưu tiên thì chọn POI gần hơn.
- Nếu cùng ưu tiên và cùng khoảng cách thì chọn theo tên bảng chữ cái.
- Giữ POI hiện tại khi GPS dao động trong buffer 5 mét.
- Chuyển POI khi POI hiện tại đã xa hơn buffer.
- Hàng đợi phát POI không enqueue trùng POI.
- Hàng đợi không enqueue POI đang phát.
- Hàng đợi phát theo thứ tự FIFO.
- Các page quan trọng có `AutomationId`.
- Không bị trùng `AutomationId`.

Khi bật Appium thật, kiểm tra thêm:

- Appium server reachable.
- App Android mở được vào một trong các màn hình đầu vào hợp lệ: launch, subscription hoặc main.

## 7. Cách vận hành các chức năng chính

### 7.1 Quản lý POI trên CMS

Admin đăng nhập CMS bằng `admin/admin`, sau đó vào mục quản lý POI để:

- Tạo quán/POI mới.
- Sửa tên, địa chỉ, số điện thoại, ảnh đại diện.
- Nhập tọa độ `ViDo`, `KinhDo`.
- Nhập `BanKinh` để tạo vùng geofence, đơn vị mét.
- Nhập `MucUuTien` để quyết định thứ tự ưu tiên khi khách nằm trong nhiều POI.
- Nhập nội dung thuyết minh tiếng Việt, tiếng Anh, tiếng Trung.
- Nhập menu/món ăn và trạng thái món.
- Bật/tắt trạng thái POI.

API chỉ trả về POI cho app khi POI thỏa các điều kiện:

- `TrangThai = true`.
- Có `NgayHetHanDuyTri`.
- `NgayHetHanDuyTri >= thời điểm hiện tại`.

Danh sách POI được sắp xếp theo `MucUuTien`.

### 7.2 Luồng app tải danh sách POI

Khi người dùng mở app và đã qua màn hình đăng ký:

1. App gọi `GET /api/poi`.
2. API trả danh sách POI đang hoạt động, còn hạn duy trì.
3. App hiển thị danh sách, bản đồ và các marker POI.
4. Nếu API lỗi, app dùng dữ liệu fallback để UI vẫn có thể demo.
5. Trong lúc app đang chạy, app refresh danh sách POI ngầm theo chu kỳ để nhận thay đổi từ CMS.

### 7.3 Luồng geofence và tự phát audio

Khi app lấy được vị trí GPS:

1. App tính khoảng cách từ vị trí người dùng tới từng POI.
2. App lọc các POI mà khoảng cách nhỏ hơn hoặc bằng `BanKinh`.
3. Nếu không nằm trong bán kính POI nào, app xóa POI hiện tại và bỏ highlight.
4. Nếu nằm trong một hoặc nhiều POI, app chọn POI phù hợp nhất.
5. Người dùng phải đứng trong vùng POI đủ 5 giây (`dwell time`) thì app mới xác nhận là đang ở POI đó.
6. Sau khi xác nhận, app highlight POI, ghi nhận lịch sử đã ghé, gửi tín hiệu lên server và đưa POI vào hàng đợi phát thuyết minh.

Việc đợi 5 giây giúp tránh trường hợp người dùng đi ngang qua hoặc GPS bị lệch nhẹ mà app phát audio sai.

### 7.4 Khi người dùng nằm giữa 2 POI

Nếu người dùng nằm trong bán kính nhiều POI cùng lúc, app chọn theo thứ tự:

1. `MucUuTien` nhỏ hơn được ưu tiên trước.
2. Nếu cùng `MucUuTien`, POI gần người dùng hơn được ưu tiên.
3. Nếu vẫn bằng nhau, sắp xếp theo tên `TenPOI` theo bảng chữ cái.

Ngoài ra app có cơ chế chống nhấp nháy khi GPS dao động:

- Nếu người dùng đang ở POI hiện tại.
- POI hiện tại và POI mới có cùng `MucUuTien`.
- Khoảng cách của POI hiện tại không xa hơn POI mới quá 5 mét.

Thì app giữ POI hiện tại, không đổi qua lại liên tục. Đây là cơ chế hysteresis để xử lý GPS jitter.

### 7.5 Heartbeat từ app lên server/CMS

Trong lúc app chạy, app gửi heartbeat định kỳ lên API:

```text
POST /api/heartbeat
```

Body gồm:

```text
MaThietBi
Lat
Lng
PoiIdHienTai
TenPoiHienTai
```

Server lưu vào bảng `vitrikhach`, mỗi thiết bị một dòng mới nhất. CMS dùng dữ liệu này để biết khách nào đang online và đang ở POI nào.

Server không tin tuyệt đối POI app báo. Khi nhận heartbeat, server kiểm tra lại:

- Tọa độ có nằm trong vùng phục vụ không.
- `PoiIdHienTai` có tồn tại và đang active không.
- Khoảng cách tới POI có nằm trong `BanKinh + 5m` không.

Nếu không hợp lệ, server vẫn ghi thiết bị online nhưng bỏ POI hiện tại, CMS sẽ hiểu khách đang di chuyển.

### 7.6 Ghi nhận khách đã ghé POI

Khi audio tự động bắt đầu phát do geofence, app gọi:

```text
POST /api/heartbeat/visit
```

Server ghi vào bảng `lichsuphat`. Để tránh trùng dữ liệu, server chỉ ghi một lượt ghé cho cùng thiết bị/POI nếu chưa có record trong 10 phút gần nhất.

App cũng có cooldown 10 phút cho từng POI. Nghĩa là nếu người dùng đứng lâu ở một POI, app không lặp lại audio liên tục.

### 7.7 Hàng đợi phát audio

Khi có POI cần phát:

1. App đưa POI vào queue.
2. Nếu POI đang phát hoặc đã nằm trong queue thì không thêm trùng.
3. Một semaphore `_speakLock` đảm bảo mỗi thời điểm chỉ phát một audio/TTS.
4. Sau khi phát xong POI hiện tại, app mới xử lý POI tiếp theo trong queue.

Cơ chế này giúp tránh nhiều audio chồng lên nhau khi người dùng di chuyển qua nhiều vùng geofence gần nhau.

### 7.8 Nguồn audio và thuyết minh

Có 2 kiểu phát:

- Tự động khi vào geofence: app lấy nội dung thuyết minh từ API rồi đọc bằng Text-to-Speech.
- Trong trang chi tiết POI: nếu POI có `FileAudio`, app phát file audio qua WebView audio player; nếu không có file audio thì dùng nội dung thuyết minh/TTS fallback.

Trang chi tiết gọi:

```text
GET /api/poi/{id}?lang=vi|en|zh
```

API trả:

- Tên POI.
- Địa chỉ, số điện thoại.
- Ảnh đại diện.
- Nội dung thuyết minh.
- File audio nếu có.
- Danh sách món ăn đang active.

Nếu không có bản dịch theo ngôn ngữ người dùng chọn, API fallback về bản tiếng Việt.

### 7.9 Xem chi tiết POI

Khi người dùng bấm vào POI:

1. App mở `DetailPage`.
2. App gọi API lấy chi tiết POI.
3. App hiển thị ảnh, mô tả, thuyết minh, menu, nút nghe audio và chỉ đường.
4. App gửi log xem chi tiết:

```text
POST /api/heartbeat/view
```

Server dedup lượt xem trong 5 phút để tránh ghi trùng khi người dùng mở lại nhiều lần.

### 7.10 Đăng ký gói app

App định danh người dùng bằng `MaThietBi`, không bắt buộc tài khoản.

Các gói:

- `thu`: dùng thử 3 ngày, miễn phí, mỗi thiết bị chỉ dùng một lần.
- `ngay`: 1 ngày.
- `tuan`: 1 tuần.
- `thang`: 1 tháng.
- `nam`: 1 năm.

Luồng dùng thử:

1. App gọi `POST /api/subscription/purchase` với `LoaiGoi = thu`.
2. API kiểm tra thiết bị đã dùng thử chưa.
3. Nếu hợp lệ, tạo gói dùng thử 3 ngày.

Luồng gói trả phí:

1. App tạo yêu cầu thanh toán bằng `POST /api/subscription/request`.
2. API trả số tiền và nội dung chuyển khoản.
3. Người dùng chuyển khoản theo nội dung đó.
4. Admin vào CMS duyệt thanh toán.
5. API kích hoạt/gia hạn gói cho thiết bị.
6. App polling trạng thái yêu cầu để biết đã được duyệt hay bị từ chối.

### 7.11 Duyệt thanh toán trên CMS

Admin vào CMS, mục duyệt thanh toán:

- Xem yêu cầu đang chờ.
- Bấm duyệt nếu đã nhận tiền.
- Bấm từ chối và nhập lý do nếu sai nội dung/số tiền.

Khi duyệt:

- Trạng thái yêu cầu đổi sang `da_duyet`.
- Bảng `DangKyApps` được thêm/gia hạn.
- Nếu thiết bị đang có gói còn hạn, gói mới được cộng nối tiếp từ ngày hết hạn cũ.

Khi từ chối:

- Trạng thái yêu cầu đổi sang `tu_choi`.
- Lý do được lưu vào `GhiChuAdmin`.
- App đọc trạng thái này để thông báo cho người dùng.

### 7.12 Gia hạn phí duy trì POI

POI chỉ hiển thị trên app khi còn hạn duy trì. Admin thao tác trên CMS:

1. Vào mục thanh toán/gia hạn.
2. Chọn POI.
3. Nhập số tháng gia hạn.
4. Ghi nhận thanh toán.

API xử lý:

- Nếu POI còn hạn, gia hạn nối tiếp từ ngày hết hạn cũ.
- Nếu POI đã hết hạn, gia hạn từ thời điểm hiện tại.
- Tạo hóa đơn từng tháng trong bảng `HoaDon`.
- Bật lại `TrangThai = true` nếu POI trước đó bị ẩn do hết hạn.

### 7.13 Theo dõi khách trên CMS

CMS đọc dữ liệu heartbeat để hiển thị:

- Thiết bị đang online.
- Vị trí GPS mới nhất.
- POI hiện tại nếu server xác minh hợp lệ.
- Số POI đã xem.
- Số POI đã ghé.
- Điểm kinh nghiệm/level.
- Thời hạn gói đăng ký còn lại.

Thiết bị được xem là online nếu heartbeat gần nhất còn trong khoảng thời gian server quy định.

## 8. Các endpoint chính

API:

- `GET /health`: kiểm tra API sống.
- `GET /health/db`: kiểm tra kết nối database.
- `GET /api/poi`: danh sách POI active và còn hạn.
- `GET /api/poi/{id}?lang=vi`: chi tiết POI.
- `POST /api/heartbeat`: cập nhật vị trí thiết bị.
- `POST /api/heartbeat/visit`: ghi nhận khách vào geofence.
- `POST /api/heartbeat/view`: ghi nhận khách xem chi tiết POI.
- `POST /api/heartbeat/sync-history`: đồng bộ lịch sử từ app lên server.
- `GET /api/heartbeat/active`: danh sách thiết bị online.
- `GET /api/subscription/plans`: danh sách gói.
- `GET /api/subscription/status/{maThietBi}`: trạng thái đăng ký.
- `POST /api/subscription/purchase`: đăng ký dùng thử hoặc mua trực tiếp.
- `POST /api/subscription/request`: tạo yêu cầu thanh toán.
- `GET /api/subscription/request/{id}`: kiểm tra trạng thái yêu cầu.
- `POST /api/payment/maintenance`: ghi nhận phí duy trì POI.
- `POST /api/payment/convert/{poiId}`: ghi nhận phí convert TTS.

CMS:

- `/Login`: đăng nhập CMS.
- `/`: dashboard.
- `/Poi`: quản lý POI.
- `/ThuyetMinh`: quản lý nội dung thuyết minh.
- `/ThanhToan`: ghi nhận phí duy trì POI.
- `/DuyetThanhToan`: duyệt/từ chối thanh toán app.
- `/BanDo`: theo dõi vị trí và hoạt động khách.

## 9. Ghi chú khi demo

Nên demo theo thứ tự:

1. Mở API và kiểm tra `/health`.
2. Mở CMS, đăng nhập `admin/admin`.
3. Tạo hoặc sửa POI, nhập tọa độ, bán kính, mức ưu tiên và thuyết minh.
4. Mở app, kiểm tra danh sách POI.
5. Giả lập hoặc di chuyển GPS vào bán kính POI.
6. Chờ ít nhất 5 giây để app xác nhận dwell.
7. App phát thuyết minh và gửi heartbeat/visit lên server.
8. Mở CMS để xem thiết bị online, POI hiện tại và lịch sử ghé.
9. Demo trường hợp 2 POI chồng bán kính bằng cách đặt cùng tọa độ/bán kính và thay đổi `MucUuTien`.
10. Demo thanh toán: tạo yêu cầu trong app, duyệt trên CMS, app nhận trạng thái đã duyệt.

Nếu cần chứng minh automation test, chạy:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-all-tests.ps1
```

Sau đó mở các file `.trx` trong `TestResults`.

Nếu cần log console đầy đủ cho báo cáo, mở thêm file `.log` mới nhất trong `TestResults`, ví dụ `final-comprehensive-check.log` hoặc `run-all-tests-YYYYMMDD-HHMMSS.log`.
