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
using System.Globalization;

namespace Burntime.MonoGame
{
    public class BurntimeGame : Game, IEngine, ILoadingCounter
    {
        const int TargetFramesPerSecond = 60;

        public Resolution Resolution { get; } = new();
        public DeviceManager DeviceManager { get; set; }
        public ResourceManager ResourceManager { get; set; }
        public float Layer { get; set; }

        public RenderDevice RenderDevice { get; private set; }
        public RenderTarget MainTarget { get; private set; }
        public BlendOverlay BlendOverlay => RenderDevice?.BlendOverlay;
        BlendOverlayBase IEngine.BlendOverlay => RenderDevice?.BlendOverlay;

        BurntimeClassic _burntimeApp;
        internal bool UseRemasteredGraphics => _burntimeApp?.IsNewGfx ?? true;
        readonly GraphicsDeviceManager _graphics;
        readonly GameThread _gameThread = new();
        readonly bool _emulateSteamMachine;
        readonly bool _emulateSteamDeck;
        readonly bool _chooseLanguage;
        public OutputFiltering OutputFiltering { get; set; }
        internal OutputFiltering DefaultOutputFiltering { get; }
        public bool ForceLinearOutputFiltering { get; }
        public bool ForceNearestPointOutputFiltering { get; }
        public bool DisableShaders { get; }
        public bool SupportsSharpBilinearShader =>
            RenderDevice?.SharpBilinearAvailable ?? false;
        public bool SupportsXbr2Shader =>
            RenderDevice?.Xbr2Available ?? false;
        internal bool ShowFps { get; }
        Platform.Graphics.Font? _fpsFont;
        long _fpsSampleStart = Stopwatch.GetTimestamp();
        int _fpsSampleFrames;
        volatile int _framesPerSecond;

        public MusicPlayback Music { get; } = new MusicPlayback();
        IMusic IEngine.Music => Music;
        readonly object _inputGlyphSync = new();
        SteamInputGlyphProvider? _steamInputGlyphs;
        volatile IInputGlyphProvider _inputGlyphs = TextInputGlyphProvider.Instance;
        public IInputGlyphProvider InputGlyphs => _inputGlyphs;
        string? _automaticLanguage;
        public string AutomaticLanguage => _automaticLanguage ??= DetectAutomaticLanguage();
        ControllerGlyphMode _controllerGlyphMode = ControllerGlyphMode.Auto;
        bool _inputGlyphsConfigured;
        public ControllerGlyphMode ControllerGlyphMode
        {
            get { lock (_inputGlyphSync) return _controllerGlyphMode; }
            set
            {
                lock (_inputGlyphSync)
                {
                    if (_inputGlyphsConfigured && _controllerGlyphMode == value)
                        return;
                    _controllerGlyphMode = value;
                    ConfigureInputGlyphs();
                }
            }
        }
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

        bool _isFullscreen = false;
        bool _requestFullscreen = false;
        bool IsSteamSession => _emulateSteamMachine || _emulateSteamDeck ||
            IsGamescopeSession() || IsSteamDeck();
        public bool SupportsFullscreenToggle => !IsSteamSession;
        public bool IsFullscreen 
        {
            get => _isFullscreen;
            set
            {
                if (SupportsFullscreenToggle)
                    _requestFullscreen = value; // value will be handled in render thread
            }
        }

        static bool IsGamescopeSession() =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAMESCOPE_WAYLAND_DISPLAY"));

        static bool IsSteamDeck() =>
            Environment.GetEnvironmentVariable("SteamDeck") == "1";

        bool _initialized = false;

        public BurntimeGame(bool emulateSteamMachine = false, bool emulateSteamDeck = false,
            bool chooseLanguage = false, bool linearOutputFiltering = false,
            bool nearestPointOutputFiltering = false, bool disableShaders = false,
            bool showFps = false)
        {
            _emulateSteamMachine = emulateSteamMachine;
            _emulateSteamDeck = emulateSteamDeck;
            _chooseLanguage = chooseLanguage;
            ForceLinearOutputFiltering = linearOutputFiltering;
            ForceNearestPointOutputFiltering = nearestPointOutputFiltering;
            DisableShaders = disableShaders;
            DefaultOutputFiltering = linearOutputFiltering
                ? OutputFiltering.Linear
                : nearestPointOutputFiltering
                    ? OutputFiltering.NearestPoint
                    : disableShaders
                    ? OutputFiltering.SharpBilinear
                    : OutputFiltering.SharpBilinearShader;
            OutputFiltering = DefaultOutputFiltering;
            ShowFps = showFps;
            _graphics = new GraphicsDeviceManager(this);
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / TargetFramesPerSecond);
            _graphics.SynchronizeWithVerticalRetrace = true;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        void ConfigureInputGlyphs()
        {
            lock (_inputGlyphSync)
            {
                _steamInputGlyphs?.Dispose();
                _steamInputGlyphs = null;

                if (_controllerGlyphMode == ControllerGlyphMode.Auto)
                {
                    try
                    {
                        _steamInputGlyphs = new SteamInputGlyphProvider();
                        _inputGlyphs = _steamInputGlyphs;
                    }
                    catch (Exception exception) when (exception is System.IO.FileNotFoundException or
                        System.IO.FileLoadException or BadImageFormatException or TypeInitializationException)
                    {
                        // Auto uses Steam Input when available and Xbox conventions otherwise.
                        _inputGlyphs = new ForcedInputGlyphProvider(GamepadLabelStyle.Xbox);
                        Log.Info($"Steamworks.NET unavailable ({exception.GetType().Name}); " +
                            "using Xbox controller glyphs");
                    }
                }
                else
                {
                    GamepadLabelStyle labelStyle = _controllerGlyphMode switch
                    {
                        ControllerGlyphMode.PlayStation => GamepadLabelStyle.PlayStation,
                        ControllerGlyphMode.Steam => GamepadLabelStyle.Steam,
                        ControllerGlyphMode.Switch => GamepadLabelStyle.Switch,
                        _ => GamepadLabelStyle.Xbox
                    };
                    _inputGlyphs = new ForcedInputGlyphProvider(labelStyle);
                    Log.Info($"Controller glyph family selected: {labelStyle}");
                }

                _inputGlyphsConfigured = true;
            }
        }

        string DetectAutomaticLanguage()
        {
            string? steamLanguage;
            lock (_inputGlyphSync)
                steamLanguage = _steamInputGlyphs?.CurrentGameLanguage;

            // A manual controller family must not disable Steam language detection.
            if (string.IsNullOrWhiteSpace(steamLanguage))
            {
                try
                {
                    steamLanguage = SteamInputGlyphProvider.DetectCurrentGameLanguage();
                }
                catch (Exception exception) when (exception is System.IO.FileNotFoundException or
                    System.IO.FileLoadException or BadImageFormatException or TypeInitializationException)
                {
                    Log.Info($"Steam language unavailable ({exception.GetType().Name})");
                }
            }

            if (!string.IsNullOrWhiteSpace(steamLanguage))
            {
                Log.Info($"Steam language: {steamLanguage}");
                return IsGermanLanguage(steamLanguage) ? "de" : "en";
            }

            string systemLanguage = CultureInfo.CurrentUICulture.Name;
            Log.Info($"System UI language: {systemLanguage}");
            return IsGermanLanguage(systemLanguage) ? "de" : "en";
        }

        static bool IsGermanLanguage(string language) =>
            language.Equals("german", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("de", StringComparison.OrdinalIgnoreCase) ||
            language.StartsWith("de-", StringComparison.OrdinalIgnoreCase) ||
            language.StartsWith("de_", StringComparison.OrdinalIgnoreCase);

        protected override void Initialize()
        {
            string logPath = "log.txt";
            if (OperatingSystem.IsMacOS())
            {
                string logDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Burntime");
                System.IO.Directory.CreateDirectory(logDirectory);
                logPath = System.IO.Path.Combine(logDirectory, "log.txt");
            }

            Log.Initialize(logPath);
            Log.Info(System.DateTime.Now.ToLocalTime().ToString());
            Log.Info("Burntime version " + BurntimeClassic.Version);
            if (_emulateSteamDeck)
                Log.Info("Steam Deck test mode: 1280x800 windowed");
            else if (_emulateSteamMachine)
                Log.Info("Steam Machine test mode: gamescope features, windowed");

            Window.Title = "Burntime " + BurntimeClassic.Version;

            // Installed content lives next to the executable on every desktop platform.
            FileSystem.BasePath = AppContext.BaseDirectory;
            PackageManager paketManager = new("game/");

            paketManager.LoadPackages("classic", FileSystem.VFS, null);

            ConfigFile cfg = new();
            cfg.Open("classic:settings.txt");
            Log.DebugOut = cfg["engine"].GetBool("debug");

            _burntimeApp = new();
            _burntimeApp.ChooseLanguageOnStart = _chooseLanguage;
            _burntimeApp.LastInputMode = IsSteamSession ? InputMode.Gamepad : InputMode.Mouse;

            Resolution.RatioCorrection = _burntimeApp.RatioCorrection;
            Resolution.MinResolution = _burntimeApp.MinResolution;
            Resolution.MaxResolution = _burntimeApp.MaxResolution;
            if (_emulateSteamDeck || IsSteamDeck())
                Resolution.OutputScaleOverride = 1.5f;

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

        bool _resizing = false;
        private void ApplyGraphicsDeviceResolution(bool initialize, bool resetWindowSize = false)
        {
            if ((!_initialized && !initialize) || _resizing) return;

            _resizing = true;
            if (IsFullscreen)
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
                    var displayResolution = new Platform.Vector2(
                        GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width,
                        GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height);
                    Resolution.Native = _emulateSteamDeck
                        ? new Platform.Vector2(1280, 800)
                        : IsGamescopeSession() || IsSteamDeck()
                            ? displayResolution
                            : displayResolution / 2;
                    //Resolution.Native = new Platform.Vector2(2560, 1440);
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

            // PreferredBackBufferWidth/Height resize the client area. Read it back
            // after the platform has applied window decorations and DPI handling.
            if (!IsFullscreen)
            {
                Resolution.Native = new Platform.Vector2(Window.ClientBounds.Width,
                    Window.ClientBounds.Height);
            }
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
            if (ShowFps)
                _fpsFont = ResourceManager.GetFont(BurntimeClassic.FontName, new PixelColor(204, 204, 204));

            Log.Info("Start resource manager thread...");
            ResourceManager.Run();

            Log.Info("Start game thread...");
            _gameThread.Start((Platform.GameTime gameTime) =>
            {
                _burntimeApp.Process(gameTime.Elapsed);
                MainTarget.Elapsed = gameTime.Elapsed;
                MainTarget.TotalElapsed += gameTime.Elapsed;

                RenderDevice.Begin();
                _burntimeApp.Render(MainTarget);
                _fpsFont?.DrawText(MainTarget, new Platform.Vector2(2, 2),
                    string.Create(CultureInfo.InvariantCulture,
                        $"{_framesPerSecond} FPS\n{ResourceManager.TextureMemoryUsage / (1024.0 * 1024.0):0.0} MB"),
                    TextAlignment.Left, VerticalTextAlignment.Top);
                RenderDevice.End();
            }, framesPerSecond: TargetFramesPerSecond);
        }

        bool _leftClicked = false;
        bool _rightClicked = false;
        Point? _previousMousePosition;
        int? _previousScrollWheelValue;
        InputAction _leftStickDirection;
        bool _leftStickNavigationLatched;

        private void HandleMouseInput()
        {
            var mouseState = Mouse.GetState();
            var nativeMousePosition = new Point(mouseState.X, mouseState.Y);
            bool mouseInside = mouseState.X >= 0 && mouseState.Y >= 0 &&
                mouseState.X < Resolution.Native.x && mouseState.Y < Resolution.Native.y;

            // Keep the native cursor available outside an inactive/windowed game,
            // and hide it again when it enters the active game surface. In
            // particular, Cocoa can reveal the cursor when it leaves an SDL
            // window; changing this state on re-entry makes SDL hide it again.
            IsMouseVisible = !IsActive || !mouseInside;

            if (_previousMousePosition.HasValue)
            {
                int deltaX = nativeMousePosition.X - _previousMousePosition.Value.X;
                int deltaY = nativeMousePosition.Y - _previousMousePosition.Value.Y;
                if (deltaX * deltaX + deltaY * deltaY > 1)
                {
                    _burntimeApp.LastInputMode = InputMode.Mouse;
                }
            }
            _previousMousePosition = nativeMousePosition;

            if (!mouseInside)
            {
                _leftClicked = false;
                _rightClicked = false;
                DeviceManager.MouseLeave();
            }
            //else
            {
                DeviceManager.IsRightDown = mouseState.RightButton == ButtonState.Pressed;

                var mousePosition = new Vector2f(mouseState.X, mouseState.Y) * (Vector2f)Resolution.Game / (Vector2f)Resolution.Native;
                DeviceManager.MouseMove(mousePosition);

                if (_previousScrollWheelValue.HasValue && mouseInside)
                {
                    int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue.Value;
                    if (wheelDelta != 0)
                    {
                        _burntimeApp.LastInputMode = InputMode.Mouse;
                        DeviceManager.MouseWheel(mousePosition, wheelDelta);
                    }
                }
                _previousScrollWheelValue = mouseState.ScrollWheelValue;

                // ignore clicks when not shown and active
                if (!IsActive) return;

                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    if (!_leftClicked)
                    {
                        _burntimeApp.LastInputMode = InputMode.Mouse;
                        DeviceManager.MouseDown(mousePosition, MouseButton.Left);
                    }
                    _leftClicked = true;
                }
                if (mouseState.RightButton == ButtonState.Pressed)
                {
                    if (!_rightClicked)
                    {
                        _burntimeApp.LastInputMode = InputMode.Mouse;
                        DeviceManager.MouseDown(mousePosition, MouseButton.Right);
                    }
                    _rightClicked = true;
                }

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
        GamePadState _previousGamePadState;
        bool _gamePadWasConnected;
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
            if (e.Key == Keys.Escape || e.Key == Keys.Pause || e.Key == Keys.Enter || e.Key == Keys.Tab
                || e.Key == Keys.F1 || e.Key == Keys.F2 || e.Key == Keys.F3 || e.Key == Keys.F4 || e.Key == Keys.F8 || e.Key == Keys.F9)
            {
                // handled in Update
            }
            else
            {
                var keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
                DeviceManager?.KeyPress(e.Character, GetModifiers(keyboard));
            }
        }

        static ModifierKeys GetModifiers(Microsoft.Xna.Framework.Input.KeyboardState keyboard)
        {
            ModifierKeys modifier = ModifierKeys.None;
            if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
                modifier |= ModifierKeys.LeftAlt;
            if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                modifier |= ModifierKeys.Shift;
            return modifier;
        }

        private void HandleKeyboardInput()
        {
            if (!IsActive)
            {
                _burntimeApp?.InputManager.ClearDown(InputSource.Keyboard);
                return;
            }

            var keyboard = Microsoft.Xna.Framework.Input.Keyboard.GetState();
            var keys = keyboard.GetPressedKeys();
            ModifierKeys modifier = GetModifiers(keyboard);

            _burntimeApp.InputManager.ClearDown(InputSource.Keyboard);
            foreach (var key in keys)
            {
                Key? bindingKey = ConvertToBindingKey(key, modifier);
                if (bindingKey.HasValue)
                {
                    InputAction action = _burntimeApp.KeyboardActionBindings.GetAction(bindingKey.Value);
                    if (action != InputAction.None)
                        _burntimeApp.InputManager.SetDown(InputSource.Keyboard, action, true);
                }
            }

            foreach (var key in keys)
            {
                if (_previousKeyboardState.IsKeyUp(key))
                {
                    bool isArrowKey = key is Keys.Up or Keys.Down or Keys.Left or Keys.Right;
                    if (_burntimeApp.LastInputMode != InputMode.Mouse || isArrowKey)
                        _burntimeApp.LastInputMode = InputMode.Keyboard;

                    if (SupportsFullscreenToggle && (key == Keys.F11
                        || (key == Keys.Enter && (modifier & ModifierKeys.LeftAlt) == ModifierKeys.LeftAlt))
                    )
                    {
                        IsFullscreen = !IsFullscreen;
                        DeviceManager.Clear();
                        break;
                    }

                    if (key == Keys.Escape || key == Keys.Pause || key == Keys.Enter
                        || key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right
                        || key == Keys.Tab
                        || key == Keys.F1 || key == Keys.F2 || key == Keys.F3 || key == Keys.F4 || key == Keys.F8 || key == Keys.F9)
                    {
                        DeviceManager?.VKeyPress(key switch
                        {
                            Keys.Escape => SystemKey.Escape,
                            Keys.Pause => SystemKey.Pause,
                            Keys.Enter => SystemKey.Enter,
                            Keys.Up => SystemKey.Up,
                            Keys.Down => SystemKey.Down,
                            Keys.Left => SystemKey.Left,
                            Keys.Right => SystemKey.Right,
                            Keys.Tab => SystemKey.Tab,
                            Keys.F1 => SystemKey.F1,
                            Keys.F2 => SystemKey.F2,
                            Keys.F3 => SystemKey.F3,
                            Keys.F4 => SystemKey.F4,
                            Keys.F8 => SystemKey.F8,
                            Keys.F9 => SystemKey.F9,
                            _ => SystemKey.Other
                        }, modifier);
                    }
                }
            }

            _previousKeyboardState = keyboard;
        }

        static Key? ConvertToBindingKey(Keys key, ModifierKeys modifier)
        {
            if (key >= Keys.A && key <= Keys.Z)
                return new Key(char.ToLowerInvariant(key.ToString()[0]), modifier);
            if (key >= Keys.D0 && key <= Keys.D9)
                return new Key(key.ToString()[1], modifier);

            return key switch
            {
                Keys.Space => new Key(' ', modifier),
                Keys.Back => new Key('\b', modifier),
                Keys.Enter => new Key(SystemKey.Enter, modifier),
                Keys.Escape => new Key(SystemKey.Escape, modifier),
                Keys.Tab => new Key(SystemKey.Tab, modifier),
                Keys.Up => new Key(SystemKey.Up, modifier),
                Keys.Down => new Key(SystemKey.Down, modifier),
                Keys.Left => new Key(SystemKey.Left, modifier),
                Keys.Right => new Key(SystemKey.Right, modifier),
                _ => null
            };
        }

        private void HandleGamePadInput()
        {
            GamePadState gamePad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);
            if (!IsActive || !gamePad.IsConnected)
            {
                _burntimeApp?.InputManager.ClearDown(InputSource.GamepadOne);
                DeviceManager?.ClearGamepadControlsDown();
                _gamePadWasConnected = false;
                _leftStickDirection = InputAction.None;
                _leftStickNavigationLatched = false;
                return;
            }

            GamePadState previous = _gamePadWasConnected ? _previousGamePadState : default;

            const float stickThreshold = 0.4f;
            InputAction previousLeftStickDirection = _leftStickDirection;
            bool useCardinalMovement = _burntimeApp.SceneManager.UseCardinalGamepadMovement;
            _leftStickDirection = useCardinalMovement
                ? GetCardinalStickDirection(gamePad.ThumbSticks.Left.X,
                    gamePad.ThumbSticks.Left.Y, stickThreshold, previousLeftStickDirection)
                : InputAction.None;
            bool up = useCardinalMovement
                ? _leftStickDirection == InputAction.MoveUp
                : gamePad.ThumbSticks.Left.Y >= stickThreshold;
            bool down = useCardinalMovement
                ? _leftStickDirection == InputAction.MoveDown
                : gamePad.ThumbSticks.Left.Y <= -stickThreshold;
            bool left = useCardinalMovement
                ? _leftStickDirection == InputAction.MoveLeft
                : gamePad.ThumbSticks.Left.X <= -stickThreshold;
            bool right = useCardinalMovement
                ? _leftStickDirection == InputAction.MoveRight
                : gamePad.ThumbSticks.Left.X >= stickThreshold;
            bool previousUp = useCardinalMovement
                ? previousLeftStickDirection == InputAction.MoveUp
                : previous.ThumbSticks.Left.Y >= stickThreshold;
            bool previousDown = useCardinalMovement
                ? previousLeftStickDirection == InputAction.MoveDown
                : previous.ThumbSticks.Left.Y <= -stickThreshold;
            bool previousLeft = useCardinalMovement
                ? previousLeftStickDirection == InputAction.MoveLeft
                : previous.ThumbSticks.Left.X <= -stickThreshold;
            bool previousRight = useCardinalMovement
                ? previousLeftStickDirection == InputAction.MoveRight
                : previous.ThumbSticks.Left.X >= stickThreshold;

            bool panUp = gamePad.ThumbSticks.Right.Y >= stickThreshold;
            bool panDown = gamePad.ThumbSticks.Right.Y <= -stickThreshold;
            bool panLeft = gamePad.ThumbSticks.Right.X <= -stickThreshold;
            bool panRight = gamePad.ThumbSticks.Right.X >= stickThreshold;
            bool previousPanUp = previous.ThumbSticks.Right.Y >= stickThreshold;
            bool previousPanDown = previous.ThumbSticks.Right.Y <= -stickThreshold;
            bool previousPanLeft = previous.ThumbSticks.Right.X <= -stickThreshold;
            bool previousPanRight = previous.ThumbSticks.Right.X >= stickThreshold;
            bool confirmOperation = gamePad.Buttons.Y == ButtonState.Pressed;

            bool gamePadActivated = up && !previousUp || down && !previousDown ||
                left && !previousLeft || right && !previousRight ||
                panUp && !previousPanUp || panDown && !previousPanDown ||
                panLeft && !previousPanLeft || panRight && !previousPanRight;

            if (gamePadActivated)
                _burntimeApp.LastInputMode = InputMode.Gamepad;

            _burntimeApp.InputManager.SetDown(InputSource.GamepadOne, InputAction.MoveUp, up);
            _burntimeApp.InputManager.SetDown(InputSource.GamepadOne, InputAction.MoveDown, down);
            _burntimeApp.InputManager.SetDown(InputSource.GamepadOne, InputAction.MoveLeft, left);
            _burntimeApp.InputManager.SetDown(InputSource.GamepadOne, InputAction.MoveRight, right);
            _burntimeApp.InputManager.SetDown(InputSource.GamepadOne, InputAction.PanCameraUp, panUp);
            _burntimeApp.InputManager.SetDown(InputSource.GamepadOne, InputAction.PanCameraDown, panDown);
            _burntimeApp.InputManager.SetDown(InputSource.GamepadOne, InputAction.PanCameraLeft, panLeft);
            _burntimeApp.InputManager.SetDown(InputSource.GamepadOne, InputAction.PanCameraRight, panRight);

            foreach (GamepadControl control in Enum.GetValues<GamepadControl>())
            {
                if (control == GamepadControl.None)
                    continue;

                bool isDown = IsControlDown(gamePad, control);
                bool wasDown = IsControlDown(previous, control);
                DeviceManager.SetGamepadControlDown(control, isDown);
                if (isDown && !wasDown)
                {
                    _burntimeApp.LastInputMode = InputMode.Gamepad;
                    DeviceManager.GamepadControlPress(control);
                }
            }

            bool stickNavigationActive = up || down || left || right;
            if (!stickNavigationActive)
                _leftStickNavigationLatched = false;
            else if (!_leftStickNavigationLatched)
            {
                _leftStickNavigationLatched = true;
                InputAction navigationAction = _burntimeApp.SceneManager.UseDiagonalGamepadNavigation &&
                    (up || down) && (left || right)
                    ? (up, left) switch
                    {
                        (true, true) => InputAction.MoveUpLeft,
                        (true, false) => InputAction.MoveUpRight,
                        (false, true) => InputAction.MoveDownLeft,
                        _ => InputAction.MoveDownRight
                    }
                    : GetCardinalStickDirection(gamePad.ThumbSticks.Left.X,
                        gamePad.ThumbSticks.Left.Y, stickThreshold, InputAction.None);
                _burntimeApp.InputManager.Press(navigationAction);
            }
            PressOnRising(panUp, previousPanUp, InputAction.PanCameraUp);
            PressOnRising(panDown, previousPanDown, InputAction.PanCameraDown);
            PressOnRising(panLeft, previousPanLeft, InputAction.PanCameraLeft);
            PressOnRising(panRight, previousPanRight, InputAction.PanCameraRight);
            _previousGamePadState = gamePad;
            _gamePadWasConnected = true;
        }

        static InputAction GetCardinalStickDirection(float x, float y, float threshold,
            InputAction previousDirection)
        {
            float absoluteX = System.Math.Abs(x);
            float absoluteY = System.Math.Abs(y);
            bool horizontal = absoluteX >= threshold;
            bool vertical = absoluteY >= threshold;

            if (!horizontal && !vertical)
                return InputAction.None;
            if (!vertical)
                return x < 0 ? InputAction.MoveLeft : InputAction.MoveRight;
            if (!horizontal)
                return y < 0 ? InputAction.MoveDown : InputAction.MoveUp;

            // Keep the selected axis stable around a diagonal. The other axis
            // must become clearly stronger before navigation changes direction.
            const float axisSwitchMargin = 0.1f;
            if ((previousDirection is InputAction.MoveLeft or InputAction.MoveRight) &&
                absoluteY <= absoluteX + axisSwitchMargin)
                return x < 0 ? InputAction.MoveLeft : InputAction.MoveRight;
            if ((previousDirection is InputAction.MoveUp or InputAction.MoveDown) &&
                absoluteX <= absoluteY + axisSwitchMargin)
                return y < 0 ? InputAction.MoveDown : InputAction.MoveUp;

            return absoluteX > absoluteY
                ? x < 0 ? InputAction.MoveLeft : InputAction.MoveRight
                : y < 0 ? InputAction.MoveDown : InputAction.MoveUp;
        }

        static bool IsControlDown(GamePadState state, GamepadControl control) => control switch
        {
            GamepadControl.A => state.Buttons.A == ButtonState.Pressed,
            GamepadControl.B => state.Buttons.B == ButtonState.Pressed,
            GamepadControl.X => state.Buttons.X == ButtonState.Pressed,
            GamepadControl.Y => state.Buttons.Y == ButtonState.Pressed,
            GamepadControl.Menu => state.Buttons.Start == ButtonState.Pressed,
            GamepadControl.View => state.Buttons.Back == ButtonState.Pressed,
            GamepadControl.LeftShoulder => state.Buttons.LeftShoulder == ButtonState.Pressed,
            GamepadControl.RightShoulder => state.Buttons.RightShoulder == ButtonState.Pressed,
            GamepadControl.LeftStick => state.Buttons.LeftStick == ButtonState.Pressed,
            GamepadControl.RightStick => state.Buttons.RightStick == ButtonState.Pressed,
            GamepadControl.LeftTrigger => state.Triggers.Left >= 0.5f,
            GamepadControl.RightTrigger => state.Triggers.Right >= 0.5f,
            GamepadControl.DPadUp => state.DPad.Up == ButtonState.Pressed,
            GamepadControl.DPadDown => state.DPad.Down == ButtonState.Pressed,
            GamepadControl.DPadLeft => state.DPad.Left == ButtonState.Pressed,
            GamepadControl.DPadRight => state.DPad.Right == ButtonState.Pressed,
            _ => false
        };

        void PressOnRising(bool isDown, bool wasDown, params InputAction[] actions)
        {
            if (!isDown || wasDown)
                return;

            foreach (InputAction action in actions)
                _burntimeApp.InputManager.Press(action);
        }

        protected override void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            lock (_inputGlyphSync)
                _steamInputGlyphs?.RunFrame();
            HandleMouseInput();
            HandleKeyboardInput();
            HandleGamePadInput();
            
            if (_requestFullscreen != _isFullscreen)
            {
                _isFullscreen = _requestFullscreen;
                ApplyGraphicsDeviceResolution(initialize: false, resetWindowSize: true);
            }

            RenderDevice.Update();

            base.Update(gameTime);
        }

        protected override void Draw(Microsoft.Xna.Framework.GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            UpdateFpsCounter();
            RenderDevice.Render((float)gameTime.ElapsedGameTime.TotalSeconds);

            base.Draw(gameTime);
        }

        void UpdateFpsCounter()
        {
            _fpsSampleFrames++;
            long now = Stopwatch.GetTimestamp();
            double sampleSeconds = (now - _fpsSampleStart) / (double)Stopwatch.Frequency;
            if (sampleSeconds < 0.5)
                return;

            _framesPerSecond = (int)System.Math.Round(_fpsSampleFrames / sampleSeconds);
            _fpsSampleFrames = 0;
            _fpsSampleStart = now;
        }

        void IEngine.CenterMouse()
        {
            if (_burntimeApp.RenderMouse && _burntimeApp.MouseInputVisible && IsActive)
            {
                var center = Resolution.Native / 2;
                Mouse.SetPosition(center.x, center.y);
                _previousMousePosition = new Point(center.x, center.y);
            }
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            lock (_inputGlyphSync)
                _steamInputGlyphs?.Dispose();
            base.OnExiting(sender, args);

            Music.StopThread();
            _gameThread.Stop();
            _burntimeApp.Close();
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
        public float MaxLayers => MAX_LAYERS;
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

        public void RenderLine(Platform.Vector2 start, Platform.Vector2 end, PixelColor color)
        {
            var entity = new LineEntity
            {
                Color = new Color(color.r, color.g, color.b, color.a),
                Start = new Vector3(start.x, start.y, CalcZ(Layer)),
                End = new Vector3(end.x, end.y, CalcZ(Layer))
            };
            RenderDevice.AddEntity(entity);
        }

        public void RenderSprite(ISprite sprite, Platform.Vector2 pos, float alpha = 1)
        {
            if (sprite is not MonoGame.Graphics.Sprite nativeSprite || !nativeSprite.Touch()) return;

            // Module.Render draws the software mouse cursor at the reserved top
            // layer. Keep it out of XBR together with post-filtered text for now.
            bool postFilter = Layer >= MAX_LAYERS - 1;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - nativeSprite.Frame.TimeStamp < (long)(Stopwatch.Frequency / popInSpeed) && popInSpeed != 0)
                alpha *= (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed;

            Graphics.SpriteEntity entity = new()
            {
                Rectangle = new Rectangle(0, 0, nativeSprite.OriginalSize.x, nativeSprite.OriginalSize.y),
                Color = new Color(alpha, alpha, alpha, alpha),
                Factor = nativeSprite.Frame.Resolution,
                LinearFiltering = nativeSprite.LinearFiltering,
                PostFilter = postFilter
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
                Factor = nativeSprite.Frame.Resolution,
                LinearFiltering = nativeSprite.LinearFiltering,
                PostFilter = postFilter
            };
            RenderDevice.AddEntity(entity2);
        }

        public void RenderSprite(ISprite sprite, Platform.Vector2 pos, Platform.Vector2 srcPos, int srcWidth, int srcHeight, PixelColor color)
        {
            RenderSpriteF(sprite, (Platform.Vector2f)pos, srcPos, srcWidth, srcHeight, color);
        }

        public void RenderSpriteF(ISprite sprite, Platform.Vector2f pos,
            Platform.Vector2 srcPos, int srcWidth, int srcHeight, PixelColor color,
            bool postFilter = false)
        {
            if (sprite is not MonoGame.Graphics.Sprite nativeSprite || !nativeSprite.Touch()) return;

            Graphics.SpriteEntity entity = new()
            {
                Rectangle = new Rectangle(srcPos.x, srcPos.y, srcWidth, srcHeight),
                Color = new Color(color.r, color.g, color.b, color.a),
                Factor = nativeSprite.Frame.Resolution,
                LinearFiltering = nativeSprite.LinearFiltering,
                PostFilter = postFilter
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
                Factor = nativeSprite.Frame.Resolution,
                LinearFiltering = nativeSprite.LinearFiltering,
                PostFilter = postFilter
            };
            RenderDevice.AddEntity(entity2);
        }
        #endregion
    }
}
