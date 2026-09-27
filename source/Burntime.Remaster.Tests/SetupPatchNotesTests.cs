using System.Collections.Generic;
using Burntime.Remaster.GUI;

namespace Burntime.Remaster.Tests;

using static Program;

static class SetupPatchNotesTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("setup notes default to Ctrl instead of F1", 0, () =>
        {
            var bindings = new KeyboardBindings();
            bindings.Load(new Burntime.Platform.IO.ConfigFile());
            Equal(Burntime.Framework.InputAction.SetupNotes,
                bindings.GetAction(new Burntime.Platform.Key(Burntime.Platform.SystemKey.Ctrl)),
                "Ctrl opens notes");
            Equal(Burntime.Framework.InputAction.None,
                bindings.GetAction(new Burntime.Platform.Key(Burntime.Platform.SystemKey.F1)),
                "F1 no longer opens notes by default");
            Equal(new Burntime.Platform.Key(Burntime.Platform.SystemKey.Ctrl),
                bindings.GetControls(Burntime.Framework.InputAction.SetupNotes)[0],
                "prompt uses Ctrl");
            return 0;
        });
        yield return Int("patch notes preserve release and small category headings", 0, () =>
        {
            string[] lines = SetupPatchNotes.Format("# Changelog\n\n## [Unreleased]\n\n### Changes\n\n- New feature\n\n### Fixes\n\n- Fixed bug");
            Equal("#Unreleased|##Changes|- New feature||##Fixes|- Fixed bug",
                string.Join('|', lines), "distinct heading levels");
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
