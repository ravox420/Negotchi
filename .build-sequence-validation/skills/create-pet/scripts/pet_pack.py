"""Build/validate Negotchi animation packages from eleven RGBA animation strips."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw

STATES = ("idle", "running-right", "running-left", "waving", "jumping", "failed",
          "waiting", "running", "review", "thinking", "responding")
ALIASES = {"move-right": "running-right", "move_right": "running-right",
           "move-left": "running-left", "move_left": "running-left",
           "wave": "waving", "bounce": "jumping", "sad": "failed"}
ONCE = {"waving", "jumping", "failed"}
WIDTH, HEIGHT, COUNT = 192, 208, 8


def check_id(value):
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,63}", value or "") or re.fullmatch(
            r"con|prn|aux|nul|com[1-9]|lpt[1-9]", value, re.I):
        raise ValueError("id invalido ou reservado no Windows")


def load_image(path):
    with Image.open(path) as source:
        if source.format != "PNG" or source.width > 8192 or source.height > 8192 or source.width * source.height > 32_000_000:
            raise ValueError(f"PNG invalido ou muito grande: {path}")
        return source.convert("RGBA")


def validate(directory):
    directory = Path(directory)
    data = json.loads((directory / "pet.json").read_text(encoding="utf-8-sig"))
    check_id(data["id"])
    if data.get("schemaVersion") != 2 or not str(data.get("displayName", "")).strip():
        raise ValueError("Pacote precisa de schemaVersion 2 e displayName")
    frame = data["frame"]
    width, height, columns, rows = (frame[k] for k in ("width", "height", "columns", "rows"))
    if any(type(v) is not int or v <= 0 for v in (width, height, columns, rows)) or columns * rows > 4096:
        raise ValueError("Grade invalida")
    if data.get("spritesheetPath") != "spritesheet.png":
        raise ValueError("spritesheetPath precisa ser spritesheet.png")
    sheet = load_image(directory / "spritesheet.png")
    if sheet.size != (width * columns, height * rows):
        raise ValueError("Dimensoes do atlas diferentes do manifesto")
    animations = data["animations"]
    signatures, tracks = {}, {}
    for state in STATES:
        track = animations[state]
        indices = track["frames"]
        fps = track["fps"]
        if (len(indices) != COUNT or any(type(i) is not int or not 0 <= i < columns * rows for i in indices)
                or type(fps) not in (int, float) or not 0 < fps <= 60
                or type(track["loop"]) is not bool or track["fallback"] != "idle"):
            raise ValueError(f"Trilha invalida: {state}")
        if track["loop"] != (state not in ONCE):
            raise ValueError(f"Loop incorreto: {state}")
        hashes = []
        for index in indices:
            x, y = index % columns * width, index // columns * height
            cell = sheet.crop((x, y, x + width, y + height))
            alpha = cell.getchannel("A")
            bounds = alpha.getbbox()
            if bounds is None or alpha.getextrema()[0] != 0:
                raise ValueError(f"Quadro vazio ou sem fundo transparente: {state}/{index}")
            if bounds[0] == 0 or bounds[1] == 0 or bounds[2] == width or bounds[3] == height:
                raise ValueError(f"Desenho toca a borda da celula: {state}/{index}")
            hashes.append(hashlib.sha256(cell.tobytes()).hexdigest())
        if len(set(hashes)) < 4:
            raise ValueError(f"Poucos desenhos distintos em {state}; nao use imagem estatica")
        signature = tuple(sorted(set(hashes)))
        if signature in signatures:
            raise ValueError(f"Estados identicos: {state} e {signatures[signature]}")
        signatures[signature] = state
        tracks[state] = {"frames": len(indices), "uniqueFrames": len(set(hashes))}
    for alias, target in ALIASES.items():
        if animations.get(alias) != animations[target]:
            raise ValueError(f"Alias inconsistente: {alias}")
    # Check optional extra tracks too; runtime must never encounter a broken fallback.
    for name, track in animations.items():
        if (not track.get("frames") or any(type(i) is not int or not 0 <= i < columns * rows for i in track["frames"])
                or type(track.get("fps")) not in (int, float) or not 0 < track["fps"] <= 60
                or type(track.get("loop")) is not bool or track.get("fallback") not in animations):
            raise ValueError(f"Trilha adicional invalida: {name}")
    return {"valid": True, "id": data["id"], "states": tracks, "visualReview": "required"}


def pack(args):
    check_id(args.id)
    if not args.name.strip():
        raise ValueError("Informe o nome do pet")
    destination = Path(args.output).resolve()
    if destination.exists():
        raise ValueError("Destino ja existe; use uma nova pasta para preservar o original")
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix=".pet-pack-", dir=destination.parent))
    try:
        sheet = Image.new("RGBA", (WIDTH * COUNT, HEIGHT * len(STATES)))
        tracks, cells_by_state, source_size = {}, {}, None
        for row, state in enumerate(STATES):
            strip = load_image(Path(args.input) / f"{state}.png")
            if strip.width % COUNT or strip.width // COUNT <= 0:
                raise ValueError(f"Faixa precisa de oito celulas regulares: {state}")
            if source_size is not None and strip.size != source_size:
                raise ValueError("Todas as faixas precisam ter as mesmas dimensoes")
            source_size = strip.size
            cell_width = strip.width // COUNT
            factor = min(WIDTH / cell_width, HEIGHT / strip.height)
            size = (max(1, round(cell_width * factor)), max(1, round(strip.height * factor)))
            cells = []
            for col in range(COUNT):
                original = strip.crop((col * cell_width, 0, (col + 1) * cell_width, strip.height))
                resized = original.resize(size, Image.Resampling.LANCZOS)
                cell = Image.new("RGBA", (WIDTH, HEIGHT))
                cell.alpha_composite(resized, ((WIDTH - size[0]) // 2, (HEIGHT - size[1]) // 2))
                sheet.alpha_composite(cell, (col * WIDTH, row * HEIGHT))
                cells.append(cell)
            fps = 8 if state in {"running", "running-right", "running-left", "jumping", "responding"} else 6
            tracks[state] = {"frames": list(range(row * COUNT, (row + 1) * COUNT)), "fps": fps,
                             "loop": state not in ONCE, "fallback": "idle"}
            cells_by_state[state] = cells
        for alias, target in ALIASES.items():
            tracks[alias] = dict(tracks[target])
        data = {"schemaVersion": 2, "id": args.id, "displayName": args.name,
                "description": args.description, "spritesheetPath": "spritesheet.png",
                "frame": {"width": WIDTH, "height": HEIGHT, "columns": COUNT, "rows": len(STATES)},
                "animations": tracks}
        (staging / "pet.json").write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
        sheet.save(staging / "spritesheet.png")
        cells_by_state["idle"][0].save(staging / "preview.png")
        report = validate(staging)
        (staging / "animations").mkdir()
        contact = Image.new("RGB", (WIDTH * COUNT, (HEIGHT + 24) * len(STATES)), "#30343a")
        draw = ImageDraw.Draw(contact)
        for row, state in enumerate(STATES):
            draw.text((8, row * (HEIGHT + 24) + 4), state, fill="white")
            frames = []
            for col, cell in enumerate(cells_by_state[state]):
                contact.paste(cell, (col * WIDTH, row * (HEIGHT + 24) + 24), cell)
                # GIF previews use an explicit dark background to avoid disposal artifacts.
                preview = Image.new("RGB", cell.size, "#30343a")
                preview.paste(cell, mask=cell.getchannel("A"))
                frames.append(preview)
            options = {"loop": 0} if tracks[state]["loop"] else {}
            frames[0].save(staging / "animations" / f"{state}.gif", save_all=True,
                           append_images=frames[1:], duration=round(1000 / tracks[state]["fps"]), **options)
        contact.save(staging / "contact-sheet.png")
        (staging / "validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        staging.rename(destination)
        return report
    finally:
        if staging.exists():
            shutil.rmtree(staging)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    builder = sub.add_parser("pack")
    for key in ("input", "output", "id", "name"):
        builder.add_argument(f"--{key}", required=True)
    builder.add_argument("--description", default="")
    verifier = sub.add_parser("validate")
    verifier.add_argument("directory")
    args = parser.parse_args()
    try:
        print(json.dumps(pack(args) if args.command == "pack" else validate(args.directory), indent=2))
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, f"Falha: {error}\n")


if __name__ == "__main__":
    main()
