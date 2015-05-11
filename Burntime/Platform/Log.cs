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
using System.IO;

namespace Burntime.Platform
{
    public class Log
    {
        static StreamWriter file;
        public static bool DebugOut;

        static public void Initialize(String file)
        {
            Log.file = new StreamWriter(file, false);
        }

        static public void Info(String str)
        {
            if (file != null)
            {
                file.WriteLine("[info] " + str);
                file.Flush();
            }
        }

        static public void Warning(String str)
        {
            if (file != null)
            {
                file.WriteLine("[warning] " + str);
                file.Flush();
            }
        }

        static public void Debug(String str)
        {
            if (file != null && DebugOut)
            {
                file.WriteLine("[debug] " + str);
                file.Flush();
            }
        }
    }
}
