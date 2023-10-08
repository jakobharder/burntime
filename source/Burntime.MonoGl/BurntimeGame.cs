using Burntime.Classic;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.MonoGl.Graphics;
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
using System.Reflection;

namespace Burntime.MonoGl
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

        internal bool isLoading = true;
        public bool IsLoading
        {
            get { return isLoading; }
            //  set { isLoading = value; if (value) fadeOutState = 1; }
        }

#if (DEBUG)
        public bool FullScreen { get; set; } = false;
#else
        public bool FullScreen { get; set; } = true;
#endif

        public BurntimeGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            Log.Initialize("log.txt");

            Window.Title = "Burntime " + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion ?? "?";

            FileSystem.BasePath = "";
            FileSystem.AddPackage("system", "system");

            PackageManager paketManager = new("game/");

            paketManager.LoadPackages("classic", FileSystem.VFS, null);

            ConfigFile cfg = new();
            cfg.Open("classic:settings.txt");
            Log.DebugOut = cfg["engine"].GetBool("debug");

            RenderDevice = new RenderDevice(this);
            RenderDevice.Initialize();

            _burntimeApp = new();

            Resolution.RatioCorrection = _burntimeApp.RatioCorrection;
            Resolution.MaxVerticalResolution = _burntimeApp.MaxVerticalResolution;

            _burntimeApp.Engine = this;
            _burntimeApp.SceneManager = new SceneManager(_burntimeApp);
            _burntimeApp.DeviceManager = new DeviceManager(Resolution.Native, Resolution.Game);
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
                Resolution.Native = new Platform.Vector2(GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                    GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height) / 2;

                _graphics.PreferredBackBufferWidth = Resolution.Native.x;
                _graphics.PreferredBackBufferHeight = Resolution.Native.y;
            }
            _graphics.ApplyChanges();
            base.Initialize();
        }

        private void OnResize(object sender, EventArgs e)
        {
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
                Resolution.Native = new Platform.Vector2(Window.ClientBounds.Width,
                    Window.ClientBounds.Height);

                _graphics.PreferredBackBufferWidth = Resolution.Native.x;
                _graphics.PreferredBackBufferHeight = Resolution.Native.y;
            }
            _graphics.ApplyChanges();
            _burntimeApp.SceneManager.ResizeScene();
            MainTarget = new RenderTarget(this, new Rect(Platform.Vector2.Zero, Resolution.Game));
        }

        protected override void LoadContent()
        {
            ConfigFile cfg = new();
            cfg.Open("classic:settings.txt");

            Log.Info("Setup render device...");
            RenderDevice = new RenderDevice(this);
            RenderDevice.Initialize();
            BlendOverlay.Speed = cfg["engine"].GetFloat("scene_blend");
            MainTarget = new RenderTarget(this, new Rect(Platform.Vector2.Zero, Resolution.Game));

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

        private void HandleKeyboardInput()
        {
            var keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            var keys = keyboard.GetPressedKeys();

            foreach (var key in keys)
            {
                if (_previousKeyboardState.IsKeyUp(key))
                {
                    char charValue = ConvertKeyToChar(key, keyboard);
                    if (charValue != 0)
                        DeviceManager.KeyPress(charValue);
                    else
                    {
                        DeviceManager.VKeyPress(key switch
                            {
                                Microsoft.Xna.Framework.Input.Keys.Escape => Platform.Keys.Escape,
                                Microsoft.Xna.Framework.Input.Keys.Pause => Platform.Keys.Pause,
                                Microsoft.Xna.Framework.Input.Keys.F1 => Platform.Keys.F1,
                                Microsoft.Xna.Framework.Input.Keys.F2 => Platform.Keys.F2,
                                Microsoft.Xna.Framework.Input.Keys.F3 => Platform.Keys.F3,
                                Microsoft.Xna.Framework.Input.Keys.F4 => Platform.Keys.F4,
                                _ => Platform.Keys.Other
                            });
                    }
                }
            }

            _previousKeyboardState = keyboard;
        }

        protected override void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            //if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
            //    || Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            //    Exit();

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
            var center = Resolution.Native / 2;
            Mouse.SetPosition(center.x, center.y);
        }

        void IEngine.ExitApplication()
        {
            Exit();
        }

        void IEngine.ReloadGraphics()
        {
            BlendOverlay.FadeOut(wait: true);
            ResourceManager.ReleaseAll();
            isLoading = true;
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
            if (sprite is not MonoGl.Graphics.Sprite nativeSprite || !nativeSprite.Touch()) return;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - nativeSprite.Frame.TimeStamp < (long)(Stopwatch.Frequency / popInSpeed) && popInSpeed != 0)
                alpha *= (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed;

            Graphics.SpriteEntity entity = new()
            {
                Rectangle = new Rectangle(0, 0, nativeSprite.OriginalSize.x, nativeSprite.OriginalSize.y),
                Color = new Color(1, 1, 1, alpha),
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
            if (sprite is not MonoGl.Graphics.Sprite nativeSprite || !nativeSprite.Touch()) return;

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