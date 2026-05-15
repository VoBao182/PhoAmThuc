import zlib
import base64
from pathlib import Path
import urllib.request

puml_path = Path('docs/diagrams/03-geofence-audio-sequence.puml')
output_path = Path('docs/diagrams/03-geofence-audio-sequence.png')
text = puml_path.read_text(encoding='utf-8')
compressed = zlib.compress(text.encode('utf-8'), 9)[2:-4]
encoded = base64.b64encode(compressed).decode('ascii').translate(str.maketrans('+/=', '-_.'))
url = 'https://www.plantuml.com/plantuml/png/' + encoded
print('Requesting:', url)
with urllib.request.urlopen(url, timeout=30) as response:
    data = response.read()
output_path.write_bytes(data)
print('Saved', output_path)
