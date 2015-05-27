
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

using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Framework.GUI
{
    public enum PositionAlignment
    {
        Left, 
        Right, 
        Center
    }

    public class Window
    {
        internal Container parent;
        public Container Parent
        {
            get { return parent; }
        }

        public Vector2 PositionOnScreen
        {
            get
            {
                if (parent == null)
                    return Position;
                else
                    return Position + parent.PositionOnScreen;
            }
        }

        protected Module app;

        public event EventHandler WindowShow;
        public event EventHandler WindowHide;

        // attributes
        protected bool visible = true;
        //bool active = false;
        bool modal;
        bool hasFocus = false;
        Vector2 pos = new Vector2();
        Vector2 size = new Vector2();
        PositionAlignment vertAlign;
        PositionAlignment horizAlign;
        bool isEntered = false;
        bool captureAllMouseMove = false;
        bool captureAllMouseClicks = false;

        public bool CaptureAllMouseMove
        {
            get { return captureAllMouseMove; }
            set { captureAllMouseMove = value; }
        }

        public bool CaptureAllMouseClicks
        {
            get { return captureAllMouseClicks; }
            set { captureAllMouseClicks = value; }
        }

        public bool HasFocus
        {
            get { return hasFocus; }
            set { hasFocus = value; }
        }

        public PositionAlignment VerticalAlignment
        {
            get { return vertAlign; }
            set { vertAlign = value; RefreshBoundings(); }
        }

        public PositionAlignment HorizontalAlignment
        {
            get { return horizAlign; }
            set { horizAlign = value; RefreshBoundings(); }
        }

        private int group = 0;
        public int Group
        {
            get { return group; }
            set { group = value; }
        }

        protected int layer = 0;
        public virtual int Layer
        {
            get { return layer; }
            set { layer = value; }
        }

        public Vector2 Position
        {
            get { return pos; }
            set 
            { 
                pos = value;
                RefreshBoundings();
            }
        }

        public Vector2 Size
        {
            get { return size; }
            set
            {
                size = value;
                RefreshBoundings();
            }
        }

        public bool IsVisible
        {
            get { return visible; }
            set { if (value) Show(); else Hide(); }
        }

        public bool IsModal
        {
            get { return modal; }
            set { Hide();  modal = value; }
        }

        // public attributes
        Rect boundings = new Rect(0, 0, 0, 0);
        
        void RefreshBoundings()
        {
            boundings.Left = pos.x;
            if (horizAlign == PositionAlignment.Center)
                boundings.Left -= size.x / 2;
            else if (horizAlign == PositionAlignment.Right)
                boundings.Left -= size.x;
            boundings.Right = boundings.Left + size.x;
            boundings.Top = pos.y;
            if (vertAlign == PositionAlignment.Center)
                boundings.Top -= size.y / 2;
            else if (vertAlign == PositionAlignment.Right)
                boundings.Top -= size.y;
            boundings.Bottom = boundings.Top + size.y;
        }

        public Rect Boundings
        {
            get { return boundings; }
            //set { pos = value.Position; size = value.Size; }
        }

        public Window(Module App)
        {
            app = App;
        }

        public void Show() 
        {
            if (!visible && modal)
                app.SceneManager.PushModalStack(this);
            visible = true;

            OnShow();
            if (WindowShow != null)
                WindowShow.Invoke(this, new EventArgs());
        }

        public void Hide() 
        {
            if (visible && modal)
                app.SceneManager.PopModalStack();
            visible = false;

            OnHide();
            if (WindowHide != null)
                WindowHide.Invoke(this, new EventArgs());
        }

        public void MoveInside(Rect Rectangle)
        {
            pos.ThresholdLT(Rectangle.Position);
            pos.ThresholdGT(Rectangle.Position + Rectangle.Size - Size);
            RefreshBoundings();
        }

        // internal message handling
        internal virtual bool MouseClick(Vector2 Position, MouseButton Button) 
        {
            if (!visible)
                return false;

            if (Boundings.PointInside(Position))
                return OnMouseClick(Position - this.Position, Button);
            else if (captureAllMouseClicks)
                return OnMouseClick(Position - this.Position, Button);
            return false;
        }

        internal virtual void ModalLeave()
        {
            if (isEntered)
            {
                OnMouseLeave();
                isEntered = false;
            }
        }

        internal virtual bool MouseMove(Vector2 position) 
        {
            if (!visible)
                return false;

            if (Boundings.PointInside(position))
            {
                if (!isEntered)
                {
                    OnMouseEnter();
                    isEntered = true;
                }

                OnMouseMove(position - this.Position);
            }
            else if (isEntered)
            {
                OnMouseLeave();
                isEntered = false;
            }

            if (captureAllMouseMove && !isEntered)
                OnMouseMove(position - this.Position);

            return true;
        }

        internal virtual bool MouseDown(Vector2 Position, MouseButton Button) { return true; }
        internal virtual bool MouseUp(Vector2 Position, MouseButton Button) { return true; }
        internal virtual void KeyPress(char Key) 
        {
            if (hasFocus)
                OnKeyPress(Key);
        }

        internal virtual void VKeyPress(Keys key)
        {
            if (hasFocus)
                OnVKeyPress(key);
        }

        internal virtual void Render(RenderTarget Target)
        {
            if (!visible)
                return;

            Target.Layer = layer;
            OnRender(Target.GetSubBuffer(Boundings));
        }

        internal virtual void Update(float Elapsed)
        {
            if (!visible)
                return;

            OnUpdate(Elapsed);
        }

        internal Rect LocalToGlobal(Rect rc)
        {
            if (parent == null)
                return rc;
            return parent.LocalToGlobal(rc + pos);
        }

        // message handling
        // input
        public virtual bool OnMouseClick(Vector2 Position, MouseButton Button) { return false; }
        public virtual bool OnMouseMove(Vector2 Position) { return false; }
        public virtual void OnMouseEnter() { }
        public virtual void OnMouseLeave() { }
        public virtual bool OnMouseDown(Vector2 Position, MouseButton Button) { return false; }
        public virtual bool OnMouseUp(Vector2 Position, MouseButton Button) { return false; }
        public virtual bool OnKeyPress(char Key) { return false; }
        public virtual bool OnVKeyPress(Keys key) { return false; }
        // state
        public virtual void OnActivate() { }
        public virtual void OnShow() { }
        public virtual void OnHide() { }
        public virtual void OnInactivate() { }
        // frame update
        public virtual void OnUpdate(float Elapsed) { }
        // render
        public virtual void OnRender(RenderTarget Target) { }
    }
}
