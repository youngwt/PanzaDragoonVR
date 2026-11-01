using System;
using System.IO;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>Finds the repository from the test build's folder.</summary>
internal static class Repository
{
    /// <summary>The repository's root: the nearest folder above the test's output that holds <c>game/DiscExtraction.cs</c>.</summary>
    public static string Root()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "game", "DiscExtraction.cs")))
            {
                return d.FullName;
            }
        }

        throw new InvalidOperationException("the repository's root was not found above " + AppContext.BaseDirectory);
    }
}
