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
