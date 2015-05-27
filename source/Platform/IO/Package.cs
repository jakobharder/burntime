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

using System.Collections.Generic;

namespace Burntime.Platform.IO
{
    public enum FileOpenMode
    {
        Read = 0,
        Write = 1,
        NoPackage = 2
    }

    public interface IPackage
    {
        string Name { get; }
        ICollection<string> Files { get; }

        File GetFile(FilePath filePath, FileOpenMode mode);
        bool ExistsFile(FilePath filePath);
        bool AddFile(FilePath filePath);
        bool RemoveFile(FilePath filePath);
        void Close();
    }
}
