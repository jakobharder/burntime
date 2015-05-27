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
using System.Collections.Generic;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    public interface IFontProcessor
    {
        void Process(ResourceID ID);
        Vector2 Size { get; }
        void Render(Stream Stream, int Stride, PixelColor Fore, PixelColor Back);
        Dictionary<char, CharInfo> CharInfo { get; }
        int Offset { get; }
        float Factor { get; }
    }
}
