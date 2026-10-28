"""Stage an eight-frame dance in an existing built-in atlas without altering its old frames."""
import argparse
import json
from pathlib import Path

from PIL import Image


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package', type=Path)
    parser.add_argument('strip', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    manifest = json.loads((args.package / 'pet.json').read_text(encoding='utf-8'))
    frame = manifest['frame']
    width, height, columns, rows = (frame[k] for k in ('width', 'height', 'columns', 'rows'))
    if columns != 8 or 'dancing' in manifest['animations']:
        raise ValueError('Expected eight columns and no existing dance')
    atlas = Image.open(args.package / 'spritesheet.png').convert('RGBA')
    strip = Image.open(args.strip).convert('RGBA')
    if atlas.size != (width * columns, height * rows):
        raise ValueError('Atlas has an unexpected size')
    # Find one shared grid, allowing a small pitch and origin correction.
    alpha = strip.getchannel('A').point(lambda p: 255 if p > 16 else 0)
    occupied = [alpha.crop((x, 0, x + 1, strip.height)).getbbox() is not None for x in range(strip.width)]
    candidates = []
    expected = strip.width / 8
    for cell_width in range(round(expected * .94), round(expected * 1.06) + 1):
        for offset in range(-cell_width // 4, cell_width // 4 + 1):
            cuts = [offset + i * cell_width for i in range(9)]
            if any(any(occupied[max(0, x - 2):min(strip.width, x + 2)]) for x in cuts):
                continue
            candidates.append((abs(cell_width - expected) + abs(offset) / 8, cell_width, offset))
    if not candidates:
        raise ValueError('No shared grid separates all eight frames')
    _, cell_width, offset = min(candidates)
    boxes = []
    for i in range(8):
        cell_alpha = strip.crop((offset + i * cell_width, 0, offset + (i + 1) * cell_width, strip.height)).getchannel('A')
        box = cell_alpha.point(lambda p: 255 if p > 16 else 0).getbbox()
        if not box or box[0] < 2 or box[2] > cell_width - 2:
            raise ValueError('Empty or clipped generated frame: ' + str(i))
        boxes.append(box)
    top = min(b[1] for b in boxes)
    bottom = max(b[3] for b in boxes)
    idle = atlas.crop((0, 0, width, height)).getchannel('A').point(lambda p: 255 if p > 16 else 0).getbbox()
    if not idle:
        raise ValueError('Original idle frame is empty')
    # One crop and one scale for the whole strip: never fit individual poses.
    scale = min((idle[3] - idle[1]) / (bottom - top), (width - 8) / cell_width, (height - 8) / (bottom - top))
    scaled_width = max(1, round(cell_width * scale))
    scaled_height = max(1, round((bottom - top) * scale))
    y = min(height - scaled_height - 2, max(2, idle[3] - scaled_height))
    result = Image.new('RGBA', (width * columns, height * (rows + 1)))
    result.paste(atlas, (0, 0))
    for i in range(8):
        cell = strip.crop((offset + i * cell_width, top, offset + (i + 1) * cell_width, bottom))
        cell = cell.resize((scaled_width, scaled_height), Image.Resampling.LANCZOS)
        result.paste(cell, (i * width + (width - scaled_width) // 2, rows * height + y))
    frame['rows'] += 1
    manifest['animations']['dancing'] = {'frames': list(range(rows * 8, rows * 8 + 8)), 'fps': 6, 'loop': True, 'fallback': 'idle'}
    args.output.mkdir(parents=True, exist_ok=False)
    result.save(args.output / 'spritesheet.png')
    (args.output / 'pet.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    Image.open(args.package / 'preview.png').save(args.output / 'preview.png')


if __name__ == '__main__':
    main()
