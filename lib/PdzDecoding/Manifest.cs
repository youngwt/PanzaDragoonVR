using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PdzDecoding;

/// <summary>One file a manifest lists: its asset ID, path relative to the asset folder, bytes and kind.</summary>
public readonly record struct ManifestEntry(string Id, string Path, byte[] Contents, string Kind = Manifest.ImageKind);

/// <summary>
/// The <c>manifest.json</c> extraction writes, as the asset contract's "What extraction writes" fixes
/// it: <c>format</c> 1, <c>generator</c> <c>pdz-extract</c> and <see cref="Extraction.Version"/>,
/// <c>source</c> the disc's product and version, and one entry per file, of kind <c>image</c> or <c>model</c>, sorted by ID in
/// ordinal order, each with its size and SHA-256. The text is what Python's
/// <c>json.dumps(indent=2, ensure_ascii=False)</c> and a final newline give for the same document,
/// which is how <c>tools/manifest.py</c> writes a manifest.
/// </summary>
public static class Manifest
{
    /// <summary>The manifest's name, at the top of the asset folder.</summary>
    public const string FileName = "manifest.json";

    /// <summary>The kind of a PNG.</summary>
    public const string ImageKind = "image";

    /// <summary>The kind of a <c>.glb</c>.</summary>
    public const string ModelKind = "model";

    /// <summary>The contract version written.</summary>
    public const int Format = 1;

    /// <summary>What wrote the manifest: <c>pdz-extract 0.3</c>.</summary>
    public const string Generator = "pdz-extract " + Extraction.Version;

    /// <summary>The bytes of <c>manifest.json</c> for <paramref name="entries"/> from the disc with <paramref name="source"/>: UTF-8, no byte-order mark.</summary>
    public static byte[] Write(SaturnHeader source, IEnumerable<ManifestEntry> entries)
    {
        var text = new StringBuilder();
        text.Append("{\n");
        text.Append("  \"format\": ").Append(Format.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        text.Append("  \"generator\": ").Append(Quote(Generator)).Append(",\n");
        text.Append("  \"source\": {\n");
        text.Append("    \"product\": ").Append(Quote(source.Product)).Append(",\n");
        text.Append("    \"version\": ").Append(Quote(source.Version)).Append('\n');
        text.Append("  },\n");
        ManifestEntry[] sorted = entries.OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        if (sorted.Length == 0)
        {
            text.Append("  \"assets\": []\n");
        }
        else
        {
            text.Append("  \"assets\": [\n");
            for (int i = 0; i < sorted.Length; i++)
            {
                ManifestEntry e = sorted[i];
                text.Append("    {\n");
                text.Append("      \"id\": ").Append(Quote(e.Id)).Append(",\n");
                text.Append("      \"kind\": ").Append(Quote(e.Kind)).Append(",\n");
                text.Append("      \"path\": ").Append(Quote(e.Path)).Append(",\n");
                text.Append("      \"bytes\": ").Append(e.Contents.Length.ToString(CultureInfo.InvariantCulture)).Append(",\n");
                text.Append("      \"sha256\": \"").Append(Convert.ToHexString(SHA256.HashData(e.Contents)).ToLowerInvariant()).Append("\"\n");
                text.Append(i == sorted.Length - 1 ? "    }\n" : "    },\n");
            }

            text.Append("  ]\n");
        }

        text.Append("}\n");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString());
    }

    /// <summary>A JSON string as Python's <c>json.dumps</c> writes it with <c>ensure_ascii=False</c>.</summary>
    internal static string Quote(string value)
    {
        var text = new StringBuilder(value.Length + 2);
        text.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\n': text.Append("\\n"); break;
                case '\r': text.Append("\\r"); break;
                case '\t': text.Append("\\t"); break;
                case '\b': text.Append("\\b"); break;
                case '\f': text.Append("\\f"); break;
                default:
                    if (c < 0x20)
                    {
                        text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        text.Append(c);
                    }

                    break;
            }
        }

        return text.Append('"').ToString();
    }
}
