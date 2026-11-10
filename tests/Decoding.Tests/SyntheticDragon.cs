using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace PdzDecoding.Tests;

/// <summary>A polygon of a test mesh: its four corner indexes, four texture words and CMDCTRL's low byte.</summary>
internal sealed record TestPolygon(byte A, byte B, byte C, byte D, ushort Pmod, ushort Colr, ushort Srca, ushort Size, byte Flags = 0x02);

/// <summary>A mesh of a test node, in the first layout.</summary>
internal sealed record TestMesh((int X, int Y, int Z)[] Vertices, TestPolygon[] Polygons);

/// <summary>A node of a test tree: its local matrix (12 longs), its mesh or none, and its children.</summary>
internal sealed record TestNode(int[] Matrix, TestMesh? Mesh, params TestNode[] Children);

/// <summary>
/// Builds a dragon's <c>.MDB</c> as <c>zwei-disc-formats.md</c> lays it out: meshes (vertices, polygons,
/// shading, normals, header), the body tree's matrices straight after the last mesh, the nodes children
/// first, and a shadow root as the last 12 bytes. Every byte is made here; nothing comes from the disc.
/// </summary>
internal static class SyntheticDragon
{
    public const uint Load = Dragons.LoadAddress;

    /// <summary>16.16's one.</summary>
    public const int One = 0x10000;

    /// <summary>A matrix with no turn, moving by whole units.</summary>
    public static int[] Move(int x, int y, int z) => new[] { One, 0, 0, x, 0, One, 0, y, 0, 0, One, z };

    /// <summary>A quarter turn about Y, taking +X to -Z and +Z to +X, then moving by whole units.</summary>
    public static int[] QuarterTurnY(int x = 0, int y = 0, int z = 0) => new[] { 0, 0, One, x, 0, One, 0, y, -One, 0, 0, z };

    /// <summary>A polygon drawing mode 1's 8 x 2 texture at <c>.CGB</c> offset 0 of <see cref="SyntheticTextures.Cgb"/>, opaque.</summary>
    public static TestPolygon Textured(byte a = 0, byte b = 1, byte c = 2, byte d = 3, byte flags = 0x02, ushort pmod = 0x04CC)
    {
        (ushort _, ushort colr, ushort srca, ushort size) = SyntheticTextures.Record();
        return new TestPolygon(a, b, c, d, pmod, colr, srca, size, flags);
    }

    /// <summary>A flat polygon of an RGB555 colour.</summary>
    public static TestPolygon Flat(ushort colour) => new(0, 1, 2, 3, 0x04C4, colour, 0, 0, 0x04);

    /// <summary>Four corners, in the plane z = 0, round the node's origin.</summary>
    public static readonly (int, int, int)[] Square = { (-400, 400, 0), (400, 400, 0), (400, -400, 0), (-400, -400, 0) };

    public static TestMesh SquareMesh(params TestPolygon[] polygons) =>
        new(Square, polygons.Length == 0 ? new[] { Textured() } : polygons);

    /// <summary>The <c>.MDB</c> of <paramref name="root"/>'s tree, after <paramref name="padding"/> zero bytes (a long-aligned count), and a shadow root that must not be drawn.</summary>
    public static byte[] Mdb(TestNode root, int padding = 0x40) => Layout(root, padding).Mdb;

    /// <summary>The address of <paramref name="root"/>'s node in the file <see cref="Mdb"/> builds.</summary>
    public static uint RootAddress(TestNode root, int padding = 0x40) => Layout(root, padding).Root;

    private static (byte[] Mdb, uint Root) Layout(TestNode root, int padding)
    {
        var file = new List<byte>(new byte[padding]);
        var order = new List<TestNode>();
        void Flatten(TestNode n)
        {
            order.Add(n);
            foreach (TestNode c in n.Children)
            {
                Flatten(c);
            }
        }

        Flatten(root);
        var meshes = new Dictionary<TestNode, uint>();
        foreach (TestNode n in order.Where(n => n.Mesh != null))
        {
            meshes[n] = WriteMesh(file, n.Mesh!);
        }

        foreach (TestNode n in order)
        {
            foreach (int v in n.Matrix)
            {
                Long(file, (uint)v);
            }
        }

        uint Emit(IReadOnlyList<TestNode> siblings)
        {
            uint next = 0;
            for (int i = siblings.Count - 1; i >= 0; i--)
            {
                uint child = Emit(siblings[i].Children);
                uint at = Load + (uint)file.Count;
                Long(file, meshes.GetValueOrDefault(siblings[i]));
                Long(file, child);
                Long(file, next);
                next = at;
            }

            return next;
        }

        uint rootAt = Emit(new[] { root });

        // The shadow: one leaf and the root above it, whose mesh pointer is left out so it cannot draw.
        uint leaf = Load + (uint)file.Count;
        Long(file, 0);
        Long(file, 0);
        Long(file, 0);
        Long(file, 0);
        Long(file, leaf);
        Long(file, 0);
        return (file.ToArray(), rootAt);
    }

    /// <summary>Writes a mesh in the file's order and returns its header's address.</summary>
    private static uint WriteMesh(List<byte> file, TestMesh mesh)
    {
        uint vertices = Load + (uint)file.Count;
        foreach ((int x, int y, int z) in mesh.Vertices)
        {
            Long(file, (uint)x);
            Long(file, (uint)y);
            Long(file, (uint)z);
        }

        uint polygons = Load + (uint)file.Count;
        foreach (TestPolygon p in mesh.Polygons)
        {
            file.AddRange(new[] { p.A, p.B, p.C, p.D });
            Word(file, p.Pmod);
            Word(file, p.Colr);
            Word(file, p.Srca);
            Word(file, p.Size);
            Word(file, p.Flags);
            Word(file, p.Flags);
        }

        uint shading = Load + (uint)file.Count;
        file.AddRange(new byte[mesh.Polygons.Length * 8]);
        uint normals = Load + (uint)file.Count;
        file.AddRange(new byte[mesh.Polygons.Length * 12]);
        uint header = Load + (uint)file.Count;
        Long(file, 1000); // The radius.
        Long(file, (uint)mesh.Vertices.Length - 1);
        Long(file, vertices);
        Long(file, (uint)mesh.Polygons.Length - 1);
        Long(file, polygons);
        Long(file, shading);
        Long(file, normals);
        Long(file, 0);
        return header;
    }

    private static void Long(List<byte> file, uint value)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, value);
        file.AddRange(b);
    }

    private static void Word(List<byte> file, ushort value)
    {
        var b = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(b, value);
        file.AddRange(b);
    }

    /// <summary>
    /// Ten forms' trees, each one mesh-bearing root moved by its form number, with a different padding so
    /// that their roots differ.
    /// </summary>
    public static TestNode Form(int index) => new(Move(index * 1000, 0, 0), SquareMesh());

    public static int Padding(int index) => 0x40 + index * 4;

    /// <summary>The seat <see cref="FirstRead"/> holds for the form at <paramref name="index"/> in <see cref="Dragons.FormFiles"/>.</summary>
    public static Seat SeatOf(int index) => new(0, 4000 + index * 40, 8000 - index * 80);

    /// <summary>
    /// A <c>1ST_READ</c>: <paramref name="lead"/> bytes of filler, the ten body roots in the game's order, and
    /// at <see cref="Dragons.SeatTableGap"/> after them the seat table of <see cref="SeatOf"/>, in the same order.
    /// </summary>
    public static byte[] FirstRead(IReadOnlyList<uint> roots, int lead = 0x123)
    {
        var data = new byte[lead + Dragons.SeatTableGap + 120 + 16];
        data.AsSpan(0, lead).Fill(0xEE);
        for (int i = 0; i < 10; i++)
        {
            int form = Dragons.GameOrder[i];
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(lead + i * 4), roots[form]);
            Seat seat = SeatOf(form);
            int at = lead + Dragons.SeatTableGap + i * 12;
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(at), seat.X);
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(at + 4), seat.Y);
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(at + 8), seat.Z);
        }

        return data;
    }

    // ------------------------------------------------------------ bodies of 27 nodes and their motion

    /// <summary>The body nodes with a mesh: 0 and 1 the beat leaves still, the rest it moves.</summary>
    public static readonly int[] BodyMeshes = { 0, 1, 5, 11, 13, 20, 23, 26 };

    /// <summary>The body nodes with a mesh that the beat moves.</summary>
    public static readonly string[] MovedParts = { "node05", "node11", "node13", "node20", "node23", "node26" };

    /// <summary>
    /// The rest translation of body node <paramref name="node"/> of form <paramref name="form"/>, in whole
    /// units, each a multiple of 16 so a <c>.MTB</c> record can hold it.
    /// </summary>
    public static (int X, int Y, int Z) RestMove(int form, int node) => (node * 160 - 2000 + form * 16, 320 - node * 16, node % 3 * 480 - 480);

    /// <summary>Whether body node <paramref name="node"/>'s rest matrix holds a quarter turn about Y, so the rest is not all straight.</summary>
    public static bool RestTurned(int node) => node is 11 or 21;

    /// <summary>The rest matrix of a body node.</summary>
    public static int[] RestMatrix(int form, int node)
    {
        (int x, int y, int z) = RestMove(form, node);
        return RestTurned(node) ? QuarterTurnY(x, y, z) : Move(x, y, z);
    }

    /// <summary>
    /// A body tree of <see cref="Dragons.BodyNodes"/> nodes in the shape of the format notes: 0 root; 1 to 5
    /// and 6 to 10 a chain each; 11 under the root, with 12 (and 13 under it), 14, the chains 15 to 20, 21
    /// to 23 and 24 to 26 under it. <paramref name="matrix"/> gives each node's local matrix, and the nodes of
    /// <see cref="BodyMeshes"/> hold a square.
    /// </summary>
    public static TestNode Body(Func<int, int[]> matrix)
    {
        TestNode Node(int i, params TestNode[] children) => new(matrix(i), BodyMeshes.Contains(i) ? SquareMesh() : null, children);
        TestNode Chain(int first, int last) => first == last ? Node(first) : Node(first, Chain(first + 1, last));
        return Node(0, Chain(1, 5), Chain(6, 10),
            Node(11, Node(12, Node(13)), Node(14), Chain(15, 20), Chain(21, 23), Chain(24, 26)));
    }

    /// <summary>Form <paramref name="index"/>'s body at rest.</summary>
    public static TestNode Body(int index) => Body(node => RestMatrix(index, node));

    /// <summary>A pose record: <c>tx ty tz</c> in units of 16, then <c>rx ry rz</c>, 4,096 to a turn.</summary>
    public readonly record struct TestRecord(short Tx, short Ty, short Tz, short Rx, short Ry, short Rz);

    /// <summary>The record that rebuilds body node <paramref name="node"/>'s rest matrix exactly.</summary>
    public static TestRecord RestRecord(int form, int node)
    {
        (int x, int y, int z) = RestMove(form, node);
        return new TestRecord((short)(x / 16), (short)(y / 16), (short)(z / 16), 0, (short)(RestTurned(node) ? 1024 : 0), 0);
    }

    /// <summary>
    /// A beat for tests: from the rest, the wing roots 21 and 24 roll a quarter turn more each frame, the tail's
    /// 15 pitches a quarter turn and moves, the trunk turns about X and Y at frame 1, and the root bobs and turns,
    /// which must not show.
    /// </summary>
    public static TestRecord BeatRecord(int form, int frame, int node)
    {
        TestRecord r = RestRecord(form, node);
        return node switch
        {
            0 => r with { Ty = (short)(r.Ty + frame * 50), Ry = (short)(frame * 512), Rx = (short)(frame * 300) },
            21 or 24 => r with { Rz = (short)(frame * (node == 21 ? 1024 : -1024)) },
            15 => r with { Rx = (short)(frame * 1024), Tz = (short)(r.Tz + frame * 10) },
            11 when frame == 1 => r with { Rx = 1024, Ry = 1024 + 1024 },
            _ => r,
        };
    }

    /// <summary>
    /// A <c>.MTB</c> of one motion of <paramref name="frames"/> frames as the format notes lay it out: every
    /// frame's part A, then every B, then every C, then the pointer table of n A, n B and n C pointers.
    /// Returns the file and the slot pointing at it.
    /// </summary>
    public static (byte[] Mtb, MotionSlot Slot) Mtb(int frames, Func<int, int, TestRecord> record)
    {
        var file = new List<byte>();
        int[][] parts = { new[] { 12, 13, 15, 16, 17, 18, 19, 20 }, new[] { 0, 2, 3, 4, 5, 7, 8, 9, 10, 11 }, new[] { 21, 22, 23, 24, 25, 26 } };
        var pointers = new List<uint>();
        foreach (int[] part in parts)
        {
            for (int f = 0; f < frames; f++)
            {
                pointers.Add(Dragons.MotionLoadAddress + (uint)file.Count);
                foreach (int node in part)
                {
                    TestRecord r = record(f, node);
                    foreach (short w in new[] { r.Tx, r.Ty, r.Tz, r.Rx, r.Ry, r.Rz })
                    {
                        Word(file, (ushort)w);
                    }
                }
            }
        }

        uint table = Dragons.MotionLoadAddress + (uint)file.Count;
        foreach (uint p in pointers)
        {
            Long(file, p);
        }

        return (file.ToArray(), new MotionSlot(table, table + (uint)frames * 4, table + (uint)frames * 8, frames));
    }

    /// <summary>Form <paramref name="index"/>'s <c>.MTB</c> holding <see cref="BeatRecord"/> for <paramref name="frames"/> frames.</summary>
    public static (byte[] Mtb, MotionSlot Slot) BeatMtb(int index, int frames = BeatFrames) => Mtb(frames, (f, node) => BeatRecord(index, f, node));

    /// <summary>The frames of the test beat.</summary>
    public const int BeatFrames = 3;

    /// <summary>
    /// <see cref="FirstRead"/> with, <see cref="Dragons.MotionTableGap"/> before the body roots, the motion
    /// table: a pointer per form, in the game's order, to its 12 slots after the seat table, slot 0 from
    /// <paramref name="slots"/> (in <see cref="Dragons.FormFiles"/>' order) and the rest zeros.
    /// </summary>
    public static byte[] FirstRead(IReadOnlyList<uint> roots, IReadOnlyList<MotionSlot> slots, int lead = 0x123)
    {
        byte[] head = FirstRead(roots, lead);
        int blocks = (head.Length + 3) / 4 * 4;
        var data = new byte[blocks + 10 * 0xC0];
        head.CopyTo(data, 0);
        for (int i = 0; i < 10; i++)
        {
            int form = Dragons.GameOrder[i];
            int at = blocks + i * 0xC0;
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(lead - Dragons.MotionTableGap + i * 4), Dragons.FirstReadLoadAddress + (uint)at);
            MotionSlot s = slots[form];
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at), s.A);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at + 4), s.B);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at + 8), s.C);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(at + 12), (uint)s.Frames << 16);
        }

        return data;
    }

    /// <summary>
    /// Adds ten forms of <see cref="Body(int)"/> (or <paramref name="body"/>), each with its beat's <c>.MTB</c>,
    /// and a <c>1ST_READ</c> with the seat and motion tables, leaving out the file named <paramref name="without"/>.
    /// <paramref name="slot"/> may change a form's slot 0.
    /// </summary>
    public static SyntheticDisc WithBeatingForms(SyntheticDisc disc, Func<int, TestNode>? body = null, Func<int, MotionSlot, MotionSlot>? slot = null, string? without = null)
    {
        string[] absent = without == null ? Array.Empty<string>() : new[] { without };
        var roots = new List<uint>();
        var slots = new List<MotionSlot>();
        for (int i = 0; i < Dragons.FormFiles.Count; i++)
        {
            string name = Dragons.FormFiles[i];
            TestNode form = body?.Invoke(i) ?? Body(i);
            roots.Add(RootAddress(form, Padding(i)));
            (byte[] mtb, MotionSlot s) = BeatMtb(i);
            slots.Add(slot?.Invoke(i, s) ?? s);
            Add(disc, name + ".MDB", Mdb(form, Padding(i)), absent);
            Add(disc, name + ".CGB", SyntheticTextures.Cgb(), absent);
            Add(disc, name + ".MTB", mtb, absent);
        }

        Add(disc, Dragons.SeatFile, FirstRead(roots, slots), absent);
        return disc;
    }

    /// <summary>Adds all ten forms and their <c>1ST_READ</c> to a disc, leaving out any file named in <paramref name="without"/>.</summary>
    public static SyntheticDisc WithForms(SyntheticDisc disc, params string[] without)
    {
        var roots = new List<uint>();
        for (int i = 0; i < Dragons.FormFiles.Count; i++)
        {
            string name = Dragons.FormFiles[i];
            TestNode form = Form(i);
            roots.Add(RootAddress(form, Padding(i)));
            Add(disc, name + ".MDB", Mdb(form, Padding(i)), without);
            Add(disc, name + ".CGB", SyntheticTextures.Cgb(), without);
        }

        Add(disc, Dragons.SeatFile, FirstRead(roots), without);
        return disc;
    }

    private static void Add(SyntheticDisc disc, string file, byte[] data, string[] without)
    {
        if (!without.Contains(file))
        {
            disc.With(file, data);
        }
    }
}
