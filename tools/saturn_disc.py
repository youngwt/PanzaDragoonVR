#!/usr/bin/env python3
"""List or extract the files on a Sega Saturn disc image (MODE1/2352 data track).

    python3 tools/saturn_disc.py list  <track1.bin>
    python3 tools/saturn_disc.py extract <track1.bin> <out_dir> [--with-video]
"""
import struct
import sys
from pathlib import Path

SECTOR_RAW = 2352
SECTOR_HEADER = 16
SECTOR_DATA = 2048


class SaturnDisc:
    def __init__(self, path):
        self.f = open(path, "rb")
        pvd = self.read_sectors(16)
        if pvd[1:6] != b"CD001":
            raise ValueError("no ISO9660 volume descriptor at sector 16")
        root = pvd[156:190]
        self.files = []
        self._walk(*struct.unpack("<I", root[2:6]), *struct.unpack("<I", root[10:14]), "")
        self.files.sort(key=lambda e: e[1])

    def read_sectors(self, lba, count=1):
        out = bytearray()
        for i in range(lba, lba + count):
            self.f.seek(i * SECTOR_RAW + SECTOR_HEADER)
            out += self.f.read(SECTOR_DATA)
        return bytes(out)

    def header(self):
        h = self.read_sectors(0)[:256]
        return {
            "hardware": h[0:16].decode("ascii").strip(),
            "maker": h[16:32].decode("ascii").strip(),
            "product": h[32:42].decode("ascii").strip(),
            "version": h[42:48].decode("ascii").strip(),
            "date": h[48:56].decode("ascii").strip(),
            "title": h[96:208].decode("ascii").strip(),
        }

    def _walk(self, lba, size, path):
        data = self.read_sectors(lba, (size + SECTOR_DATA - 1) // SECTOR_DATA)
        i = 0
        while i < len(data):
            length = data[i]
            if length == 0:
                i = (i // SECTOR_DATA + 1) * SECTOR_DATA
                continue
            rec = data[i:i + length]
            entry_lba, entry_size = struct.unpack("<I", rec[2:6])[0], struct.unpack("<I", rec[10:14])[0]
            name = rec[33:33 + rec[32]]
            if name not in (b"\x00", b"\x01"):
                text = name.decode("ascii", "replace").split(";")[0]
                if rec[25] & 2:
                    self._walk(entry_lba, entry_size, path + text + "/")
                else:
                    self.files.append((path + text, entry_lba, entry_size))
            i += length

    def read_file(self, lba, size):
        return self.read_sectors(lba, (size + SECTOR_DATA - 1) // SECTOR_DATA)[:size]


def main(argv):
    if len(argv) < 3 or argv[1] not in ("list", "extract"):
        print(__doc__)
        return 1
    disc = SaturnDisc(argv[2])
    if argv[1] == "list":
        for key, value in disc.header().items():
            print(f"{key:9}: {value}")
        for name, lba, size in disc.files:
            print(f"{name:28} {lba:7} {size:>11,}")
        return 0
    out = Path(argv[3])
    with_video = "--with-video" in argv
    count = 0
    for name, lba, size in disc.files:
        if name.endswith(".CPK") and not with_video:
            continue
        target = out / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(disc.read_file(lba, size))
        count += 1
    print(f"extracted {count} files to {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
