
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;

namespace Burntime.Platform.IO
{
    public class ConfigFile
    {
        Dictionary<string, ConfigSection> sections;
        List<ConfigSection> order;

        public bool Open(String name)
        {
            Burntime.Platform.IO.File file = FileSystem.GetFile(name);
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

            sections = new Dictionary<string,ConfigSection>();
            order = new List<ConfigSection>();
            ConfigSectionTemplate currentTemplate = new ConfigSectionTemplate();

            List<ConfigLineTemplate> lines = new List<ConfigLineTemplate>();

            StreamReader reader = new StreamReader(stream);
            String line;
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

        public bool Save(String name)
        {
            Burntime.Platform.IO.File file = FileSystem.CreateFile(name);
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

        public ConfigSection GetSection(String name)
        {
            if (!sections.ContainsKey(name.ToLower()))
                return ConfigSection.NullSection;

            return sections[name.ToLower()];
        }

        public ConfigSection this[String name]
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
}
