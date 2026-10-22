# Dev diary 3: finding the missing colours

*17 October 2026. Milestone 1, in-game extraction.*

## Short version

Since day one, almost half of Zwei's textures have come off the disc in grey. Their colours weren't in their own files. Now I know where they are: tucked inside the Episode 3 program file, in a block the game copies into the Saturn's colour memory when the level loads. Coloured from the disc alone, the forest's leaves, vines and mossy stone come out as they should. No save state needed, which matters, because the finished game will only ever have your disc to work with.

## The grey half

A Saturn texture doesn't carry its own description. The pixels sit in one file (`.CGB`) with no header at all. The size, the position and how to colour each texture are in the matching model file (`.MDB`), as four 16-bit words per polygon. They're in exactly the layout the Saturn's sprite chip, VDP1, reads: a drawing mode, a colour word, a pixel address and a size.

The drawing mode says how the pixels turn into colours. For about half the textures that was easy, because the colours are in the file. For the other half the colour word didn't point into the file at all, so I decoded them as shades of grey and moved on. That was 4,033 textures across the disc, and 161 of the 420 in Episode 3, much of the forest.

## A hunch from a save state

The Saturn has two graphics chips. VDP1 draws the sprites and polygons. VDP2 draws the backgrounds and does the final mixing, and it owns a small colour memory (colour RAM): 2,048 colours, each 15-bit RGB. My guess was that the grey textures don't name colours at all. They name positions in VDP2's colour RAM, and VDP2 fills in the colour when the frame is put together.

I had a save state from the emulator, taken in the middle of Episode 3, and a save state holds a copy of colour RAM. So the first test was cheap. For each grey texture I took its colour word, used it to pick a block of 256 colours in that copy, and looked each pixel up there.

The result was forest: leaves in a dozen greens, vines, tree trunks, bark. Pixels that are meant to be see-through came out see-through. The rule is short. The colour word picks a 256-colour block, and the pixel value picks the colour inside it.

But a save state can't ship. It's a snapshot of my own emulator session, not something a player has. The question was whether those colours are on the disc somewhere.

## Searching the disc for colours

If the game copies colours into colour RAM, they have to come from somewhere. The simplest possibility was that they're stored exactly as they end up in colour RAM, so the next test was a plain search. I took each 256-colour block from the save state and searched every file on the disc for those exact bytes, in both byte orders, with and without the Saturn's unused top bit.

Every block turned up, all in the same file, `EPISODE3.PRG`, the program for the forest level. The first 1,024 colours of colour RAM appear in it byte for byte, in four consecutive blocks. A run that long doesn't match by chance.

Then I looked at what in the program points at those blocks. Two places do. Each is a short table of pairs: "copy into colour RAM at this position, from this address in the program". Five pairs per table, 256 colours per copy. The level reads one of these tables when it loads and uploads the colours. Recolouring the textures from the disc's copy, with no save state at all, gives an identical image.

## Two tables, and some shadows

There are two tables, and they almost agree. Three of their five blocks are the same. The other two, which a group of stone textures in the `E03END` file use, differ: one table makes the stone grey-green and mossy, the other greener with red specks. My save state matches the first, and it's the one that looks right. Which part of Episode 3 uses the other table is still a mystery; that needs the game's code traced. For now the extractor uses the one the game showed me.

One more surprise. A handful of textures use a different mode, 16 colours from a colour bank, and in the boss fight they point at a block that is one transparent entry followed by fifteen copies of the same very dark colour. Those textures are shadows: solid dark shapes for the boss to cast on the ground.

## The texture modes, in one place

VDP1 has six ways of turning a texture's pixels into colours, numbered 0 to 5. Zwei uses four of them. Here is each, with how many of the disc's 9,020 textures use it.

| Mode | Bits per pixel | Where the colours come from | Textures on the disc |
|------|----------------|-----------------------------|----------------------|
| 0 | 4 (16 colours) | Colour RAM, through a colour bank | 100 records found, most likely false; the real ones are shadows |
| 1 | 4 (16 colours) | A 16-entry colour table inside the texture file | 4,873 |
| 2 | 8 (64 colours) | Colour RAM, through a colour bank | none |
| 3 | 8 (128 colours) | Colour RAM, through a colour bank | none |
| 4 | 8 (256 colours) | Colour RAM, through a colour bank | 4,033 |
| 5 | 16 | The pixel is its own colour (15-bit RGB) | 13 |

In more detail:

- **Mode 1, the colour table.** Each pixel is half a byte, a number from 0 to 15. The colour word points at a table of 16 colours stored in the same file, and the pixel picks one. This was the easy half from the start: everything needed is in the file. Different polygons can point the same pixels at different tables and get differently coloured copies of one image.
- **Mode 4, 256 colours from colour RAM.** Each pixel is a whole byte, 0 to 255. The colour word picks which block of 256 in colour RAM to use, and the pixel picks the colour in it. Those blocks come from the program file's upload table. This was the grey half until today.
- **Mode 0, 16 colours from colour RAM.** It works like mode 4 with half-byte pixels, so the colour word can pick a block of only 16 colours. In Episode 3 the clear examples are the boss's shadows.
- **Mode 5, true colour.** Each pixel is a 16-bit word holding its own red, green and blue, 5 bits each, so there's nothing to look up. There are very few of these.
- **Modes 2 and 3** are the 64- and 128-colour versions of mode 4. None of Zwei's textures use them.

Two details apply across the modes. In the paletted modes, pixel value 0 normally means see-through, unless the polygon's drawing mode sets a bit that turns transparency off. And all the colours are 15-bit: 5 bits each of red, green and blue, which the extractor scales up to ordinary 8-bit colour when it writes a PNG.

## Where this leaves things

The extractor already decodes the 16-colour-table textures (mode 1) on the headset itself, straight from my disc image, and writes them out byte for byte the same as the PC version. Next comes the rest of Episode 3's textures in that mode and in true colour, then the colour-bank modes, which the extractor will now colour from `EPISODE3.PRG`'s upload table. After that, the headset should hold every forest texture in colour, extracted from the player's own disc.

Still unknown:

- Which part of Episode 3 uses the second colour table.
- Where the forest floor and canopy images are. They're VDP2 backgrounds, not VDP1 textures, and their colours probably sit higher up in colour RAM, beyond the blocks the tables fill.
- How the 3D models themselves are stored.

---

*I'm building this with Claude Code doing much of the typing and the reverse engineering alongside me. No images of the game's own art appear in this diary, as nothing from the disc goes in the repository.*
