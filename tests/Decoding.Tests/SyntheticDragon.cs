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
