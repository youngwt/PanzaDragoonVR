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

/// <summary>Where the dragons' seats and wing beats came from, and what could not be drawn as the disc does.</summary>
/// <param name="Forms">How many forms were written.</param>
/// <param name="SeatTable">The file offset in <c>1ST_READ</c> of the seat table.</param>
/// <param name="GreyPolygons">Polygons over all forms whose texture could not be decoded.</param>
/// <param name="MotionTable">The file offset in <c>1ST_READ</c> of the motion table the wing beats came from; null when the forms are still.</param>
/// <param name="NoBeat">Why the forms are still: one line, no full stop. Null when they beat their wings.</param>
public sealed record DragonSources(int Forms, int SeatTable, int GreyPolygons, int? MotionTable = null, string? NoBeat = null)
{
    /// <summary>
    /// One line, with no full stop: <c>&lt;n&gt; dragon forms, seats from 0x... of 1ST_READ.PRG</c>, followed
    /// by <c>, wing beats from 0x... of 1ST_READ.PRG</c> when they have them, <c>, &lt;n&gt; polygons drawn flat grey</c>
    /// when any are, and <c>, no wing beat: &lt;why&gt;</c> when they are still.
    /// </summary>
    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"{Forms} dragon forms, seats from 0x{SeatTable:X} of {Dragons.SeatFile}") +
        (MotionTable is int motions ? string.Create(CultureInfo.InvariantCulture, $", wing beats from 0x{motions:X} of {Dragons.SeatFile}") : "") +
        (GreyPolygons == 0 ? "" : string.Create(CultureInfo.InvariantCulture, $", {GreyPolygons} {(GreyPolygons == 1 ? "polygon" : "polygons")} drawn flat grey")) +
        (NoBeat == null ? "" : $", no wing beat: {NoBeat}");
}

/// <summary>
/// A form's motion slot as <c>1ST_READ</c> holds it: the addresses of its lists of pointers to the pose
/// parts A, B and C in the form's <c>.MTB</c>, and its number of frames.
/// </summary>
public readonly record struct MotionSlot(uint A, uint B, uint C, int Frames);

/// <summary>The ten forms ready to write, in form order, then the form the player rides.</summary>
public sealed record DragonModels(DragonSources Sources, IReadOnlyList<(ExtractedModel File, byte[] Glb)> Files);

/// <summary>
/// The dragon forms' geometry, as <c>zwei-disc-formats.md</c>, "Models", says: the body tree of each
/// <c>DRA</c> <c>.MDB</c> in the first mesh layout, with its matrices, vertices and polygons, turned into
/// a <see cref="Model"/> whose origin is the seat. The seat comes from a table in <c>1ST_READ</c> that is
/// found by its neighbour, the table of body roots, and not by an offset for each disc. Matrix products
/// are integers, each sum of products shifted down as a whole, so the output is the same on every
/// runtime; a position becomes a <c>float</c> once, at the end.
/// <para>
/// Each form also carries its wing beat, motion slot 0 of its <c>.MTB</c> ("Motion: <c>.MTB</c>" in the
/// same notes), as a stepped animation of its parts, found through the motion table beside the body
/// roots. Angles become matrices through an integer sine table, and a key's turn becomes a quaternion
/// with only <c>+ - * /</c> and <see cref="Math.Sqrt"/>, so these too are the same on every runtime.
/// </para>
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

    /// <summary>The form the player rides, by its number from 1: the author's choice for the demo.</summary>
    public const int RideForm = 9;

    /// <summary>The folder inside the output folder that the dragon forms go in.</summary>
    internal const string ModelFolder = "models";

    /// <summary>The asset ID of the form the player rides.</summary>
    public const string RideId = "dragon.ride";

    /// <summary>Where the form the player rides goes in the asset folder.</summary>
    public const string RidePath = ModelFolder + "/dragon_ride.glb";

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

    /// <summary>The address every <c>DRA</c> <c>.MTB</c> loads at, which its pointers and the motion slots' count from.</summary>
    public const uint MotionLoadAddress = 0x20215000;

    /// <summary>The address <c>1ST_READ</c> loads at, which the motion table's pointers count from.</summary>
    public const uint FirstReadLoadAddress = 0x06008000;

    /// <summary>Bytes from the start of the motion table to the start of the body roots table.</summary>
    public const int MotionTableGap = 0xA0;

    /// <summary>The nodes of a body tree the motion is laid out for.</summary>
    public const int BodyNodes = 27;

    /// <summary>The name of the wing beat's animation in every form's model.</summary>
    public const string BeatName = "wing_beat";

    /// <summary>Frames of the wing beat a second: the game's rate, as the author gives it.</summary>
    public const int BeatFramesPerSecond = 30;

    /// <summary>The body nodes that pose parts A, B and C drive, record by record.</summary>
    private static readonly int[][] PoseNodes =
    {
        new[] { 12, 13, 15, 16, 17, 18, 19, 20 },
        new[] { 0, 2, 3, 4, 5, 7, 8, 9, 10, 11 },
        new[] { 21, 22, 23, 24, 25, 26 },
    };

    /// <summary>The largest distance from 1 a key rotation's length may have, as the writer allows.</summary>
    private const float UnitTolerance = 1e-5f;

    /// <summary>The root, whose own motion is left out: the dragon as a whole stays at the seat.</summary>
    private const int RootNode = 0;

    private const int SlotSize = 16;
    private const int RecordSize = 12;
    private const int SeatSize = 12;
    private const int NodeSize = 12;
    private const int MatrixSize = 48;
    private const int MeshHeaderSize = 32;
    private const int VertexSize = 12;
    private const int PolygonSize = 16;

    /// <summary>The asset ID of a form, by its number from 1: <c>dragon.form01</c>.</summary>
    private static string FormId(int number) => string.Create(CultureInfo.InvariantCulture, $"dragon.form{number:D2}");

    /// <summary>Where a form goes in the asset folder, by its number from 1: <c>models/dragon_form01.glb</c>.</summary>
    public static string FormPath(int number) => string.Create(CultureInfo.InvariantCulture, $"{ModelFolder}/dragon_form{number:D2}.glb");

    /// <summary>
    /// Every form of the disc as a <c>.glb</c>, and the ridden one again as <see cref="RideId"/>, or null and a one-line notice, with no full stop, saying why
    /// there are none: a form's file or the seat table is missing, some geometry is malformed or the writer rejects a model. It is all
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

            byte[] firstRead = disc.ReadFile(seatFile);
            (int table, Seat[] seats) = FindSeats(firstRead, roots);
            (int? motionTable, ModelAnimation[]? beats, string? noBeat) = WingBeats(disc, firstRead, table - SeatTableGap, files, seats);

            var written = new List<(ExtractedModel, byte[])>();
            var models = new List<Model>();
            int grey = 0;
            for (int i = 0; i < files.Count; i++)
            {
                reading = FormFiles[i];
                int number = i + 1;
                DecodedDragon dragon = Decode(files[i].Mdb, files[i].Cgb, RootName(FormId(number)), seats[i]);
                Model model = beats == null ? dragon.Model : dragon.Model with { Animation = beats[i] };
                grey += dragon.GreyPolygons;
                models.Add(model);
                written.Add((new ExtractedModel(FormId(number), FormPath(number)), Write(model)));
            }

            // The ridden form again, under its own root name, as the asset contract's naming rule says.
            int forms = written.Count;
            reading = FormFiles[RideForm - 1];
            written.Add((new ExtractedModel(RideId, RidePath), Write(models[RideForm - 1] with { Name = RootName(RideId) })));
            return (new DragonModels(new DragonSources(forms, table, grey, motionTable, noBeat), written), null);
        }
        catch (DecodingException e)
        {
            return (null, $"{reading}: {e.Message}");
        }
    }

    /// <summary>
    /// <paramref name="model"/> as a <c>.glb</c>, through <see cref="GlbWriter.Write"/>. A model the writer
    /// rejects, which a decoded form should never be, is a <see cref="DecodingException"/> with the writer's
    /// reason on one line, so <see cref="FromDisc"/> turns it into its notice like any other.
    /// </summary>
    /// <exception cref="DecodingException">The writer or its atlas rejected the model.</exception>
    public static byte[] Write(Model model)
    {
        try
        {
            return GlbWriter.Write(model);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            throw new DecodingException("the model cannot be written: " + DecodingException.OneLine(e.Message), e);
        }
    }

    /// <summary>A model's root name, from its asset ID: what follows the prefix, <c>form01</c> of <c>dragon.form01</c>.</summary>
    private static string RootName(string id) => id.Split('.')[1];

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
        if (table + GameOrder.Count * (long)SeatSize > firstRead.Length)
        {
            throw new DecodingException("the seat table would run off the end of the file");
        }

        var seats = new Seat[GameOrder.Count];
        for (int i = 0; i < GameOrder.Count; i++)
        {
            ReadOnlySpan<byte> entry = firstRead.AsSpan(table + i * SeatSize, SeatSize);
            seats[GameOrder[i]] = new Seat(BinaryPrimitives.ReadInt32BigEndian(entry), BinaryPrimitives.ReadInt32BigEndian(entry[4..]), BinaryPrimitives.ReadInt32BigEndian(entry[8..]));
        }

        return (table, seats);
    }

    /// <summary>
    /// Every form's wing beat, in <see cref="FormFiles"/>' order, with the motion table's file offset; or,
    /// when any cannot be read, none and why, on one line with no full stop. It is all ten or none, and
    /// nothing here stops the dragons being written still.
    /// </summary>
    private static (int? Table, ModelAnimation[]? Beats, string? NoBeat) WingBeats(
        DiscImage disc, byte[] firstRead, int rootsTable, List<(byte[] Mdb, byte[] Cgb)> files, Seat[] seats)
    {
        string reading = SeatFile;
        try
        {
            (int table, MotionSlot[] slots) = FindMotions(firstRead, rootsTable);
            var beats = new ModelAnimation[files.Count];
            for (int i = 0; i < files.Count; i++)
            {
                reading = FormFiles[i];
                if (disc.Find(FormFiles[i] + ".MTB") is not DiscFile mtb)
                {
                    return (null, null, $"the disc has no {FormFiles[i]}.MTB");
                }

                beats[i] = WingBeat(files[i].Mdb, disc.ReadFile(mtb), slots[i], seats[i]);
            }

            return (table, beats, null);
        }
        catch (DecodingException e)
        {
            return (null, null, $"{reading}: {e.Message}");
        }
    }

    /// <summary>
    /// The motion table in <paramref name="firstRead"/>, <see cref="MotionTableGap"/> before the body roots
    /// table at <paramref name="rootsTable"/>: a pointer per form, in the game's order, to its 12 motion
    /// slots. Returns the table's file offset and each form's slot 0, the wing beat, in <see cref="FormFiles"/>' order.
    /// </summary>
    /// <exception cref="DecodingException">The table or a slot is not inside the file, or a slot's frame count is not a whole number above 0.</exception>
    public static (int Table, MotionSlot[] Slots) FindMotions(byte[] firstRead, int rootsTable)
    {
        int table = rootsTable - MotionTableGap;
        string none = string.Create(CultureInfo.InvariantCulture, $"it holds no motion table 0x{MotionTableGap:X} before the body roots");
        if (table < 0)
        {
            throw new DecodingException(none);
        }

        var slots = new MotionSlot[GameOrder.Count];
        for (int i = 0; i < GameOrder.Count; i++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32BigEndian(firstRead.AsSpan(table + i * 4));
            long at = pointer - (long)FirstReadLoadAddress;
            if (at < 0 || at % 4 != 0 || at + SlotSize > firstRead.Length)
            {
                throw new DecodingException(none);
            }

            ReadOnlySpan<byte> slot = firstRead.AsSpan((int)at, SlotSize);
            uint count = BinaryPrimitives.ReadUInt32BigEndian(slot[12..]);
            string form = FormFiles[GameOrder[i]];
            if ((count & 0xFFFF) != 0 || count == 0 || count > 0x7FFF0000)
            {
                throw new DecodingException(string.Create(CultureInfo.InvariantCulture,
                    $"{form}'s wing beat has a frame count of 0x{count:X8}, not a whole number of frames above 0"));
            }

            slots[GameOrder[i]] = new MotionSlot(BinaryPrimitives.ReadUInt32BigEndian(slot), BinaryPrimitives.ReadUInt32BigEndian(slot[4..]),
                BinaryPrimitives.ReadUInt32BigEndian(slot[8..]), (int)(count >> 16));
        }

        return (table, slots);
    }

    /// <summary>
    /// A form's wing beat from its <c>.MTB</c>, as <see cref="BeatName"/>: for every frame of
    /// <paramref name="slot"/>, the pose's local matrices in place of the <c>.MDB</c>'s for the nodes it
    /// drives, the root's left as the <c>.MDB</c> has it, and for every part one of those nodes moves (itself
    /// or above it), a key: the turn from its rest pose, <c>R_f * R_rest⁻¹</c>, and its origin less
    /// <paramref name="seat"/>, both in glTF terms as <see cref="Decode"/> gives the rest.
    /// </summary>
    /// <exception cref="DecodingException">The body tree is not <see cref="BodyNodes"/> nodes, a pointer is outside the <c>.MTB</c>, a part's rest turn cannot be inverted, a key is not a rotation, or a matrix product does not fit.</exception>
    public static ModelAnimation WingBeat(byte[] mdb, byte[] mtb, MotionSlot slot, Seat seat)
    {
        List<(int Mesh, int Parent)> nodes = Walk(mdb, BodyRoot(mdb));
        if (nodes.Count != BodyNodes)
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the body tree is not the {BodyNodes} nodes the motion is for: it has {nodes.Count}"));
        }

        if (slot.Frames < 1)
        {
            throw new DecodingException("the wing beat has no frames");
        }

        long[][] rest = RestLocals(mdb, nodes);
        uint[] lists = { slot.A, slot.B, slot.C };
        var frames = new int[PoseNodes.Length][];
        for (int part = 0; part < PoseNodes.Length; part++)
        {
            frames[part] = FrameList(mtb, lists[part], slot.Frames, PoseNodes[part].Length * RecordSize);
        }

        var driven = new bool[nodes.Count];
        foreach (int node in PoseNodes.SelectMany(n => n).Where(n => n != RootNode))
        {
            driven[node] = true;
        }

        var moved = new bool[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            moved[i] = driven[i] || (nodes[i].Parent >= 0 && moved[nodes[i].Parent]);
        }

        int[] parts = Enumerable.Range(0, nodes.Count).Where(i => nodes[i].Mesh != 0 && moved[i]).ToArray();
        if (parts.Length == 0)
        {
            throw new DecodingException("the wing beat moves no part with a mesh");
        }

        var rotations = parts.Select(_ => new Quaternion[slot.Frames]).ToArray();
        var translations = parts.Select(_ => new Vector3[slot.Frames]).ToArray();
        try
        {
            long[][] restProducts = Products(nodes, rest);
            var inverses = parts.Select(i => Inverse(restProducts[i], i)).ToArray();
            for (int f = 0; f < slot.Frames; f++)
            {
                var local = (long[][])rest.Clone();
                for (int part = 0; part < PoseNodes.Length; part++)
                {
                    for (int r = 0; r < PoseNodes[part].Length; r++)
                    {
                        if (PoseNodes[part][r] != RootNode)
                        {
                            local[PoseNodes[part][r]] = PoseMatrix(mtb, frames[part][f] + r * RecordSize);
                        }
                    }
                }

                long[][] products = Products(nodes, local);
                for (int p = 0; p < parts.Length; p++)
                {
                    long[] product = products[parts[p]];
                    Quaternion q = KeyRotation(product, inverses[p]);
                    if (!float.IsFinite(q.X) || !float.IsFinite(q.Y) || !float.IsFinite(q.Z) || !float.IsFinite(q.W) || MathF.Abs(q.Length() - 1) > UnitTolerance)
                    {
                        throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the turn of {PartName(parts[p])} at frame {f} is not a rotation"));
                    }

                    rotations[p][f] = q;
                    translations[p][f] = ToGltf(product[3] - seat.X, product[7] - seat.Y, product[11] - seat.Z);
                }
            }
        }
        catch (OverflowException)
        {
            throw new DecodingException("a matrix product is too large to hold");
        }

        var channels = parts.Select((node, p) => new ModelChannel(PartName(node), rotations[p], translations[p])).ToArray();
        return new ModelAnimation(BeatName, BeatFramesPerSecond, channels);
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

        long[][] locals = RestLocals(mdb, nodes);
        var painter = new Painter(mdb, cgb);
        var parts = new List<ModelPart>();
        try
        {
            long[][] products = Products(nodes, locals);
            for (int i = 0; i < nodes.Count; i++)
            {
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

    /// <summary>The <c>.MDB</c>'s local matrix of each node of <paramref name="nodes"/>, from the table straight after the last mesh.</summary>
    /// <exception cref="DecodingException">The table runs off the end of the file.</exception>
    private static long[][] RestLocals(byte[] mdb, List<(int Mesh, int Parent)> nodes)
    {
        long matrices = nodes.Max(n => n.Mesh) + (long)MeshHeaderSize;
        if (matrices + nodes.Count * (long)MatrixSize > mdb.Length)
        {
            throw new DecodingException($"the {nodes.Count} matrices after the last mesh run off the end of the file");
        }

        var locals = new long[nodes.Count][];
        for (int i = 0; i < nodes.Count; i++)
        {
            locals[i] = ReadMatrix(mdb, (int)matrices + i * MatrixSize);
        }

        return locals;
    }

    /// <summary>Each node's product of local matrices from the root down; a parent comes before its children in depth-first order.</summary>
    /// <exception cref="OverflowException">A product does not fit.</exception>
    private static long[][] Products(List<(int Mesh, int Parent)> nodes, long[][] locals)
    {
        var products = new long[nodes.Count][];
        for (int i = 0; i < nodes.Count; i++)
        {
            products[i] = nodes[i].Parent < 0 ? locals[i] : Multiply(products[nodes[i].Parent], locals[i]);
        }

        return products;
    }

    /// <summary>A part's name from its node's depth-first index: <c>node07</c>.</summary>
    private static string PartName(int index) => string.Create(CultureInfo.InvariantCulture, $"node{index:D2}");

    /// <summary>
    /// The file offset of each frame's pose part in <paramref name="mtb"/>, from the list of
    /// <paramref name="frames"/> pointers at <paramref name="list"/>, each to <paramref name="size"/> bytes.
    /// </summary>
    /// <exception cref="DecodingException">The list or a pose part is not inside the file.</exception>
    private static int[] FrameList(byte[] mtb, uint list, int frames, int size)
    {
        long at = list - (long)MotionLoadAddress;
        if (at < 0 || at + frames * 4L > mtb.Length)
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"a slot 0 pointer, 0x{list:X8}, is outside the .MTB"));
        }

        var offsets = new int[frames];
        for (int f = 0; f < frames; f++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32BigEndian(mtb.AsSpan((int)at + f * 4));
            long pose = pointer - (long)MotionLoadAddress;
            if (pose < 0 || pose + size > mtb.Length)
            {
                throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"a frame pointer, 0x{pointer:X8}, is outside the .MTB"));
            }

            offsets[f] = (int)pose;
        }

        return offsets;
    }

    /// <summary>
    /// The local matrix of a <c>.MTB</c> record at <paramref name="at"/>, six signed words <c>tx ty tz rx ry rz</c>:
    /// <c>Ry * Rx * Rz</c> in 16.16, angles 4,096 to a turn, and the translation times 16 in whole units.
    /// </summary>
    private static long[] PoseMatrix(byte[] mtb, int at)
    {
        int Word(int i) => BinaryPrimitives.ReadInt16BigEndian(mtb.AsSpan(at + i * 2));
        long[] m = Multiply(Multiply(RotationY(Word(4)), RotationX(Word(3))), RotationZ(Word(5)));
        m[3] = Word(0) * 16L;
        m[7] = Word(1) * 16L;
        m[11] = Word(2) * 16L;
        return m;
    }

    private static long[] RotationX(int angle)
    {
        long c = Cos(angle), s = Sin(angle);
        return new[] { 0x10000, 0, 0, 0, 0, c, -s, 0, 0, s, c, 0L };
    }

    private static long[] RotationY(int angle)
    {
        long c = Cos(angle), s = Sin(angle);
        return new[] { c, 0, s, 0, 0, 0x10000, 0, 0, -s, 0, c, 0L };
    }

    private static long[] RotationZ(int angle)
    {
        long c = Cos(angle), s = Sin(angle);
        return new[] { c, -s, 0, 0, s, c, 0, 0, 0, 0, 0x10000, 0L };
    }

    /// <summary>The sine of <paramref name="angle"/>, 4,096 to a turn, in 16.16, from <see cref="QuarterSine"/>.</summary>
    public static long Sin(int angle)
    {
        int a = angle & 4095;
        int quarter = a >> 10, within = a & 1023;
        long s = quarter is 0 or 2 ? QuarterSine[within] : QuarterSine[1024 - within];
        return quarter < 2 ? s : -s;
    }

    /// <summary>The cosine of <paramref name="angle"/>, 4,096 to a turn, in 16.16.</summary>
    public static long Cos(int angle) => Sin(angle + 1024);

    /// <summary>The sine of 0 to 1,024 steps of 4,096 to a turn, in 16.16, rounded to nearest.</summary>
    private static readonly long[] QuarterSine = BuildQuarterSine();

    /// <summary>
    /// <see cref="QuarterSine"/> by Taylor series in 2.60 fixed point, integers only, so every runtime builds
    /// the same table: no library sine, whose last bit may differ.
    /// </summary>
    private static long[] BuildQuarterSine()
    {
        const int Scale = 60;
        Int128 pi = 3622009729038561421; // π times 2^60.
        var table = new long[1025];
        for (int k = 0; k <= 1024; k++)
        {
            Int128 x = pi * k / 2048;
            Int128 x2 = x * x >> Scale;
            Int128 term = x, sum = x;
            for (int n = 1; term != 0; n += 2)
            {
                term = -(term * x2 >> Scale) / ((n + 1) * (n + 2));
                sum += term;
            }

            table[k] = (long)((sum + ((Int128)1 << (Scale - 17))) >> (Scale - 16));
        }

        return table;
    }

    /// <summary>
    /// The exact inverse of a rest product's turn as its adjugate and determinant, each held as integers
    /// (the adjugate in 32.32, the determinant in 48.48 of 16.16 inputs).
    /// </summary>
    /// <exception cref="DecodingException">The turn has no inverse.</exception>
    /// <exception cref="OverflowException">The adjugate or determinant does not fit.</exception>
    private static (Int128[] Adjugate, Int128 Determinant) Inverse(long[] m, int node)
    {
        Int128 a = m[0], b = m[1], c = m[2], d = m[4], e = m[5], f = m[6], g = m[8], h = m[9], i = m[10];
        Int128[] adj;
        Int128 det;
        checked
        {
            adj = new[]
            {
                e * i - f * h, c * h - b * i, b * f - c * e,
                f * g - d * i, a * i - c * g, c * d - a * f,
                d * h - e * g, b * g - a * h, a * e - b * d,
            };
            det = a * adj[0] + b * adj[3] + c * adj[6];
        }

        if (det == 0 || !Exact(det))
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the rest turn of {PartName(node)} cannot be inverted"));
        }

        return (adj, det);
    }

    /// <summary>Whether an integer converts to a <c>double</c> exactly, so its conversion is the same on every runtime.</summary>
    private static bool Exact(Int128 value) => value > -((Int128)1 << 53) && value < (Int128)1 << 53;

    /// <summary>
    /// A part's key rotation at a frame: its product's turn times the inverse of its rest turn, in glTF's
    /// axes (the half-turn about Y flips the signs that mix Y with X or Z), as a unit quaternion with w not
    /// below 0. Each element is one exact integer over another, divided once; the quaternion comes by the
    /// trace method, with only <c>+ - * /</c> and <see cref="Math.Sqrt"/>.
    /// </summary>
    /// <exception cref="OverflowException">An element is too large to divide exactly.</exception>
    private static Quaternion KeyRotation(long[] product, (Int128[] Adjugate, Int128 Determinant) inverse)
    {
        double det = (long)inverse.Determinant;
        var m = new double[9];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                Int128 sum = checked(product[r * 4] * inverse.Adjugate[c] + product[r * 4 + 1] * inverse.Adjugate[3 + c] + product[r * 4 + 2] * inverse.Adjugate[6 + c]);
                if (!Exact(sum))
                {
                    throw new OverflowException();
                }

                // To glTF's axes: (x, y, z) to (-x, y, -z) on both sides, so an element mixing Y with X or Z changes sign.
                double value = (long)sum / det;
                m[r * 3 + c] = (r == 1) != (c == 1) ? -value : value;
            }
        }

        double m00 = m[0], m01 = m[1], m02 = m[2], m10 = m[3], m11 = m[4], m12 = m[5], m20 = m[6], m21 = m[7], m22 = m[8];
        double x, y, z, w;
        double trace = m00 + m11 + m22;
        if (trace > 0)
        {
            double s = Math.Sqrt(trace + 1) * 2;
            w = s / 4;
            x = (m21 - m12) / s;
            y = (m02 - m20) / s;
            z = (m10 - m01) / s;
        }
        else if (m00 > m11 && m00 > m22)
        {
            double s = Math.Sqrt(1 + m00 - m11 - m22) * 2;
            w = (m21 - m12) / s;
            x = s / 4;
            y = (m01 + m10) / s;
            z = (m02 + m20) / s;
        }
        else if (m11 > m22)
        {
            double s = Math.Sqrt(1 + m11 - m00 - m22) * 2;
            w = (m02 - m20) / s;
            x = (m01 + m10) / s;
            y = s / 4;
            z = (m12 + m21) / s;
        }
        else
        {
            double s = Math.Sqrt(1 + m22 - m00 - m11) * 2;
            w = (m10 - m01) / s;
            x = (m02 + m20) / s;
            y = (m12 + m21) / s;
            z = s / 4;
        }

        double length = Math.Sqrt(x * x + y * y + z * z + w * w);
        double sign = w < 0 ? -1 : 1;

        // Adding 0 turns a negative zero into zero.
        return new Quaternion((float)(sign * x / length) + 0f, (float)(sign * y / length) + 0f, (float)(sign * z / length) + 0f, (float)(sign * w / length) + 0f);
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
        return new ModelPart(PartName(index), ToGltf(t[0], t[1], t[2]), quads);
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
