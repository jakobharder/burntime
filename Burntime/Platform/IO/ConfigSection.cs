/*
 *  Burntime Platform
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

using System;
using System.Collections.Generic;
using System.IO;

namespace Burntime.Platform.IO
{
    public class ConfigSection
    {
        static readonly char[] SplitChars = new char[] { ' ', ',', '\n' };

        static readonly ConfigSection nullSection = new ConfigSection();
        public static ConfigSection NullSection
        {
            get { return nullSection; }
        }

        ConfigSectionTemplate template;
        Dictionary<String, String> values;

        public String Name
        {
            get { if (this == NullSection) return ""; return template.Name; }
        }

        internal ConfigSection()
        {
        }

        internal bool Open(ConfigSectionTemplate template)
        {
            this.template = template;
            values = new Dictionary<string, string>();

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

        public int GetInt(String key)
        {
            if (this == NullSection)
                return 0;

            int res = 0;
            if (!int.TryParse(Get(key), out res))
                return 0;
            return res;
        }

        public bool GetBool(String key)
        {
            String str = Get(key);
            if (str == null || str == "")
                return false;

            if (str.ToLower() == "1" || str.ToLower() == "yes" || str.ToLower() == "true" || str.ToLower() == "on")
                return true;

            return false;
        }

        public Vector2 GetVector2(String key)
        {
            Vector2 v = new Vector2();
            String str = Get(key);
            if (str == null || str == "")
                return v;

            String[] token = str.Split(new char[] { 'x' });
            if (token.Length == 1)
            {
                if (!int.TryParse(token[0], out v.x))
                    return Vector2.Zero;
                return v;
            }
            if (token.Length != 2)
                return v;
            if (!int.TryParse(token[0], out v.x))
                return Vector2.Zero;
            if (!int.TryParse(token[1], out v.y))
                return Vector2.Zero;
            return v;
        }

        public float GetFloat(string key)
        {
            if (this == NullSection)
                return 0;

            float res = 0;
            if (!float.TryParse(Get(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out res))
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
                    return new float[0];

                float[] floats = new float[strs.Length];
                for (int i = 0; i < strs.Length; i++)
                {
                    if (!float.TryParse(strs[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out floats[i]))
                        floats[i] = 0;
                }
                return floats;
            }
            return new float[0];
        }

        public int[] GetInts(string key)
        {
            String str = Get(key);
            if (str != null)
            {
                string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
                if (strs.Length == 1 && strs[0] == "")
                    return new int[0];

                int[] ints = new int[strs.Length];
                for (int i = 0; i < strs.Length; i++)
                {
                    if (!int.TryParse(strs[i], out ints[i]))
                        ints[i] = 0;
                }
                return ints;
            }
            return new int[0];
        }

        public bool[] GetBools(string key)
        {
            String str = Get(key);
            if (str != null)
            {
                string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
                if (strs.Length == 1 && strs[0] == "")
                    return new bool[0];

                bool[] bools = new bool[strs.Length];
                for (int i = 0; i < strs.Length; i++)
                {
                    if ((strs[i].ToLower() == "1" || strs[i].ToLower() == "yes" || strs[i].ToLower() == "true" || strs[i].ToLower() == "on"))
                        bools[i] = true;
                }
                return bools;
            }
            return new bool[0];
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
}
