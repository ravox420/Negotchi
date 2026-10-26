"""Register generated regular grids, then use the create-pet helper unchanged.

Only one grid, scale and ground anchor are used per strip. No per-pose tight
cropping, mirroring, synthetic poses, alpha keying or background repainting.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import shutil
from pathlib import Path
from types import SimpleNamespace
from statistics import median

from PIL import Image, ImageChops, ImageDraw

BASE = Path(__file__).resolve().parent
ROOT = BASE.parents[1]
FRAME = (192, 208)


def threshold(channel, value):
    return channel.point(lambda v: 255 if v > value else 0)


def measure(record):
    source = BASE / 'raw' / (record['state'] + '.png')
    if not source.exists():
        source.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(record['source'], source)
    image = Image.open(source).convert('RGBA')
    # A single uniform resampling for the full strip, preserving aspect ratio.
    factor = 2048 / image.width
    image = image.resize((2048, round(image.height * factor)), Image.Resampling.LANCZOS)
    red, green, blue, alpha = image.split()
    screen = ImageChops.multiply(threshold(red, 130), threshold(green, 170))
    screen = ImageChops.multiply(screen, threshold(blue, 100))
    screen = ImageChops.multiply(screen, threshold(alpha, 128))
    screens = []
    for i in range(8):
        box = screen.crop((i * 256, 0, (i + 1) * 256, image.height)).getbbox()
        if box is None:
            raise ValueError('Screen not detected: ' + record['state'])
        screens.append((i * 256 + (box[0] + box[2]) / 2, box[3] - box[1]))
    # Estimate ONE regular grid pitch from the eight screen centers.
    mean_x = sum(x for x, h in screens) / 8
    fitted_pitch = sum((i - 3.5) * (x - mean_x) for i, (x, h) in enumerate(screens)) / 42
    solid = threshold(alpha, 16)
    occupied = [solid.crop((x, 0, x + 1, image.height)).getbbox() is not None for x in range(2048)]
    ranges = []
    for x, value in enumerate(occupied):
        if value and (x == 0 or not occupied[x - 1]):
            start = x
        if value and (x == 2047 or not occupied[x + 1]):
            if ranges and start - ranges[-1][1] <= 6:
                ranges[-1][1] = x + 1
            else:
                ranges.append([start, x + 1])
    ranges = [r for r in ranges if r[1] - r[0] >= 30]
    if len(ranges) != 8:
        raise ValueError(f"{record['state']}: expected eight isolated figures, found {ranges}")
    candidates = []
    for pitch in range(round(fitted_pitch) - 8, round(fitted_pitch) + 9):
        lower = max(right - (i + 1) * pitch for i, (left, right) in enumerate(ranges))
        upper = min(left - i * pitch for i, (left, right) in enumerate(ranges))
        if upper - lower >= 2:
            candidates.append((abs(pitch - fitted_pitch), -(upper - lower), pitch, (lower + upper) // 2))
    if not candidates:
        raise ValueError(f"{record['state']}: no regular grid can contain the complete poses")
    _, _, pitch, origin = min(candidates)
    boxes = [solid.crop((origin + i * pitch, 0, origin + (i + 1) * pitch, image.height)).getbbox() for i in range(8)]
    ground = max(b[3] for b in boxes)
    top = min(b[1] for b in boxes)
    screen_height = median(h for x, h in screens)
    return dict(state=record['state'], image=image, pitch=pitch, origin=origin,
                ground=ground, top=top, screen_height=screen_height, boxes=boxes)


def normalize(measurements):
    # Leave enough headroom for the highest jump without changing scale mid-track.
    face_height = min(50, *(m['screen_height'] * 182 / (m['ground'] - m['top']) for m in measurements))
    report = dict(targetScreenHeight=face_height, frame=list(FRAME), strips={})
    strips = BASE / 'strips'
    strips.mkdir(exist_ok=True)
    for m in measurements:
        factor = face_height / m['screen_height']
        resized_size = (round(m['pitch'] * factor), round(m['image'].height * factor))
        position = (round(96 - resized_size[0] / 2), round(196 - m['ground'] * factor))
        strip = Image.new('RGBA', (192 * 8, 208))
        bounds = []
        for i in range(8):
            whole_cell = m['image'].crop((m['origin'] + i * m['pitch'], 0,
                                          m['origin'] + (i + 1) * m['pitch'], m['image'].height))
            resized = whole_cell.resize(resized_size, Image.Resampling.LANCZOS)
            # Fixed viewport and transparent gutter for every pose; only almost
            # invisible alpha noise may fall outside this common viewport.
            inner = Image.new('RGBA', (184, 200))
            inner.alpha_composite(resized, (position[0] - 4, position[1] - 4))
            cell = Image.new('RGBA', FRAME)
            cell.alpha_composite(inner, (4, 4))
            box = threshold(cell.getchannel('A'), 16).getbbox()
            if box is None or box[0] <= 5 or box[1] <= 5 or box[2] >= 187 or box[3] >= 203:
                raise ValueError(f"Visible art reaches viewport: {m['state']}/{i}: {box}")
            bounds.append(box)
            strip.alpha_composite(cell, (192 * i, 0))
        strip.save(strips / (m['state'] + '.png'))
        report['strips'][m['state']] = dict(pitch=m['pitch'], origin=m['origin'], scale=factor,
                                            ground=m['ground'], position=position, bounds=bounds)
    (BASE / 'normalization.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    return strips


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    records = json.loads((BASE / 'generation.json').read_text(encoding='utf-8'))['states']
    strips = normalize([measure(record) for record in records])
    helper = ROOT / '.agents/skills/create-pet/scripts/pet_pack.py'
    spec = importlib.util.spec_from_file_location('pet_pack', helper)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    # The runtime accepts extra tracks. Keep the helper source untouched and
    # include dancing in all its existing assembly and validation operations.
    module.STATES = module.STATES + ('dancing',)
    output = Path(args.output).resolve()
    report = module.pack(SimpleNamespace(input=strips, output=output, id='bmo', name='BMO',
        description='BMO em estilo cartoon, com onze estados de conversa e uma dancinha em loop.'))
    report = module.validate(output)
    report['visualReview'] = 'required'
    report['totalFrames'] = 96
    (output / 'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    shutil.copy2(BASE / 'generation.json', output / 'generation.json')
    # Actual GIF decoding is also checked, rather than assuming the exporter kept
    # all eight drawings and their intended timing.
    gif_report = {}
    decoded = Image.new('RGB', (1536, 232 * 12), '#30343a')
    draw = ImageDraw.Draw(decoded)
    for row, state in enumerate(module.STATES):
        gif = Image.open(output / 'animations' / (state + '.gif'))
        durations = []
        draw.text((8, row * 232 + 4), state, fill='white')
        for col in range(gif.n_frames):
            gif.seek(col)
            durations.append(gif.info.get('duration'))
            decoded.paste(gif.convert('RGB'), (col * 192, row * 232 + 24))
        if gif.n_frames != 8 or any(not v or v <= 0 for v in durations):
            raise ValueError('Invalid GIF: ' + state)
        gif_report[state] = dict(frames=gif.n_frames, durationsMs=durations, loop=gif.info.get('loop'))
    decoded.save(BASE / 'gif-contact-sheet.png')
    (BASE / 'gif-validation.json').write_text(json.dumps(gif_report, indent=2), encoding='utf-8')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
