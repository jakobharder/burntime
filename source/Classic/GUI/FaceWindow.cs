
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

using Burntime.Platform;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Classic
{
    class FaceWindow : Image
    {
        public FaceWindow(Module App)
            : base(App)
        {
        }

        int faceID = -1;
        public int FaceID
        {
            get
            {
                return faceID;
            }
            set
            {
                if (faceID != value)
                {
                    faceID = CheckSameFace(value);
                    RefreshFace();
                }
            }
        }

        public bool DisplayOnly = false;

        void RefreshFace()
        {
            if (faceID >= 0)
                Background = "ges_" + faceID.ToString("D2") + ".ani";
        }

        int CheckSameFace(int NewFaceID)
        {
            if (Parent == null || NewFaceID == -1 || NewFaceID > MaxFaceID)
                return NewFaceID;

            int direction = (NewFaceID - faceID > 0) ? 1 : -1;

            Window[] group = Parent.Windows.GetGroup(Group);
            foreach (Window window in group)
            {
                if (window is FaceWindow)
                {
                    FaceWindow face = window as FaceWindow;
                    if (face.FaceID == NewFaceID)
                    {
                        NewFaceID = CheckSameFace(NewFaceID + direction);
                        break;
                    }
                }
            }

            if (NewFaceID == -1 || NewFaceID > MaxFaceID)
                return faceID;

            return NewFaceID;
        }

        public int MaxFaceID = 1;

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (DisplayOnly)
                return false;

            if (Button == MouseButton.Left)
            {
                int id = faceID + 1;
                if (id > MaxFaceID)
                    id = MaxFaceID;
                FaceID = id;
            }
            else if (Button == MouseButton.Right)
            {
                int id = faceID - 1;
                if (id < 0)
                    id = 0;
                FaceID = id;
            }

            return true;
        }
    }
}
