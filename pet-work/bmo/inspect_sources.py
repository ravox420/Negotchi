import json
from pathlib import Path
from PIL import Image

base = Path(__file__).resolve().parent
for record in json.loads((base / 'generation.json').read_text(encoding='utf-8'))['states']:
    im = Image.open(record['source']).convert('RGBA').resize((2048, 688))
    alpha = im.getchannel('A')
    occupied = [sum(v > 16 for v in alpha.crop((x, 0, x + 1, 688)).getdata()) > 3 for x in range(2048)]
    ranges = []
    for x, value in enumerate(occupied):
        if value and (x == 0 or not occupied[x - 1]):
            start = x
        if value and (x == 2047 or not occupied[x + 1]):
            if ranges and start - ranges[-1][1] <= 6:
                ranges[-1][1] = x + 1
            else:
                ranges.append([start, x + 1])
    ranges = [pair for pair in ranges if pair[1] - pair[0] >= 30]
    phase = None
    if len(ranges) == 8:
        lo = max(right - (i + 1) * 256 for i, (left, right) in enumerate(ranges))
        hi = min(left - i * 256 for i, (left, right) in enumerate(ranges))
        phase = [lo, hi]
    boxes = [alpha.crop((i * 256, 0, (i + 1) * 256, 688)).point(lambda v: 255 if v > 16 else 0).getbbox() for i in range(8)]
    print(record['state'], 'segments=', ranges, 'phase=', phase, 'boxes=', boxes)
