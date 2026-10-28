"""Technical pipeline tests. Geometric fixtures are not example pet artwork."""
import importlib.util
import json
import tempfile
import sys
import unittest
from argparse import Namespace
from pathlib import Path

from PIL import Image, ImageDraw

MODULE = Path(__file__).resolve().parents[3] / ".agents/skills/create-pet/scripts/pet_pack.py"
spec = importlib.util.spec_from_file_location("pet_pack", MODULE)
pet_pack = importlib.util.module_from_spec(spec)
sys.dont_write_bytecode = True
spec.loader.exec_module(pet_pack)


class PetPackTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.strips = self.root / "strips"
        self.strips.mkdir()
        for row, state in enumerate(pet_pack.STATES):
            image = Image.new("RGBA", (192 * 8, 208))
            draw = ImageDraw.Draw(image)
            for col in range(8):
                x = col * 192
                draw.ellipse((x + 45, 45 + col, x + 140, 180), fill=(row * 20, col * 30, 110, 255))
            image.save(self.strips / f"{state}.png")

    def args(self):
        return Namespace(input=self.strips, output=self.root / "package", id="test-pet",
                         name="Test pet", description="Geometric test fixture")

    def test_package_round_trip_and_previews(self):
        args = self.args()
        report = pet_pack.pack(args)
        self.assertTrue(report["valid"])
        self.assertEqual(len(report["states"]), 11)
        self.assertEqual(pet_pack.validate(args.output), report)
        with Image.open(args.output / "spritesheet.png") as sheet:
            self.assertEqual(sheet.size, (1536, 2288))
        self.assertEqual(len(list((args.output / "animations").glob("*.gif"))), 11)
        self.assertTrue((args.output / "contact-sheet.png").is_file())
        self.assertEqual(report["visualReview"], "required")
        with Image.open(args.output / "animations" / "thinking.gif") as preview:
            self.assertEqual(preview.n_frames, 8)
            self.assertEqual(preview.info["loop"], 0)
        with Image.open(args.output / "animations" / "jumping.gif") as preview:
            self.assertNotIn("loop", preview.info)
        with self.assertRaisesRegex(ValueError, "Destino ja existe"):
            pet_pack.pack(args)

    def test_rejects_static_strip_and_leaves_no_partial_package(self):
        image = Image.new("RGBA", (192 * 8, 208))
        draw = ImageDraw.Draw(image)
        for col in range(8):
            draw.rectangle((col * 192 + 40, 40, col * 192 + 150, 180), fill="red")
        image.save(self.strips / "thinking.png")
        with self.assertRaisesRegex(ValueError, "Poucos desenhos"):
            pet_pack.pack(self.args())
        self.assertFalse((self.root / "package").exists())
        self.assertFalse(list(self.root.glob(".pet-pack-*")))

    def test_rejects_duplicate_states(self):
        (self.strips / "thinking.png").write_bytes((self.strips / "idle.png").read_bytes())
        with self.assertRaisesRegex(ValueError, "Estados identicos"):
            pet_pack.pack(self.args())

    def test_rejects_opaque_or_clipped_cells(self):
        for image, message in ((Image.new("RGBA", (1536, 208), "red"), "sem fundo transparente"),
                               (Image.new("RGBA", (1536, 208)), "toca a borda")):
            if message == "toca a borda":
                draw = ImageDraw.Draw(image)
                for col in range(8):
                    draw.rectangle((col * 192, 10, col * 192 + 60, 120 + col), fill="red")
            image.save(self.strips / "thinking.png")
            with self.assertRaisesRegex(ValueError, message):
                pet_pack.pack(self.args())

    def test_rejects_broken_alias(self):
        args = self.args()
        pet_pack.pack(args)
        path = args.output / "pet.json"
        data = json.loads(path.read_text())
        data["animations"]["move-right"]["frames"] = [0] * 8
        path.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "Alias inconsistente"):
            pet_pack.validate(args.output)

    def test_requires_all_states_and_safe_id(self):
        for value in ("..", "../escape", "CON", "lpt1", "a/b", "a" * 65):
            with self.assertRaises(ValueError):
                pet_pack.check_id(value)
        (self.strips / "responding.png").unlink()
        with self.assertRaises(FileNotFoundError):
            pet_pack.pack(self.args())
        self.assertFalse((self.root / "package").exists())


if __name__ == "__main__":
    unittest.main()
