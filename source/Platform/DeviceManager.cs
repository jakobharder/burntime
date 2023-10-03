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
        
        public Rect? Boundings { get; set; }

        public MouseDevice(Vector2 resolution)
        {
            this.resolution = resolution;
        }

        public Vector2 Position
        {
            get { return current; }
            set
            {
                if (Boundings is not null)
                    current.Clamp(Boundings.Value);
                else
                    current.ClampMaxExcluding(Vector2.Zero, resolution);

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

        public void ClearPrevious()
        {
            previous = null;
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
        Other = 7
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
        private readonly MouseDevice _mouse;
        public IMouseDevice Mouse => _mouse;

        public Keyboard Keyboard { get; } = new();

        private readonly Vector2 _gameResolution;

        public DeviceManager(Vector2 resolution, Vector2 gameResolution)
        {
#warning TODO thread synchronization, update and UI thread may be different now
            _mouse = new MouseDevice(resolution);
            _gameResolution = gameResolution;
        }

        public void MouseMove(Vector2 Position)
        {
            _mouse.Position = new Vector2(Position);
        }

        public void MouseClick(Vector2 Position, MouseButton Button)
        {
            _mouse.AddClick(new()
            {
                Position = new Vector2(Position),
                Button = Button
            });
        }

        public void MouseLeave()
        {
            if (_mouse.LastDirection == Vector2.Zero) return;

            Vector2 position = _mouse.Position;

            Rect bounds = new(Vector2.Zero, _gameResolution);
            while (bounds.PointInside(position))
                position += _mouse.LastDirection;

            position.Clamp(bounds);
            _mouse.Position = position;
            _mouse.ClearPrevious();
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
            _mouse.ClearClicks();
            Keyboard.ClearKeys();
        }
    }
}
