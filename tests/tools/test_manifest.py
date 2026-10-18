"""Tests for tools/manifest.py and tools/write_manifest.py. Run by tools/test.sh.

Every test works in a temporary folder with small files made here.
"""
import contextlib
import hashlib
import io
import json
import os
import struct
import sys
import tempfile
import unittest
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import make_test_model
import manifest
import write_manifest


def png_bytes(depth=8, colour=6, fill=0):
    """A 1 x 1 PNG with the given bit depth and colour type."""
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    header = struct.pack(">IIBBBBB", 1, 1, depth, colour, 0, 0, 0)
    pixels = zlib.compress(b"\x00" + bytes([fill]) * 8)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", pixels) + chunk(b"IEND", b"")


def glb_bytes(version=2):
    """A .glb header and nothing more: enough to fail on its version, never a usable model."""
    return b"glTF" + struct.pack("<II", version, 12)


def glb_of(gltf, binary=b""):
    """A .glb holding this glTF JSON, given as a dict or as JSON text, and binary chunk."""
    text = (gltf if isinstance(gltf, str) else json.dumps(gltf)).encode("utf-8")
    text += b" " * (-len(text) % 4)
    body = struct.pack("<II", len(text), 0x4E4F534A) + text
    if binary:
        body += struct.pack("<II", len(binary), 0x004E4942) + binary
    return struct.pack("<III", 0x46546C67, 2, 12 + len(body)) + body


def gltf(**changes):
    """A model the game accepts, with these top-level fields set."""
    document = {"asset": {"version": "2.0"}, "nodes": [{"mesh": 0}], "meshes": [{"primitives": []}]}
    document.update(changes)
    return document


class FolderCase(unittest.TestCase):
    def setUp(self):
        holder = tempfile.TemporaryDirectory()
        self.addCleanup(holder.cleanup)
        self.folder = holder.name

    def put(self, path, data):
        full = Path(self.folder, path)
        full.parent.mkdir(parents=True, exist_ok=True)
        full.write_bytes(data)
        return full

    def two_images(self):
        self.put("images/e03.floor.png", png_bytes(fill=1))
        self.put("images/e03.canopy.png", png_bytes(colour=2, fill=2))

    def run_command(self, *arguments):
        """Run the command. Returns (status, stdout, stderr)."""
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            status = write_manifest.main(["write_manifest.py", *arguments])
        return status, out.getvalue(), err.getvalue()

    def read_manifest(self):
        return json.loads(Path(self.folder, "manifest.json").read_text(encoding="utf-8"))

    def write_manifest(self, document):
        self.put("manifest.json", json.dumps(document).encode("utf-8"))

    def files(self):
        return sorted(str(p.relative_to(self.folder)) for p in Path(self.folder).rglob("*") if p.is_file())

    def assert_fails(self, *arguments, mentions=()):
        """The command fails with one line per problem and writes nothing."""
        before = self.files()
        status, _, err = self.run_command(self.folder, *arguments)
        self.assertNotEqual(status, 0)
        self.assertEqual(self.files(), before)
        lines = err.splitlines()
        self.assertTrue(lines)
        for text in mentions:
            self.assertTrue(any(text in line for line in lines), "%r not in %r" % (text, err))
        return lines


class Rules(unittest.TestCase):
    def test_valid_ids(self):
        for asset_id in ("e03.floor", "dragon", "a.b_c.9", "e03.flyer_2"):
            self.assertIsNone(manifest.id_problem(asset_id), asset_id)

    def test_invalid_ids(self):
        for asset_id in ("", "E03.Floor", "e03..floor", ".e03", "e03.", "e03-floor", "e03 floor", "e03.floor\n"):
            self.assertIsNotNone(manifest.id_problem(asset_id), asset_id)

    def test_valid_paths(self):
        for path in ("a.png", "images/e03.floor.png", "models/e03_flyer-2.glb"):
            self.assertIsNone(manifest.path_problem(path), path)

    def test_invalid_paths(self):
        for path in ("", "/a.png", "../a.png", "images/../a.png", "./a.png", "images\\a.png",
                     "Images/a.png", "images//a.png", "images/a b.png", "images/é.png"):
            self.assertIsNotNone(manifest.path_problem(path), path)

    def test_the_extraction_manifest_recorded_for_the_decoding_tests_is_this_writers_text(self):
        # The decoding library writes manifest.json itself; its test compares against this file,
        # so the two writers give the same bytes for the same document.
        recorded = Path(__file__).resolve().parents[1] / "Decoding.Tests" / "expected" / "fixed-disc-manifest.json"
        raw = recorded.read_bytes()
        document = json.loads(raw.decode("utf-8"))
        self.assertEqual(manifest.dumps(document), raw)
        self.assertEqual(list(document), ["format", "generator", "source", "assets"])
        self.assertEqual([list(a) for a in document["assets"]], [["id", "kind", "path", "bytes", "sha256"]] * 3)

    def test_kind_from_extension(self):
        self.assertEqual(manifest.kind_of("images/e03.floor.png"), "image")
        self.assertEqual(manifest.kind_of("models/e03.flyer.glb"), "model")
        self.assertIsNone(manifest.kind_of("notes.txt"))
        self.assertIsNone(manifest.kind_of("images/e03.floor.PNG"))


class Writing(FolderCase):
    def test_automatic(self):
        self.two_images()
        status, out, _ = self.run_command(self.folder)
        self.assertEqual(status, 0)
        raw = Path(self.folder, "manifest.json").read_bytes()
        self.assertFalse(raw.startswith(b"\xef\xbb\xbf"))
        document = json.loads(raw.decode("utf-8"))
        self.assertEqual(sorted(document), ["assets", "format", "generator", "source"])
        self.assertEqual(document["format"], 1)
        self.assertIsNone(document["source"])
        self.assertIsInstance(document["generator"], str)
        self.assertEqual([a["id"] for a in document["assets"]], ["e03.canopy", "e03.floor"])
        for asset in document["assets"]:
            data = Path(self.folder, asset["path"]).read_bytes()
            self.assertEqual(asset["path"], "images/%s.png" % asset["id"])
            self.assertEqual(asset["kind"], "image")
            self.assertEqual(asset["bytes"], len(data))
            self.assertEqual(asset["sha256"], hashlib.sha256(data).hexdigest())
        self.assertIn("2 assets", out)
        self.assertEqual(self.files(), ["images/e03.canopy.png", "images/e03.floor.png", "manifest.json"])

    def test_model_is_found(self):
        self.put("models/e03.flyer.glb", make_test_model.build())
        self.assertEqual(self.run_command(self.folder)[0], 0)
        self.assertEqual(self.read_manifest()["assets"][0]["kind"], "model")

    def test_explicit_pair(self):
        self.put("images/a.png", png_bytes())
        self.put("images/e03.canopy.png", png_bytes())
        status, _, err = self.run_command(self.folder, "e03.floor=images/a.png")
        self.assertEqual(status, 0)
        self.assertEqual(err, "")
        listed = {a["id"]: a["path"] for a in self.read_manifest()["assets"]}
        self.assertEqual(listed, {"e03.floor": "images/a.png", "e03.canopy": "images/e03.canopy.png"})

    def test_pair_renames_a_file_named_after_another_id(self):
        self.put("images/e03.floor.png", png_bytes())
        self.assertEqual(self.run_command(self.folder, "e03.canopy=images/e03.floor.png")[0], 0)
        listed = {a["id"]: a["path"] for a in self.read_manifest()["assets"]}
        self.assertEqual(listed, {"e03.canopy": "images/e03.floor.png"})

    def test_not_an_asset_is_left_out_with_a_notice(self):
        self.two_images()
        self.put("notes.txt", b"hello")
        self.put("images/My Photo.png", png_bytes())
        status, _, err = self.run_command(self.folder)
        self.assertEqual(status, 0)
        self.assertEqual(len(self.read_manifest()["assets"]), 2)
        lines = err.splitlines()
        self.assertEqual(len(lines), 2)
        self.assertTrue(any("notes.txt" in line for line in lines))
        self.assertTrue(any("images/My Photo.png" in line for line in lines))

    def test_nothing_found(self):
        self.put("notes.txt", b"hello")
        lines = self.assert_fails(mentions=["no assets were found"])
        self.assertEqual(len(lines), 1)

    def test_empty_folder(self):
        self.assert_fails(mentions=["no assets were found"])

    def test_same_id_from_two_files(self):
        self.put("images/e03.floor.png", png_bytes())
        self.put("other/e03.floor.png", png_bytes())
        lines = self.assert_fails(mentions=["e03.floor"])
        self.assertEqual(len(lines), 1)
        self.assertIn("images/e03.floor.png", lines[0])
        self.assertIn("other/e03.floor.png", lines[0])

    def test_pair_repeats_a_scanned_id(self):
        self.put("images/e03.floor.png", png_bytes())
        self.put("images/a.png", png_bytes())
        lines = self.assert_fails("e03.floor=images/a.png")
        self.assertEqual(len(lines), 1)
        for text in ("e03.floor", "images/a.png", "images/e03.floor.png"):
            self.assertIn(text, lines[0])

    def test_run_again_gives_the_same_manifest(self):
        self.two_images()
        self.assertEqual(self.run_command(self.folder)[0], 0)
        first = Path(self.folder, "manifest.json").read_bytes()
        status, _, err = self.run_command(self.folder)
        self.assertEqual(status, 0)
        self.assertEqual(err, "")
        self.assertEqual(Path(self.folder, "manifest.json").read_bytes(), first)

    def test_bad_id_in_a_pair(self):
        self.put("images/a.png", png_bytes())
        for asset_id in ("E03.Floor", "e03..floor"):
            lines = self.assert_fails(asset_id + "=images/a.png", mentions=[asset_id])
            self.assertEqual(len(lines), 1)
            self.assertIn("lowercase", lines[0])

    def test_bad_path_in_a_pair(self):
        outside = self.put("outside.png", png_bytes())
        self.folder = os.path.join(self.folder, "assets")  # so "outside" is still in the temporary folder
        self.two_images()
        for path in (str(outside), "../" + outside.name, "images/../images/e03.floor.png", "images\\e03.floor.png"):
            self.assert_fails("e03.sky=" + path, mentions=[path])

    def test_pair_through_a_link_that_leaves_the_folder(self):
        holder = tempfile.TemporaryDirectory()
        self.addCleanup(holder.cleanup)
        Path(holder.name, "a.png").write_bytes(png_bytes())
        os.symlink(holder.name, os.path.join(self.folder, "link"))
        self.assert_fails("e03.floor=link/a.png", mentions=["link/a.png", "outside"])

    def test_pair_through_a_linked_folder_lists_the_file_once(self):
        self.put("images/e03.floor.png", png_bytes())
        os.symlink("images", os.path.join(self.folder, "link"))
        self.assertEqual(self.run_command(self.folder, "e03.sky=link/e03.floor.png")[0], 0)
        listed = {a["id"]: a["path"] for a in self.read_manifest()["assets"]}
        self.assertEqual(listed, {"e03.sky": "link/e03.floor.png"})

    @unittest.skipIf(os.geteuid() == 0, "root can read any folder")
    def test_folder_that_cannot_be_listed(self):
        self.two_images()
        locked = os.path.join(self.folder, "locked")
        os.mkdir(locked)
        os.chmod(locked, 0)
        self.addCleanup(os.chmod, locked, 0o700)
        lines = self.assert_fails(mentions=["locked"])
        self.assertEqual(len(lines), 1)

    def test_manifest_gets_an_ordinary_files_mode(self):
        self.two_images()
        self.addCleanup(os.umask, os.umask(0o022))
        self.assertEqual(self.run_command(self.folder)[0], 0)
        self.assertEqual(os.stat(os.path.join(self.folder, "manifest.json")).st_mode & 0o777, 0o644)

    def test_pair_with_an_empty_side(self):
        self.two_images()
        for argument in ("=images/e03.floor.png", "e03.sky="):
            lines = self.assert_fails(argument, mentions=[argument, "ID=path"])
            self.assertEqual(len(lines), 1)

    def test_help(self):
        for option in ("-h", "--help"):
            status, out, err = self.run_command(option)
            self.assertEqual(status, 0)
            self.assertIn("write_manifest.py --check <folder>", out)
            self.assertEqual(err, "")

    def test_unknown_option_is_an_error(self):
        status, out, err = self.run_command("--verbose", self.folder)
        self.assertNotEqual(status, 0)
        self.assertEqual(out, "")
        self.assertIn("write_manifest.py", err)

    def test_scanned_path_breaks_the_path_rules(self):
        self.put("Images/e03.floor.png", png_bytes())
        self.assert_fails(mentions=["Images/e03.floor.png", "lowercase"])

    def test_missing_file(self):
        self.two_images()
        self.assert_fails("e03.sky=images/absent.png", mentions=["images/absent.png"])

    def test_path_is_a_folder(self):
        self.two_images()
        self.assert_fails("e03.sky=images", mentions=["images", "not a file"])

    def test_folder_does_not_exist(self):
        status, _, err = self.run_command(os.path.join(self.folder, "absent"))
        self.assertNotEqual(status, 0)
        self.assertIn("absent", err)

    def test_unknown_kind_in_a_pair(self):
        self.two_images()
        self.put("sounds/roar.wav", b"RIFF")
        lines = self.assert_fails("dragon.roar=sounds/roar.wav", mentions=["sounds/roar.wav"])
        self.assertIn(".png", lines[0])
        self.assertIn(".glb", lines[0])

    def test_png_that_is_not_a_png(self):
        self.put("images/e03.floor.png", b"GIF89a, not a PNG at all, padded out a bit")
        self.assert_fails(mentions=["images/e03.floor.png", "not a PNG"])

    def test_empty_png(self):
        self.put("images/e03.floor.png", b"")
        self.assert_fails(mentions=["images/e03.floor.png", "not a PNG"])

    def test_png_of_the_wrong_sort(self):
        for depth, colour, found in ((16, 6, "16-bit"), (8, 3, "palette"), (8, 0, "greyscale")):
            self.put("images/e03.floor.png", png_bytes(depth, colour))
            self.assert_fails(mentions=["images/e03.floor.png", found])

    def test_glb_without_the_header(self):
        self.put("models/e03.flyer.glb", b'{"asset": {"version": "2.0"}}')
        self.assert_fails(mentions=["models/e03.flyer.glb", "glTF"])

    def test_glb_of_another_version(self):
        self.put("models/e03.flyer.glb", glb_bytes(version=1))
        self.assert_fails(mentions=["models/e03.flyer.glb", "version 1"])

    def test_model_the_game_rejects(self):
        # The game's whole invalid-model table applies, not only the header; more in test_asset_rules.py.
        self.put("models/e03.flyer.glb", glb_of(gltf(extensionsRequired=["EXT_x"])))
        lines = self.assert_fails()
        self.assertEqual(lines, ["write_manifest: error: e03.flyer: models/e03.flyer.glb: "
                                 "unsupported model: it requires the extension EXT_x"])

    def test_every_problem_is_reported(self):
        self.put("images/e03.floor.png", b"nope")
        lines = self.assert_fails("E03=images/absent.png", "e03.sky=/etc/passwd")
        self.assertEqual(len(lines), 3)

    def test_an_error_leaves_the_old_manifest_alone(self):
        self.two_images()
        self.assertEqual(self.run_command(self.folder)[0], 0)
        before = Path(self.folder, "manifest.json").read_bytes()
        self.put("images/e03.floor.png", b"no longer a PNG")
        self.assert_fails()
        self.assertEqual(Path(self.folder, "manifest.json").read_bytes(), before)

    def test_a_failed_write_leaves_no_partial_file(self):
        self.two_images()
        self.put("manifest.json", b"old")
        real_replace = os.replace

        def refuse(source, target):
            raise OSError(28, "No space left on device", source, None, target)
        os.replace = refuse
        self.addCleanup(setattr, os, "replace", real_replace)
        lines = self.assert_fails(mentions=["No space left", os.path.join(self.folder, "manifest.json")])
        self.assertNotIn(".tmp", lines[0])
        self.assertEqual(Path(self.folder, "manifest.json").read_bytes(), b"old")

    def test_argument_that_is_not_a_pair(self):
        self.two_images()
        self.assert_fails("images/e03.floor.png", mentions=["ID=path"])

    def test_no_arguments_prints_usage(self):
        status, _, err = self.run_command()
        self.assertNotEqual(status, 0)
        self.assertIn("write_manifest.py", err)

    def test_build_returns_the_manifest_without_writing(self):
        self.two_images()
        built = manifest.build(self.folder)
        self.assertEqual(len(built["assets"]), 2)
        self.assertNotIn("manifest.json", self.files())


class Checking(FolderCase):
    def setUp(self):
        super().setUp()
        self.two_images()
        self.assertEqual(self.run_command(self.folder)[0], 0)

    def check_fails(self, *mentions, count=1):
        before = {name: Path(self.folder, name).read_bytes() for name in self.files()}
        status, _, err = self.run_command("--check", self.folder)
        self.assertNotEqual(status, 0)
        self.assertEqual({name: Path(self.folder, name).read_bytes() for name in self.files()}, before)
        lines = err.splitlines()
        self.assertEqual(len(lines), count, err)
        for text in mentions:
            self.assertIn(text, err)
        self.assertEqual(manifest.check(self.folder), [line.split("error: ", 1)[1] for line in lines])
        return lines

    def test_all_good(self):
        status, out, err = self.run_command("--check", self.folder)
        self.assertEqual(status, 0)
        self.assertEqual(err, "")
        self.assertIn("2 assets checked", out)
        self.assertEqual(manifest.check(self.folder), [])

    def test_unlisted_files_are_ignored(self):
        self.put("notes.txt", b"hello")
        self.assertEqual(manifest.check(self.folder), [])

    def test_hand_written_manifest_without_checksums(self):
        document = self.read_manifest()
        document["generator"] = "hand-written"
        for asset in document["assets"]:
            del asset["sha256"]
        self.write_manifest(document)
        self.assertEqual(manifest.check(self.folder), [])

    def test_empty_assets_is_a_problem(self):
        document = self.read_manifest()
        document["assets"] = []
        self.write_manifest(document)
        lines = self.check_fails(count=1)
        self.assertEqual(lines, ["write_manifest: error: manifest.json: lists no assets"])

    def test_unsupported_format_with_empty_assets_is_the_unsupported_format(self):
        document = self.read_manifest()
        document["format"] = 2
        document["assets"] = []
        self.write_manifest(document)
        lines = self.check_fails("unsupported format 2", count=1)
        self.assertNotIn("lists no assets", lines[0])

    def test_missing_file(self):
        os.unlink(os.path.join(self.folder, "images/e03.floor.png"))
        self.check_fails("e03.floor", "images/e03.floor.png", "missing")

    def test_path_is_a_folder(self):
        os.unlink(os.path.join(self.folder, "images/e03.floor.png"))
        os.mkdir(os.path.join(self.folder, "images/e03.floor.png"))
        self.check_fails("e03.floor", "images/e03.floor.png", "not a file")

    def test_replaced_by_a_file_of_another_size(self):
        self.put("images/e03.floor.png", png_bytes() + b"extra")
        lines = self.check_fails("e03.floor", "images/e03.floor.png", "incomplete or changed", count=1)
        self.assertTrue(all("e03.canopy" not in line for line in lines))

    def test_wrong_checksum(self):
        self.put("images/e03.floor.png", png_bytes(fill=9))
        document = self.read_manifest()
        sizes = {a["id"]: a["bytes"] for a in document["assets"]}
        self.assertEqual(sizes["e03.floor"], os.path.getsize(os.path.join(self.folder, "images/e03.floor.png")))
        self.check_fails("e03.floor", "images/e03.floor.png", "wrong checksum")

    def test_duplicate_id(self):
        document = self.read_manifest()
        document["assets"].append(dict(document["assets"][0]))
        self.write_manifest(document)
        self.check_fails("duplicate ID", document["assets"][0]["id"])

    def test_duplicate_id_when_one_entry_is_incomplete(self):
        document = self.read_manifest()
        copy = dict(document["assets"][0])
        del copy["bytes"]
        document["assets"].append(copy)
        self.write_manifest(document)
        # As in the game, the duplicate is reported and both entries dropped, whatever else is wrong.
        self.check_fails("duplicate ID", copy["id"], count=1)
        document["assets"][2] = {"id": copy["id"]}
        self.write_manifest(document)
        self.check_fails("duplicate ID", "entry 3", count=1)

    def test_no_manifest(self):
        os.unlink(os.path.join(self.folder, "manifest.json"))
        self.check_fails("manifest.json")

    def test_manifest_is_not_json(self):
        self.put("manifest.json", b"{ not json")
        self.check_fails("manifest.json", "not valid JSON")

    def test_manifest_nested_too_deeply(self):
        self.put("manifest.json", b"[" * 200000)
        self.check_fails("manifest.json", "not valid JSON")

    def test_manifest_has_a_byte_order_mark(self):
        raw = Path(self.folder, "manifest.json").read_bytes()
        self.put("manifest.json", b"\xef\xbb\xbf" + raw)
        self.check_fails("byte-order mark")

    def test_manifest_is_not_an_object(self):
        self.put("manifest.json", b"[]")
        self.check_fails("manifest.json")

    def test_unsupported_format(self):
        document = self.read_manifest()
        document["format"] = 2
        self.write_manifest(document)
        self.check_fails("unsupported format 2")

    def test_top_level_fields_absent_or_of_the_wrong_type(self):
        document = self.read_manifest()
        del document["generator"]
        document["source"] = "disc"
        document["format"] = True
        self.write_manifest(document)
        # One line, as the game gives one reason the manifest cannot be read.
        self.check_fails("'generator'", "'source'", "'format'", count=1)

    def test_entry_fields_absent_or_of_the_wrong_type(self):
        document = self.read_manifest()
        del document["assets"][0]["bytes"]
        document["assets"][1]["bytes"] = "12"
        document["assets"].append("e03.flyer")
        self.write_manifest(document)
        self.check_fails("e03.canopy", "e03.floor", "entry 3", count=3)

    def test_invalid_entries(self):
        document = self.read_manifest()
        canopy, floor = document["assets"]
        canopy["path"] = "../" + os.path.basename(self.folder) + "/images/e03.canopy.png"
        floor["bytes"] = -1
        floor["sha256"] = "ABC"
        self.write_manifest(document)
        self.check_fails("e03.canopy", "invalid entry: invalid path", "invalid entry: 'bytes' is negative", "sha256", count=3)

    def test_invalid_id(self):
        document = self.read_manifest()
        document["assets"][0]["id"] = "E03.Canopy"
        self.write_manifest(document)
        self.check_fails("E03.Canopy: invalid entry: not a valid ID")

    def test_unknown_kind_is_a_notice_not_a_problem(self):
        document = self.read_manifest()
        document["assets"][1]["kind"] = "sound"
        self.write_manifest(document)
        status, out, err = self.run_command("--check", self.folder)
        self.assertEqual(status, 0)
        self.assertIn("2 assets checked", out)
        lines = err.splitlines()
        self.assertEqual(len(lines), 1)
        for text in ("notice", "e03.floor", "images/e03.floor.png", "unknown kind 'sound'"):
            self.assertIn(text, lines[0])
        self.assertEqual(manifest.check(self.folder), [])

    def test_unknown_kind_still_has_its_file_checked(self):
        document = self.read_manifest()
        document["assets"][1]["kind"] = "sound"
        self.write_manifest(document)
        size = os.path.getsize(os.path.join(self.folder, "images/e03.floor.png"))
        self.put("images/e03.floor.png", b"RIFF, and a different size")
        status, _, err = self.run_command("--check", self.folder)
        self.assertNotEqual(status, 0)
        problems = manifest.check(self.folder)
        self.assertEqual(len(problems), 1)
        self.assertIn("incomplete or changed", problems[0])
        self.assertIn("notice", err)
        self.put("images/e03.floor.png", b"R" * size)
        self.assertEqual(manifest.check(self.folder), [
            "e03.floor: images/e03.floor.png: wrong checksum: the file's sha256 differs from the manifest's"])
        os.unlink(os.path.join(self.folder, "images/e03.floor.png"))
        self.assertIn("missing", manifest.check(self.folder)[0])
        document["assets"][1]["path"] = "../x.wav"
        self.write_manifest(document)
        self.assertIn("invalid path", manifest.check(self.folder)[0])

    def test_wrong_contents(self):
        size = os.path.getsize(os.path.join(self.folder, "images/e03.floor.png"))
        self.put("images/e03.floor.png", glb_bytes().ljust(size, b"\0"))
        self.check_fails("e03.floor", "wrong checksum", "unreadable image: not a PNG", count=2)

    def test_wrong_size_is_reported_alone(self):
        # As in the game, a file of the wrong size is not looked into further.
        self.put("images/e03.floor.png", glb_bytes())
        self.check_fails("e03.floor: images/e03.floor.png: incomplete or changed", count=1)

    def test_every_problem_is_listed(self):
        os.unlink(os.path.join(self.folder, "images/e03.floor.png"))
        self.put("images/e03.canopy.png", b"short")
        self.check_fails("e03.floor", "e03.canopy", "missing", "incomplete or changed", count=2)

    def test_entry_problems_come_before_file_problems(self):
        os.unlink(os.path.join(self.folder, "images/e03.floor.png"))
        document = self.read_manifest()
        document["assets"].append({"id": "e03.sky", "kind": "image", "path": "../sky.png", "bytes": 1})
        self.write_manifest(document)
        lines = self.check_fails(count=2)
        self.assertIn("e03.sky: invalid entry", lines[0])
        self.assertIn("e03.floor: images/e03.floor.png: missing", lines[1])

    def test_manifest_with_nan_or_infinity(self):
        raw = Path(self.folder, "manifest.json").read_bytes()
        for constant in (b"NaN", b"Infinity", b"-Infinity"):
            self.put("manifest.json", raw.replace(b'"source": null', b'"source": null, "x": ' + constant))
            self.check_fails("manifest.json: is not valid JSON")

    def test_manifest_nested_65_deep(self):
        def nested(levels):
            return "[" * levels + "]" * levels
        document = self.read_manifest()
        # The manifest, 'assets', the entry and 'meta' make four levels.
        document["assets"][0]["meta"] = {"a": json.loads(nested(60))}
        self.write_manifest(document)
        self.assertEqual(manifest.check(self.folder), [])
        document["assets"][0]["meta"] = {"a": json.loads(nested(61))}
        self.write_manifest(document)
        self.check_fails("manifest.json: is not valid JSON", "64")

    def test_lone_surrogate(self):
        raw = Path(self.folder, "manifest.json").read_bytes()
        self.put("manifest.json", raw.replace(b'"id": "e03.canopy"', b'"id": "e03.\\ud800"'))
        self.check_fails("entry 1: invalid entry: field 'id' is not readable text")
        self.put("manifest.json", raw.replace(b'"path": "images/e03.canopy.png"', b'"path": "\\udfff.png"'))
        self.check_fails("e03.canopy: invalid entry: field 'path' is not readable text")
        self.put("manifest.json", raw.replace(b'"kind": "image"', b'"kind": "\\ud800"', 1))
        status, _, err = self.run_command("--check", self.folder)
        self.assertEqual(status, 0)
        self.assertIn("unknown kind", err)

    def test_check_takes_exactly_one_folder(self):
        status, _, err = self.run_command("--check")
        self.assertNotEqual(status, 0)
        self.assertIn("--check <folder>", err)


class GlbProblem(unittest.TestCase):
    """The game's invalid-model table, as GlbCheckTests.cs has it, in tools/manifest.py."""

    def problem(self, document, binary=b""):
        return manifest.glb_problem(glb_of(document, binary))

    def test_the_test_model_is_usable(self):
        self.assertIsNone(manifest.glb_problem(make_test_model.build()))

    def test_conventions_are_not_policed(self):
        materials = [{"name": "shiny", "alphaMode": "BLEND"}]
        self.assertIsNone(self.problem(gltf(materials=materials, extensionsUsed=["KHR_x"], extensionsRequired=[])))

    def test_header(self):
        self.assertEqual(manifest.glb_problem(b"glTF"), "unreadable model: not a .glb file: no glTF header")
        self.assertEqual(manifest.glb_problem(glb_bytes()), "unreadable model: the file has no JSON chunk")
        data = glb_of(gltf())
        self.assertIn("the header gives a length of", manifest.glb_problem(data[:-4]))
        wrong = data[:16] + b"BIN\0" + data[20:]
        self.assertEqual(manifest.glb_problem(wrong), "unreadable model: the first chunk is not JSON")
        past = data[:12] + struct.pack("<I", len(data)) + data[16:]
        self.assertEqual(manifest.glb_problem(past), "unreadable model: the JSON chunk runs past the end of the file")

    def test_json_chunk(self):
        self.assertEqual(self.problem("{ not json"), "unreadable model: the JSON chunk is not valid JSON")
        self.assertEqual(self.problem('{"asset": {"version": "2.0"}, "x": NaN}'), "unreadable model: the JSON chunk is not valid JSON")
        self.assertEqual(self.problem("[]"), "unreadable model: the JSON chunk is not one JSON object")
        self.assertEqual(self.problem({"meshes": []}), "unreadable model: it has no 'asset' object with a 'version'")
        self.assertEqual(self.problem(gltf(asset={"version": "1.0"})), "unreadable model: glTF version '1.0'; it must be 2.0")

    def test_lone_surrogate_cannot_be_read(self):
        for extra in ('"asset": {"version": "\\ud800"}', '"extensionsRequired": ["\\ud800"]', '"images": [{"uri": "\\ud800.png"}]'):
            text = '{"asset": {"version": "2.0"}, "nodes": [{"mesh": 0}], "meshes": [{}], %s}' % extra
            self.assertEqual(self.problem(text), "unreadable model: the JSON chunk could not be read", extra)

    def test_buffers(self):
        self.assertEqual(self.problem(gltf(buffers=[{"byteLength": 4}])),
                         "unreadable model: buffer 0 is the file's binary chunk, but the file has none")
        self.assertEqual(self.problem(gltf(buffers=[{"byteLength": 4096}]), b"\0" * 4),
                         "unreadable model: buffer 0 is 4096 bytes but the binary chunk holds 4")
        self.assertEqual(self.problem(gltf(buffers=[{"byteLength": 4}, {"byteLength": 4}]), b"\0" * 4),
                         "unreadable model: buffer 1 has no uri, and only buffer 0 can be the file's binary chunk")
        for length in (None, -1, 1.5):
            buffer = {} if length is None else {"byteLength": length}
            self.assertEqual(self.problem(gltf(buffers=[buffer]), b"\0" * 4),
                             "unreadable model: buffer 0 has no valid 'byteLength'", length)
        self.assertEqual(self.problem(gltf(buffers=[{"byteLength": "4"}]), b"\0" * 4),
                         "unreadable model: the JSON chunk could not be read")
        self.assertIsNone(self.problem(gltf(buffers=[{"byteLength": 4}]), b"\0" * 4))

    def test_references_outside(self):
        self.assertEqual(self.problem(gltf(buffers=[{"uri": "data:application/octet-stream;base64,AAAA"}])),
                         "not self-contained: buffer 0 has a data: URI")
        self.assertEqual(self.problem(gltf(images=[{"bufferView": 0}, {"uri": "../../../secret.png"}])),
                         "not self-contained: image 1 names the file '../../../secret.png', which was not opened")
        self.assertEqual(self.problem(gltf(images=[{"uri": 3}])),
                         "not self-contained: image 0 has a uri; nothing it names was opened")
        long_name = self.problem(gltf(buffers=[{"uri": "a" * 200 + ".bin"}]))
        self.assertEqual(long_name, "not self-contained: buffer 0 names the file '%s...', which was not opened" % ("a" * 57))
        self.assertIn("'a b'", self.problem(gltf(images=[{"uri": "a\nb"}])))
        # Found before a required extension.
        self.assertIn("not self-contained", self.problem(gltf(images=[{"uri": "skin.png"}], extensionsRequired=["KHR_x"])))

    def test_required_extensions(self):
        self.assertEqual(self.problem(gltf(extensionsRequired=["KHR_materials_unlit", "EXT_meshopt_compression"])),
                         "unsupported model: it requires the extensions KHR_materials_unlit, EXT_meshopt_compression")

    def test_empty(self):
        self.assertEqual(self.problem(gltf(meshes=[])), "empty model: it has no mesh")
        self.assertEqual(self.problem({"asset": {"version": "2.0"}}), "empty model: it has no mesh")
        self.assertEqual(self.problem(gltf(nodes=[{"name": "root"}])), "empty model: no node uses any of its meshes")


if __name__ == "__main__":
    unittest.main()
