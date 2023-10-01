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

        /// <summary>
        /// thread-safe
        /// </summary>
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

        /// <summary>
        /// returns a copy, thread-safe
        /// </summary>
        public IEnumerable<MouseClickInfo> Clicks
        {
            get 
            {
                IEnumerable<MouseClickInfo> copy;
                lock (this)
                    copy = clicks.ToArray();
                return copy; 
            }
        }

        public Nullable<Rect> Boundings
        {
            get { return boundings; }
            set { boundings = value; }
        }

        /// <summary>
        /// thread-safe
        /// </summary>
        /// <param name="click"></param>
        public void AddClick(MouseClickInfo click)
        {
            lock (this)
                clicks.Add(click);
        }

        /// <summary>
        /// thread-safe
        /// </summary>
        public void ClearClicks()
        {
            lock (this)
                clicks.Clear();
        }
    }

    public enum Keys
    {
        None = 0,//,System.Windows.Forms.Keys.None,
        F1 = 1,//System.Windows.Forms.Keys.F1,
        F2 = 2,//System.Windows.Forms.Keys.F2,
        F3 = 3,//System.Windows.Forms.Keys.F3,
        F4 = 4,//System.Windows.Forms.Keys.F4,
        Escape = 5,//System.Windows.Forms.Keys.Escape,
        Pause = 6,//System.Windows.Forms.Keys.Pause
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
        List<Key> keys = new List<Key>();
        /// <summary>
        /// returns a copy, thread-safe
        /// </summary>
        public Key[] Keys
        {
            get 
            {
                Key[] copy;
                lock (this)
                    copy =  keys.ToArray();
                return copy;
            }
        }

        /// <summary>
        /// thread-safe
        /// </summary>
        /// <param name="key"></param>
        public void AddKey(Key key)
        {
            lock (this)
                keys.Add(key);
        }

        /// <summary>
        /// thread-safe
        /// </summary>
        public void ClearKeys()
        {
            lock (this)
                keys.Clear();
        }
    }

    public class DeviceManager
    {
        private readonly MouseDevice mouse_;
        public IMouseDevice Mouse => mouse_;

        public Keyboard Keyboard { get; } = new();

        private readonly Vector2 gameResolution_;

        public DeviceManager(Vector2 resolution, Vector2 gameResolution)
        {
            mouse_ = new MouseDevice(resolution);
            gameResolution_ = gameResolution;
        }

        public void MouseMove(Vector2 Position)
        {
            mouse_.Position = new Vector2(Position);
        }

        public void MouseClick(Vector2 Position, MouseButton Button)
        {
            mouse_.AddClick(new()
            {
                Position = new Vector2(Position),
                Button = Button
            });
        }

        public void MouseLeave()
        {
            Vector2 position = mouse_.Position;

            Vector2 direction = mouse_.LastDirection;
            if (direction != Vector2.Zero)
            {
                while (position.x > 0 && position.y > 0 && position.x <= gameResolution_.x && position.y <= gameResolution_.y)
                    position += direction;

                mouse_.Position = position;
            }
        }

        public void KeyPress(char key)
        {
            Keyboard.AddKey(new Key(key));
        }

        public void VKeyPress(Keys key)
        {
            Keyboard.AddKey(new Key(key));
        }

        public void Refresh()
        {
            mouse_.ClearClicks();
            Keyboard.ClearKeys();
        }
    }
}
