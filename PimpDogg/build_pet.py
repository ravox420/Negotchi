"""Convert the existing ChatGPT pet GIF into a Codex 0.159.2 local pet.

Uses installed FFmpeg/FFprobe, with no Python packages or image generation.
All generated artifacts stay beside this script.
"""

import json
import shutil
import collections
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parent
SOURCE = Path(r"C:\Users\DIRECTA\Desktop\PimpDogg.gif")
PACKAGE = ROOT / "package-transparent"
STATES = [
    ("idle", 6),
    ("running-right", 8),
    ("running-left", 8),
    ("waving", 4),
    ("jumping", 5),
    ("failed", 8),
    ("waiting", 6),
    ("running", 6),
    ("review", 6),
]
ALIASES = {
    "move_right": "running-right",
    "move_left": "running-left",
    "wave": "waving",
    "bounce": "jumping",
    "sad": "failed",
}


def run(*args):
    return subprocess.run(args, check=True, capture_output=True, text=True).stdout


def main():
    ffmpeg = shutil.which("ffmpeg")
    ffprobe = shutil.which("ffprobe")
    if not ffmpeg or not ffprobe:
        raise SystemExit("FFmpeg and FFprobe must be available on PATH.")
    probe = json.loads(run(
        ffprobe, "-v", "error", "-select_streams", "v:0", "-show_frames",
        "-show_entries", "frame=duration_time", "-of", "json", str(SOURCE),
    ))
    frames = probe["frames"]
    expected = sum(count for _, count in STATES)
    if len(frames) != expected:
        raise SystemExit(f"Expected the original {expected} pet frames, found {len(frames)}.")
    PACKAGE.mkdir(parents=True, exist_ok=True)
    spritesheet = PACKAGE / "spritesheet.png"
    if spritesheet.exists():
        raise SystemExit("The generated sheet already exists; refusing to overwrite it.")
    # The exported GIF includes a state label and a flat preview background.
    # Crop only the label strip and remove border-connected background pixels;
    # enclosed light details (eyes, jewelry) remain unchanged.
    raw = subprocess.run(
        [ffmpeg, "-hide_banner", "-loglevel", "error", "-i", str(SOURCE),
         "-vf", "crop=192:214:0:20", "-fps_mode", "passthrough",
         "-pix_fmt", "rgba", "-f", "rawvideo", "pipe:1"],
        check=True, capture_output=True,
    ).stdout
    width, height = 192, 214
    stride = width * height * 4
    assert len(raw) == expected * stride
    cleaned = bytearray()
    for frame_number in range(expected):
        pixels = bytearray(raw[frame_number * stride:(frame_number + 1) * stride])
        seen = bytearray(width * height)
        queue = collections.deque()
        for x in range(width):
            queue.append(x)
            queue.append((height - 1) * width + x)
        for y in range(height):
            queue.append(y * width)
            queue.append(y * width + width - 1)
        while queue:
            position = queue.popleft()
            if seen[position]:
                continue
            seen[position] = 1
            channel = position * 4
            red, green, blue, alpha = pixels[channel:channel + 4]
            if alpha and max(abs(red - 242), abs(green - 241), abs(blue - 241)) > 12:
                continue
            pixels[channel + 3] = 0
            x, y = position % width, position // width
            if x:
                queue.append(position - 1)
            if x + 1 < width:
                queue.append(position + 1)
            if y:
                queue.append(position - width)
            if y + 1 < height:
                queue.append(position + width)
        cleaned.extend(pixels)
    subprocess.run([
        ffmpeg, "-hide_banner", "-loglevel", "error", "-n",
        "-f", "rawvideo", "-pix_fmt", "rgba", "-video_size", "192x214",
        "-framerate", "50", "-i", "pipe:0",
        "-vf", "scale=192:208:force_original_aspect_ratio=decrease,"
        "pad=192:208:(ow-iw)/2:(oh-ih)/2:color=0x00000000,"
        "tile=8x9:nb_frames=57:color=0x00000000",
        "-frames:v", "1", "-update", "1", str(spritesheet),
    ], input=cleaned, capture_output=True, check=True)
    # The GIF stores the 57 populated cells consecutively. Explicit tracks
    # retain that layout rather than assuming the default row padding.
    animations = {}
    offset = 0
    durations = []
    for state, count in STATES:
        timeline = []
        for index in range(offset, offset + count):
            seconds = float(frames[index].get("duration_time", "0.12"))
            durations.append(seconds)
            # The manifest accepts up to 60 fps. A 20-ms clock preserves GIF
            # holds within 10 ms, including its longer final-frame pauses.
            ticks = max(1, int(seconds * 50 + 0.5))
            timeline.extend([index] * ticks)
        animations[state] = {
            "frames": timeline,
            "fps": 50,
            "loop": state in {"idle", "waiting", "running", "review"},
            "fallback": "idle",
        }
        offset += count
    for alias, state in ALIASES.items():
        animations[alias] = dict(animations[state])
    manifest = {
        "id": "PimpDogg",
        "displayName": "PimpDogg",
        "description": "Mascote de traje rosa felpudo, tranças e correntes douradas.",
        "spritesheetPath": "spritesheet.png",
        "frame": {"width": 192, "height": 208, "columns": 8, "rows": 9},
        "animations": animations,
    }
    with (PACKAGE / "pet.json").open("x", encoding="utf-8") as output:
        json.dump(manifest, output, ensure_ascii=False, indent=2)
        output.write("\n")
    dimensions = json.loads(run(
        ffprobe, "-v", "error", "-show_entries", "stream=width,height",
        "-of", "json", str(spritesheet),
    ))["streams"][0]
    assert (dimensions["width"], dimensions["height"]) == (1536, 1872)
    for name, animation in animations.items():
        assert animation["frames"] and all(0 <= i < 72 for i in animation["frames"]), name
        assert animation["fallback"] in animations
        assert 0 < animation["fps"] <= 60
    preview = ROOT / "preview-transparent.png"
    run(
        ffmpeg, "-hide_banner", "-loglevel", "error", "-n", "-i", str(spritesheet),
        "-vf", "crop=192:208:0:0", "-frames:v", "1", "-update", "1", str(preview),
    )
    report = {
        "source": str(SOURCE), "source_frames": len(frames),
        "source_duration_seconds": round(sum(durations), 3),
        "sheet": dimensions, "animations": list(animations),
        "selector": "custom:PimpDogg", "codex_version": "0.159.2",
    }
    with (ROOT / "validation-transparent.json").open("x", encoding="utf-8") as output:
        json.dump(report, output, ensure_ascii=False, indent=2)
        output.write("\n")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
