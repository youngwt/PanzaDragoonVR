using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace PdzDecoding;

/// <summary>A rider's offset from a form's root, as <c>1ST_READ</c>'s table holds it, in Saturn units and axes.</summary>
public readonly record struct Seat(int X, int Y, int Z);

/// <summary>One form decoded: its model, and how many of its textured polygons came out flat grey.</summary>
public sealed record DecodedDragon(Model Model, int GreyPolygons);

/// <summary>One <c>.glb</c> extraction wrote: its asset ID and its path relative to the output folder.</summary>
public readonly record struct ExtractedModel(string Id, string Path);

/// <summary>Where the dragons' seats came from, and what could not be drawn as the disc does.</summary>
/// <param name="Forms">How many forms were written.</param>
/// <param name="SeatTable">The file offset in <c>1ST_READ</c> of the seat table.</param>
/// <param name="GreyPolygons">Polygons over all forms whose texture could not be decoded.</param>
public sealed record DragonSources(int Forms, int SeatTable, int GreyPolygons)
{
    /// <summary>
    /// One line, with no full stop: <c>10 dragon forms, seats from 0x54840 of 1ST_READ.PRG</c>, followed
    /// by <c>, 3 polygons drawn flat grey</c> when any are.
    /// </summary>
    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"{Forms} dragon forms, seats from 0x{SeatTable:X} of {Dragons.SeatFile}") +
        (GreyPolygons == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $", {GreyPolygons} {(GreyPolygons == 1 ? "polygon" : "polygons")} drawn flat grey"));
}

/// <summary>The ten forms ready to write, in form order.</summary>
public sealed record DragonModels(DragonSources Sources, IReadOnlyList<(ExtractedModel File, byte[] Glb)> Files);

/// <summary>
/// The dragon forms' geometry, as <c>zwei-disc-formats.md</c>, "Models", says: the body tree of each
/// <c>DRA</c> <c>.MDB</c> in the first mesh layout, with its matrices, vertices and polygons, turned into
/// a <see cref="Model"/> whose origin is the seat. The seat comes from a table in <c>1ST_READ</c> that is
/// found by its neighbour, the table of body roots, and not by an offset for each disc. Matrix products
/// are integers, each sum of products shifted down as a whole, so the output is the same on every
/// runtime; a position becomes a <c>float</c> once, at the end.
/// </summary>
public static class Dragons
{
    /// <summary>The address every <c>DRA</c> <c>.MDB</c> loads at, which its pointers count from.</summary>
    public const uint LoadAddress = 0x20288000;

    /// <summary>
    /// Saturn units to a metre, the same for every model, as the asset contract says. The author's choice in
    /// the headset: nothing in the data says what a unit is.
    /// </summary>
    public const int UnitsPerMetre = 4000;

    /// <summary>The disc file holding the seat table.</summary>
    public const string SeatFile = "1ST_READ.PRG";

    /// <summary>Bytes from the start of the body roots table to the start of the seat table.</summary>
    public const int SeatTableGap = 0x50;

    /// <summary>The colour of a polygon whose texture cannot be decoded.</summary>
    public static readonly ModelColour Grey = new(128, 128, 128);

    /// <summary>The ten forms' disc files, in the order of their numbers.</summary>
    public static readonly IReadOnlyList<string> FormFiles = new[]
    {
        "DRA01A", "DRA02B", "DRA03C", "DRA04D", "DRA05E", "DRA06F", "DRA07G", "DRA08H", "DRA09I", "DRA10J",
    };

    /// <summary>For each index the game's tables use, the place of its form in <see cref="FormFiles"/>: A, I, B, F, C, D, H, E, G, J.</summary>
    public static readonly IReadOnlyList<int> GameOrder = new[] { 0, 8, 1, 5, 2, 3, 7, 4, 6, 9 };

    private const int NodeSize = 12;
    private const int MatrixSize = 48;
    private const int MeshHeaderSize = 32;
    private const int VertexSize = 12;
    private const int PolygonSize = 16;

    /// <summary>The asset ID of a form, by its number from 1: <c>dragon.form01</c>.</summary>
    public static string FormId(int number) => string.Create(CultureInfo.InvariantCulture, $"dragon.form{number:D2}");

    /// <summary>Where a form goes in the asset folder, by its number from 1: <c>models/dragon_form01.glb</c>.</summary>
    public static string FormPath(int number) => string.Create(CultureInfo.InvariantCulture, $"models/dragon_form{number:D2}.glb");

    /// <summary>
    /// Every form of the disc as a <c>.glb</c>, or null and a one-line notice, with no full stop, saying why
    /// there are none: a form's file or the seat table is missing, or some geometry is malformed. It is all
    /// ten or none, and nothing here stops the rest of extraction.
    /// </summary>
    public static (DragonModels? Models, string? Notice) FromDisc(DiscImage disc)
    {
        var files = new List<(byte[] Mdb, byte[] Cgb)>();
        string reading = "";
        try
        {
            foreach (string form in FormFiles)
            {
                reading = form;
                DiscFile? mdb = disc.Find(form + ".MDB");
                DiscFile? cgb = disc.Find(form + ".CGB");
                if (mdb == null || cgb == null)
                {
                    return (null, $"the disc has no {form}{(mdb == null ? ".MDB" : ".CGB")}");
                }

                files.Add((disc.ReadFile(mdb.Value), disc.ReadFile(cgb.Value)));
            }

            var roots = new uint[files.Count];
            for (int i = 0; i < files.Count; i++)
            {
                reading = FormFiles[i];
                roots[i] = LoadAddress + (uint)BodyRoot(files[i].Mdb);
            }

            reading = SeatFile;
            if (disc.Find(SeatFile) is not DiscFile seatFile)
            {
                return (null, $"the disc has no {SeatFile}");
            }

            (int table, Seat[] seats) = FindSeats(disc.ReadFile(seatFile), roots);

            var written = new List<(ExtractedModel, byte[])>();
            int grey = 0;
            for (int i = 0; i < files.Count; i++)
            {
                reading = FormFiles[i];
                int number = i + 1;
                DecodedDragon dragon = Decode(files[i].Mdb, files[i].Cgb, FormId(number).Split('.')[1], seats[i]);
                grey += dragon.GreyPolygons;
                written.Add((new ExtractedModel(FormId(number), FormPath(number)), GlbWriter.Write(dragon.Model)));
            }

            return (new DragonModels(new DragonSources(written.Count, table, grey), written), null);
        }
        catch (DecodingException e)
        {
            return (null, $"{reading}: {e.Message}");
        }
    }

    /// <summary>
    /// The seat table in <paramref name="firstRead"/>: the bytes of <paramref name="roots"/> (the body root
    /// addresses, in <see cref="FormFiles"/>' order) in the game's order, then the table of three longs per
    /// form <see cref="SeatTableGap"/> after their start. Returns the table's file offset and the seats in
    /// <see cref="FormFiles"/>' order.
    /// </summary>
    /// <exception cref="DecodingException">No match, or the table would run off the end of the file.</exception>
    public static (int Table, Seat[] Seats) FindSeats(byte[] firstRead, IReadOnlyList<uint> roots)
    {
        var wanted = new byte[GameOrder.Count * 4];
        for (int i = 0; i < GameOrder.Count; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(wanted.AsSpan(i * 4), roots[GameOrder[i]]);
        }

        int match = firstRead.AsSpan().IndexOf(wanted);
        if (match < 0)
        {
            throw new DecodingException($"it holds no table of the {GameOrder.Count} forms' body roots");
        }

        int table = match + SeatTableGap;
        if (table + GameOrder.Count * 12L > firstRead.Length)
        {
            throw new DecodingException("the seat table would run off the end of the file");
        }

        var seats = new Seat[GameOrder.Count];
        for (int i = 0; i < GameOrder.Count; i++)
        {
            ReadOnlySpan<byte> entry = firstRead.AsSpan(table + i * 12, 12);
            seats[GameOrder[i]] = new Seat(BinaryPrimitives.ReadInt32BigEndian(entry), BinaryPrimitives.ReadInt32BigEndian(entry[4..]), BinaryPrimitives.ReadInt32BigEndian(entry[8..]));
        }

        return (table, seats);
    }

    /// <summary>
    /// The file offset of a form's body root: the first node, in file order, that no other node points at.
    /// Nodes fill the end of the file, so they are read back from the end while three longs can be a node.
    /// Three zeros that nothing points at are not a root: there is nothing to walk.
    /// </summary>
    /// <exception cref="DecodingException">No such node.</exception>
    public static int BodyRoot(byte[] mdb)
    {
        var candidates = new List<int>();
        var referenced = new HashSet<long>();
        for (int o = mdb.Length - NodeSize; o >= 0; o -= NodeSize)
        {
            uint mesh = BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(o));
            uint child = BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(o + 4));
            uint sibling = BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(o + 8));
            if (!CanPoint(mesh, mdb.Length) || !CanPoint(child, mdb.Length) || !CanPoint(sibling, mdb.Length))
            {
                break;
            }

            if (mesh != 0 || child != 0 || sibling != 0)
            {
                candidates.Add(o);
            }

            if (child != 0)
            {
                referenced.Add(child - (long)LoadAddress);
            }

            if (sibling != 0)
            {
                referenced.Add(sibling - (long)LoadAddress);
            }
        }

        // The scan went backward, so the last candidate unreferenced is the first in the file.
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (!referenced.Contains(candidates[i]))
            {
                return candidates[i];
            }
        }

        throw new DecodingException("no node is the root of a tree");
    }

    /// <summary>
    /// One form's body tree as a <see cref="Model"/> named <paramref name="name"/>: each mesh-bearing node a
    /// part named <c>node</c> and its two-digit depth-first index, its origin the translation of its matrix
    /// product less <paramref name="seat"/>, its corners relative to that, all converted to glTF.
    /// </summary>
    /// <exception cref="DecodingException">A pointer, count or corner index is out of range, a node is reached twice, the tree has no mesh, or a matrix product does not fit.</exception>
    public static DecodedDragon Decode(byte[] mdb, byte[] cgb, string name, Seat seat)
    {
        List<(int Mesh, int Parent)> nodes = Walk(mdb, BodyRoot(mdb));
        if (!nodes.Any(n => n.Mesh != 0))
        {
            throw new DecodingException("the body tree holds no mesh");
        }

        long matrices = nodes.Max(n => n.Mesh) + (long)MeshHeaderSize;
        if (matrices + nodes.Count * (long)MatrixSize > mdb.Length)
        {
            throw new DecodingException($"the {nodes.Count} matrices after the last mesh run off the end of the file");
        }

        var painter = new Painter(mdb, cgb);
        var parts = new List<ModelPart>();
        var products = new long[nodes.Count][];
        try
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                long[] local = ReadMatrix(mdb, (int)matrices + i * MatrixSize);
                products[i] = nodes[i].Parent < 0 ? local : Multiply(products[nodes[i].Parent], local);
                if (nodes[i].Mesh != 0)
                {
                    parts.Add(Part(mdb, nodes[i].Mesh, i, products[i], seat, painter));
                }
            }
        }
        catch (OverflowException)
        {
            throw new DecodingException("a matrix product is too large to hold");
        }

        return new DecodedDragon(new Model(name, parts), painter.Grey);
    }

    /// <summary>A pointer field's value is usable: zero, or a long-aligned address inside the file.</summary>
    private static bool CanPoint(uint value, int length) =>
        value == 0 || (value >= LoadAddress && value - LoadAddress < (uint)length && (value - LoadAddress) % 4 == 0);

    /// <summary>The nodes under <paramref name="root"/>, depth first (node, children, then siblings), each with its mesh's offset (0 for none) and its parent's index (-1 for none).</summary>
    private static List<(int Mesh, int Parent)> Walk(byte[] mdb, int root)
    {
        var nodes = new List<(int, int)>();
        var seen = new HashSet<int>();
        var stack = new Stack<(int Node, int Parent)>();
        stack.Push((root, -1));
        while (stack.Count > 0)
        {
            (int node, int parent) = stack.Pop();
            if (!seen.Add(node))
            {
                throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the node at 0x{node:X} is reached twice"));
            }

            int mesh = Offset(mdb, BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(node)), MeshHeaderSize, "mesh");
            uint child = BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(node + 4));
            uint sibling = BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(node + 8));
            int index = nodes.Count;
            nodes.Add((mesh, parent));

            // The child goes on last, so it and all below it come before the sibling.
            if (sibling != 0)
            {
                stack.Push((Offset(mdb, sibling, NodeSize, "node"), parent));
            }

            if (child != 0)
            {
                stack.Push((Offset(mdb, child, NodeSize, "node"), index));
            }
        }

        return nodes;
    }

    /// <summary>The file offset of a pointer to <paramref name="size"/> bytes; a pointer of 0 gives 0.</summary>
    private static int Offset(byte[] mdb, uint pointer, long size, string what)
    {
        if (pointer == 0)
        {
            return 0;
        }

        long offset = pointer - (long)LoadAddress;
        if (offset < 0 || offset + size > mdb.Length)
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"a {what} pointer, 0x{pointer:X8}, is outside the file"));
        }

        return (int)offset;
    }

    private static long[] ReadMatrix(byte[] mdb, int at)
    {
        var m = new long[12];
        for (int i = 0; i < 12; i++)
        {
            m[i] = BinaryPrimitives.ReadInt32BigEndian(mdb.AsSpan(at + i * 4));
        }

        return m;
    }

    /// <summary>
    /// The parent's matrix times the child's, each 12 values row by row. The three-by-three is 16.16 fixed
    /// point and the translations are whole units, as the vertices they add to are.
    /// </summary>
    private static long[] Multiply(long[] a, long[] b)
    {
        var r = new long[12];
        checked
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    r[i * 4 + j] = Row(a, i, b[j], b[4 + j], b[8 + j]) + (j == 3 ? a[i * 4 + 3] : 0);
                }
            }
        }

        return r;
    }

    /// <summary>Row <paramref name="row"/> of the rotation of <paramref name="m"/> times a column, shifted back from 16.16 as one sum.</summary>
    private static long Row(long[] m, int row, long x, long y, long z)
    {
        checked
        {
            Int128 sum = (Int128)m[row * 4] * x + (Int128)m[row * 4 + 1] * y + (Int128)m[row * 4 + 2] * z;
            return (long)(sum >> 16);
        }
    }

    /// <summary>A glTF position in metres from Saturn units: <c>(-x, y, -z)</c>, a half-turn about Y, over <see cref="UnitsPerMetre"/>.</summary>
    private static Vector3 ToGltf(long x, long y, long z) => new(Metres(-x), Metres(y), Metres(-z));

    private static float Metres(long units) => (float)(units / (double)UnitsPerMetre);

    private static ModelPart Part(byte[] mdb, int mesh, int index, long[] product, Seat seat, Painter painter)
    {
        ReadOnlySpan<byte> header = mdb.AsSpan(mesh, MeshHeaderSize);
        long vertexCount = BinaryPrimitives.ReadUInt32BigEndian(header[4..]) + 1L;
        long polygonCount = BinaryPrimitives.ReadUInt32BigEndian(header[12..]) + 1L;
        uint vertexPointer = BinaryPrimitives.ReadUInt32BigEndian(header[8..]);
        uint polygonPointer = BinaryPrimitives.ReadUInt32BigEndian(header[16..]);
        if (vertexPointer == 0 || polygonPointer == 0)
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the mesh at 0x{mesh:X} has no vertices or no polygons"));
        }

        int vertices = Offset(mdb, vertexPointer, vertexCount * VertexSize, "vertex list");
        int polygons = Offset(mdb, polygonPointer, polygonCount * PolygonSize, "polygon list");

        // A corner is relative to the part's origin, so it takes the rotation of the product alone.
        var corners = new Vector3[vertexCount];
        for (int v = 0; v < vertexCount; v++)
        {
            ReadOnlySpan<byte> at = mdb.AsSpan(vertices + v * VertexSize, VertexSize);
            long x = BinaryPrimitives.ReadInt32BigEndian(at);
            long y = BinaryPrimitives.ReadInt32BigEndian(at[4..]);
            long z = BinaryPrimitives.ReadInt32BigEndian(at[8..]);
            corners[v] = ToGltf(Row(product, 0, x, y, z), Row(product, 1, x, y, z), Row(product, 2, x, y, z));
        }

        var quads = new ModelQuad[polygonCount];
        for (int p = 0; p < polygonCount; p++)
        {
            int at = polygons + p * PolygonSize;
            for (int c = 0; c < 4; c++)
            {
                if (mdb[at + c] >= vertexCount)
                {
                    throw new DecodingException(string.Create(CultureInfo.InvariantCulture,
                        $"the polygon at 0x{at:X} has corner index {mdb[at + c]} of a mesh with {vertexCount} vertices"));
                }
            }

            var record = new TextureRecord(at + 4,
                BinaryPrimitives.ReadUInt16BigEndian(mdb.AsSpan(at + 4)),
                BinaryPrimitives.ReadUInt16BigEndian(mdb.AsSpan(at + 6)),
                BinaryPrimitives.ReadUInt16BigEndian(mdb.AsSpan(at + 8)),
                BinaryPrimitives.ReadUInt16BigEndian(mdb.AsSpan(at + 10)));
            int flags = mdb[at + 13]; // The low byte of the first flag word: CMDCTRL's.
            (ModelTexture? texture, ModelColour colour) = painter.Paint(record);
            quads[p] = new ModelQuad(corners[mdb[at]], corners[mdb[at + 1]], corners[mdb[at + 2]], corners[mdb[at + 3]],
                texture, colour, ModeOf(record.Pmod), FlipH: (flags & 0x10) != 0, FlipV: (flags & 0x20) != 0);
        }

        long[] t = { product[3] - seat.X, product[7] - seat.Y, product[11] - seat.Z };
        return new ModelPart(string.Create(CultureInfo.InvariantCulture, $"node{index:D2}"), ToGltf(t[0], t[1], t[2]), quads);
    }

    /// <summary>The material a polygon's CMDPMOD asks for, as the format notes' table says.</summary>
    private static DrawingMode ModeOf(ushort pmod)
    {
        if ((pmod & 0x0100) != 0)
        {
            return DrawingMode.Mesh;
        }

        if ((pmod & 7) is 3 or 7)
        {
            return DrawingMode.Half;
        }

        return (pmod & 0x40) != 0 ? DrawingMode.Opaque : DrawingMode.Cutout;
    }

    /// <summary>One form's polygons' looks: flat colours, and textures decoded once per four words from its <c>.CGB</c>.</summary>
    private sealed class Painter
    {
        private readonly byte[] _cgb;
        private readonly int? _base;
        private readonly Dictionary<(ushort, ushort, ushort, ushort), ModelTexture?> _textures = new();

        public Painter(byte[] mdb, byte[] cgb)
        {
            _cgb = cgb;
            _base = Textures.InferBase(Textures.FindRecords(mdb), cgb.Length);
        }

        /// <summary>Polygons so far whose texture could not be decoded.</summary>
        public int Grey { get; private set; }

        public (ModelTexture? Texture, ModelColour Colour) Paint(TextureRecord r)
        {
            if (r.Size == 0)
            {
                var rgb = new byte[3];
                Textures.Rgb555(r.Colr, rgb);
                return (null, new ModelColour(rgb[0], rgb[1], rgb[2]));
            }

            if (!_textures.TryGetValue(r.Words, out ModelTexture? texture))
            {
                texture = Decode(r);
                _textures[r.Words] = texture;
            }

            if (texture == null)
            {
                Grey++;
                return (null, Dragons.Grey);
            }

            return (texture, default);
        }

        /// <summary>The record's texture, or null when its colour mode needs more than its <c>.CGB</c> or it does not fit there.</summary>
        private ModelTexture? Decode(TextureRecord r)
        {
            if (_base is not int at || r.Width == 0 || r.Height == 0 || !Textures.Fits(r, at, _cgb.Length))
            {
                return null;
            }

            return r.ColourMode switch
            {
                1 => new ModelTexture(r.Width, r.Height, Textures.DecodeMode1(_cgb, at, r)),
                5 => new ModelTexture(r.Width, r.Height, Textures.DecodeMode5(_cgb, at, r)),
                _ => null,
            };
        }
    }
}
