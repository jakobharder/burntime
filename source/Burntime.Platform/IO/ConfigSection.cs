﻿using System.Globalization;

namespace Burntime.Platform.IO;

public class ConfigSection
{
    static readonly char[] SplitChars = new char[] { ' ', ',', '\n' };

    static readonly ConfigSection nullSection = new ConfigSection();
    public static ConfigSection NullSection
    {
        get { return nullSection; }
    }

    ConfigSectionTemplate template;
    Dictionary<String, String> values = new();

    public String Name
    {
        get { if (this == NullSection) return ""; return template.Name; }
    }

    internal ConfigSection()
    {
        template = new ConfigSectionTemplate();
    }

    internal bool Open(ConfigSectionTemplate template)
    {
        this.template = template;
        values.Clear();

        foreach (ConfigLineTemplate line in template.Lines)
        {
            if (line.Type == ConfigLineType.Key)
            {
                if (values.ContainsKey(line.Key.ToLower()))
                {
                    // if the key is already read, read this one as a next line
                    values[line.Key.ToLower()] += "\n" + line.Value;
                }
                else
                    values.Add(line.Key.ToLower(), line.Value);
            }
        }

        return true;
    }

    internal bool Save(Stream stream)
    {
        StreamWriter writer = new StreamWriter(stream);

        Dictionary<string, int> multilineCounter = new Dictionary<string,int>();

        List<string> stored = new List<string>();

        foreach (ConfigLineTemplate line in template.Lines)
        {
            if (line.Type == ConfigLineType.Key)
            {
                string[] lines = values[line.Key].Split('\n');
                int index = 0;

                if (!multilineCounter.ContainsKey(line.Key))
                {
                    // if first line add counter
                    multilineCounter.Add(line.Key, 0);
                }
                else
                {
                    // if not first, then increase line counter
                    index = multilineCounter[line.Key];
                    index++;
                    multilineCounter[line.Key] = index;
                }

                // select line
                string v = "";
                if (lines.Length > index)
                    v = lines[index];

                // store line
                writer.WriteLine(line.MakeLine(v));

                if (!stored.Contains(line.Key.ToLower()))
                    stored.Add(line.Key.ToLower());
            }
            else
                writer.WriteLine(line.MakeLine(line.Value));
        }

        // check and save new keys
        foreach (string key in values.Keys)
        {
            if (!stored.Contains(key.ToLower()))
            {
                writer.WriteLine(key + "=" + values[key]);
            }
        }

        writer.Flush();
        return true;
    }

    public bool ContainsKey(string key)
    {
        if (this == NullSection)
            return false;

        return values.ContainsKey(key.ToLower());
    }

    public String Get(String key)
    {
        if (this == NullSection)
            return "";

        if (values.ContainsKey(key.ToLower()))
            return values[key.ToLower()];
        return "";
    }

    public String GetString(String key)
    {
        if (this == NullSection)
            return "";

        if (values.ContainsKey(key.ToLower()))
            return values[key.ToLower()];
        return "";
    }

    public Version GetVersion(string key)
    {
        if (this == NullSection)
            return new Version();

        Version version = new Version();
        if (values.ContainsKey(key.ToLower()))
        {
            try
            {
                string str = values[key.ToLower()].Trim();

                // make valid version number
                if (str == "0" || str == "")
                    str = "0.0";

                version = new Version(str);
            }
            catch
            {
                return new Version();
            }
        }

        return version;
    }

    public String[] GetStrings(String key)
    {
        String str = Get(key);
        if (str != null)
        {
            string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 1 && strs[0] == "")
                return new string[0];
            return strs;
        }
        return new string[0];
    }

    public IEnumerable<KeyValuePair<string, string>> Values => values;

    public int GetInt(String key)
    {
        if (this == NullSection)
            return 0;

        int res = 0;
        if (!int.TryParse(Get(key), out res))
            return 0;
        return res;
    }

    public bool GetBool(string key, bool defaultResult = false)
    {
        string? str = Get(key)?.ToLower();
        if (string.IsNullOrWhiteSpace(str))
            return defaultResult;

        if (str == "1" || str == "yes" || str == "true" || str == "on")
            return true;
        if (defaultResult && (str != "0" && str != "no" && str != "false" && str != "off"))
            return true;

        return false;
    }

    public Vector2 GetVector2(string key) => GetVector2(key, Vector2.Zero);
    public Vector2f GetVector2f(string key) => GetVector2f(key, Vector2f.Zero);

    public Vector2 GetVector2(string key, Vector2 defaultValue)
    {
        Vector2 v = new();
        string str = Get(key);
        if (string.IsNullOrEmpty(str))
            return defaultValue;

        string[] token = str.Split(new char[] { 'x' });
        if (token.Length == 1)
        {
            if (!int.TryParse(token[0], out v.x))
                return defaultValue;
            return v;
        }
        if (token.Length != 2)
            return v;
        if (!int.TryParse(token[0], out v.x))
            return defaultValue;
        if (!int.TryParse(token[1], out v.y))
            return defaultValue;
        return v;
    }

    public Vector2f GetVector2f(string key, Vector2f defaultValue)
    {
        Vector2f v = new();
        string str = Get(key);
        if (string.IsNullOrEmpty(str))
            return defaultValue;

        string[] token = str.Split(new char[] { 'x' });
        if (token.Length == 1)
        {
            if (!float.TryParse(token[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x))
                return defaultValue;
            return v;
        }
        if (token.Length != 2)
            return v;
        if (!float.TryParse(token[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v.x))
            return defaultValue;
        if (!float.TryParse(token[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v.y))
            return defaultValue;
        return v;
    }

    public float GetFloat(string key)
    {
        if (this == NullSection)
            return 0;

        float res = 0;
        if (!float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out res))
            return 0;
        return res;
    }

    public float[] GetFloats(string key)
    {
        String str = Get(key);
        if (str != null)
        {
            string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 1 && strs[0] == "")
                return Array.Empty<float>();

            float[] floats = new float[strs.Length];
            for (int i = 0; i < strs.Length; i++)
            {
                if (!float.TryParse(strs[i], NumberStyles.Float, CultureInfo.InvariantCulture, out floats[i]))
                    floats[i] = 0;
            }
            return floats;
        }
        return Array.Empty<float>();
    }

    public int[] GetInts(string key)
    {
        String str = Get(key);
        if (str != null)
        {
            string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 1 && strs[0] == "")
                return Array.Empty<int>();

            int[] ints = new int[strs.Length];
            for (int i = 0; i < strs.Length; i++)
            {
                if (!int.TryParse(strs[i], out ints[i]))
                    ints[i] = 0;
            }
            return ints;
        }
        return Array.Empty<int>();
    }

    public bool[] GetBools(string key)
    {
        String str = Get(key);
        if (str != null)
        {
            string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 1 && strs[0] == "")
                return Array.Empty<bool>();

            bool[] bools = new bool[strs.Length];
            for (int i = 0; i < strs.Length; i++)
            {
                if ((strs[i].ToLower() == "1" || strs[i].ToLower() == "yes" || strs[i].ToLower() == "true" || strs[i].ToLower() == "on"))
                    bools[i] = true;
            }
            return bools;
        }
        return Array.Empty<bool>();
    }

    public Vector2[] GetVector2s(string key)
    {
        String str = Get(key);
        if (str != null)
        {
            string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 1 && strs[0] == "")
                return new Vector2[0];

            Vector2[] vectors = new Vector2[strs.Length];
            for (int i = 0; i < strs.Length; i++)
            {
                String[] token = strs[i].Split(new char[] { 'x' });
                if (token.Length == 1)
                {
                    if (!int.TryParse(token[0], out vectors[i].x))
                        vectors[i] = Vector2.Zero;
                }
                else if (token.Length != 2)
                {
                    vectors[i] = Vector2.Zero;
                }
                else
                {
                    if (!int.TryParse(token[0], out vectors[i].x))
                        vectors[i] = Vector2.Zero;
                    else if (!int.TryParse(token[1], out vectors[i].y))
                        vectors[i] = Vector2.Zero;
                }
            }
            return vectors;
        }
        return new Vector2[0];
    }

    public void Set(String key, String value)
    {
        if (this == NullSection)
            return;

        if (values.ContainsKey(key.ToLower()))
            values[key.ToLower()] = value;
        else
            values.Add(key.ToLower(), value);
    }

    public void Set(String key, String[] value)
    {
        if (this == NullSection)
            return;

        String v = "";
        if (value.Length > 0)
            v += value[0];
        for (int i = 1; i < value.Length; i++)
            v += " " + value[i];

        if (values.ContainsKey(key.ToLower()))
            values[key.ToLower()] = v;
        else
            values.Add(key.ToLower(), v);
    }

    public void Set(String key, int value)
    {
        if (this == NullSection)
            return;

        if (values.ContainsKey(key.ToLower()))
            values[key.ToLower()] = value.ToString();
        else
            values.Add(key.ToLower(), value.ToString());
    }

    public void Set(String key, bool value)
    {
        if (this == NullSection)
            return;

        if (values.ContainsKey(key.ToLower()))
            values[key.ToLower()] = value ? "on" : "off";
        else
            values.Add(key.ToLower(), value ? "on" : "off");
    }

    public void Set(String key, Vector2 value)
    {
        if (this == NullSection)
            return;

        if (values.ContainsKey(key.ToLower()))
            values[key.ToLower()] = value.x.ToString() + "x" + value.y.ToString();
        else
            values.Add(key.ToLower(), value.x.ToString() + "x" + value.y.ToString());
    }
}
