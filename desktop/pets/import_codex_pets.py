"""Import the eight cached Codex pets for the native Negotchi app.

Requires FFmpeg only while importing. No network requests or asset generation.
The original pixels, transparency and padded animation rows are preserved.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parent
PETS = [
    ("codex", "Codex", "O companheiro original do Codex."),
    ("dewey", "Dewey", "Um pato tranquilo para acompanhar o trabalho."),
    ("fireball", "Fireball", "Uma chama cheia de energia."),
    ("rocky", "Rocky", "Uma pedra firme para os dias mais intensos."),
    ("seedy", "Seedy", "Um pequeno broto para novas ideias."),
    ("stacky", "Stacky", "Uma pilha equilibrada para manter o foco."),
    ("bsod", "BSOD", "Um pequeno gremlin de tela azul."),
    ("null-signal", "Null Signal", "Um sinal silencioso vindo do vazio."),
]
# Built-in sheets use one padded row per state, unlike PimpDogg's packed layout.
STATES = [
    ("idle", 6, 6), ("running-right", 8, 12), ("running-left", 8, 12),
    ("waving", 4, 8), ("jumping", 5, 10), ("failed", 8, 8),
    ("waiting", 6, 6), ("running", 6, 10), ("review", 6, 6),
]
ALIASES = {"move_right": "running-right", "move_left": "running-left", "wave": "waving", "bounce": "jumping", "sad": "failed"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    home = Path(os.environ.get("CODEX_HOME", Path.home() / ".codex"))
    parser.add_argument("--cache", type=Path, default=home / "cache/tui-pets/v1/assets")
    args = parser.parse_args()
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise SystemExit("FFmpeg precisa estar no PATH para importar os pets.")
    sources = [args.cache / (pet_id + "-spritesheet-v4.webp") for pet_id, _, _ in PETS]
    missing = [str(path) for path in sources if not path.is_file()]
    if missing:
        raise SystemExit("Pets ausentes no cache:\n" + "\n".join(missing))
    report = []
    for (pet_id, name, description), source in zip(PETS, sources):
        output = ROOT / "pets" / pet_id
        output.mkdir(parents=True, exist_ok=True)
        sheet = output / "spritesheet.png"
        subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", str(source), "-frames:v", "1", "-pix_fmt", "rgba", "-update", "1", str(sheet)], check=True)
        subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", str(sheet), "-vf", "crop=192:208:0:0", "-frames:v", "1", "-update", "1", str(output / "preview.png")], check=True)
        animations = {}
        for row, (state, count, fps) in enumerate(STATES):
            animations[state] = {"frames": list(range(row * 8, row * 8 + count)), "fps": fps, "loop": state in {"idle", "waiting", "running", "review"}, "fallback": "idle"}
        for alias, state in ALIASES.items():
            animations[alias] = dict(animations[state])
        manifest = {"id": pet_id, "displayName": name, "description": description, "spritesheetPath": "spritesheet.png", "frame": {"width": 192, "height": 208, "columns": 8, "rows": 9}, "animations": animations}
        (output / "pet.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        report.append({"id": pet_id, "displayName": name, "sourceFile": source.name, "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(), "pngSha256": hashlib.sha256(sheet.read_bytes()).hexdigest()})
        print("Importado:", name)
    (ROOT / "pets" / "import-report.json").write_text(json.dumps({"source": "$CODEX_HOME/cache/tui-pets/v1/assets", "layout": "8 columns x 9 rows; 192 x 208 per frame", "pets": report}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
