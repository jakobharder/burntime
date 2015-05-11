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

namespace Burntime.Platform
{
    public static class Debug
    {
        static internal DebugForm form;

        static public void SetInfo(string name, string info)
        {
            if (form == null)
                return;

            form.SetInfo(name, info);
        }

        static public void SetInfoMB(string name, int bytes)
        {
            if (form == null)
                return;

            form.SetInfo(name, ((float)bytes / 1024 / 1024).ToString("F02") + " MB");
        }
    }
}
