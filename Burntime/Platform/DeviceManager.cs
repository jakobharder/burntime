
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2013 Jakob Harder
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
