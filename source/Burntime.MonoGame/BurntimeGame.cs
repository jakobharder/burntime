using Burntime.Remaster;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.MonoGame.Graphics;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;
using Burntime.Platform.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace Burntime.MonoGame
{
    public class BurntimeGame : Game, IEngine, ILoadingCounter
    {
        public Resolution Resolution { get; } = new();
        public DeviceManager DeviceManager { get; set; }
        public ResourceManager ResourceManager { get; set; }
        public int Layer { get; set; }

        public RenderDevice RenderDevice { get; private set; }
        public RenderTarget MainTarget { get; private set; }
        public BlendOverlay BlendOverlay => RenderDevice?.BlendOverlay;
        BlendOverlayBase IEngine.BlendOverlay => RenderDevice?.BlendOverlay;

        BurntimeClassic _burntimeApp;
        readonly GraphicsDeviceManager _graphics;
        readonly GameThread _gameThread = new();

        public MusicPlayback Music { get; } = new MusicPlayback();
        IMusic IEngine.Music => Music;
        public bool MusicBlend { get; set; } = false;

        internal int loadingStack = 0;
        public int LoadingStack
        {
            //set { loadingStack = value; }
            get { return loadingStack; }
        }

        public void IncreaseLoadingCount()
        {
            lock (this)
                loadingStack++;
        }

        public void DecreaseLoadingCount()
        {
            lock (this)
                loadingStack--;
        }

        public bool IsLoading { get; set; }

        // #if (DEBUG)
        public bool FullScreen { get; set; } = false;
        // #else
        //         public bool FullScreen { get; set; } = true;
        // #endif
        bool _initialized = false;

        public BurntimeGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            Log.Initialize("log.txt");
            Log.Info(System.DateTime.Now.ToLocalTime().ToString());
            string version = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(System.AppContext.BaseDirectory, "burntime.exe")).ProductVersion ?? "?";
            Log.Info("Burntime version " + version);

            Window.Title = "Burntime " + version;

            FileSystem.BasePath = "";
            PackageManager paketManager = new("game/");

            paketManager.LoadPackages("classic", FileSystem.VFS, null);

            ConfigFile cfg = new();
            cfg.Open("classic:settings.txt");
            Log.DebugOut = cfg["engine"].GetBool("debug");

            _burntimeApp = new();

            Resolution.RatioCorrection = _burntimeApp.RatioCorrection;
            Resolution.MaxVerticalResolution = _burntimeApp.MaxVerticalResolution;

            _burntimeApp.Engine = this;
            _burntimeApp.SceneManager = new SceneManager(_burntimeApp);
            _burntimeApp.DeviceManager = new DeviceManager(Resolution);
            _burntimeApp.Engine.DeviceManager = _burntimeApp.DeviceManager;

            _burntimeApp.Initialize(new ResourceManager(this));

            BurnGfxModule burnGfx = new();
            burnGfx.Initialize(_burntimeApp.ResourceManager);

            Log.Info("Run main module...");
            _burntimeApp.Run();

            Log.Info("Start engine...");
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnResize;
            IsMouseVisible = false;
            ApplyGraphicsDeviceResolution(initialize: true);

            Window.TextInput += Window_TextInput;
            Music.RunThread();

            base.Initialize();
            _initialized = true;
        }

        private void OnResize(object sender, EventArgs e)
        {
            ApplyGraphicsDeviceResolution(initialize: false);
        }

        private void ToggleFullscreen()
        {
            FullScreen = !FullScreen;
            ApplyGraphicsDeviceResolution(initialize: false, resetWindowSize: true);
        }

        bool _resizing = false;
        private void ApplyGraphicsDeviceResolution(bool initialize, bool resetWindowSize = false)
        {
            if ((!_initialized && !initialize) || _resizing) return;

            _resizing = true;
            if (FullScreen)
            {
                Resolution.Native = new Platform.Vector2(GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                    GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height);

                _graphics.PreferredBackBufferWidth = Resolution.Native.x;
                _graphics.PreferredBackBufferHeight = Resolution.Native.y;
                _graphics.HardwareModeSwitch = false;
                _graphics.IsFullScreen = true;
            }
            else
            {
                if (resetWindowSize || initialize)
                {
                    Resolution.Native = new Platform.Vector2(GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                        GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height) / 2;
                }
                else
                {
                    Resolution.Native = new Platform.Vector2(Window.ClientBounds.Width,
                    Window.ClientBounds.Height);
                }

                _graphics.PreferredBackBufferWidth = Resolution.Native.x;
                _graphics.PreferredBackBufferHeight = Resolution.Native.y;
                _graphics.HardwareModeSwitch = false;
                _graphics.IsFullScreen = false;
            }
            _graphics.ApplyChanges();
            if (!initialize)
                _burntimeApp.SceneManager.ResizeScene();
            MainTarget = new RenderTarget(this, new Rect(Platform.Vector2.Zero, Resolution.Game));

            _resizing = false;
        }

        protected override void LoadContent()
        {
            ConfigFile cfg = new();
            cfg.Open("classic:settings.txt");

            Log.Info("Setup render device...");
            RenderDevice = new RenderDevice(this);
            RenderDevice.Initialize();
            BlendOverlay.Speed = cfg["engine"].GetFloat("scene_blend");

            Log.Info("Start resource manager thread...");
            ResourceManager.Run();

            Log.Info("Start game thread...");
            _gameThread.Start((Platform.GameTime gameTime) =>
            {
                _burntimeApp.Process(gameTime.Elapsed);
                MainTarget.Elapsed = gameTime.Elapsed;

                RenderDevice.Begin();
                _burntimeApp.Render(MainTarget);
                RenderDevice.End();
            });
        }

        bool _leftClicked = false;
        bool _rightClicked = false;

        private void HandleMouseInput()
        {
            var mouseState = Mouse.GetState();
            if (mouseState.X < 0 || mouseState.Y < 0 || mouseState.X >= Resolution.Native.x || mouseState.Y >= Resolution.Native.y)
            {
                _leftClicked = false;
                _rightClicked = false;
                DeviceManager.MouseLeave();
            }
            else
            {
                var mousePosition = new Vector2f(mouseState.X, mouseState.Y) * (Vector2f)Resolution.Game / (Vector2f)Resolution.Native;
                DeviceManager.MouseMove(mousePosition);

                if (mouseState.LeftButton == ButtonState.Pressed)
                    _leftClicked = true;
                if (mouseState.RightButton == ButtonState.Pressed)
                    _rightClicked = true;

                if (_leftClicked && mouseState.LeftButton == ButtonState.Released)
                {
                    DeviceManager.MouseClick(mousePosition, MouseButton.Left);
                    _leftClicked = false;
                }
                if (_rightClicked && mouseState.RightButton == ButtonState.Released)
                {
                    DeviceManager.MouseClick(mousePosition, MouseButton.Right);
                    _rightClicked = false;
                }
            }
        }

        Microsoft.Xna.Framework.Input.KeyboardState _previousKeyboardState;
        static char ConvertKeyToChar(Microsoft.Xna.Framework.Input.Keys key, Microsoft.Xna.Framework.Input.KeyboardState state)
        {
            bool shift = state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || state.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift);
            if ((int)key > 64 && (int)key < 91)
            {
                return shift ? key.ToString()[0] : key.ToString().ToLower()[0];
            }
            else if (!shift && (int)key > 47 && (int)key < 58)
            {
                return key.ToString().TrimStart('D')[0];
            }
            else if (key == Microsoft.Xna.Framework.Input.Keys.Back)
            {
                return (char)8;
            }
            return '\0';
        }

        private void Window_TextInput(object sender, TextInputEventArgs e)
        {
            if (e.Key == Keys.Escape || e.Key == Keys.Pause || e.Key == Keys.Enter
                || e.Key == Keys.F1 || e.Key == Keys.F2 || e.Key == Keys.F3 || e.Key == Keys.F4)
            {
                // handled in Update
            }
            else
            {
                DeviceManager?.KeyPress(e.Character);
            }
        }

        private void HandleKeyboardInput()
        {
            var keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            var keys = keyboard.GetPressedKeys();

            ModifierKeys modifier = ModifierKeys.None;
            if (keyboard.IsKeyDown(Keys.LeftAlt))
                modifier |= ModifierKeys.LeftAlt;
            
            foreach (var key in keys)
            {
                if (_previousKeyboardState.IsKeyUp(key))
                {
                    if (key == Keys.F11
                        || (key == Keys.Enter && (modifier & ModifierKeys.LeftAlt) == ModifierKeys.LeftAlt))
                    {
                        ToggleFullscreen();
                        DeviceManager.Clear();
                        break;
                    }

                    if (key == Keys.Escape || key == Keys.Pause || key == Keys.Enter
                        || key == Keys.F1 || key == Keys.F2 || key == Keys.F3 || key == Keys.F4)
                    {
                        DeviceManager?.VKeyPress(key switch
                        {
                            Keys.Escape => SystemKey.Escape,
                            Keys.Pause => SystemKey.Pause,
                            Keys.Enter => SystemKey.Enter,
                            Keys.F1 => SystemKey.F1,
                            Keys.F2 => SystemKey.F2,
                            Keys.F3 => SystemKey.F3,
                            Keys.F4 => SystemKey.F4,
                            _ => SystemKey.Other
                        });
                    }
                }
            }

            _previousKeyboardState = keyboard;
        }

        protected override void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            HandleMouseInput();
            HandleKeyboardInput();

            RenderDevice.Update();

            base.Update(gameTime);
        }

        protected override void Draw(Microsoft.Xna.Framework.GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            RenderDevice.Render((float)gameTime.ElapsedGameTime.TotalSeconds);

            base.Draw(gameTime);
        }

        void IEngine.CenterMouse()
        {
            if (_burntimeApp.RenderMouse && IsActive)
            {
                var center = Resolution.Native / 2;
                Mouse.SetPosition(center.x, center.y);
            }
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            Music.StopThread();
            base.OnExiting(sender, args);
        }

        void IEngine.ExitApplication()
        {
            Exit();
        }

        void IEngine.ReloadGraphics()
        {
            BlendOverlay.FadeOut(wait: true);
            ResourceManager.ReleaseAll();
            IsLoading = true;
            BlendOverlay.FadeIn();
        }

        #region render methods
        const float MAX_LAYERS = 256.0f;
        const float popInSpeed = 16.0f;
        static float CalcZ(float Layer) => 0.05f + (Layer / MAX_LAYERS) * 0.9f;

        public void RenderRect(Platform.Vector2 pos, Platform.Vector2 size, PixelColor color)
        {
            SpriteEntity entity = new()
            {
                Rectangle = new Rectangle(0, 0, size.x, size.y),
                Color = new Color(color.r, color.g, color.b, color.a),
                Texture = RenderDevice.WhiteTexture,
                Position = new Vector3(pos.x, pos.y, CalcZ(Layer))
            };
            RenderDevice.AddEntity(entity);
        }

        public void RenderSprite(ISprite sprite, Platform.Vector2 pos, float alpha = 1)
        {
            if (sprite is not MonoGame.Graphics.Sprite nativeSprite || !nativeSprite.Touch()) return;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - nativeSprite.Frame.TimeStamp < (long)(Stopwatch.Frequency / popInSpeed) && popInSpeed != 0)
                alpha *= (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed;

            Graphics.SpriteEntity entity = new()
            {
                Rectangle = new Rectangle(0, 0, nativeSprite.OriginalSize.x, nativeSprite.OriginalSize.y),
                Color = new Color(alpha, alpha, alpha, alpha),
                Factor = nativeSprite.Frame.Resolution
            };

            if (sprite.Animation != null && sprite.Animation.Progressive && nativeSprite.Frames != null)
            {
                entity.SpriteFrame = nativeSprite.Frames[0];
                entity.Position = new Vector3(pos.x, pos.y, CalcZ(Layer) - 0.001f);
                RenderDevice.AddEntity(entity);
            }

            Graphics.SpriteEntity entity2 = new()
            {
                Rectangle = entity.Rectangle,
                Color = entity.Color,
                SpriteFrame = nativeSprite.Frame,
                Position = new Vector3(pos.x, pos.y, CalcZ(Layer)),
                Factor = nativeSprite.Frame.Resolution
            };
            RenderDevice.AddEntity(entity2);
        }

        public void RenderSprite(ISprite sprite, Platform.Vector2 pos, Platform.Vector2 srcPos, int srcWidth, int srcHeight, PixelColor color)
        {
            if (sprite is not MonoGame.Graphics.Sprite nativeSprite || !nativeSprite.Touch()) return;

            Graphics.SpriteEntity entity = new()
            {
                Rectangle = new Rectangle(srcPos.x, srcPos.y, srcWidth, srcHeight),
                Color = new Color(color.r, color.g, color.b, color.a),
                Factor = nativeSprite.Frame.Resolution
            };

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - nativeSprite.Frame.TimeStamp < (long)(Stopwatch.Frequency / popInSpeed) && popInSpeed != 0)
            {
                entity.Color.A *= (byte)System.Math.Min(255, (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed);
                entity.Color.R *= (byte)System.Math.Min(255, (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed);
                entity.Color.G *= (byte)System.Math.Min(255, (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed);
                entity.Color.B *= (byte)System.Math.Min(255, (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed);
            }

            if (nativeSprite.Animation != null && nativeSprite.Animation.Progressive && nativeSprite.Frames != null)
            {
                entity.SpriteFrame = nativeSprite.Frames[0];
                entity.Position = new Vector3(pos.x, pos.y, CalcZ(Layer) - 0.001f);
                RenderDevice.AddEntity(entity);
            }

            Graphics.SpriteEntity entity2 = new()
            {
                Rectangle = entity.Rectangle,
                Color = entity.Color,
                SpriteFrame = nativeSprite.Frame,
                Position = new Vector3(pos.x, pos.y, CalcZ(Layer)),
                Factor = nativeSprite.Frame.Resolution
            };
            RenderDevice.AddEntity(entity2);
        }

        public void RenderLine(Platform.Vector2 start, Platform.Vector2 end, int color)
        {
            //Graphics.LineEntity entity = new Burntime.Platform.Graphics.LineEntity();
            //entity.Color = new SlimDX.Color4(color);
            //entity.Start = new SlimDX.Vector3(start.x, start.y, CalcZ(layer));
            //entity.End = new SlimDX.Vector3(end.x, end.y, CalcZ(layer));
            //device.AddEntity(entity);
        }
        #endregion
    }
}