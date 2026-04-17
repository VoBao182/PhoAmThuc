from docx import Document
from docx.shared import Pt, RGBColor, Cm, Inches
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
import copy, os

PNG_DIR = os.path.join(os.path.dirname(__file__), "diagrams", "png")

doc = Document()

# ── Page margins ──────────────────────────────────────────────────────────────
for section in doc.sections:
    section.top_margin    = Cm(2.5)
    section.bottom_margin = Cm(2.5)
    section.left_margin   = Cm(3.0)
    section.right_margin  = Cm(2.0)

# ── Style helpers ─────────────────────────────────────────────────────────────
def set_font(run, name="Times New Roman", size=13, bold=False, italic=False, color=None):
    run.font.name = name
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.italic = italic
    if color:
        run.font.color.rgb = RGBColor(*color)
    # force Vietnamese font
    rPr = run._r.get_or_add_rPr()
    rFonts = OxmlElement('w:rFonts')
    rFonts.set(qn('w:ascii'),    name)
    rFonts.set(qn('w:hAnsi'),    name)
    rFonts.set(qn('w:cs'),       name)
    rFonts.set(qn('w:eastAsia'), name)
    existing = rPr.find(qn('w:rFonts'))
    if existing is not None:
        rPr.remove(existing)
    rPr.insert(0, rFonts)

def para(text, style='Normal', align=WD_ALIGN_PARAGRAPH.LEFT,
         bold=False, size=13, color=None, space_before=0, space_after=6, italic=False):
    p = doc.add_paragraph(style=style)
    p.alignment = align
    p.paragraph_format.space_before = Pt(space_before)
    p.paragraph_format.space_after  = Pt(space_after)
    run = p.add_run(text)
    set_font(run, size=size, bold=bold, color=color, italic=italic)
    return p

def heading1(text):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.LEFT
    p.paragraph_format.space_before = Pt(12)
    p.paragraph_format.space_after  = Pt(6)
    run = p.add_run(text)
    set_font(run, size=14, bold=True, color=(0, 70, 127))
    # bottom border
    pPr = p._p.get_or_add_pPr()
    pBdr = OxmlElement('w:pBdr')
    bottom = OxmlElement('w:bottom')
    bottom.set(qn('w:val'), 'single')
    bottom.set(qn('w:sz'), '6')
    bottom.set(qn('w:space'), '1')
    bottom.set(qn('w:color'), '00468F')
    pBdr.append(bottom)
    pPr.append(pBdr)
    return p

def heading2(text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(8)
    p.paragraph_format.space_after  = Pt(4)
    run = p.add_run(text)
    set_font(run, size=13, bold=True, color=(31, 73, 125))
    return p

def bullet(text, level=0):
    p = doc.add_paragraph(style='List Bullet')
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.left_indent = Cm(1.0 + level * 0.5)
    run = p.add_run(text)
    set_font(run, size=12)
    return p

def table(headers, rows, col_widths=None):
    t = doc.add_table(rows=1 + len(rows), cols=len(headers))
    t.style = 'Table Grid'
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    # header row
    hrow = t.rows[0]
    for i, h in enumerate(headers):
        cell = hrow.cells[i]
        cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
        # shade header
        tc = cell._tc
        tcPr = tc.get_or_add_tcPr()
        shd = OxmlElement('w:shd')
        shd.set(qn('w:val'),   'clear')
        shd.set(qn('w:color'), 'auto')
        shd.set(qn('w:fill'),  '1F497D')
        tcPr.append(shd)
        p2 = cell.paragraphs[0]
        p2.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run = p2.add_run(h)
        set_font(run, size=12, bold=True, color=(255,255,255))
    # data rows
    for ri, row in enumerate(rows):
        fill = 'EAF1FB' if ri % 2 == 0 else 'FFFFFF'
        for ci, val in enumerate(row):
            cell = t.rows[ri+1].cells[ci]
            cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
            tc = cell._tc
            tcPr = tc.get_or_add_tcPr()
            shd = OxmlElement('w:shd')
            shd.set(qn('w:val'),   'clear')
            shd.set(qn('w:color'), 'auto')
            shd.set(qn('w:fill'),  fill)
            tcPr.append(shd)
            p2 = cell.paragraphs[0]
            run = p2.add_run(str(val))
            set_font(run, size=11)
    # column widths
    if col_widths:
        for row in t.rows:
            for i, w in enumerate(col_widths):
                row.cells[i].width = Cm(w)
    return t

def code_block(text):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(4)
    p.paragraph_format.space_after  = Pt(4)
    p.paragraph_format.left_indent  = Cm(1)
    pPr = p._p.get_or_add_pPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'),   'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'),  'F2F2F2')
    pPr.append(shd)
    run = p.add_run(text)
    set_font(run, name='Courier New', size=10)
    return p

def hline():
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(2)
    p.paragraph_format.space_after  = Pt(2)

def add_diagram(label, png_name, width_cm=15.0):
    """Chèn ảnh sơ đồ với caption căn giữa."""
    path = os.path.join(PNG_DIR, png_name)
    if not os.path.exists(path):
        return
    # Caption
    cap = doc.add_paragraph()
    cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    cap.paragraph_format.space_before = Pt(8)
    cap.paragraph_format.space_after  = Pt(2)
    run = cap.add_run(label)
    set_font(run, size=11, bold=True, italic=True, color=(89, 89, 89))
    # Ảnh
    pic = doc.add_paragraph()
    pic.alignment = WD_ALIGN_PARAGRAPH.CENTER
    pic.paragraph_format.space_after = Pt(10)
    pic.add_run().add_picture(path, width=Cm(width_cm))

# ══════════════════════════════════════════════════════════════════════════════
# COVER PAGE
# ══════════════════════════════════════════════════════════════════════════════
para("TRƯỜNG ĐẠI HỌC ...", align=WD_ALIGN_PARAGRAPH.CENTER, size=13, bold=True, space_before=0, space_after=2)
para("KHOA CÔNG NGHỆ THÔNG TIN", align=WD_ALIGN_PARAGRAPH.CENTER, size=13, bold=True, space_before=0, space_after=40)

para("ĐỒ ÁN TỐT NGHIỆP", align=WD_ALIGN_PARAGRAPH.CENTER, size=18, bold=True,
     color=(0,70,127), space_before=0, space_after=16)

para("Product Requirements Document", align=WD_ALIGN_PARAGRAPH.CENTER, size=14,
     italic=True, color=(89,89,89), space_before=0, space_after=4)

para("VinhKhanhTour", align=WD_ALIGN_PARAGRAPH.CENTER, size=22, bold=True,
     color=(31,73,125), space_before=0, space_after=8)

para("Hệ thống hướng dẫn du lịch phố ẩm thực Vĩnh Khánh", align=WD_ALIGN_PARAGRAPH.CENTER,
     size=14, italic=True, color=(89,89,89), space_before=0, space_after=60)

tbl_info = doc.add_table(rows=4, cols=2)
tbl_info.alignment = WD_TABLE_ALIGNMENT.CENTER
info_data = [
    ("Sinh viên thực hiện:", "Cao Hoàng Thịnh"),
    ("Email:", "caohoangthinh2@gmail.com"),
    ("Phiên bản:", "1.0"),
    ("Ngày:", "17/04/2026"),
]
for i, (k, v) in enumerate(info_data):
    c1, c2 = tbl_info.rows[i].cells
    r1 = c1.paragraphs[0].add_run(k)
    set_font(r1, size=12, bold=True)
    r2 = c2.paragraphs[0].add_run(v)
    set_font(r2, size=12)
    c1.width = Cm(5)
    c2.width = Cm(8)

doc.add_page_break()

# ══════════════════════════════════════════════════════════════════════════════
# 1. TỔNG QUAN DỰ ÁN
# ══════════════════════════════════════════════════════════════════════════════
heading1("1. TỔNG QUAN DỰ ÁN")

heading2("1.1 Bối cảnh")
para("Phố ẩm thực Vĩnh Khánh (Quận 4, TP.HCM) là một trong những tuyến phố ẩm thực nổi tiếng tại TP.HCM, thu hút hàng nghìn lượt khách mỗi ngày. Tuy nhiên, khách du lịch — đặc biệt khách nước ngoài — gặp nhiều khó khăn trong việc khám phá: không biết quán nào nổi tiếng, không có thông tin tiếng Anh, thiếu hướng dẫn bản đồ, không biết gọi món gì.")

heading2("1.2 Mô tả sản phẩm")
para("VinhKhanhTour là hệ thống du lịch thông minh gồm ba thành phần:")

table(
    ["Thành phần", "Mô tả", "Công nghệ"],
    [
        ("Mobile App", "Ứng dụng cho khách tham quan", ".NET MAUI (Android / Windows)"),
        ("REST API",   "Backend trung tâm",             "ASP.NET Core 10, EF Core, PostgreSQL"),
        ("CMS Web",    "Bảng điều khiển quản trị",      "ASP.NET Core Razor Pages"),
    ],
    col_widths=[3.5, 5.5, 6.0]
)
hline()
para("Cơ sở dữ liệu: Supabase PostgreSQL (cloud-hosted)", size=12, italic=True)

heading2("1.3 Mục tiêu")
for t in [
    "Cung cấp hướng dẫn tham quan tự động (audio guide) khi khách bước vào khu vực quán.",
    "Hỗ trợ đa ngôn ngữ: Việt, Anh, Trung.",
    "Cho phép theo dõi vị trí khách thực tế (live map) phục vụ quản lý.",
    "Mô hình kinh doanh rõ ràng: gói subscription cho khách, phí duy trì tháng cho chủ quán.",
]:
    bullet(t)

# ══════════════════════════════════════════════════════════════════════════════
# 2. ĐỐI TƯỢNG SỬ DỤNG
# ══════════════════════════════════════════════════════════════════════════════
heading1("2. ĐỐI TƯỢNG SỬ DỤNG")

heading2("2.1 Khách du lịch (App User)")
bullet("Đặc điểm: Người dùng ẩn danh, định danh bằng Device UUID (không đăng nhập).")
bullet("Nhu cầu: Khám phá quán ăn, nghe thuyết minh tự động, xem thực đơn, chỉ đường.")
bullet("Hành vi: Cài app → chọn gói → dạo phố → app tự động phát thuyết minh khi đến gần quán.")

heading2("2.2 Quản trị viên (Admin CMS)")
bullet("Đặc điểm: Nhân viên quản lý hệ thống, truy cập CMS web nội bộ.")
bullet("Nhu cầu: Quản lý nội dung POI, thu phí duy trì, duyệt thanh toán, xem bản đồ live.")

heading2("2.3 Chủ quán (POI Owner)")
bullet("Đặc điểm: Đăng ký dịch vụ hướng dẫn cho quán của mình.")
bullet("Nhu cầu: Cập nhật thực đơn, nội dung thuyết minh, thanh toán phí duy trì hàng tháng.")

# 3. KIẾN TRÚC HỆ THỐNG
# ══════════════════════════════════════════════════════════════════════════════
heading1("3. KIẾN TRÚC HỆ THỐNG")

code_block(
    "┌──────────────────────────────────────────────────────────┐\n"
    "│                         CLIENTS                          │\n"
    "│  ┌──────────────────┐        ┌────────────────────────┐  │\n"
    "│  │  MAUI Mobile App │        │  CMS Web (Razor Pages) │  │\n"
    "│  │  (Android/Win)   │        │  (Admin Panel)         │  │\n"
    "│  └────────┬─────────┘        └──────────┬─────────────┘  │\n"
    "└───────────┼──────────────────────────────┼───────────────┘\n"
    "            │ HTTP/REST                    │ HTTP + EF Core\n"
    "            ▼                             ▼\n"
    "┌──────────────────────────────────────────────────────────┐\n"
    "│                ASP.NET Core 10 REST API                  │\n"
    "│  Auth │ Poi │ Subscription │ Heartbeat │ Payment │ Upload │\n"
    "└──────────────────────────────┬───────────────────────────┘\n"
    "                               │ EF Core + Npgsql\n"
    "                               ▼\n"
    "                    ┌────────────────────┐\n"
    "                    │ Supabase PostgreSQL │\n"
    "                    │   (11 tables)       │\n"
    "                    └────────────────────┘"
)

# ══════════════════════════════════════════════════════════════════════════════
# 4. CẤU TRÚC DỮ LIỆU
# ══════════════════════════════════════════════════════════════════════════════
heading1("4. CẤU TRÚC DỮ LIỆU")

heading2("4.1 Danh sách bảng")
table(
    ["Bảng", "Mô tả", "Cột quan trọng"],
    [
        ("poi",               "Điểm du lịch / quán ăn",         "KinhDo, ViDo, BanKinh, NgayHetHanDuyTri"),
        ("thuyetminh",        "Nội dung audio guide của POI",    "POIId, ThuTu, TrangThai"),
        ("bandich",           "Bản dịch thuyết minh",           "NgonNgu (vi/en/zh), NoiDung, FileAudio"),
        ("monan",             "Thực đơn quán",                  "TenMonAn, DonGia, PhanLoai, HinhAnh"),
        ("dangkyapp",         "Gói đăng ký app của khách",       "MaThietBi, LoaiGoi, NgayBatDau, NgayHetHan"),
        ("yeucauthanhtoan",   "Yêu cầu thanh toán QR",          "MaThietBi, LoaiGoi, SoTien, TrangThai"),
        ("vitrikhach",        "Vị trí GPS real-time",           "MaThietBi (UNIQUE), Lat, Lng, LanCuoiHeartbeat"),
        ("dangkydichvu",      "Gói dịch vụ POI",                "POIId, PhiDuyTriThang, PhiConvert"),
        ("hoadon",            "Hóa đơn thanh toán POI",         "LoaiPhi (duytri/convert), KyThanhToan"),
        ("lichsuphat",        "Log xem/nghe thuyết minh",       "Nguon (GPS/VIEW), NgonNguDung, MaThietBi"),
        ("taikhoan",          "Tài khoản admin CMS",            "TenDangNhap, VaiTro"),
    ],
    col_widths=[3.5, 4.5, 7.0]
)

# ══════════════════════════════════════════════════════════════════════════════
# 5. MÔ HÌNH GÓI DỊCH VỤ
# ══════════════════════════════════════════════════════════════════════════════
heading1("5. MÔ HÌNH GÓI DỊCH VỤ")

heading2("5.1 Gói đăng ký App (khách tham quan)")
table(
    ["Mã gói", "Tên", "Giá", "Thời hạn", "Ghi chú"],
    [
        ("thu",   "Dùng thử",  "0đ",         "3 ngày",   "1 lần/thiết bị, kích hoạt ngay"),
        ("ngay",  "1 ngày",    "29.000đ",     "1 ngày",   "Thanh toán qua QR"),
        ("tuan",  "1 tuần",    "99.000đ",     "7 ngày",   "Thanh toán qua QR"),
        ("thang", "1 tháng",   "199.000đ",    "30 ngày",  "Thanh toán qua QR"),
        ("nam",   "1 năm",     "999.000đ",    "365 ngày", "Thanh toán qua QR"),
    ],
    col_widths=[2.0, 2.5, 2.5, 2.5, 5.5]
)
hline()
para("Gói nối tiếp: nếu chưa hết hạn gói cũ → NgayBatDau = NgayHetHan cũ.", size=12, italic=True)

heading2("5.2 Phí dịch vụ POI (chủ quán)")
table(
    ["Loại phí", "Mức phí", "Chu kỳ", "Ghi chú"],
    [
        ("Phí duy trì",    "50.000đ/tháng", "Hàng tháng",   "Quá hạn → POI bị ẩn khỏi app"),
        ("Phí convert TTS","20.000đ/lần",   "Mỗi lần convert","Chuyển text → audio"),
    ],
    col_widths=[3.5, 3.5, 3.5, 4.5]
)

# ══════════════════════════════════════════════════════════════════════════════
# 6. YÊU CẦU CHỨC NĂNG
# ══════════════════════════════════════════════════════════════════════════════
heading1("6. YÊU CẦU CHỨC NĂNG")

heading2("6.0 Tổng quan use case")
add_diagram("Hình 0 – Use case tổng thể hệ thống VinhKhanhTour", "00-overall-usecase.png", width_cm=15.0)

# F01
heading2("F01 – Khởi động & Subscription Gate")
para("Mỗi lần mở app, kiểm tra xem thiết bị có gói hợp lệ không.", size=12)
para("Luồng chính:", size=12, bold=True)
for s in [
    "MainPage.OnAppearing() → đọc sub_ngay_het_han từ Preferences",
    "Nếu hết hạn hoặc chưa có → điều hướng sang SubscriptionPage",
    "Nếu còn hạn → tải danh sách POI và bắt đầu GPS tracking",
]:
    bullet(s)
para("Điều kiện biên:", size=12, bold=True)
bullet("Gói miễn phí 'thu': chỉ được dùng 1 lần/thiết bị (kiểm tra flag da_dung_thu)")
bullet("Gói trả phí: cần trải qua luồng QR → admin duyệt")
add_diagram("Hình 1a – F01: Sơ đồ tuần tự – Kích hoạt quyền sử dụng app", "01-subscription-gate-sequence.png")
add_diagram("Hình 1b – F01: Sơ đồ hoạt động – Kích hoạt quyền sử dụng app", "01-subscription-gate-activity.png", width_cm=11.0)

# F02
heading2("F02 – Danh sách & Tìm kiếm POI")
para("Hiển thị danh sách quán/điểm tham quan, hỗ trợ tìm kiếm.", size=12)
para("API: GET /api/poi", size=12, bold=True)
para("Điều kiện lọc: TrangThai = true AND (NgayHetHanDuyTri IS NULL OR NgayHetHanDuyTri > now())", size=11, italic=True)
para("Tính năng:", size=12, bold=True)
bullet("Tìm kiếm văn bản: lọc theo tên POI (normalize dấu tiếng Việt)")
bullet("Sắp xếp theo MucUuTien")
bullet("Hiển thị ảnh đại diện, tên, địa chỉ, số điện thoại")
add_diagram("Hình 2a – F02: Sơ đồ tuần tự – Tải và khám phá danh sách POI", "02-poi-explore-sequence.png")
add_diagram("Hình 2b – F02: Sơ đồ hoạt động – Tải và khám phá danh sách POI", "02-poi-explore-activity.png", width_cm=11.0)

# F03
heading2("F03 – Tự động phát thuyết minh (Geofence + Audio)")
para("Khi khách bước vào vùng bán kính của quán, app tự động phát audio guide.", size=12)
para("Luồng:", size=12, bold=True)
for s in [
    "GPS poll mỗi 5 giây",
    "Tính khoảng cách đến từng POI bằng công thức Haversine",
    "Nếu khoảng cách ≤ BanKinh (30m) → đánh dấu 'đang trong geofence'",
    "Gọi SpeakPoiAsync() → GET /api/thuyet-minh/{poiId}?lang={ngonNgu}",
    "Phát audio file; nếu không có audio → TTS văn bản",
    "Ghi log: POST /api/log với Nguon='GPS'",
    "Dedup: không phát lại trong vòng 10 phút/POI",
]:
    bullet(s)
para("Heartbeat GPS: POST /api/heartbeat mỗi 15 giây với {Lat, Lng, PoiIdHienTai}", size=12, italic=True)
add_diagram("Hình 3a – F03: Sơ đồ tuần tự – Theo dõi GPS, geofence và tự động phát thuyết minh", "03-geofence-audio-sequence.png")
add_diagram("Hình 3b – F03: Sơ đồ hoạt động – Theo dõi GPS, geofence và tự động phát thuyết minh", "03-geofence-audio-activity.png", width_cm=11.0)

# F04
heading2("F04 – Chi tiết POI")
para("Xem thông tin đầy đủ một quán: thuyết minh, thực đơn, nút nghe audio, chỉ đường.", size=12)
for s in [
    "Chọn ngôn ngữ (vi/en/zh) → tải lại thuyết minh",
    "Nút 'Nghe' → phát audio hoặc TTS",
    "Nút 'Chỉ đường' → mở Google Maps với tọa độ POI",
    "Ghi log: POST /api/heartbeat/view khi mở trang (Nguon='VIEW')",
]:
    bullet(s)
add_diagram("Hình 4a – F04: Sơ đồ tuần tự – Xem chi tiết POI, thực đơn, audio guide và chỉ đường", "04-poi-detail-sequence.png")
add_diagram("Hình 4b – F04: Sơ đồ hoạt động – Xem chi tiết POI, thực đơn, audio guide và chỉ đường", "04-poi-detail-activity.png", width_cm=11.0)

# F05
heading2("F05 – Thanh toán QR & Phê duyệt")
para("Luồng thanh toán gói trả phí qua QR VietQR MBBank, admin duyệt thủ công.", size=12)
para("Luồng phía khách:", size=12, bold=True)
for s in [
    "SubscriptionPage → chọn gói → POST /api/subscription/request",
    "API tạo YeuCauThanhToan (status=cho_duyet) → trả về {YeuCauId, NoiDungChuyen}",
    "PaymentPage → hiển thị QR (VietQR) + nội dung CK (VD: VKT THANG A1B2C3)",
    "Khách chuyển khoản → nhấn 'Đã chuyển' → PaymentStatusPage",
    "Polling GET /api/subscription/request/{id} mỗi 10 giây",
    "Khi status=da_duyet → lưu NgayHetHan vào Preferences → đóng luồng",
]:
    bullet(s)
para("Luồng Admin CMS (/DuyetThanhToan):", size=12, bold=True)
for s in [
    "Tab 3 trạng thái: Chờ duyệt / Đã duyệt / Từ chối",
    "Nút 'Duyệt' → POST /api/subscription/approve/{id} → tạo DangKyApp",
    "Nút 'Từ chối' → modal nhập lý do → POST /api/subscription/reject/{id}",
]:
    bullet(s)
add_diagram("Hình 5a – F05: Sơ đồ tuần tự – Thanh toán gói trả phí và chờ duyệt", "05-paid-plan-sequence.png")
add_diagram("Hình 5b – F05: Sơ đồ hoạt động – Thanh toán gói trả phí và chờ duyệt", "05-paid-plan-activity.png", width_cm=11.0)
add_diagram("Hình 5c – F05: Sơ đồ tuần tự – Duyệt / từ chối thanh toán (Admin CMS)", "08-app-payment-approval-sequence.png")
add_diagram("Hình 5d – F05: Sơ đồ hoạt động – Duyệt / từ chối thanh toán (Admin CMS)", "08-app-payment-approval-activity.png", width_cm=11.0)

# F06
heading2("F06 – Theo dõi khách hàng & Trạng thái sử dụng (CMS)")
para("CMS hiển thị bảng tổng hợp tất cả thiết bị đã sử dụng app, trạng thái gói và hành trình tham quan — render server-side, không cần JavaScript realtime.", size=12)
para("Dữ liệu tổng hợp từ 4 nguồn:", size=12, bold=True)
for s in [
    "DangKyApps → hạn gói hiện tại của từng thiết bị",
    "VitriKhachs → thời điểm heartbeat cuối (xác định online/offline, ngưỡng 2 phút)",
    "LichSuPhat (Nguon=GPS) → số POI đã ghé thực tế",
    "LichSuPhat (Nguon=VIEW) → số POI đã xem chi tiết",
]:
    bullet(s)
para("Thống kê hiển thị:", size=12, bold=True)
for s in [
    "TotalCustomers, ActiveCustomers, CustomersAtPoi, ExpiredCustomers",
    "Trạng thái từng thiết bị: GetStatusText() / GetSubscriptionText()",
    "Cột: Device ID, POI đang ở, số ghé, số xem, hạn gói, trạng thái",
]:
    bullet(s)
add_diagram("Hình 6a – F06: Sơ đồ tuần tự – Theo dõi khách hàng và trạng thái sử dụng", "09-live-map-sequence.png")
add_diagram("Hình 6b – F06: Sơ đồ hoạt động – Theo dõi khách hàng và trạng thái sử dụng", "09-live-map-activity.png", width_cm=11.0)

# F07
heading2("F07 – Quản lý POI (CMS)")
table(
    ["Chức năng", "URL CMS", "Ghi chú"],
    [
        ("Danh sách POI",     "/Poi",               "Xem, lọc, tìm kiếm"),
        ("Tạo POI mới",       "/Poi/Create",         "Upload ảnh qua /api/upload"),
        ("Chỉnh sửa POI",     "/Poi/Edit/{id}",      "Sửa thông tin, menu"),
        ("Quản lý thuyết minh","/ThuyetMinh/Edit/{id}","Nội dung + bản dịch đa ngôn ngữ"),
        ("Dashboard tổng quan","/",                  "TongPOI, TongMonAn, SoQuanQuaHan"),
    ],
    col_widths=[4.0, 4.5, 6.5]
)
hline()
para("Upload ảnh: POST /api/upload — tối đa 5MB, định dạng: .jpg, .jpeg, .png, .webp, .gif", size=12, italic=True)
add_diagram("Hình 7a – F07: Sơ đồ tuần tự – Quản lý POI, ảnh, thuyết minh và menu", "06-cms-poi-management-sequence.png")
add_diagram("Hình 7b – F07: Sơ đồ hoạt động – Quản lý POI, ảnh, thuyết minh và menu", "06-cms-poi-management-activity.png", width_cm=11.0)

# F08
heading2("F08 – Quản lý Phí Duy Trì POI")
para("Admin ghi nhận thanh toán phí hàng tháng của chủ quán.", size=12)
for s in [
    "/ThanhToan → danh sách POI với trạng thái hạn duy trì",
    "/ThanhToan/GhiNhan/{poiId} → chọn số tháng → POST /api/payment/maintenance",
    "API tạo HoaDon (mỗi tháng một dòng) + gia hạn NgayHetHanDuyTri",
    "/ThanhToan/LichSu/{poiId} → xem lịch sử hóa đơn",
    "Cảnh báo: Dashboard hiển thị số quán quá hạn; POI quá hạn bị ẩn khỏi app",
]:
    bullet(s)
add_diagram("Hình 8a – F08: Sơ đồ tuần tự – Ghi nhận phí duy trì và lịch sử hóa đơn POI", "07-maintenance-payment-sequence.png")
add_diagram("Hình 8b – F08: Sơ đồ hoạt động – Ghi nhận phí duy trì và lịch sử hóa đơn POI", "07-maintenance-payment-activity.png", width_cm=11.0)

# F09
heading2("F09 – Dashboard tổng quan CMS")
para("Trang chủ CMS hiển thị các chỉ số tổng quan hệ thống, tải khi admin mở trang.", size=12)
for s in [
    "_db.POIs.Include(MonAns).OrderBy(MucUuTien).ToListAsync()",
    "TongPOI — số POI đang hiển thị (TrangThai = true)",
    "TongMonAn — số món ăn đang hoạt động trên toàn hệ thống",
    "SoQuanQuaHan — POI đang hiển thị nhưng đã hết hạn duy trì",
]:
    bullet(s)
add_diagram("Hình 9a – F09: Sơ đồ tuần tự – Dashboard tổng quan CMS", "10-dashboard-sequence.png")
add_diagram("Hình 9b – F09: Sơ đồ hoạt động – Dashboard tổng quan CMS", "10-dashboard-activity.png", width_cm=11.0)

# ══════════════════════════════════════════════════════════════════════════════
# 7. YÊU CẦU PHI CHỨC NĂNG
# ══════════════════════════════════════════════════════════════════════════════
heading1("7. YÊU CẦU PHI CHỨC NĂNG")

heading2("7.1 Hiệu năng")
table(
    ["Chỉ số", "Mục tiêu"],
    [
        ("Thời gian phản hồi API",  "< 500ms (p95)"),
        ("GPS poll interval",       "5 giây"),
        ("Heartbeat interval",      "15 giây"),
        ("CMS map auto-refresh",    "30 giây"),
        ("App polling thanh toán",  "10 giây"),
        ("API probe cache",         "5 phút (success) / 10 giây (fail)"),
    ],
    col_widths=[7.0, 8.0]
)

heading2("7.2 Độ tin cậy")
for s in [
    "Geofence dedup: không phát lại thuyết minh trong 10 phút/POI",
    "Visit dedup: không ghi visit trong 5 phút/POI",
    "Subscription rollover: gia hạn nối tiếp, không mất ngày còn lại",
    "Image URL fallback: xử lý cả đường dẫn tương đối, localhost, và URL ngoài",
]:
    bullet(s)

heading2("7.3 Khả năng mở rộng")
for s in [
    "Ngôn ngữ: thêm ngôn ngữ mới chỉ cần thêm bản dịch trong bảng bandich",
    "Gói subscription: thêm gói mới bằng cách mở rộng enum trong SubscriptionController",
    "Multi-platform: MAUI hỗ trợ Android, Windows; iOS sau",
]:
    bullet(s)

heading2("7.4 Bảo mật")
for s in [
    "CMS chạy nội bộ (không public internet) — không cần auth phức tạp cho demo",
    "App không lưu thông tin cá nhân — chỉ Device UUID (tự sinh, không liên kết danh tính)",
    "Phiên bản demo dùng plain-text password; sản phẩm thực tế cần BCrypt + JWT",
    "Supabase connection string không commit vào repository công khai",
]:
    bullet(s)

# ══════════════════════════════════════════════════════════════════════════════
# 8. API REFERENCE
# ══════════════════════════════════════════════════════════════════════════════
heading1("8. API REFERENCE")

heading2("8.1 POI")
table(
    ["Method", "Endpoint", "Mô tả"],
    [
        ("GET", "/api/poi",      "Danh sách POI đang hoạt động"),
        ("GET", "/api/poi/{id}", "Chi tiết POI (kèm menu + thuyết minh)"),
    ],
    col_widths=[2.0, 5.5, 7.5]
)

heading2("8.2 Subscription")
table(
    ["Method", "Endpoint", "Mô tả"],
    [
        ("GET",  "/api/subscription/plans",           "Danh sách gói"),
        ("GET",  "/api/subscription/status/{maThietBi}", "Trạng thái gói hiện tại"),
        ("POST", "/api/subscription/purchase",        "Mua gói miễn phí (kích hoạt ngay)"),
        ("POST", "/api/subscription/request",         "Tạo yêu cầu thanh toán QR"),
        ("GET",  "/api/subscription/request/{id}",    "Kiểm tra trạng thái yêu cầu"),
        ("POST", "/api/subscription/approve/{id}",    "Admin duyệt yêu cầu"),
        ("POST", "/api/subscription/reject/{id}",     "Admin từ chối yêu cầu"),
        ("GET",  "/api/subscription/requests",        "Danh sách yêu cầu (CMS)"),
    ],
    col_widths=[2.0, 5.5, 7.5]
)

heading2("8.3 Heartbeat & Tracking")
table(
    ["Method", "Endpoint", "Mô tả"],
    [
        ("POST", "/api/heartbeat",                        "Upsert vị trí + POI hiện tại"),
        ("POST", "/api/heartbeat/visit",                  "Ghi nhận vào geofence POI"),
        ("POST", "/api/heartbeat/view",                   "Ghi nhận mở chi tiết POI"),
        ("POST", "/api/heartbeat/sync-history",           "Đồng bộ lịch sử (offline catch-up)"),
        ("GET",  "/api/heartbeat/active",                 "Thiết bị online (2 phút qua)"),
        ("GET",  "/api/heartbeat/history/{deviceShort}",  "Lịch sử POI (4 giờ gần nhất)"),
    ],
    col_widths=[2.0, 5.5, 7.5]
)

heading2("8.4 Thanh toán POI")
table(
    ["Method", "Endpoint", "Mô tả"],
    [
        ("GET",  "/api/payment/status/{poiId}",   "Trạng thái phí duy trì"),
        ("GET",  "/api/payment/history/{poiId}",  "Lịch sử hóa đơn"),
        ("POST", "/api/payment/maintenance",       "Ghi nhận phí duy trì (Admin)"),
        ("POST", "/api/payment/convert/{poiId}",  "Thanh toán phí convert TTS"),
        ("GET",  "/api/payment/overdue",           "POI quá hạn duy trì"),
    ],
    col_widths=[2.0, 5.5, 7.5]
)

heading2("8.5 Các endpoint khác")
table(
    ["Method", "Endpoint", "Mô tả"],
    [
        ("GET",  "/api/thuyet-minh/{poiId}?lang=vi", "Nội dung thuyết minh theo ngôn ngữ"),
        ("POST", "/api/log",                          "Ghi log phát audio"),
        ("POST", "/api/upload",                       "Upload ảnh (5MB, jpg/png/webp/gif)"),
        ("POST", "/api/auth/login",                   "Đăng nhập CMS"),
        ("POST", "/api/auth/register",                "Đăng ký tài khoản"),
    ],
    col_widths=[2.0, 5.5, 7.5]
)

# ══════════════════════════════════════════════════════════════════════════════
# 9. CẤU TRÚC THƯ MỤC
# ══════════════════════════════════════════════════════════════════════════════
heading1("9. CẤU TRÚC THƯ MỤC DỰ ÁN")

code_block(
    "VinhKhanhTourDemo/                  ← MAUI App\n"
    "├── AppConfig.cs                    ← Cấu hình API URL, image URL resolver\n"
    "├── MainPage.xaml(.cs)              ← Trang chính: POI list, GPS, geofence, audio\n"
    "├── DetailPage.xaml(.cs)            ← Chi tiết POI, menu, audio player\n"
    "├── SubscriptionPage.xaml(.cs)      ← Chọn gói đăng ký\n"
    "├── PaymentPage.xaml(.cs)           ← Hiển thị QR thanh toán\n"
    "└── PaymentStatusPage.xaml(.cs)     ← Polling trạng thái thanh toán\n"
    "\n"
    "VinhKhanhTour.API/\n"
    "├── Controllers/\n"
    "│   ├── AuthController.cs\n"
    "│   ├── PoiController.cs\n"
    "│   ├── SubscriptionController.cs\n"
    "│   ├── HeartbeatController.cs\n"
    "│   ├── PaymentController.cs\n"
    "│   ├── ThuyetMinhController.cs\n"
    "│   ├── LogController.cs\n"
    "│   └── UploadController.cs\n"
    "└── Models/                         ← Entity classes (EF Core)\n"
    "\n"
    "VinhKhanhTour.CMS/\n"
    "└── Pages/\n"
    "    ├── Index.cshtml(.cs)           ← Dashboard\n"
    "    ├── Poi/                        ← CRUD quán ăn\n"
    "    ├── ThuyetMinh/                 ← Quản lý thuyết minh\n"
    "    ├── ThanhToan/                  ← Phí duy trì POI\n"
    "    ├── DuyetThanhToan/             ← Duyệt thanh toán app\n"
    "    └── BanDo/                      ← Bản đồ live"
)

# ══════════════════════════════════════════════════════════════════════════════
# 10. PHẠM VI & TRẠNG THÁI
# ══════════════════════════════════════════════════════════════════════════════
heading1("10. PHẠM VI & TRẠNG THÁI ĐỒ ÁN")

table(
    ["Tính năng", "Trạng thái"],
    [
        ("Hướng dẫn du lịch tự động (audio guide)",   "✅ Hoàn thành"),
        ("Đa ngôn ngữ (vi/en/zh)",                     "✅ Hoàn thành"),
        ("Geofence tự động phát thuyết minh",           "✅ Hoàn thành"),
        ("Subscription & QR payment",                   "✅ Hoàn thành"),
        ("CMS quản lý nội dung",                        "✅ Hoàn thành"),
        ("Bản đồ live tracking",                        "✅ Hoàn thành"),
        ("Duyệt thanh toán CMS",                        "✅ Hoàn thành"),
        ("Phí duy trì POI",                             "✅ Hoàn thành"),
        ("Xác thực JWT cho API",                        "❌ Ngoài phạm vi (demo plain text)"),
        ("Push notification",                           "❌ Ngoài phạm vi"),
        ("Hỗ trợ iOS",                                  "❌ Ngoài phạm vi"),
        ("Đặt bàn / Order online",                      "❌ Ngoài phạm vi"),
    ],
    col_widths=[10.0, 5.0]
)

# ══════════════════════════════════════════════════════════════════════════════
# 11. LUỒNG THANH TOÁN
# ══════════════════════════════════════════════════════════════════════════════
heading1("11. PHỤ LỤC – LUỒNG THANH TOÁN QR CHI TIẾT")

code_block(
    "App                        API                       Admin CMS\n"
    " │                          │                             │\n"
    " ├─ POST /subscription/request ──►                        │\n"
    " │                          ├─ Tạo YeuCauThanhToan        │\n"
    " │                          │  (status=cho_duyet)         │\n"
    " │◄── {yeuCauId, noiDungCK} ─┤                            │\n"
    " │                          │                             │\n"
    " │  [Hiển thị QR + CK]      │  GET /requests?trangthai=   │\n"
    " │  [Khách chuyển khoản]    │  cho_duyet ◄────────────────┤\n"
    " │                          │                             │\n"
    " │  polling (10s)           │  POST /approve/{id} ◄───────┤\n"
    " ├─ GET /request/{id} ───────►  → tạo DangKyApp           │\n"
    " │◄── {status:da_duyet} ────┤                             │\n"
    " │                          │                             │\n"
    " │  [Lưu NgayHetHan]        │                             │\n"
    " │  [Chuyển MainPage]       │                             │"
)

# ── Footer ────────────────────────────────────────────────────────────────────
hline()
para("Tài liệu này mô tả toàn bộ phạm vi và yêu cầu của đồ án VinhKhanhTour phiên bản 1.0.",
     align=WD_ALIGN_PARAGRAPH.CENTER, size=11, italic=True, color=(89,89,89))

# ── Save ──────────────────────────────────────────────────────────────────────
out = r"c:\Users\ASUS\OneDrive\Desktop\VinhKhanhTourDemo\docs\PRD_VinhKhanhTour.docx"
doc.save(out)
print(f"Saved: {out}")
