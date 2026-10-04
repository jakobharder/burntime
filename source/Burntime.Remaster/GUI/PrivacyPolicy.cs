using System.IO;
using System.Linq;
using Burntime.Platform.IO;

namespace Burntime.Remaster.GUI;

// Keep the public policy and the in-game text in sync.
internal static class PrivacyPolicy
{
    public static string[] Read()
    {
        using Stream? stream = typeof(PrivacyPolicy).Assembly.GetManifestResourceStream("Burntime.PrivacyPolicy");
        if (stream == null) return ["Privacy policy unavailable. Contact burntime(at)mailbox.org."];
        using var reader = new StreamReader(stream);
        string[] sections = reader.ReadToEnd().Replace("\r", "").Split("\n---\n");
        string text = sections[FileSystem.LocalizationCode == "de" && sections.Length > 1 ? 1 : 0];
        return text.Split('\n').Select(line => line.StartsWith("# ") ? "#" + line[2..] :
            line.Replace("[burntime(at)mailbox.org](mailto:burntime@mailbox.org)", "burntime(at)mailbox.org")).ToArray();
    }
}
