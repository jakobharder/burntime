namespace Burntime.Platform.IO;

internal sealed class ConfigSectionTemplate
{
    List<ConfigLineTemplate> lines = new List<ConfigLineTemplate>();
    public List<ConfigLineTemplate> Lines
    {
        get { return lines; }
    }

    public String Name
    {
        get { if (lines.Count == 0 || lines[0].Type != ConfigLineType.Section) return ""; return lines[0].Value; }
    }
}
