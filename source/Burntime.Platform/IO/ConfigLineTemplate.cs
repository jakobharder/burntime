namespace Burntime.Platform.IO;

internal enum ConfigLineType
{
    Invalid,
    Empty,
    Section,
    Key
}

internal sealed class ConfigLineTemplate
{
    ConfigLineType type;
    public ConfigLineType Type
    {
        get { return type; }
    }

    String pre;
    String post;

    String key;
    String value;

    public String Key
    {
        get { return key; }
    }

    public String Value
    {
        get { return value; }
    }

    public ConfigLineTemplate(String line)
    {
        type = ConfigLineType.Empty;

        bool hasString = false;
        bool isInsideString = false;
        int length = line.Length;

        value = "";

        char[] buf = line.ToCharArray();
        for (int i = 0; i < line.Length; i++)
        {
            if (isInsideString)
            {
                hasString = true;
                if (buf[i] == '\"')
                {
                    if (i < line.Length - 1 && buf[i + 1] == '\"')
                    {
                        // skip one character
                        i++;
                    }
                    else
                        isInsideString = false;
                }
            }
            else
            {
                if (buf[i] == '\"')
                    isInsideString = true;
                else if (buf[i] == '\'' || buf[i] == ';' || (buf[i] == '/' && i < line.Length - 1 && buf[i + 1] == '/'))
                {
                    length = i;
                    break;
                }
                else if (buf[i] == '[')
                {
                    if (type == ConfigLineType.Key)
                    {
                        type = ConfigLineType.Invalid;
                        break;
                    }

                    type = ConfigLineType.Section;

                    int pos = line.IndexOf("]", i);
                    if (pos == -1)
                        type = ConfigLineType.Invalid;
                    else
                    {
                        pre = line.Substring(0, i + 1);
                        value = line.Substring(i + 1, pos - 1 - i);
                        post = line.Substring(pos);

                        if (value.Length == 0)
                            type = ConfigLineType.Invalid;
                    }
                    break;
                }
                else if (buf[i] == '=')
                {
                    if (hasString)
                    {
                        type = ConfigLineType.Invalid;
                        break;
                    }
                    type = ConfigLineType.Key;

                    String keyString = line.Substring(0, i);
                    keyString = keyString.Trim();

                    if (keyString.Length == 0)
                    {
                        type = ConfigLineType.Invalid;
                        break;
                    }

                    key = keyString;
                    pre = line.Substring(0, i + 1);
                }
            }
        }

        if (isInsideString)
            type = ConfigLineType.Invalid;

        if (type == ConfigLineType.Key)
        {
            String valueString = line.Substring(pre.Length, length - pre.Length);
            valueString = valueString.Trim();
            
            if (valueString.StartsWith("\""))
                valueString = valueString.Substring(1);
            if (valueString.EndsWith("\""))
                valueString = valueString.Substring(0, valueString.Length - 1);

            if (valueString.Length == 0)
            {
                value = "";
                post = line.Substring(pre.Length);
            }
            else
            {
                int pos = line.IndexOf(valueString);
                pre = line.Substring(0, pos);
                value = valueString;
                post = line.Substring(pos + valueString.Length);

                // replace "" to "
                value = value.Replace("\"\"", "\"");
            }
        }
        else if (type == ConfigLineType.Empty)
        {
            pre = line;
            post = "";
        }
    }

    public String MakeLine(String value)
    {
        switch (type)
        {
            case ConfigLineType.Empty:
                return pre;
            case ConfigLineType.Key:
            case ConfigLineType.Section:
                // replace " to ""
                return pre + value.Replace("\"", "\"\"") + post;
            case ConfigLineType.Invalid:
            default:
                return "";
        }
    }

    public override string ToString()
    {
        switch (type)
        {
            case ConfigLineType.Empty:
                return pre;
            case ConfigLineType.Key:
            case ConfigLineType.Section:
                return pre + "$value" + post;
            case ConfigLineType.Invalid:
            default:
                return "invalid";
        }
    }
}
