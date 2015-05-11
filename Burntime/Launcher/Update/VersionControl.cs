
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
using Burntime.Platform.IO;

namespace Burntime.Launcher.Update
{
    class VersionControl
    {
        #region private attributes
        Version local;
        ModuleUpdateInfo info;
        ModuleUpdateInfoItem infoItem;
        #endregion

        #region public attributes
        public string Name
        {
            get { return info.Name; }
        }

        public Version ServerVersion
        {
            get { return infoItem.Version; }
        }

        public Version LocalVersion
        {
            get { return local; }
        }

        public bool SavegameCompatible
        {
            get { return infoItem.SavegameCompatible; }
        }

        public ModuleUpdateInfoItem Info
        {
            get { return infoItem; }
        }

        /// <summary>
        /// Update of every file is necessary
        /// </summary>
        public bool FullUpdate
        {
            get { return infoItem.Base == null; }
        }

        /// <summary>
        /// Stable version.
        /// </summary>
        public bool Stable
        {
            get { return (infoItem.Version <= info.Stable); }
        }
        #endregion

        public VersionControl(string name, Version server, Version local, ModuleUpdateInfo info)
        {
            this.local = local;
            this.info = info;

            // select update item by version numbers
            infoItem = null;
            bool merged = false;

            do
            {
                merged = false;

                foreach (ModuleUpdateInfoItem item in info.Items)
                {
                    if (infoItem == null && item.Base == local)
                    {
                        infoItem = item;
                        merged = true;
                        break;
                    }
                    // make sure update is not higher than the requested version
                    else if (infoItem != null && item.Version <= server && item.Base == infoItem.Version)
                    {
                        // merge
                        infoItem = new ModuleUpdateInfoItem(infoItem, item);
                        merged = true;
                        break;
                    }
                }
            
                // no update for our version found, fallback to full update
                if (infoItem == null)
                {
                    foreach (ModuleUpdateInfoItem item in info.Items)
                    {
                        if (item.Base == null && item.Version <= server)
                        {
                            infoItem = item;
                            merged = true;
                            break;
                        }
                    }
                }

            } while (merged);
        }
    }
}
