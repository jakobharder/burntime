using System;
using System.Collections.Generic;

namespace Burntime.Remaster.GUI;

internal static class ManualTextLayout
{
    // Each resource line is a paragraph. Empty lines and illustration directives
    // are explicit boundaries; only rendered text is wrapped.
    internal static string[] Wrap(IEnumerable<string> paragraphs, int width,
        Func<string, int> measureText, Func<string, int> measureHeading)
    {
        var result = new List<string>();
        foreach (string paragraph in paragraphs)
        {
            if (paragraph.Length == 0 || paragraph.StartsWith('@'))
            {
                result.Add(paragraph);
                continue;
            }

            bool heading = paragraph.StartsWith('#');
            bool subheading = paragraph.StartsWith("##");
            string prefix = subheading ? "##" : heading ? "#" : "";
            Func<string, int> measure = heading && !subheading ? measureHeading : measureText;
            string line = "";
            foreach (string word in paragraph[prefix.Length..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string next = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && measure(next) > width)
                {
                    result.Add(prefix + line);
                    line = word;
                }
                else
                    line = next;
            }
            result.Add(prefix + line);
        }
        return result.ToArray();
    }
}
