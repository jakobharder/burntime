
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic.ResourceProcessor
{
    class ClassicMapProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID ID, ResourceManager ResourceManager)
        {
            IDataProcessor processor = ResourceManager.GetDataProcessor("burngfxmap");
            MapData data = processor.Process(ID, ResourceManager) as MapData;
            
            int map = 0;
            if (ID.Custom != null)
                map = int.Parse(ID.Custom);

            for (int i = 0; i < data.Tiles.Length; i++)
                data.Tiles[i].Image = ResourceManager.GetImage("burngfxtile@zei_" + data.Tiles[i].Section.ToString("D3") + ".raw?" + data.Tiles[i].Item.ToString() + "?" + map, ResourceLoadType.Delayed);

            return data;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "classicmap" }; }
        }
    }
}
