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
