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
using System.Runtime.InteropServices;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Platform.Graphics
{
    public class AniProcessorPng : ISpriteProcessor, ISpriteAnimationProcessor, IDataProcessor
    {
        Vector2 size;
        int frameOffset;
        int frameCount;
        string format;
        SpriteProcessorPng png;

        public Vector2 Size
        {
            get { return size; }
        }

        public int FrameCount
        {
            get { return frameCount; }
        }

        public Vector2 FrameSize
        {
            get { return size; }
        }

        public bool SetFrame(int frame)
        {
            png = new SpriteProcessorPng();
            png.Process(String.Format(format, frame + frameOffset));
            size = png.Size;
            return true;
        }

        public void Process(ResourceID ID)
        {
            if (ID.EndIndex == -1)
                frameCount = 1;
            else
                frameCount = ID.EndIndex - ID.Index + 1;

            frameOffset = ID.Index;
            format = ID.File;
            size = new Vector2();
        }

        public DataObject Process(ResourceID ID, ResourceManager ResourceManager)
        {
            return ResourceManager.GetImage(ID);
        }

        public void Render(IntPtr ptr)
        {
            throw new NotSupportedException();
        }

        public void Render(System.IO.Stream s, int stride)
        {
            png.Render(s, stride);
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "pngani" }; }
        }
    }
}
