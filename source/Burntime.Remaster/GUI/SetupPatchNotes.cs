using System;
using System.IO;
using System.Collections.Generic;

namespace Burntime.Remaster.GUI;

// The release changelog remains the only source of patch notes.
internal static class SetupPatchNotes
{
    static readonly Lazy<string[]> lines = new(() =>
    {
        using Stream? stream = typeof(SetupPatchNotes).Assembly
            .GetManifestResourceStream("Burntime.Changelog");
        if (stream == null)
            return ["Patch notes are unavailable."];
        using var reader = new StreamReader(stream);
        return Format(reader.ReadToEnd());
    });

    internal static string[] Format(string markdown)
    {
        var result = new List<string>();
        foreach (string raw in markdown.Split('\n'))
        {
            string line = raw.Trim().TrimStart('\uFEFF');
            if (line.StartsWith("# "))
                continue;
            if (line.StartsWith("## "))
            {
                result.Add("#" + line[3..].Replace("[", "").Replace("]", ""));
                continue;
            }
            if (line.StartsWith("### "))
            {
                line = "##" + line[4..];
            }
            else if (line.StartsWith('#'))
                line = "#" + line.TrimStart('#', ' ');
            if (line.Length == 0 && (result.Count == 0 || result[^1].Length == 0 || result[^1].StartsWith('#')))
                continue;
            result.Add(line);
        }
        return result.ToArray();
    }

    public static string[] Read() => lines.Value;
}
