using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Remaster
{
    class FaceWindow : Image
    {
        public Action? TouchAction { get; set; }
        public override bool IsTouchTarget => !DisplayOnly ||
            app.LastInputMode == InputMode.Touch && TouchAction != null;
        public override int MinimumTouchTargetSize => TouchHitTest.MinimumSize;
        public override bool OnTouchTap(Vector2 position)
        {
            if (app.LastInputMode != InputMode.Touch || TouchAction == null)
                return false;
            TouchAction();
            return true;
        }
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

        public void MoveFace(int direction)
        {
            if (DisplayOnly || direction == 0 || MaxFaceID < 0)
                return;

            var unavailable = new HashSet<int>();
            if (Parent != null)
            {
                foreach (Window window in Parent.Windows.GetGroup(Group))
                {
                    if (window is FaceWindow face && !ReferenceEquals(face, this))
                        unavailable.Add(face.FaceID);
                }
            }

            FaceID = NextFaceId(faceID, direction, MaxFaceID, unavailable);
        }

        internal static int NextFaceId(int current, int direction, int maximum,
            ISet<int> unavailable)
        {
            direction = System.Math.Sign(direction);
            int faceCount = maximum + 1;
            int candidate = current;
            for (int i = 0; i < faceCount; i++)
            {
                candidate = (candidate + direction + faceCount) % faceCount;
                if (!unavailable.Contains(candidate))
                    return candidate;
            }
            return current;
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (DisplayOnly)
                return false;

            if (Button == MouseButton.Left)
                MoveFace(1);
            else if (Button == MouseButton.Right)
                MoveFace(-1);

            return true;
        }
    }
}
