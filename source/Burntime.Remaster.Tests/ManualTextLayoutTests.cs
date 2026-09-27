using System.Collections.Generic;
using Burntime.Remaster.GUI;

namespace Burntime.Remaster.Tests;

using static Program;

static class ManualTextLayoutTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("manual reflows paragraphs for active font metrics", 0, () =>
        {
            string[] paragraphs = ["one two three four"];
            Equal("one two|three|four", string.Join('|',
                ManualTextLayout.Wrap(paragraphs, 8, text => text.Length, text => text.Length)),
                "regular font wrap");
            Equal("one two three|four", string.Join('|',
                ManualTextLayout.Wrap(paragraphs, 8, text => (text.Length + 1) / 2, text => text.Length)),
                "smaller font fits more words");
            return 0;
        });
        yield return Int("manual preserves headings paragraphs and illustration directives", 0, () =>
        {
            string[] paragraphs = ["#ONE TWO", "one two", "", "@construction|item_rat_trap", "@extended-only-end"];
            Equal("#ONE|#TWO|one two||@construction|item_rat_trap|@extended-only-end",
                string.Join('|', ManualTextLayout.Wrap(paragraphs, 7,
                    text => text.Length, text => text.Length * 2)), "structural boundaries");
            return 0;
        });
    }
}
