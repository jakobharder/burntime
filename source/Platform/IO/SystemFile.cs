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
using System.Text;

namespace Burntime.Platform.IO
{
    public class SystemFile : File
    {
        String path;
        public override bool HasFullPath { get { return true; } }
        public override String FullPath { get { return path; } }

        String name;
        String package;
        public override bool HasName { get { return true; } }
        public override String Name { get { return name; } }
        public override String PackageName { get { return package; } }
        public override String FullName { get { return package + ":" + name; } }

        public SystemFile(String Path, String Name, bool WriteAccess)
        {
            stream = new System.IO.FileStream(Path, System.IO.FileMode.OpenOrCreate, WriteAccess ? System.IO.FileAccess.ReadWrite : System.IO.FileAccess.Read);
            path = System.IO.Path.GetFullPath(Path);

            String[] token = Name.Split(new Char[] { ':' });
            name = token[1];
            package = token[0];
        }
    }
}
