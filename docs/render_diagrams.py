"""
Render tất cả file .puml thành PNG qua PlantUML public server.
Output: docs/diagrams/png/<tên>.png
"""
import zlib, os, time, base64, urllib.request, urllib.error

PUML_DIR  = os.path.join(os.path.dirname(__file__), "diagrams")
PNG_DIR   = os.path.join(PUML_DIR, "png")
# Dùng kroki.io (hỗ trợ PlantUML, không chặn automated request)
SERVER_KROKI    = "https://kroki.io/plantuml/png/"
SERVER_PLANTUML = "https://www.plantuml.com/plantuml/png/"

os.makedirs(PNG_DIR, exist_ok=True)

# ── PlantUML encoding ─────────────────────────────────────────────────────────
def _e6(b):
    if b < 10: return chr(48 + b)
    b -= 10
    if b < 26: return chr(65 + b)
    b -= 26
    if b < 26: return chr(97 + b)
    b -= 26
    return '-' if b == 0 else '_'

def _a3(b1, b2, b3):
    return (_e6(b1 >> 2) +
            _e6(((b1 & 3) << 4) | (b2 >> 4)) +
            _e6(((b2 & 15) << 2) | (b3 >> 6)) +
            _e6(b3 & 63))

def encode_plantuml(text: str) -> str:
    """PlantUML custom encoding (dùng cho plantuml.com)"""
    raw = zlib.compress(text.encode("utf-8"), 9)[2:-4]
    res = ""
    for i in range(0, len(raw), 3):
        b1 = raw[i]
        b2 = raw[i+1] if i+1 < len(raw) else 0
        b3 = raw[i+2] if i+2 < len(raw) else 0
        res += _a3(b1, b2, b3)
    return res

def encode_kroki(text: str) -> str:
    """Base64url(zlib(utf8)) — dùng cho kroki.io"""
    compressed = zlib.compress(text.encode("utf-8"), 9)
    return base64.urlsafe_b64encode(compressed).decode("ascii")

def fetch(url: str, timeout=45) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()

# ── Render ────────────────────────────────────────────────────────────────────
files = sorted(f for f in os.listdir(PUML_DIR) if f.endswith(".puml"))
total = len(files)
ok, fail = 0, []

for i, fname in enumerate(files, 1):
    stem = fname[:-5]
    src  = os.path.join(PUML_DIR, fname)
    dst  = os.path.join(PNG_DIR, stem + ".png")

    if os.path.exists(dst):
        print(f"[{i:02d}/{total}] SKIP (cached)  {fname}")
        ok += 1
        continue

    text = open(src, encoding="utf-8").read()

    # Thử kroki.io trước, fallback plantuml.com
    urls = [
        SERVER_KROKI    + encode_kroki(text),
        SERVER_PLANTUML + encode_plantuml(text),
    ]

    success = False
    for url in urls:
        for attempt in range(2):
            try:
                data = fetch(url)
                # Kiểm tra không phải trang lỗi HTML
                if data[:4] == b'\x89PNG':
                    open(dst, "wb").write(data)
                    print(f"[{i:02d}/{total}] OK   {fname}  ({len(data)//1024}KB)")
                    ok += 1
                    success = True
                    break
                else:
                    raise ValueError("Response không phải PNG")
            except Exception as e:
                if attempt == 0:
                    time.sleep(1)
                else:
                    print(f"       {url[:60]}... -> {e}")
        if success:
            break

    if not success:
        print(f"[{i:02d}/{total}] FAIL  {fname}")
        fail.append(fname)
    time.sleep(0.5)

print(f"\nDone: {ok}/{total} OK  |  {len(fail)} failed")
if fail:
    print("Failed:", fail)
