namespace Burntime.Platform.IO;

public class ConfigFile
{
    readonly Dictionary<string, ConfigSection> sections = new();
    readonly List<ConfigSection> order = new();

    public bool Open(string name)
    {
        File file = FileSystem.GetFile(name);
        if (file == null)
            return false;
        bool result = Open(file.Stream);
        file.Close();
        return result;
    }

    public bool Open(Stream stream)
    {
        if (stream == null)
            return false;

        sections.Clear();
        order.Clear();
        ConfigSectionTemplate currentTemplate = new ConfigSectionTemplate();

        var reader = new StreamReader(stream);
        string? line;
        while (null != (line = reader.ReadLine()))
        {
            ConfigLineTemplate templateLine = new ConfigLineTemplate(line);
            //if (templateLine.Type == ConfigLineType.Invalid)
            //    return false;
            if (templateLine.Type != ConfigLineType.Section)
                currentTemplate.Lines.Add(templateLine);
            else
            {
                if (sections.ContainsKey(currentTemplate.Name.ToLower()))
                    return false;
                ConfigSection current = new ConfigSection();
                if (!current.Open(currentTemplate))
                    return false;
                sections.Add(currentTemplate.Name.ToLower(), current);
                order.Add(current);
                currentTemplate = new ConfigSectionTemplate();
                currentTemplate.Lines.Add(templateLine);
            }
        }

        if (sections.ContainsKey(currentTemplate.Name.ToLower()))
            return false;
        ConfigSection last = new ConfigSection();
        if (!last.Open(currentTemplate))
            return false;
        sections.Add(currentTemplate.Name.ToLower(), last);
        order.Add(last);

        stream.Close();
        return true;
    }

    public bool Save(string name)
    {
        File file = FileSystem.CreateFile(name);
        if (file == null)
            return false; 

        bool result = Save(file.Stream);
        file.Close();
        return result;
    }

    public bool Save(Stream stream)
    {
        if (stream == null)
            return false;

        foreach (ConfigSection section in order)
        {
            if (!section.Save(stream))
            {
                stream.Close();
                return false;
            }
        }

        stream.Close();
        return true;
    }

    public ConfigSection GetSection(string name, bool add = false)
    {
        if (!sections.ContainsKey(name.ToLower()))
        {
            if (add)
            {
                var section = new ConfigSection();
                sections.Add(name.ToLower(), section);
                order.Add(section);
                return section;
            }
            else
                return ConfigSection.NullSection;
        }

        return sections[name.ToLower()];
    }

    public ConfigSection this[string name]
    {
        get
        {
            if (sections == null || !sections.ContainsKey(name.ToLower()))
                return ConfigSection.NullSection;

            return sections[name.ToLower()];
        }
    }

    public ConfigSection[] GetAllSections()
    {
        List<ConfigSection> list = new List<ConfigSection>();
        foreach (ConfigSection sec in sections.Values)
            list.Add(sec);
        return list.ToArray();
    }
}
