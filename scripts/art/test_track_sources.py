import json
from pathlib import Path
import struct
import tempfile
import unittest

from track_sources import scan, main, glb_problem


def glb(path):
    data = json.dumps({"asset": {"version": "2.0"}, "meshes": [{"primitives": []}]}).encode()
    data += b" " * (-len(data) % 4)
    path.write_bytes(struct.pack("<4sII", b"glTF", 2, 20 + len(data)) +
                     struct.pack("<I4s", len(data), b"JSON") + data)


class TrackerTests(unittest.TestCase):
    def test_inventory_progress_and_idempotence(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            src = root / "ArtSource"
            for folder in ("Dragons/images", "Habitats/concepts", "Habitats/props_3d",
                           "NatureAndGameplay/foliage"):
                (src / folder).mkdir(parents=True)
            (src / "Dragons/images/01_dragon.jpg").write_bytes(b"image")
            glb(src / "Dragons/images/01_dragon.glb")
            (src / "Habitats/concepts/01_scene.jpg").write_bytes(b"image")
            (src / "Habitats/props_3d/01_statue.jpg").write_bytes(b"image")
            (src / "NatureAndGameplay/foliage/01_tree.jpg").write_bytes(b"image")
            (src / "NatureAndGameplay/foliage/01_tree.glb").write_bytes(b"not glb")
            glb(src / "NatureAndGameplay/foliage/02_unmatched.glb")
            (src / "NatureAndGameplay/preview_all.png").write_bytes(b"preview")
            manifest, progress = scan(root)
            status = {a["id"]: a["sourceStatus"] for a in progress["assets"]}
            self.assertEqual(status["Dragons/images/01_dragon"], "GLB_UPLOADED")
            self.assertEqual(status["Habitats/concepts/01_scene"], "CONCEPT_ONLY")
            self.assertEqual(status["Habitats/props_3d/01_statue"], "IMAGE_ONLY")
            self.assertEqual(status["NatureAndGameplay/foliage/01_tree"], "GLB_INVALID")
            self.assertEqual(status["NatureAndGameplay/foliage/02_unmatched"], "UNMATCHED_GLB")
            self.assertEqual(progress["groups"]["nature_gameplay"]["references"], 1)
            self.assertEqual(len(manifest["files"]), 8)
            self.assertEqual(len(progress["warnings"]), 2)
            main(root)
            before = [(src / x).read_bytes() for x in ("manifest.json", "progress.json", "PROGRESS.md")]
            main(root)
            self.assertEqual(before, [(src / x).read_bytes() for x in ("manifest.json", "progress.json", "PROGRESS.md")])
            glb(src / "NatureAndGameplay/foliage/01_tree.glb")
            self.assertEqual(scan(root)[1]["groups"]["nature_gameplay"]["glbUploaded"], 1)

    def test_truncated_glb(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "test.glb"
            glb(p)
            self.assertIsNone(glb_problem(p))
            p.write_bytes(p.read_bytes()[:-1])
            self.assertIsNotNone(glb_problem(p))


if __name__ == "__main__":
    unittest.main()
