"""The LZ77 unpacker in tools/plane_probe.py, on streams built by hand (no disc data)."""
import contextlib
import io
import struct
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import plane_probe  # noqa: E402


class UnpackTests(unittest.TestCase):
    def test_literals_short_copy_and_long_copy(self):
        # Flags 0x93, least significant bit first: literal, literal, short copy (length 2 + 2,
        # offset -2), long copy (word 0xFFD3: offset -6, length 3 + 2); then flags 0x02: end.
        stream = bytes([0x93, 0x41, 0x42, 0xFE, 0xD3, 0xFF, 0x02, 0x00, 0x00])
        self.assertEqual(plane_probe.unpack(stream, 0), (b"ABABABABABA", 9))

    def test_long_copy_with_length_byte(self):
        # Flags 0x15: literal, long copy (word 0xFFF8: offset -1, length in the next byte, 9 + 1),
        # then the end word.
        stream = bytes([0x15, 0x5A, 0xF8, 0xFF, 0x09, 0x00, 0x00])
        self.assertEqual(plane_probe.unpack(stream, 0), (b"Z" * 11, 7))

    def test_unpack_starts_at_the_given_offset(self):
        stream = b"\xFF\xFF" + bytes([0x93, 0x41, 0x42, 0xFE, 0xD3, 0xFF, 0x02, 0x00, 0x00])
        self.assertEqual(plane_probe.unpack(stream, 2)[0], b"ABABABABABA")

    def test_copy_from_before_the_stream_is_an_error(self):
        stream = bytes([0x00, 0xFE])
        with self.assertRaises(ValueError):
            plane_probe.unpack(stream, 0)


def make_prg(map_pointer):
    """A small EPISODE3.PRG with the set-up literal pool at 0x100 and a map list at 0x200."""
    prg = bytearray(0x300)
    load = plane_probe.LOAD_ADDRESS
    struct.pack_into(">2I", prg, 0x100 - 0x3C, 0x25F00C00, load + 0x240)
    struct.pack_into(">2I", prg, 0x100 - 0x30, 0x25F00E00, load + 0x280)
    struct.pack_into(">5I", prg, 0x100, 0x25E40000, load + 0x180, 0, 0x25E60000, load + 0x200)
    struct.pack_into(">2I", prg, 0x200, map_pointer, 0)
    return bytes(prg)


class FindSourcesTests(unittest.TestCase):
    def test_sources_come_from_the_literal_pool(self):
        prg = make_prg(plane_probe.LOAD_ADDRESS + 0x1C0)
        self.assertEqual(plane_probe.find_sources(prg),
                         (0x180, [0x1C0], [(0x25F00C00, 256, 0x240), (0x25F00E00, 128, 0x280)]))

    def test_map_pointer_outside_the_file_is_an_error(self):
        with self.assertRaisesRegex(ValueError, "map list entry 0x06100000"):
            plane_probe.find_sources(make_prg(0x06100000))


class MainTests(unittest.TestCase):
    def test_help_prints_the_usage_and_exits_0(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            self.assertEqual(plane_probe.main(["plane_probe.py", "-h"]), 0)
        self.assertIn("plane_probe.py [-h]", out.getvalue())

    def test_wrong_argument_count_is_one_error_line(self):
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            self.assertEqual(plane_probe.main(["plane_probe.py", "disc.bin"]), 1)
        self.assertTrue(err.getvalue().startswith("plane_probe: error: usage:"))
        self.assertEqual(err.getvalue().count("\n"), 1)


if __name__ == "__main__":
    unittest.main()
