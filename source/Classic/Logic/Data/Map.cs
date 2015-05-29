
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
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform.Resource;

namespace Burntime.Classic
{
    [Serializable]
    public class Map : StateObject
    {
        protected DataID<MapData> mapData;
        public MapData MapData
        {
            get { return mapData; }
        }

        public MapEntrance[] Entrances
        {
            get { return MapData.Entrances; }
        }

        public PathMask Mask
        {
            get { return MapData.Mask; }
        }

        protected override void InitInstance(object[] parameter)
        {
            if (parameter.Length != 1)
                throw new BurntimeLogicException();

            mapData = ResourceManager.GetData((string)parameter[0], ResourceLoadType.LinkOnly);
        }
    }
}