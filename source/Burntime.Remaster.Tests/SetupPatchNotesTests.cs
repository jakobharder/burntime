using System.Collections.Generic;
using Burntime.Remaster.GUI;

namespace Burntime.Remaster.Tests;

using static Program;

static class SetupPatchNotesTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("patch notes combine release and category headings", 0, () =>
        {
            string[] lines = SetupPatchNotes.Format("# Changelog\n\n## [Unreleased]\n\n### Changes\n\n- New feature\n\n### Fixes\n\n- Fixed bug");
            Equal("#Unreleased: Changes|- New feature||#Unreleased: Fixes|- Fixed bug",
                string.Join('|', lines), "compact release headings");
            return 0;
        });
        yield return Int("patch notes preserve releases without categories", 0, () =>
        {
            string[] lines = SetupPatchNotes.Format("# Changelog\n## 1.0\n\n- Old change\n## 0.9\n- Older change");
            Equal("#1.0|- Old change|#0.9|- Older change",
                string.Join('|', lines), "historical release headings");
            return 0;
        });
    }
}
