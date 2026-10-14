"""Tests for tools/make_test_model.py. Run by tools/test.sh."""
import json
import struct
import sys
import tempfile
import unittest
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import make_test_model
import manifest


def chunks(data):
    """The JSON object and the binary chunk of a .glb."""
    magic, version, length = struct.unpack_from("<III", data, 0)
    assert (magic, version, length) == (0x46546C67, 2, len(data))
    json_length, json_type = struct.unpack_from("<II", data, 12)
    assert json_type == 0x4E4F534A
    gltf = json.loads(data[20:20 + json_length])
    bin_length, bin_type = struct.unpack_from("<II", data, 20 + json_length)
    assert bin_type == 0x004E4942
    start = 28 + json_length
    return gltf, data[start:start + bin_length]


class MakeTestModelTest(unittest.TestCase):
    def setUp(self):
        self.data = make_test_model.build()
        self.gltf, self.binary = chunks(self.data)

    def test_is_a_glb_the_manifest_writer_accepts(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "e03.flyer.glb"
            self.assertEqual(make_test_model.main([str(path)]), 0)
            self.assertEqual(path.read_bytes(), self.data)
            self.assertIsNone(manifest.contents_problem(str(path), "model"))

    def test_is_self_contained(self):
        self.assertNotIn("uri", json.dumps(self.gltf))
        self.assertEqual(len(self.gltf["buffers"]), 1)
        self.assertEqual(self.gltf["buffers"][0]["byteLength"], len(self.binary))
        self.assertNotIn("extensionsRequired", self.gltf)

    def test_every_view_and_accessor_fits_the_binary_chunk(self):
        sizes = {5126: 4, 5123: 2}
        widths = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}
        for view in self.gltf["bufferViews"]:
            self.assertLessEqual(view["byteOffset"] + view["byteLength"], len(self.binary))
        for accessor in self.gltf["accessors"]:
            view = self.gltf["bufferViews"][accessor["bufferView"]]
            needed = accessor["count"] * sizes[accessor["componentType"]] * widths[accessor["type"]]
            self.assertEqual(needed, view["byteLength"])

    def test_indices_address_existing_vertices(self):
        mesh = self.gltf["meshes"][0]["primitives"][0]
        index = self.gltf["accessors"][mesh["indices"]]
        view = self.gltf["bufferViews"][index["bufferView"]]
        raw = self.binary[view["byteOffset"]:view["byteOffset"] + view["byteLength"]]
        indices = struct.unpack("<%dH" % index["count"], raw)
        count = self.gltf["accessors"][mesh["attributes"]["POSITION"]]["count"]
        self.assertEqual(len(indices) % 3, 0)
        self.assertTrue(all(i < count for i in indices))

    def test_embeds_a_16_by_16_rgba_png_that_decodes(self):
        image = self.gltf["images"][0]
        self.assertEqual(image["mimeType"], "image/png")
        view = self.gltf["bufferViews"][image["bufferView"]]
        png = self.binary[view["byteOffset"]:view["byteOffset"] + view["byteLength"]]
        self.assertEqual(png[:8], b"\x89PNG\r\n\x1a\n")
        width, height, depth, colour = struct.unpack(">IIBB", png[16:26])
        self.assertEqual((width, height, depth, colour), (16, 16, 8, 6))
        idat_length = struct.unpack(">I", png[33:37])[0]
        self.assertEqual(len(zlib.decompress(png[41:41 + idat_length])), 16 * (1 + 16 * 4))

    def test_carries_vertex_colours_for_the_game_to_ignore(self):
        self.assertIn("COLOR_0", self.gltf["meshes"][0]["primitives"][0]["attributes"])

    def test_follows_the_contract_conventions(self):
        self.assertEqual(self.gltf["nodes"][0]["name"], "flyer")
        material = self.gltf["materials"][0]
        self.assertEqual(material["name"], "opaque")
        self.assertTrue(material["doubleSided"])
        self.assertIn("KHR_materials_unlit", material["extensions"])
        self.assertEqual(self.gltf["samplers"][0], {"magFilter": 9728, "minFilter": 9728, "wrapS": 33071, "wrapT": 33071})

    def test_refuses_a_name_not_ending_in_glb(self):
        with tempfile.TemporaryDirectory() as folder:
            self.assertEqual(make_test_model.main([str(Path(folder) / "model.gltf")]), 1)


if __name__ == "__main__":
    unittest.main()
