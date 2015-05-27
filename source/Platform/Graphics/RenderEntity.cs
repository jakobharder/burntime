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

using System.Drawing;
using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics
{
    class RenderEntity
    {
    }

    class SpriteEntity : RenderEntity
    {
        public Texture Texture;         // texture
        public Rectangle Rectangle;     // source rectangle
        public SlimDX.Vector3 Position; // position
        public SlimDX.Color4 Color;     // color fill
        public float Factor = 1;        // texture resolution relative to game resolution
    }

    class LineEntity : RenderEntity
    {
        public SlimDX.Vector3 Start;
        public SlimDX.Vector3 End;
        public SlimDX.Color4 Color;
    }
}
