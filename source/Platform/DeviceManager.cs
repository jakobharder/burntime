
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

namespace Burntime.Platform
{
    public enum MouseButton
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    public struct MouseClickInfo
    {
        public Vector2 Position;
        public MouseButton Button;
    }

    public interface IMouseDevice
    {
        Vector2 Position { get; }
        Nullable<Rect> Boundings { get; set; }
        IEnumerable<MouseClickInfo> Clicks { get; }
    }

    sealed class MouseDevice : IMouseDevice
    {
        private Vector2 resolution;

        private Vector2 current = Vector2.Zero;
        private Nullable<Vector2> previous;
        private List<MouseClickInfo> clicks = new List<MouseClickInfo>();
        private Nullable<Rect> boundings;

        public MouseDevice(Vector2 resolution)
        {
            this.resolution = resolution;
        }

        public Vector2 Position
        {
            get { return current; }
            set
            {
                if (Boundings.HasValue)
                {
                    current.ThresholdLT(boundings.Value.Position);
                    current.ThresholdGT(boundings.Value.Position + boundings.Value.Size);
                }
                else
                {
                    current.ThresholdLT(0);
                    current.ThresholdGT(resolution);
                }

                if (!previous.HasValue || previous.Value != current)
                    previous = current;
                current = value;
            }
        }

        public Vector2 LastDirection
        {
            get
            {
                if (previous.HasValue)
                    return current - previous.Value;

                return Vector2.Zero;
            }
        }

        public IEnumerable<MouseClickInfo> Clicks
        {
            get { return clicks; }
        }

        public Nullable<Rect> Boundings
        {
            get { return boundings; }
            set { boundings = value; }
        }

        public void AddClick(MouseClickInfo click)
        {
            clicks.Add(click);
        }

        public void ClearClicks()
        {
            clicks.Clear();
        }
    }

    public enum Keys
    {
        None = System.Windows.Forms.Keys.None,
        F1 = System.Windows.Forms.Keys.F1,
        F2 = System.Windows.Forms.Keys.F2,
        F3 = System.Windows.Forms.Keys.F3,
        F4 = System.Windows.Forms.Keys.F4,
        Escape = System.Windows.Forms.Keys.Escape,
        Pause = System.Windows.Forms.Keys.Pause
    }

    public struct Key
    {
        char key;
        Keys vkey;

        public Key(char key)
        {
            this.key = key;
            this.vkey = Keys.None;
        }

        public Key(Keys vkey)
        {
            this.key = (char)0;
            this.vkey = vkey;
        }

        public bool IsVirtual
        {
            get { return vkey != Keys.None; }
        }

        public Keys VKey
        {
            get { return vkey; }
        }

        public char Character
        {
            get { return key; }
        }
    }

    public class Keyboard
    {
        internal List<Key> keys = new List<Key>();
        public Key[] Keys
        {
            get { return keys.ToArray(); }
        }
    }

    public class DeviceManager
    {
        Engine engine;

        private MouseDevice mouse;
        public IMouseDevice Mouse
        {
            get { return mouse; }
        }

        Keyboard keyboard = new Keyboard();
        public Keyboard Keyboard
        {
            get { return keyboard; }
        }

        public DeviceManager(Engine Engine)
        {
            engine = Engine;
            engine.DeviceManager = this;
            mouse = new MouseDevice(engine.Resolution);
        }

        internal void MouseMove(Vector2 Position)
        {
            mouse.Position = new Vector2(Position);
        }

        internal void MouseClick(Vector2 Position, MouseButton Button)
        {
            MouseClickInfo info = new MouseClickInfo();
            info.Position = new Vector2(Position);
            info.Button = Button;
            mouse.AddClick(info);
        }

        internal void MouseLeave()
        {
            Vector2 position = mouse.Position;

            Vector2 direction = mouse.LastDirection;
            if (direction != Vector2.Zero)
            {
                while (position.x > 0 && position.y > 0 && position.x <= engine.GameResolution.x && position.y <= engine.GameResolution.y)
                    position += direction;

                mouse.Position = position;
            }
        }

        internal void KeyPress(char key)
        {
            keyboard.keys.Add(new Key(key));
        }

        internal void VKeyPress(Keys key)
        {
            keyboard.keys.Add(new Key(key));
        }

        public void Refresh()
        {
            mouse.ClearClicks(); // TODO: critical section
            keyboard.keys.Clear();
        }
    }
}
