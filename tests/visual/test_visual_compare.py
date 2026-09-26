import importlib.util
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

spec = importlib.util.spec_from_file_location("visual_test", Path(__file__).resolve().parents[2] / "scripts/visual-test.py")
visual = importlib.util.module_from_spec(spec)
spec.loader.exec_module(visual)


class VisualComparisonTests(unittest.TestCase):
    def test_pixels_including_alpha_and_dimensions(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            actual, baseline, diff = (root / name for name in ("actual.png", "baseline.png", "diff.png"))
            image = Image.new("RGBA", (2, 2), (10, 20, 30, 255))
            image.save(actual)
            self.assertEqual("missing-baseline", visual.compare(actual, baseline, diff)["status"])
            image.save(baseline, compress_level=0)
            self.assertEqual("match", visual.compare(actual, baseline, diff)["status"])
            image.putpixel((0, 0), (10, 20, 30, 0))
            image.save(actual)
            result = visual.compare(actual, baseline, diff)
            self.assertEqual("different", result["status"])
            self.assertEqual(1, result["changed_pixels"])
            self.assertTrue(diff.exists())
            image.resize((3, 2)).save(actual)
            self.assertEqual("size-mismatch", visual.compare(actual, baseline, diff)["status"])

    def test_accept_requires_clean_run_and_selects_only_requested_image(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            actual = root / "run/classic/actual"
            actual.mkdir(parents=True)
            for name in ("map", "menu"):
                Image.new("RGB", (2, 2)).save(actual / (name + ".png"))
            args = SimpleNamespace(run=root / "run", mode="classic", all=False,
                                   scenario=["map"], baselines=root / "baselines")
            data = {"classic": {"errors": ["missing resource"],
                                "scenarios": [{"name": "map"}, {"name": "menu"}]}}
            report = args.run / "results.json"
            report.write_text(json.dumps(data))
            with self.assertRaises(ValueError):
                visual.accept(args)
            self.assertFalse(args.baselines.exists())
            data["classic"]["errors"] = []
            report.write_text(json.dumps(data))
            visual.accept(args)
            self.assertTrue((args.baselines / "classic/map.png").exists())
            self.assertFalse((args.baselines / "classic/menu.png").exists())


if __name__ == "__main__":
    unittest.main()
