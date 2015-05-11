
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
using System.Text;
using System.IO;

namespace Burntime.Launcher
{
    static class ChangelogToHtml
    {
        static public string Convert(Stream txt)
        {
            string result = "<br/>";
            bool list = false;

            StreamReader reader = new StreamReader(txt);
            string str = reader.ReadLine();
            while (str != null)
            {
                str = str.Trim();
                if (str != "")
                {
                    if (str.StartsWith("*"))
                    {
                        if (!list)
                            result += "<ul>";
                        list = true;

                        result += "<li>" + str.Substring(1).Trim() + "</li>";
                    }
                    else
                    {
                        if (list)
                            result += "</ul>";
                        list = false;

                        result += str;
                        result += "<br/>";
                    }
                }
                str = reader.ReadLine();
            }

            if (list)
                result += "</ul>";
            return result + "<br/>";
        }
    }
}