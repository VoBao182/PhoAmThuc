from __future__ import annotations

from datetime import datetime
from pathlib import Path

from docx import Document
from docx.enum.section import WD_SECTION
from docx.enum.table import WD_ALIGN_VERTICAL, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor


BASE_DIR = Path(__file__).resolve().parent
DIAGRAM_PNG_DIR = BASE_DIR / "diagrams" / "png"
OUT_DOCX = BASE_DIR / "PRD_VinhKhanhTour.docx"


USE_CASE_SUMMARY = {
    "title": "Use case tổng thể hệ thống",
    "image": "00-overall-usecase.png",
    "description": (
        "Sơ đồ use case tổng thể mô tả toàn bộ phạm vi chức năng đang có trong đồ án. "
        "Các chức năng chính đã phủ hết các màn hình MAUI, các controller API đang được gọi thật, "
        "và các Razor PageModel trong CMS."
    ),
    "coverage_note": (
        "Sau khi đối chiếu lại code hiện tại, chưa phát hiện thêm chức năng nghiệp vụ độc lập nào "
        "cần tách thành một use case mới ngoài F01 đến F10. Các luồng như khôi phục gói bằng mã QR, "
        "khôi phục mã thiết bị, polling, upload ảnh, geofence và analytics đều đã được mô hình hóa "
        "như chức năng chính hoặc subflow của các feature hiện có."
    ),
}


FEATURES = [
    {
        "id": "F01",
        "title": "Khởi động, kích hoạt và khôi phục quyền sử dụng app",
        "actor": "Khách du lịch",
        "status": "VERIFIED",
        "activity_image": "01-subscription-gate-activity.png",
        "sequence_image": "01-subscription-gate-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả chức năng mở ứng dụng, kiểm tra gói đang có, chặn người dùng khi chưa đủ điều kiện sử dụng, "
            "và cho phép khôi phục gói cũ bằng recovery code hoặc mã QR."
        ),
        "capabilities": [
            "Điều hướng khởi động qua LaunchPage rồi quyết định vào MainPage hay SubscriptionPage.",
            "Kiểm tra subscription cục bộ trước, sau đó thử phục hồi trạng thái gói từ server nếu dữ liệu local không còn hợp lệ.",
            "Cho phép dùng gói dùng thử hoặc chuyển sang luồng thanh toán gói trả phí.",
            "Hiển thị recovery payload và QR để người dùng sao chép, nhập tay, dán từ clipboard hoặc quét mã QR để khôi phục thiết bị cũ.",
        ],
        "operation": [
            "Khi app mở, LaunchPage chờ ngắn rồi gọi RouteAsync để kiểm tra SubscriptionState.",
            "Nếu local subscription còn hạn, app đi thẳng vào MainPage; nếu không thì mở SubscriptionPage.",
            "Trong SubscriptionPage, người dùng có thể sao chép recovery code hoặc quét QR; app yêu cầu quyền camera trước khi mở QrScannerPage.",
            "Khi nhận được mã hợp lệ, app gọi TrySetDeviceIdOverride(), sau đó GET /api/subscription/status/{deviceId} để khôi phục trạng thái gói.",
            "Nếu server trả về gói còn hạn, app cập nhật Preferences, cập nhật nút dùng thử và đóng subscription gate.",
        ],
    },
    {
        "id": "F02",
        "title": "Khám phá danh sách POI, đồng bộ lịch sử và khôi phục mã thiết bị",
        "actor": "Khách du lịch",
        "status": "VERIFIED",
        "activity_image": "02-poi-explore-activity.png",
        "sequence_image": "02-poi-explore-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả chức năng tải danh sách quán ăn/POI, tìm kiếm, mở trang chi tiết, đồng bộ lịch sử xem/thăm, "
            "và khôi phục lại mã thiết bị ngay trong tab cài đặt của MainPage."
        ),
        "capabilities": [
            "Tải danh sách POI từ API và dùng fallback data khi API lỗi.",
            "Đồng bộ hồ sơ trải nghiệm, XP, viewed/visited POI từ /api/heartbeat/profile và /api/heartbeat/sync-history.",
            "Hỗ trợ tìm kiếm trực tiếp và render lại card/map theo từ khóa.",
            "Cho phép sao chép hoặc khôi phục mã thiết bị bằng nhập tay, clipboard hoặc quét QR trong tab cài đặt.",
        ],
        "operation": [
            "Khi MainPage xuất hiện lần đầu, app chạy LoadPoisFromApi() rồi gọi GET /api/poi.",
            "Sau khi có dữ liệu, app phục hồi lịch sử từ server, đồng bộ lại local history và render card/map.",
            "Mỗi 20 giây app có thể refresh danh sách POI nền để dữ liệu hiển thị không quá cũ.",
            "Khi người dùng mở chi tiết POI, app ghi viewed local, POST /api/heartbeat/view, rồi đồng bộ lịch sử.",
            "Trong tab cài đặt, khi người dùng nhập hoặc quét mã QR cũ, app gọi RestoreDeviceCodeAsync() để khôi phục subscription, lịch sử xem/thăm và mã QR mới của thiết bị.",
        ],
    },
    {
        "id": "F03",
        "title": "Theo dõi GPS, geofence và tự động phát thuyết minh",
        "actor": "Khách du lịch",
        "status": "VERIFIED",
        "activity_image": "03-geofence-audio-activity.png",
        "sequence_image": "03-geofence-audio-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả phần lõi vận hành theo vị trí: lấy GPS, gửi heartbeat, xác định POI gần nhất, "
            "ghi nhận visit và tự động phát thuyết minh khi người dùng đi vào vùng geofence."
        ),
        "capabilities": [
            "Lấy vị trí định kỳ và cập nhật trạng thái người dùng trên bản đồ.",
            "Gửi heartbeat vị trí về server để CMS biết thiết bị nào đang online.",
            "Xác định geofence theo bán kính từng POI và chống phát lặp bằng cooldown.",
            "Tự động đọc nội dung thuyết minh và ghi log phát audio.",
        ],
        "operation": [
            "MainPage gọi EnsureGpsTrackingAsync(), xin quyền vị trí và bắt đầu vòng lặp GPS.",
            "App lấy vị trí mỗi 5 giây bằng Geolocation.Default.GetLocationAsync().",
            "Heartbeat được gửi mỗi 10 giây dựa trên HEARTBEAT_EVERY_TICKS = 2; refresh POI nền mỗi 20 giây dựa trên POI_REFRESH_EVERY_TICKS = 4.",
            "Khi phát hiện người dùng vừa đi vào một POI mới và đã qua cooldown, app ghi visited local, POST /api/heartbeat/sync-history, POST /api/heartbeat/visit rồi cập nhật UI.",
            "Sau đó app lấy thuyết minh qua /api/thuyet-minh/{poiId}, đọc bằng TTS hoặc nội dung phù hợp và POST /api/log để lưu lịch sử phát.",
        ],
    },
    {
        "id": "F04",
        "title": "Xem chi tiết POI, thực đơn, audio guide và chỉ đường",
        "actor": "Khách du lịch",
        "status": "VERIFIED",
        "activity_image": "04-poi-detail-activity.png",
        "sequence_image": "04-poi-detail-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả trang chi tiết quán ăn: tải nội dung POI, hiện ảnh bìa và món ăn, "
            "phát audio/thuyết minh và mở chỉ đường ngoài app."
        ),
        "capabilities": [
            "Tải chi tiết POI theo ngôn ngữ đang dùng.",
            "Dùng FoodImageCatalog để sinh ảnh fallback cho quán và món ăn khi URL ảnh lỗi hoặc thiếu.",
            "Phát audio file bằng AudioWebView khi có file, hoặc đọc text bằng TextToSpeech khi chỉ có nội dung chữ.",
            "Mở Google Maps / browser để chỉ đường đến quán.",
        ],
        "operation": [
            "DetailPage khởi tạo audio bridge, apply label rồi gọi LoadDetail().",
            "Nếu API GET /api/poi/{id}?lang=... thành công, trang render toàn bộ thông tin quán, menu và cấu hình player audio.",
            "Nếu API lỗi hoặc không trả về dữ liệu hợp lệ, trang chuyển sang UseFallback(tenPoi), dùng ảnh fallback và ẩn các vùng không đủ dữ liệu.",
            "Khi bấm Nghe, nếu có FileAudio thì AudioWebView thực hiện play/pause/stop/seek; nếu không có file thì dùng TextToSpeech đọc nội dung thuyết minh.",
            "Khi bấm Chỉ đường, app mở Google Maps bằng tọa độ của POI.",
        ],
    },
    {
        "id": "F05",
        "title": "Thanh toán gói trả phí và chờ duyệt",
        "actor": "Khách du lịch",
        "status": "VERIFIED",
        "activity_image": "05-paid-plan-activity.png",
        "sequence_image": "05-paid-plan-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả luồng thanh toán gói app bằng mã QR chuyển khoản, từ lúc tạo nội dung chuyển khoản "
            "đến lúc polling trạng thái duyệt và quay lại app."
        ),
        "capabilities": [
            "Sinh nội dung chuyển khoản dạng VKT <GOI> <SHORTID> và URL QR VietQR.",
            "Tạo yêu cầu thanh toán trên server qua /api/subscription/request.",
            "Poll trạng thái duyệt của yêu cầu để biết đã duyệt, từ chối hay còn chờ.",
            "Đóng modal stack đúng logic khi bắt đầu dùng hoặc quay lại thử thanh toán tiếp.",
        ],
        "operation": [
            "Từ SubscriptionPage, người dùng chọn gói trả phí và mở PaymentPage.",
            "PaymentPage gọi DeviceIdentity.GetDeviceId(), dựng _noiDungChuyen và hiển thị QR chuyển khoản.",
            "Khi người dùng bấm Đã chuyển khoản, app POST /api/subscription/request để tạo yêu cầu chờ duyệt.",
            "Sau đó PaymentStatusPage bắt đầu polling GET /api/subscription/request/{id} mỗi 10 giây.",
            "Nếu duyệt thành công, app lưu ngày hết hạn vào Preferences và chạy ClosePaymentFlowAsync(closeSubscriptionPage:true); nếu bị từ chối thì hiển thị lý do và cho phép thử lại hoặc đóng.",
        ],
    },
    {
        "id": "F06",
        "title": "Quản lý POI, ảnh, geofence, thuyết minh và menu trong CMS",
        "actor": "Quản trị viên CMS",
        "status": "VERIFIED",
        "activity_image": "06-cms-poi-management-activity.png",
        "sequence_image": "06-cms-poi-management-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả chức năng quản trị nội dung quán ăn trong CMS, bao gồm danh sách POI, tạo/sửa quán, "
            "upload ảnh, cập nhật geofence, thuyết minh đa ngôn ngữ và thực đơn món ăn."
        ),
        "capabilities": [
            "Danh sách /Poi hỗ trợ search, status filter, expiry filter và sort.",
            "Upload ảnh cover hoặc ảnh món ăn qua /api/upload rồi lưu URL vào form.",
            "Tạo và sửa thuyết minh đa ngôn ngữ Việt/Anh/Trung.",
            "Quản lý danh sách MonAns với logic bỏ qua row rỗng, mặc định DonGia = 0 khi null, và bảo toàn input người dùng khi validation POI lỗi.",
        ],
        "operation": [
            "Admin có thể vào /Poi để xem toàn bộ quán, lọc trạng thái hiển thị và sắp xếp theo tiêu chí đang hỗ trợ.",
            "Ở trang Create/Edit, khi chọn file ảnh, JS gọi doUpload() để POST /api/upload rồi cập nhật preview ngay trên form.",
            "CreateModel.OnPostAsync() tạo mới POI, thuyết minh, bản dịch và danh sách món ăn hợp lệ từ form.",
            "EditModel.OnPostAsync() loại các lỗi validation trên MonAns trống, giữ lại phần user vừa nhập khi POI còn lỗi, đồng thời deactivate món không còn trong form và add/update món còn lại.",
            "ImageUrlHelper.ResolvePoi() và ResolveDish() được dùng trong view để hiển thị đúng ảnh thật hoặc ảnh fallback cho quán và món ăn.",
        ],
    },
    {
        "id": "F07",
        "title": "Ghi nhận phí duy trì và lịch sử hóa đơn POI",
        "actor": "Quản trị viên CMS",
        "status": "VERIFIED",
        "activity_image": "07-maintenance-payment-activity.png",
        "sequence_image": "07-maintenance-payment-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả nghiệp vụ thu phí duy trì hằng tháng cho quán ăn và tra cứu lại lịch sử hóa đơn của từng POI."
        ),
        "capabilities": [
            "Trang /ThanhToan tổng hợp trạng thái hạn duy trì, số quán đã đóng, số quán quá hạn, số quán sắp hết hạn và tổng thu tháng.",
            "Trang /ThanhToan/GhiNhan cho phép gia hạn theo số tháng và cập nhật đơn giá phí duy trì.",
            "Tạo một bản ghi HoaDon cho từng kỳ thanh toán khi gia hạn nhiều tháng.",
            "Trang /ThanhToan/LichSu cho phép xem đầy đủ lịch sử hóa đơn của từng POI.",
        ],
        "operation": [
            "IndexModel.OnGetAsync() tải danh sách POI, các gói DangKyDichVu active và doanh thu tháng hiện tại.",
            "Khi admin chọn một POI để ghi nhận phí, GhiNhanModel.LoadPoiAsync() nạp thông tin quán, gói active và 5 hóa đơn gần nhất.",
            "Khi submit, hệ thống tính mốc gia hạn mới dựa trên hạn cũ hoặc thời điểm hiện tại, cập nhật poi.NgayHetHanDuyTri và bảo đảm poi.TrangThai = true.",
            "Nếu chưa có DangKyDichVu active thì tạo mới; nếu đã có thì cập nhật PhiDuyTriThang và NgayHetHan.",
            "Sau đó hệ thống tạo một HoaDon cho mỗi tháng gia hạn rồi lưu toàn bộ thay đổi vào database.",
        ],
    },
    {
        "id": "F08",
        "title": "Duyệt hoặc từ chối thanh toán gói app",
        "actor": "Quản trị viên CMS",
        "status": "VERIFIED",
        "activity_image": "08-app-payment-approval-activity.png",
        "sequence_image": "08-app-payment-approval-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả luồng CMS dùng để xử lý các yêu cầu thanh toán gói app mà khách gửi từ mobile app."
        ),
        "capabilities": [
            "Tải danh sách yêu cầu theo tab chờ duyệt, đã duyệt và đã từ chối.",
            "Cập nhật badge và cảnh báo bằng AJAX polling qua handler PendingSnapshot.",
            "Duyệt yêu cầu để tạo DangKyApp mới và tính lại hạn sử dụng.",
            "Từ chối yêu cầu và lưu lý do cho người dùng xem lại ở phía app.",
        ],
        "operation": [
            "Khi trang /DuyetThanhToan mở, CMS tải thống kê toàn bộ yêu cầu và danh sách yêu cầu theo tab đang chọn.",
            "JavaScript trên trang gọi ?handler=PendingSnapshot mỗi 5 giây để cập nhật số lượng chờ duyệt và phát hiện yêu cầu mới.",
            "Nếu admin bấm Duyệt, OnPostApproveAsync() tìm gói tương ứng, tính mốc bắt đầu và hết hạn mới, tạo DangKyApp rồi cập nhật YeuCauThanhToan sang da_duyet.",
            "Nếu admin bấm Từ chối, OnPostRejectAsync() chuyển trạng thái sang tu_choi, lưu NgayDuyet và GhiChuAdmin.",
            "Kết quả xử lý được redirect về đúng tab để admin tiếp tục theo dõi các yêu cầu còn lại.",
        ],
    },
    {
        "id": "F09",
        "title": "Theo dõi khách hàng, trạng thái sử dụng, XP và hành trình",
        "actor": "Quản trị viên CMS",
        "status": "VERIFIED",
        "activity_image": "09-live-map-activity.png",
        "sequence_image": "09-live-map-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả trang BanDo trong CMS dùng để theo dõi thiết bị khách, trạng thái online, gói sử dụng, "
            "mức độ tương tác với POI và chỉ số trải nghiệm."
        ),
        "capabilities": [
            "Đọc dữ liệu thuê bao, vị trí hiện tại và lịch sử hoạt động bằng raw Npgsql query.",
            "Tính viewed POI, visited POI, XP, level, progress, total spent và số gói đã mua cho từng thiết bị.",
            "Hỗ trợ search, filter và sort server-side.",
            "Tạo badge trạng thái online, ở quán, hết hạn và helper mô tả thời gian còn lại.",
        ],
        "operation": [
            "BanDo/Index.OnGetAsync() chuẩn hóa filter/sort rồi nạp dữ liệu từ các truy vấn raw Npgsql có timeout và retry.",
            "Hệ thống đọc max hạn gói từ dangkyapp, vị trí từ vitrikhach, số POI đã ghé và đã xem từ lichsuphat.",
            "BuildCustomerRows() hợp nhất các nguồn dữ liệu theo deviceId để tạo ra trạng thái online, current POI, viewedCount, visitedCount và thông tin gói.",
            "XP được tính theo công thức viewed * 50 + visited * 100; level được suy ra với ExperiencePerLevel = 500.",
            "Trang BanDo hiện tại render server-side, chưa có chu kỳ auto-refresh cố định trong code; admin xem dữ liệu mới bằng lần tải trang kế tiếp.",
        ],
    },
    {
        "id": "F10",
        "title": "Dashboard tổng quan và monitoring CMS",
        "actor": "Quản trị viên CMS",
        "status": "VERIFIED",
        "activity_image": "10-dashboard-activity.png",
        "sequence_image": "10-dashboard-sequence.png",
        "function_text": (
            "Nhóm sơ đồ này mô tả dashboard tổng hợp của CMS, bao gồm danh sách POI, các chỉ số monitoring, heatmap hoạt động, "
            "top POI và doanh thu theo khoảng thời gian."
        ),
        "capabilities": [
            "Hỗ trợ nhiều chế độ khoảng thời gian: today, last7, last30, last12m, day, week, month, year và custom.",
            "Tách riêng phần tải danh sách POI và phần analytics có retry.",
            "Đọc activity, geo heat, top POI, revenue và summary bằng raw Npgsql trên shared EF connection.",
            "Bù bucket thiếu bằng FillMissingBuckets() để biểu đồ không bị thủng mốc thời gian.",
        ],
        "operation": [
            "Index.OnGetAsync() nhận tham số mode/date/week/month/year/from/to rồi ResolveRange() để xác định SinceUtc, UntilUtc, Granularity và RangeLabel.",
            "LoadPoiSectionAsync() tải danh sách POI và tính TongPOI, TongMonAn, SoQuanQuaHan với cơ chế retry khi gặp lỗi disposed wait handle.",
            "LoadAnalyticsWithRetryAsync() gọi LoadActivityAsync(), LoadGeoAsync(), LoadTopPoiAsync(), LoadRevenueAsync() và LoadSummaryAsync() trên shared DbConnection.",
            "Sau khi có dữ liệu, hệ thống chạy FillMissingBuckets() rồi serialize ActivityJson, GeoJson và RevenueJson để giao diện JS dùng trực tiếp.",
            "Dashboard hiện tại không polling tự động; dữ liệu cập nhật theo mỗi lần admin đổi mốc thời gian hoặc tải lại trang.",
        ],
    },
]


def set_run_font(run, size=12, bold=False, italic=False, color: tuple[int, int, int] | None = None):
    run.font.name = "Times New Roman"
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.italic = italic
    if color:
        run.font.color.rgb = RGBColor(*color)

    r_pr = run._r.get_or_add_rPr()
    r_fonts = OxmlElement("w:rFonts")
    for key in ("ascii", "hAnsi", "cs", "eastAsia"):
        r_fonts.set(qn(f"w:{key}"), "Times New Roman")
    existing = r_pr.find(qn("w:rFonts"))
    if existing is not None:
        r_pr.remove(existing)
    r_pr.insert(0, r_fonts)


def add_paragraph(doc: Document, text: str, *, size=12, bold=False, italic=False,
                  align=WD_ALIGN_PARAGRAPH.LEFT, space_before=0, space_after=6,
                  color: tuple[int, int, int] | None = None):
    paragraph = doc.add_paragraph()
    paragraph.alignment = align
    paragraph.paragraph_format.space_before = Pt(space_before)
    paragraph.paragraph_format.space_after = Pt(space_after)
    run = paragraph.add_run(text)
    set_run_font(run, size=size, bold=bold, italic=italic, color=color)
    return paragraph


def add_heading(doc: Document, text: str, level=1):
    sizes = {1: 15, 2: 13.5, 3: 12.5}
    colors = {
        1: (0, 70, 127),
        2: (31, 73, 125),
        3: (63, 63, 63),
    }
    return add_paragraph(
        doc,
        text,
        size=sizes.get(level, 12),
        bold=True,
        color=colors.get(level),
        space_before=12 if level == 1 else 8,
        space_after=6 if level == 1 else 4,
    )


def add_bullet(doc: Document, text: str, level=0):
    paragraph = doc.add_paragraph(style="List Bullet")
    paragraph.paragraph_format.left_indent = Cm(0.8 + (level * 0.5))
    paragraph.paragraph_format.space_after = Pt(3)
    run = paragraph.add_run(text)
    set_run_font(run, size=11.5)
    return paragraph


def add_table(doc: Document, headers: list[str], rows: list[list[str]], widths_cm: list[float] | None = None):
    table = doc.add_table(rows=1 + len(rows), cols=len(headers))
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER

    for i, header in enumerate(headers):
        cell = table.rows[0].cells[i]
        cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
        tc_pr = cell._tc.get_or_add_tcPr()
        shading = OxmlElement("w:shd")
        shading.set(qn("w:val"), "clear")
        shading.set(qn("w:color"), "auto")
        shading.set(qn("w:fill"), "1F497D")
        tc_pr.append(shading)
        paragraph = cell.paragraphs[0]
        paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run = paragraph.add_run(header)
        set_run_font(run, size=11, bold=True, color=(255, 255, 255))

    for row_index, row in enumerate(rows, start=1):
        fill = "EAF1FB" if row_index % 2 == 1 else "FFFFFF"
        for col_index, value in enumerate(row):
            cell = table.rows[row_index].cells[col_index]
            cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
            tc_pr = cell._tc.get_or_add_tcPr()
            shading = OxmlElement("w:shd")
            shading.set(qn("w:val"), "clear")
            shading.set(qn("w:color"), "auto")
            shading.set(qn("w:fill"), fill)
            tc_pr.append(shading)
            run = cell.paragraphs[0].add_run(str(value))
            set_run_font(run, size=10.5)

    if widths_cm:
        for row in table.rows:
            for cell, width in zip(row.cells, widths_cm):
                cell.width = Cm(width)

    return table


def add_image(doc: Document, image_name: str, caption: str, width_cm: float):
    path = DIAGRAM_PNG_DIR / image_name
    if not path.exists():
        add_paragraph(doc, f"[Thiếu ảnh: {image_name}]", size=11, italic=True, color=(192, 0, 0))
        return

    caption_paragraph = add_paragraph(
        doc,
        caption,
        size=10.5,
        bold=True,
        italic=True,
        color=(96, 96, 96),
        align=WD_ALIGN_PARAGRAPH.CENTER,
        space_before=8,
        space_after=2,
    )
    caption_paragraph.paragraph_format.keep_with_next = True

    picture_paragraph = doc.add_paragraph()
    picture_paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
    picture_paragraph.paragraph_format.space_after = Pt(10)
    picture_paragraph.add_run().add_picture(str(path), width=Cm(width_cm))


def build_document() -> Document:
    doc = Document()

    for section in doc.sections:
        section.top_margin = Cm(2.2)
        section.bottom_margin = Cm(2.2)
        section.left_margin = Cm(2.8)
        section.right_margin = Cm(2.0)

    # Cover
    add_paragraph(doc, "PRODUCT REQUIREMENTS DOCUMENT", size=15, bold=True,
                  align=WD_ALIGN_PARAGRAPH.CENTER, space_before=10, space_after=4, color=(0, 70, 127))
    add_paragraph(doc, "VinhKhanhTour", size=22, bold=True,
                  align=WD_ALIGN_PARAGRAPH.CENTER, space_before=0, space_after=6, color=(31, 73, 125))
    add_paragraph(doc, "Bộ sơ đồ chức năng, activity và sequence đồng bộ theo code hiện tại",
                  size=13, italic=True, align=WD_ALIGN_PARAGRAPH.CENTER,
                  space_before=0, space_after=30, color=(89, 89, 89))

    add_table(
        doc,
        ["Mục", "Giá trị"],
        [
            ["Tài liệu", "PRD_VinhKhanhTour"],
            ["Phạm vi", "Use case tổng thể + F01 đến F10"],
            ["Nguồn sơ đồ", "docs/diagrams/*.puml"],
            ["Baseline", "HEAD b946abe"],
            ["Ngày cập nhật", datetime.now().strftime("%d/%m/%Y %H:%M")],
        ],
        widths_cm=[5.0, 10.0],
    )

    add_paragraph(
        doc,
        "Tài liệu này được dựng lại để thay thế bộ sơ đồ cũ trong PRD bằng đúng các sơ đồ PlantUML mới nhất, "
        "đồng thời bổ sung phần mô tả chức năng, tính năng chính và cách hoạt động theo code hiện tại.",
        size=11.5,
        space_before=14,
        space_after=8,
    )

    doc.add_page_break()

    # Overview
    add_heading(doc, "1. Kiểm tra độ phủ của bộ use case", level=1)
    add_paragraph(
        doc,
        "Kết quả đối chiếu code hiện tại cho thấy bộ use case F01 đến F10 đã đủ phủ các chức năng nghiệp vụ chính của đồ án. "
        "Không phát hiện thêm chức năng độc lập nào cần tách thành một use case mới.",
        size=11.5,
    )
    add_bullet(doc, "Các màn hình MAUI đang chạy thật: LaunchPage, SubscriptionPage, MainPage, DetailPage, PaymentPage, PaymentStatusPage, QrScannerPage.")
    add_bullet(doc, "Các controller API đang được dùng thật trong luồng nghiệp vụ: PoiController, SubscriptionController, HeartbeatController, ThuyetMinhController, LogController, UploadController.")
    add_bullet(doc, "Các Razor PageModel chính của CMS đều đã có use case tương ứng: Poi, ThanhToan, DuyetThanhToan, BanDo, Dashboard.")
    add_bullet(doc, "Các phần như AuthController, PaymentController dự phòng, WeatherForecastController, Privacy/Error không được mô hình hóa thành chức năng vì không nằm trong luồng chính của đồ án hiện tại.")

    add_heading(doc, "2. Sơ đồ use case tổng thể", level=1)
    add_paragraph(doc, USE_CASE_SUMMARY["description"], size=11.5)
    add_paragraph(doc, USE_CASE_SUMMARY["coverage_note"], size=11.5)
    add_image(doc, USE_CASE_SUMMARY["image"], "Hình 1 - Use case tổng thể của hệ thống", 15.8)

    add_heading(doc, "3. Nguyên tắc đọc bộ sơ đồ", level=1)
    add_bullet(doc, "Use case cho biết chức năng cấp nghiệp vụ và actor nào sử dụng chức năng đó.")
    add_bullet(doc, "Activity mô tả luồng xử lý nội bộ của chức năng, bao gồm điều kiện rẽ nhánh và các lời gọi method/API quan trọng.")
    add_bullet(doc, "Sequence mô tả thứ tự tương tác giữa UI, service, controller, database hoặc thành phần ngoài hệ thống.")
    add_bullet(doc, "PlantUML là nguồn sơ đồ chính; ảnh trong tài liệu này được render lại từ docs/diagrams/*.puml mới nhất.")

    add_heading(doc, "4. Phân tích chi tiết từng chức năng", level=1)

    for index, feature in enumerate(FEATURES, start=1):
        doc.add_page_break()

        add_heading(doc, f"4.{index} {feature['id']} - {feature['title']}", level=2)
        add_paragraph(doc, f"Actor chính: {feature['actor']} | Trạng thái đối chiếu: {feature['status']}", size=11, italic=True, color=(89, 89, 89))

        add_heading(doc, "Chức năng mà sơ đồ mô tả", level=3)
        add_paragraph(doc, feature["function_text"], size=11.5)

        add_heading(doc, "Tính năng chính", level=3)
        for item in feature["capabilities"]:
            add_bullet(doc, item)

        add_heading(doc, "Cách hoạt động theo code hiện tại", level=3)
        for item in feature["operation"]:
            add_bullet(doc, item)

        add_image(
            doc,
            feature["activity_image"],
            f"Hình {index + 1}a - {feature['id']} Activity: {feature['title']}",
            14.0,
        )
        add_paragraph(
            doc,
            f"Sơ đồ activity của {feature['id']} mô tả luồng xử lý nội bộ của chức năng {feature['title'].lower()}, "
            "từ điều kiện đầu vào, các bước gọi method/API, đến các nhánh thành công hoặc lỗi.",
            size=11,
            italic=True,
            color=(89, 89, 89),
            align=WD_ALIGN_PARAGRAPH.CENTER,
            space_before=0,
            space_after=6,
        )

        add_image(
            doc,
            feature["sequence_image"],
            f"Hình {index + 1}b - {feature['id']} Sequence: {feature['title']}",
            15.6,
        )
        add_paragraph(
            doc,
            f"Sơ đồ sequence của {feature['id']} cho thấy thứ tự tương tác giữa các thành phần tham gia vào chức năng này, "
            "giúp nhìn rõ lời gọi hàm, endpoint và dữ liệu được trao đổi trong quá trình xử lý.",
            size=11,
            italic=True,
            color=(89, 89, 89),
            align=WD_ALIGN_PARAGRAPH.CENTER,
            space_before=0,
            space_after=6,
        )

    doc.add_page_break()
    add_heading(doc, "5. Kết luận", level=1)
    add_paragraph(
        doc,
        "Bộ sơ đồ hiện tại đã đủ để chốt phần mô tả chức năng của đồ án vì đã phủ toàn bộ các luồng nghiệp vụ chính đang chạy trong code. "
        "Các ảnh trong tài liệu này là ảnh mới được render lại từ PlantUML hiện tại, không còn dùng bộ PNG cũ trước đó.",
        size=11.5,
    )
    add_bullet(doc, "Không bổ sung use case mới vì chưa phát hiện chức năng nghiệp vụ độc lập bị thiếu.")
    add_bullet(doc, "Mỗi chức năng F01 đến F10 đều đã có cặp activity + sequence tương ứng.")
    add_bullet(doc, "Nội dung mô tả đã bổ sung thêm các chi tiết vận hành quan trọng như chu kỳ polling, heartbeat, refresh, fallback ảnh, audio và khôi phục mã QR.")

    return doc


def main():
    document = build_document()
    document.save(str(OUT_DOCX))
    print(f"Saved: {OUT_DOCX}")


if __name__ == "__main__":
    main()
