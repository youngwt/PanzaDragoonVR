using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace PdzDecoding;

/// <summary>
/// Writes a <see cref="Model"/> as one self-contained binary glTF 2.0 file, as the asset contract's
/// Models section says: textures and flat colours packed into atlases (<see cref="Atlas"/>) stored as
/// PNGs in the binary chunk, one nearest/clamp sampler, one material per drawing mode and atlas that
/// some quad uses, each quad four vertices of its own and two triangles, no normals and no vertex
/// colours, one scene with one root node, and no <c>uri</c> anywhere. The same model always gives the
/// same bytes, on every runtime: the PNGs come from <see cref="PngWriter"/>, numbers are written
/// invariantly, and everything is written in the model's own order.
/// </summary>
public static class GlbWriter
{
    private const uint Magic = 0x46546C67;     // "glTF"
    private const uint JsonChunk = 0x4E4F534A; // "JSON"
    private const uint BinChunk = 0x004E4942;  // "BIN\0"

    private const int Float = 5126;
    private const int UnsignedShort = 5123;
    private const int UnsignedInt = 5125;
    private const int ArrayBuffer = 34962;
    private const int ElementArrayBuffer = 34963;

    /// <summary>The largest vertex count a primitive's indices are written as <c>UNSIGNED_SHORT</c> for (65535 itself is the restart value glTF forbids).</summary>
    public const int MaxShortVertices = 65535;

    private static readonly string[] ModeNames = { "opaque", "cutout", "half", "mesh" };

    /// <summary>The <c>.glb</c> file's bytes for <paramref name="model"/>.</summary>
    /// <exception cref="ArgumentException">The model breaks a rule the file needs; the message names the problem on one line.</exception>
    public static byte[] Write(Model model)
    {
        Validate(model);

        var atlas = new Atlas();
        var entries = new int[model.Parts.Count][];
        for (int p = 0; p < model.Parts.Count; p++)
        {
            IReadOnlyList<ModelQuad> quads = model.Parts[p].Quads;
            entries[p] = new int[quads.Count];
            for (int q = 0; q < quads.Count; q++)
            {
                entries[p][q] = quads[q].Texture is ModelTexture texture ? atlas.Add(texture) : atlas.Add(quads[q].Colour);
            }
        }

        (List<AtlasImage> images, AtlasRect[] rects) = atlas.Pack();
        int modes = ModeNames.Length;

        // A material is an atlas and a drawing mode, numbered atlas by atlas in the Transparency table's order.
        var used = new bool[images.Count * modes];
        for (int p = 0; p < model.Parts.Count; p++)
        {
            for (int q = 0; q < entries[p].Length; q++)
            {
                used[MaterialKey(rects[entries[p][q]].Atlas, model.Parts[p].Quads[q].Mode)] = true;
            }
        }

        var materialIndex = new int[used.Length];
        var materials = new List<int>();
        for (int key = 0; key < used.Length; key++)
        {
            materialIndex[key] = used[key] ? materials.Count : -1;
            if (used[key])
            {
                materials.Add(key);
            }
        }

        var bin = new MemoryStream();
        var views = new StringBuilder();
        var accessors = new StringBuilder();
        int viewCount = 0, accessorCount = 0;

        int View(byte[] data, int? target)
        {
            if (viewCount > 0)
            {
                views.Append(',');
            }

            views.Append("{\"buffer\":0,\"byteOffset\":").Append(Int(bin.Length)).Append(",\"byteLength\":").Append(Int(data.Length));
            if (target is int t)
            {
                views.Append(",\"target\":").Append(Int(t));
            }

            views.Append('}');
            bin.Write(data);
            while (bin.Length % 4 != 0)
            {
                bin.WriteByte(0);
            }

            return viewCount++;
        }

        int Accessor(int view, int componentType, int count, string type, string? minMax = null)
        {
            if (accessorCount > 0)
            {
                accessors.Append(',');
            }

            accessors.Append("{\"bufferView\":").Append(Int(view))
                .Append(",\"componentType\":").Append(Int(componentType))
                .Append(",\"count\":").Append(Int(count))
                .Append(",\"type\":\"").Append(type).Append('"');
            if (minMax != null)
            {
                accessors.Append(',').Append(minMax);
            }

            accessors.Append('}');
            return accessorCount++;
        }

        // Geometry: per part, one primitive per material, its quads in the part's order.
        var meshes = new StringBuilder();
        for (int p = 0; p < model.Parts.Count; p++)
        {
            ModelPart part = model.Parts[p];
            if (p > 0)
            {
                meshes.Append(',');
            }

            meshes.Append("{\"name\":").Append(Manifest.Quote(MeshName(model, p))).Append(",\"primitives\":[");
            bool firstPrimitive = true;
            foreach (int key in materials)
            {
                var quads = new List<int>();
                for (int q = 0; q < part.Quads.Count; q++)
                {
                    if (MaterialKey(rects[entries[p][q]].Atlas, part.Quads[q].Mode) == key)
                    {
                        quads.Add(q);
                    }
                }

                if (quads.Count == 0)
                {
                    continue;
                }

                int vertices = quads.Count * 4;
                var positions = new byte[vertices * 12];
                var uvs = new byte[vertices * 8];
                var min = new Vector3(float.PositiveInfinity);
                var max = new Vector3(float.NegativeInfinity);
                for (int k = 0; k < quads.Count; k++)
                {
                    ModelQuad quad = part.Quads[quads[k]];
                    AtlasRect rect = rects[entries[p][quads[k]]];
                    float size = images[rect.Atlas].Size;
                    float left = rect.X / size, right = (rect.X + rect.Width) / size;
                    float top = rect.Y / size, bottom = (rect.Y + rect.Height) / size;
                    if (quad.FlipH)
                    {
                        (left, right) = (right, left);
                    }

                    if (quad.FlipV)
                    {
                        (top, bottom) = (bottom, top);
                    }

                    Vector3[] corners = { quad.A, quad.B, quad.C, quad.D };
                    float[] us = { left, right, right, left };
                    float[] vs = { top, top, bottom, bottom };
                    for (int c = 0; c < 4; c++)
                    {
                        int v = k * 4 + c;
                        WriteFloat(positions, v * 12, corners[c].X);
                        WriteFloat(positions, v * 12 + 4, corners[c].Y);
                        WriteFloat(positions, v * 12 + 8, corners[c].Z);
                        WriteFloat(uvs, v * 8, us[c]);
                        WriteFloat(uvs, v * 8 + 4, vs[c]);
                        min = Vector3.Min(min, corners[c]);
                        max = Vector3.Max(max, corners[c]);
                    }
                }

                bool wide = vertices > MaxShortVertices;
                int indexBytes = wide ? 4 : 2;
                var indices = new byte[quads.Count * 6 * indexBytes];
                for (int k = 0; k < quads.Count; k++)
                {
                    int b = k * 4;
                    int[] triangles = { b, b + 1, b + 2, b, b + 2, b + 3 };
                    for (int i = 0; i < 6; i++)
                    {
                        int at = (k * 6 + i) * indexBytes;
                        if (wide)
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(indices.AsSpan(at), (uint)triangles[i]);
                        }
                        else
                        {
                            BinaryPrimitives.WriteUInt16LittleEndian(indices.AsSpan(at), (ushort)triangles[i]);
                        }
                    }
                }

                int position = Accessor(View(positions, ArrayBuffer), Float, vertices, "VEC3",
                    "\"min\":" + Vec(min) + ",\"max\":" + Vec(max));
                int uv = Accessor(View(uvs, ArrayBuffer), Float, vertices, "VEC2");
                int index = Accessor(View(indices, ElementArrayBuffer), wide ? UnsignedInt : UnsignedShort, quads.Count * 6, "SCALAR");

                if (!firstPrimitive)
                {
                    meshes.Append(',');
                }

                firstPrimitive = false;
                meshes.Append("{\"attributes\":{\"POSITION\":").Append(Int(position))
                    .Append(",\"TEXCOORD_0\":").Append(Int(uv))
                    .Append("},\"indices\":").Append(Int(index))
                    .Append(",\"material\":").Append(Int(materialIndex[key]))
                    .Append(",\"mode\":4}");
            }

            meshes.Append("]}");
        }

        // The atlases, after the geometry.
        var imageJson = new StringBuilder();
        for (int a = 0; a < images.Count; a++)
        {
            int view = View(PngWriter.Encode(images[a].Size, images[a].Size, images[a].Rgba), null);
            imageJson.Append(a > 0 ? "," : "").Append("{\"bufferView\":").Append(Int(view)).Append(",\"mimeType\":\"image/png\"}");
        }

        var json = new StringBuilder();
        json.Append("{\"asset\":{\"generator\":").Append(Manifest.Quote(Manifest.Generator)).Append(",\"version\":\"2.0\"}");
        json.Append(",\"extensionsUsed\":[\"KHR_materials_unlit\"]");
        json.Append(",\"scene\":0,\"scenes\":[{\"nodes\":[0]}]");
        json.Append(",\"nodes\":[");
        if (model.Parts.Count == 1)
        {
            json.Append("{\"name\":").Append(Manifest.Quote(model.Name)).Append(",\"mesh\":0").Append(Translation(model.Parts[0].Origin)).Append('}');
        }
        else
        {
            json.Append("{\"name\":").Append(Manifest.Quote(model.Name)).Append(",\"children\":[");
            for (int p = 0; p < model.Parts.Count; p++)
            {
                json.Append(p > 0 ? "," : "").Append(Int(p + 1));
            }

            json.Append("]}");
            for (int p = 0; p < model.Parts.Count; p++)
            {
                json.Append(",{\"name\":").Append(Manifest.Quote(model.Parts[p].Name))
                    .Append(",\"mesh\":").Append(Int(p)).Append(Translation(model.Parts[p].Origin)).Append('}');
            }
        }

        json.Append("],\"meshes\":[").Append(meshes).Append(']');
        json.Append(",\"materials\":[");
        for (int m = 0; m < materials.Count; m++)
        {
            json.Append(m > 0 ? "," : "").Append(Material(materials[m] / modes, materials[m] % modes));
        }

        json.Append(']');
        json.Append(",\"textures\":[");
        for (int a = 0; a < images.Count; a++)
        {
            json.Append(a > 0 ? "," : "").Append("{\"sampler\":0,\"source\":").Append(Int(a)).Append('}');
        }

        json.Append("],\"samplers\":[{\"magFilter\":9728,\"minFilter\":9728,\"wrapS\":33071,\"wrapT\":33071}]");
        json.Append(",\"images\":[").Append(imageJson).Append(']');
        json.Append(",\"accessors\":[").Append(accessors).Append(']');
        json.Append(",\"bufferViews\":[").Append(views).Append(']');
        json.Append(",\"buffers\":[{\"byteLength\":").Append(Int(bin.Length)).Append("}]}");

        return Glb(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json.ToString()), bin.ToArray());
    }

    private static int MaterialKey(int atlas, DrawingMode mode) => atlas * ModeNames.Length + (int)mode;

    private static string MeshName(Model model, int part) => model.Parts.Count == 1 ? model.Name : model.Parts[part].Name;

    /// <summary>A material of the Transparency table: <c>opaque</c>, <c>cutout</c>, <c>half</c> or <c>mesh</c>, with <c>_2</c> and up for later atlases.</summary>
    private static string Material(int atlas, int mode)
    {
        string name = ModeNames[mode] + (atlas == 0 ? "" : "_" + Int(atlas + 1));
        var text = new StringBuilder();
        text.Append("{\"name\":").Append(Manifest.Quote(name)).Append(",\"pbrMetallicRoughness\":{");
        if ((DrawingMode)mode is DrawingMode.Half or DrawingMode.Mesh)
        {
            text.Append("\"baseColorFactor\":[1,1,1,0.5],");
        }

        text.Append("\"baseColorTexture\":{\"index\":").Append(Int(atlas)).Append("},\"metallicFactor\":0}");
        text.Append((DrawingMode)mode switch
        {
            DrawingMode.Opaque => ",\"alphaMode\":\"OPAQUE\"",
            DrawingMode.Cutout => ",\"alphaMode\":\"MASK\",\"alphaCutoff\":0.5",
            _ => ",\"alphaMode\":\"BLEND\"",
        });
        text.Append(",\"doubleSided\":true,\"extensions\":{\"KHR_materials_unlit\":{}}}");
        return text.ToString();
    }

    private static string Translation(Vector3 origin) =>
        origin == Vector3.Zero ? "" : ",\"translation\":" + Vec(origin);

    private static string Vec(Vector3 v) => "[" + Num(v.X) + "," + Num(v.Y) + "," + Num(v.Z) + "]";

    /// <summary>A float as the exact <c>double</c> it is, shortest round-trip, invariant: a reader comparing in either precision agrees.</summary>
    private static string Num(float value) => ((double)value).ToString("R", CultureInfo.InvariantCulture);

    private static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static void WriteFloat(byte[] data, int at, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(at), value);

    /// <summary>The GLB container: header, then the JSON chunk padded with spaces and the BIN chunk padded with zeros, each to four bytes.</summary>
    private static byte[] Glb(byte[] json, byte[] bin)
    {
        int jsonPadded = (json.Length + 3) / 4 * 4;
        int binPadded = (bin.Length + 3) / 4 * 4;
        var file = new byte[12 + 8 + jsonPadded + 8 + binPadded];
        Span<byte> span = file;
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)file.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)jsonPadded);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], JsonChunk);
        json.CopyTo(span[20..]);
        span.Slice(20 + json.Length, jsonPadded - json.Length).Fill((byte)' ');
        int b = 20 + jsonPadded;
        BinaryPrimitives.WriteUInt32LittleEndian(span[b..], (uint)binPadded);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(b + 4)..], BinChunk);
        bin.CopyTo(span[(b + 8)..]);
        return file;
    }

    // ------------------------------------------------------------ checking the model

    private static void Validate(Model model)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model), "no model was given");
        }

        CheckName(model.Name, "the model's name");
        if (model.Parts == null || model.Parts.Count == 0)
        {
            throw Bad("the model has no parts");
        }

        var names = new HashSet<string>(StringComparer.Ordinal) { model.Name };
        for (int p = 0; p < model.Parts.Count; p++)
        {
            ModelPart part = model.Parts[p] ?? throw Bad($"part {p} is missing");
            CheckName(part.Name, $"part {p}'s name");
            if (model.Parts.Count > 1 && !names.Add(part.Name))
            {
                throw Bad($"part {p}'s name '{part.Name}' is already used in the model");
            }

            string where = $"part '{part.Name}'";
            CheckFinite(part.Origin, $"{where}'s origin");
            if (part.Quads == null || part.Quads.Count == 0)
            {
                throw Bad($"{where} has no quads");
            }

            for (int q = 0; q < part.Quads.Count; q++)
            {
                string at = $"{where} quad {q}";
                ModelQuad quad = part.Quads[q] ?? throw Bad($"{at} is missing");
                CheckFinite(quad.A, $"{at} corner A");
                CheckFinite(quad.B, $"{at} corner B");
                CheckFinite(quad.C, $"{at} corner C");
                CheckFinite(quad.D, $"{at} corner D");
                if (!Enum.IsDefined(quad.Mode))
                {
                    throw Bad($"{at} has drawing mode {(int)quad.Mode}, which is not one of opaque, cutout, half and mesh");
                }

                if (quad.Texture is ModelTexture texture)
                {
                    if (texture.Width < 1 || texture.Height < 1 || texture.Width > Atlas.MaxTexture || texture.Height > Atlas.MaxTexture)
                    {
                        throw Bad($"{at} has a {texture.Width} x {texture.Height} texture; a texture is 1 to {Atlas.MaxTexture} pixels each way");
                    }

                    long expected = (long)texture.Width * texture.Height * 4;
                    if (texture.Rgba == null || texture.Rgba.Length != expected)
                    {
                        throw Bad($"{at} has a {texture.Width} x {texture.Height} texture of {texture.Rgba?.Length ?? 0} bytes; it needs {expected}");
                    }
                }
            }
        }
    }

    private static void CheckName(string? name, string what)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw Bad($"{what} is empty");
        }

        foreach (char c in name)
        {
            if (!(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
            {
                string shown = string.Concat((name.Length > 40 ? name[..37] + "..." : name).Select(x => char.IsControl(x) ? ' ' : x));
                throw Bad($"{what} '{shown}' is not only lowercase letters, digits and underscores");
            }
        }
    }

    private static void CheckFinite(Vector3 v, string what)
    {
        if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))
        {
            throw Bad($"{what} is not a finite position");
        }
    }

    private static ArgumentException Bad(string problem) => new(problem);
}
