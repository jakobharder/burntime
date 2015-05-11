
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2011 Jakob Harder
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
*/
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Burntime.Launcher.Update.Xsd;

namespace Burntime.Launcher.Update
{
    /// <summary>
    /// Module type enumaration.
    /// </summary>
    enum ModuleType
    {
        Other,
        Game,
        Extra
    }

    /// <summary>
    /// Module update file path.
    /// </summary>
    class ModuleUpdateFilePath
    {
        #region private attributes
        private Version version;
        private string module;
        private string file;
        #endregion

        #region public attributes
        /// <summary>
        /// File version.
        /// </summary>
        public Version Version { get { return version; } }

        /// <summary>
        /// Base module name.
        /// </summary>
        public string Module { get { return module; } }

        /// <summary>
        /// File name.
        /// </summary>
        public string File { get { return file; } }

        /// <summary>
        /// Full file path.
        /// </summary>
        public string FullPath { get { return module + "/" + version.ToString() + "/" + Path.GetFileName(file); } }
        #endregion

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="version">file version</param>
        /// <param name="module">base module name</param>
        /// <param name="file">file name</param>
        public ModuleUpdateFilePath(Version version, string module, string file)
        {
            this.version = version;
            this.module = module;
            this.file = file;
        }
    }

    /// <summary>
    /// Holds one update version.
    /// </summary>
    class ModuleUpdateInfoItem
    {
        #region private attributes
        private Version version;
        private Version @base;
        private bool savegameCompatible;
        private ModuleUpdateFilePath[] add;
        private ModuleUpdateFilePath[] remove;
        #endregion

        #region public attributes
        /// <summary>
        /// Version of this update item.
        /// </summary>
        public Version Version { get { return version; } }

        /// <summary>
        /// Base version to update.
        /// </summary>
        public Version Base { get { return @base; } }

        /// <summary>
        /// Update is savegame compatible flag.
        /// </summary>
        public bool SavegameCompatible { get { return savegameCompatible; } }

        /// <summary>
        /// Files to add.
        /// </summary>
        public ModuleUpdateFilePath[] Add { get { return add; } }

        /// <summary>
        /// Files to remove.
        /// </summary>
        public ModuleUpdateFilePath[] Remove { get { return remove; } }
        #endregion

        /// <summary>
        /// Constructor from xml data.
        /// </summary>
        /// <param name="name">module base name</param>
        /// <param name="update">xml data</param>
        internal ModuleUpdateInfoItem(string name, burntimeupdatesModuleUpdate update)
        {
            version = new Version(update.version);
            @base = (update.@base == "*") ? null : new Version(update.@base);
            savegameCompatible = update.savegame == burntimeupdatesModuleUpdateSavegame.yes;

            if (update.add == null)
            {
                add = new ModuleUpdateFilePath[0];
            }
            else
            {
                add = new ModuleUpdateFilePath[update.add.Length];

                for (int i = 0; i < add.Length; i++)
                {
                    add[i] = new ModuleUpdateFilePath(version, name, update.add[i]);
                }
            }

            remove = new ModuleUpdateFilePath[0];
        }

        public ModuleUpdateInfoItem(ModuleUpdateInfoItem older, ModuleUpdateInfoItem newer)
        {
            version = newer.Version;
            @base = older.@base;
            savegameCompatible = older.savegameCompatible & newer.savegameCompatible;

            List<ModuleUpdateFilePath> add = new List<ModuleUpdateFilePath>();
            // add old file list
            add.AddRange(older.Add);
            
            // add or replace files by new ones
            foreach (ModuleUpdateFilePath name in newer.Add)
            {
                bool added = false;

                for (int i = 0; i < add.Count; i++)
                {
                    // file found, replace it with new version
                    if (name.File == add[i].File)
                    {
                        add[i] = name;
                        added = true;
                        break;
                    }
                }

                // file not found, add it
                if (!added)
                    add.Add(name);
            }

            this.add = add.ToArray();
            remove = new ModuleUpdateFilePath[0];
        }
    }

    /// <summary>
    /// Holds module download/update info.
    /// </summary>
    class ModuleUpdateInfo
    {
        #region private attributes
        private Version stable;
        private Version unstable;
        private string name;
        private string[] dependencies;
        private string @base;
        private ModuleType type;
        private ModuleUpdateInfoItem[] items;
        #endregion

        #region public attributes
        /// <summary>
        /// Latest stable version.
        /// </summary>
        public Version Stable { get { return stable; } }

        /// <summary>
        /// Latest unstable version.
        /// </summary>
        public Version Unstable { get { return unstable; } }

        /// <summary>
        /// Module name.
        /// </summary>
        public string Name { get { return name; } }

        /// <summary>
        /// Module dependencies.
        /// </summary>
        public string[] Dependencies { get { return dependencies; } }

        /// <summary>
        /// Base module.
        /// </summary>
        public string Base { get { return @base; } }

        /// <summary>
        /// Module type.
        /// </summary>
        public ModuleType Type { get { return type; } }

        /// <summary>
        /// Module update info items.
        /// </summary>
        public ModuleUpdateInfoItem[] Items { get { return items; } }
        #endregion

        /// <summary>
        /// Constructor from xml data.
        /// </summary>
        /// <param name="module">xml data</param>
        internal ModuleUpdateInfo(burntimeupdatesModule module)
        {
            stable = new Version(module.stable);
            unstable = new Version(module.unstable);
            name = module.name;

            dependencies = module.dependency;
            if (dependencies == null)
                dependencies = new string[0];

            @base = module.@base;
            if (@base == null)
                @base = "";

            switch (module.type)
            {
                case burntimeupdatesModuleType.extra:
                    type = ModuleType.Extra;
                    break;
                case burntimeupdatesModuleType.game:
                    type = ModuleType.Game;
                    break;
                default:
                    type = ModuleType.Other;
                    break;
            }

            items = new ModuleUpdateInfoItem[module.update != null ? module.update.Length : 0];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = new ModuleUpdateInfoItem(@base == "" ? name : @base, module.update[i]);
            }
        }
    }

    /// <summary>
    /// Burntime update info xml.
    /// </summary>
    class BurntimeUpdateInfo
    {
        #region private attributes
        Dictionary<string, ModuleUpdateInfo> modules;
        #endregion

        #region public attributes
        /// <summary>
        /// Module update info collection.
        /// </summary>
        public Dictionary<string, ModuleUpdateInfo> Modules { get { return modules; } }
        #endregion

        /// <summary>
        /// Constructor from xml data stream.
        /// </summary>
        /// <param name="data">xml data stream</param>
        public BurntimeUpdateInfo(Stream data)
        {
            TextReader reader = new StreamReader(data);
            XmlSerializer serializer = new XmlSerializer(typeof(burntimeupdates));

            burntimeupdates xml = (burntimeupdates)serializer.Deserialize(reader);

            modules = new Dictionary<string, ModuleUpdateInfo>();
            foreach (burntimeupdatesModule mod in xml.module)
            {
                modules.Add(mod.name, new ModuleUpdateInfo(mod));
            }
        }
    }
}