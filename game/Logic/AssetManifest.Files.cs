#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PanzaDragoonVR.Logic;

// Looking up an ID for the game: Find, the checks on its file, and whether its path stays inside
// the asset folder once symbolic links are followed.

/// <summary>What came of looking up one asset ID.</summary>
public enum AssetLookupStatus
{
    /// <summary>The entry and its file passed every check; <see cref="AssetLookup.FullPath"/> is the file.</summary>
    Found,

    /// <summary>The manifest does not list the ID, or there is no manifest. The normal state; nothing to report.</summary>
    NotListed,

    /// <summary>The ID is listed but cannot be used; <see cref="AssetLookup.Problem"/> says why.</summary>
    Problem,
}

/// <summary>The answer for one asset ID: the file to load, or why the placeholder is used.</summary>
public readonly record struct AssetLookup(AssetLookupStatus Status, string? FullPath, string? Problem)
{
    public static AssetLookup NotListed() => new(AssetLookupStatus.NotListed, null, null);

    public static AssetLookup Fail(string problem) => new(AssetLookupStatus.Problem, null, problem);
}

public sealed partial class AssetManifest
{
    /// <summary>
    /// Looks up <paramref name="id"/> for the game, which expects an asset of <paramref name="expectedKind"/>,
    /// and checks its file: present, a file, inside the folder, of the stated size and, for an image,
    /// a PNG of 8 bits per channel, RGB or RGBA; for a model, a self-contained <c>.glb</c> of glTF 2.0
    /// with a mesh and no required extensions (<see cref="GlbCheck"/>).
    /// </summary>
    public AssetLookup Find(string id, string expectedKind)
    {
        if (_rejected.TryGetValue(id, out string? reason))
        {
            return AssetLookup.Fail(reason);
        }

        if (!_entries.TryGetValue(id, out Entry? entry))
        {
            return AssetLookup.NotListed();
        }

        string where = $"{id}: {entry.Path}";
        if (entry.Kind != expectedKind)
        {
            return AssetLookup.Fail($"{id}: wrong kind: the manifest says '{entry.Kind}', the game needs '{expectedKind}'");
        }

        string full = System.IO.Path.Combine(Folder, entry.Path);
        try
        {
            if (!LeadsInside(Folder, entry.Path))
            {
                return AssetLookup.Fail($"{id}: invalid entry: its path leaves the asset folder");
            }

            if (Directory.Exists(full))
            {
                return AssetLookup.Fail($"{where}: missing: the path is not a file");
            }

            var file = new FileInfo(full);
            if (!file.Exists)
            {
                return AssetLookup.Fail($"{where}: missing");
            }

            if (file.Length != entry.Bytes)
            {
                return AssetLookup.Fail($"{where}: incomplete or changed: the file is {file.Length} bytes, the manifest says {entry.BytesText}");
            }

            if (expectedKind == ImageKind)
            {
                byte[] head = new byte[26];
                int read;
                using (FileStream stream = file.OpenRead())
                {
                    read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
                }

                string? notPng = PngProblem(head.AsSpan(0, read));
                if (notPng != null)
                {
                    return AssetLookup.Fail($"{where}: unreadable image: {notPng}");
                }
            }
            else if (expectedKind == ModelKind)
            {
                // Its header and JSON chunk, by the contract's "When a model is invalid" table.
                // Nothing the model names is opened.
                GlbCheckResult model = GlbCheck.Check(File.ReadAllBytes(full));
                if (!model.IsUsable)
                {
                    return AssetLookup.Fail(model.Line(id, entry.Path)!);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return AssetLookup.Fail($"{where}: cannot be read: {e.Message}");
        }

        return new AssetLookup(AssetLookupStatus.Found, full, null);
    }

    /// <summary>
    /// Whether <paramref name="path"/>, which already keeps the path rules, still ends inside
    /// <paramref name="folder"/> once every symbolic link along it is followed, as the
    /// <c>os.path.realpath</c> comparison in <c>tools/manifest.py</c> does.
    /// </summary>
    private static bool LeadsInside(string folder, string path)
    {
        string? root = RealPath(System.IO.Path.GetFullPath(folder));
        string? real = root == null ? null : RealPath(root + "/" + path);
        if (root == null || real == null)
        {
            return false;
        }

        string withSlash = root.EndsWith('/') ? root : root + "/";
        return real.StartsWith(withSlash, StringComparison.Ordinal);
    }

    /// <summary>
    /// The absolute path <paramref name="path"/> leads to, with every symbolic link in every part
    /// followed, and each <c>..</c> taken after the link before it is followed, as the file system does.
    /// Parts that do not exist are kept as written. Null after too many links, as for a loop.
    /// Paths are POSIX paths, as on Android and the PC.
    /// </summary>
    private static string? RealPath(string path)
    {
        const int maxLinks = 40;
        var resolved = new List<string>();
        var pending = new LinkedList<string>(path.Split('/'));
        int links = 0;
        while (pending.First != null)
        {
            string part = pending.First.Value;
            pending.RemoveFirst();
            if (part is "" or ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (resolved.Count > 0)
                {
                    resolved.RemoveAt(resolved.Count - 1);
                }

                continue;
            }

            string here = "/" + string.Join('/', resolved.Append(part));
            string? target = new FileInfo(here).LinkTarget;
            if (target == null)
            {
                resolved.Add(part);
                continue;
            }

            if (++links > maxLinks)
            {
                return null;
            }

            // The link's target replaces it; a relative one is taken from the link's own folder.
            if (target.StartsWith('/'))
            {
                resolved.Clear();
            }

            string[] targetParts = target.Split('/');
            for (int i = targetParts.Length - 1; i >= 0; i--)
            {
                pending.AddFirst(targetParts[i]);
            }
        }

        return "/" + string.Join('/', resolved);
    }
}
