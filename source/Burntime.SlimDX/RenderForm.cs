﻿using System;
using System.Drawing;
using System.Windows.Forms;

using Burntime.Platform.Graphics;

namespace Burntime.Platform
{
    public class RenderForm : Form
    {
        public virtual void LoadContent() { }
        public virtual void Render(RenderTarget target) { }
        public virtual void Update(GameTime gameTime) { }
        public virtual new void MouseMove(Vector2 pos) { }
        public virtual new void MouseClick(Vector2 pos, bool right) { }

        internal Engine engine;

        bool mouseHidden = false;
        bool closing = false;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            engine.OnMouseMove(e.X, e.Y);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.X >= 0 && e.Y >= 0 && e.X < ClientSize.Width && e.Y < ClientSize.Height)
                engine.OnMouseClick(e.X, e.Y, e.Button != MouseButtons.Left);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (!mouseHidden)
            {
                Cursor.Hide();
                mouseHidden = true;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (mouseHidden)
            {
                Cursor.Show();
                mouseHidden = false;
            }

            engine.OnMouseLeave();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
            engine.RequestClose();
        }

        protected override void OnActivated(EventArgs e)
        {
            //engine.Reset();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            engine.OnKeyPress(e.KeyChar);
        }

        protected override bool ProcessDialogKey(System.Windows.Forms.Keys keyData)
        {
            if (!base.ProcessDialogKey(keyData))
            {
                engine.OnVKeyPress((SystemKey)keyData);
                return false;
            }

            return true;
        }


        private delegate void CloseDelegate();
        public void CloseForm()
        {
            closing = true;

            if (InvokeRequired)
            {
                Invoke(new CloseDelegate(CloseForm));
            }
            else
            {
                base.Close();
            }
        }

        private delegate void CenterDelegate();
        public void CenterMouse()
        {
            if (closing)
                return;

            if (InvokeRequired)
            {
                Invoke(new CenterDelegate(CenterMouse));
            }
            else
            {
                // center point only if inside window
                Point pt = PointToClient(Cursor.Position);
                if (ClientRectangle.Contains(pt))
                {
                    pt = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
                    Cursor.Position = PointToScreen(pt);
                }
            }
        }
    }
}
