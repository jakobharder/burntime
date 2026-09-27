using Burntime.Remaster.Logic;
using Burntime.Framework;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Burntime.Remaster
{
    public sealed record ImageSceneRequest(
        string Resource,
        string NextScene = null,
        string SubtitleArgument = null,
        bool FinishClient = false,
        object? NextSceneParameter = null);

    public enum LanguageMode
    {
        Auto,
        English,
        German
    }

    public enum UIHintVisibilityMode
    {
        Full = 0,
        Less = 1,
        Hide = 2
    }

    public class BurntimeClassic : Module
    {
        static readonly HashSet<string> PersistedRootUserSettings = new(
            StringComparer.OrdinalIgnoreCase)
        {
            "music", "map_music", "fullscreen", "output_filtering", "newgfx", "language",
            "controller_glyphs", "prompts"
        };

        public GamepadBindings GamepadBindings { get; } = new();
        public KeyboardBindings KeyboardBindings { get; } = new();
        internal AutosaveManager Autosaves { get; }
        public static new BurntimeClassic Instance
        {
            get { return (BurntimeClassic)instance; }
        }

        public const string SavegameVersion = "1.1";
        public const string PreviousSavegameVersion = "0.1.2";
        public static bool IsSupportedSavegameVersion(string? version) =>
            version == SavegameVersion || version == PreviousSavegameVersion;
        public static string FontName = "font.txt";
        public string VersionLabel { get; set; } = Version;

        private static string? _version;
        public static string Version
        {
            get
            {
                if (_version is null)
                {
                    var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
                    _version = entryAssembly is null
                        ? "?"
                        : System.Reflection.CustomAttributeExtensions
                            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(entryAssembly)?
                            .InformationalVersion ?? "?";
                    _version = _version.Split('+').First();
                }
                return _version;
            }
        }

        // external use
        public override string Title { get { return "Burntime"; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(320, 200) }; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(480, 225), new Vector2(384, 240) }; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(400, 188), new Vector2(384, 240) }; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(640, 300), new Vector2(384, 240) }; } }

        // original size
        public override Vector2 MinResolution { get; } = new Vector2(320, 200);
        //public override Vector2 MinResolution { get; } = new Vector2(352, 220);
        public override Vector2 MaxResolution { get; } = new Vector2(680, 320);

        public override int MaxVerticalResolution => 320;
        //public override int MaxVerticalResolution => 370;

        public bool IsWideScreen { get { return Engine.Resolution.Native.Ratio > 1.5f; } }

        // Burntime's ratio is 8:5. We need to scale height by 1.2 (320x200 where screens today would be multiple of 320x240).
        // But to get a clean tile resolution of 32x38 use 1.1875
        //public override Vector2f RatioCorrection => new(1, 1.0f / 32.0f * 38.0f);
        public override Vector2f RatioCorrection => new(1.0f / 64.0f * 60.0f, 1.0f / 64.0f * 72.0f);

        public BurntimeClassic()
        {
            Autosaves = new AutosaveManager(this);
            KeyboardActionBindings = KeyboardBindings;
            GamepadActionBindings = GamepadBindings;
            FindClassesFromAssembly(typeof(BurntimeClassic).Assembly);
        }

        public bool ChooseLanguageOnStart { get; set; }
        public LanguageMode LanguageSelection { get; private set; } = LanguageMode.Auto;
        public UIHintVisibilityMode UIHintVisibility { get; private set; } = UIHintVisibilityMode.Full;
        public bool ShowUIHints => UIHintVisibility != UIHintVisibilityMode.Hide;

        public override void Start()
        {
            Engine.Music.Enabled = (!DisableMusic) && (MusicMode != MusicModes.Off);

            MouseImage = ResourceManager.GetImage("munt.raw?0");

            SceneManager.SetScene(ChooseLanguageOnStart
                ? "LanguageScene"
                : "IntroScene");
        }

        protected override void OnRun()
        {
            // set user folder to "burntime/" to get systems settings.txt for language code
            FileSystem.SetUserFolder("Burntime");

            Settings = new ConfigFile();
            Settings.Open("settings.txt");
            int musicVolumePercent = int.TryParse(
                Settings["system"].GetString("music_volume"), out int configuredMusicVolume)
                ? System.Math.Clamp(configuredMusicVolume, 0, 100)
                : 100;
            Engine.MusicVolume = musicVolumePercent / 100.0f;
            Engine.Music.Volume = Engine.MusicVolume;

            // set user folder to game specific location
            FileSystem.SetUserFolder("Burntime");

            // read user settings
            UserSettings = new ConfigFile();
            UserSettings.Open("user.txt");
            bool removedKeyboardMappings = UserSettings.RemoveSection("keyboard");
            bool removedGamepadMappings = UserSettings.RemoveSection("gamepad");
            bool removedObsoleteRootSettings = false;
            ConfigSection rootUserSettings = UserSettings[""];
            string[] obsoleteRootSettings = rootUserSettings.Values
                .Select(setting => setting.Key)
                .Where(setting => !PersistedRootUserSettings.Contains(setting))
                .ToArray();
            foreach (string setting in obsoleteRootSettings)
                removedObsoleteRootSettings |= rootUserSettings.Remove(setting);
            bool normalizedRootSettings = false;
            foreach (string setting in PersistedRootUserSettings)
                normalizedRootSettings |= rootUserSettings.NormalizeSingleValue(setting);
            if (removedKeyboardMappings || removedGamepadMappings || removedObsoleteRootSettings ||
                normalizedRootSettings)
                UserSettings.Save("user.txt");
            Engine.ControllerGlyphMode = ParseControllerGlyphMode(
                UserSettings[""].GetString("controller_glyphs"));
            UIHintVisibility = (UIHintVisibilityMode)System.Math.Clamp(
                UserSettings[""].GetInt("prompts"),
                (int)UIHintVisibilityMode.Full,
                (int)UIHintVisibilityMode.Hide);
            KeyboardBindings.Load(Settings);
            GamepadBindings.Load(Settings);
            LanguageSelection = ParseLanguageMode(UserSettings[""].GetString("language"));
            FileSystem.LocalizationCode = ResolveLanguage(LanguageSelection);
            if (Engine.SupportsFullscreenToggle)
                Engine.IsFullscreen = UserSettings[""].GetBool("fullscreen", false);
            base.IsNewGfx = UserSettings[""].GetBool("newgfx", true);
            // The graphics profile owns output filtering. Shader capabilities are
            // known only after RenderDevice initializes, which applies its own
            // supported fallback if the requested shader cannot be loaded.
            Engine.OutputFiltering = GetGraphicsModeFiltering(IsNewGfx,
                requireAvailableShaders: false);
            UserSettings[""].Set("output_filtering",
                FormatOutputFiltering(Engine.OutputFiltering));

            // set language code
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ResourceManager.Encoding = Encoding.UTF8;//Encoding.GetEncoding(852); // DOS central europe

            // legacy clean up
            _ = FileSystem.RemoveFile("user:settings.txt");
            _ = FileSystem.VFS.RemoveFolder("user:deluxe");
            _ = FileSystem.VFS.MoveFolder("user:classic/savegame", "user:saves");
            _ = FileSystem.VFS.RemoveFolder("user:classic");

            FileSystem.AddPackage("music", "game/music");
            FileSystem.AddPackage("amiga", "game/amiga");
            HasDosMusic = FileSystem.ExistsFile("songs_dos.txt") && FileSystem.ExistsFile("song_intro.ogg");
            HasAmigaMusic = FileSystem.ExistsFile("songs_amiga.txt");

            SetMusicMode(UserSettings[""].GetString("music"));
            SetMapMusicMode(UserSettings[""].GetString("map_music"));

            // add newgfx package
            if (IsNewGfx)
            {
                FileSystem.AddPackage("newgfx", "game/classic_newgfx");
            }
            RefreshResourceReplacements();
#warning TODO Santa for NewGfx (only)
            //else if (DateTime.Now.Month == 12 && 
            //    (DateTime.Now.Day >= 24 && DateTime.Now.Day <= 31 || DateTime.Now.Day == 6))
            //{
            //    ResourceManager.SetResourceReplacement("santa.txt");
            //}

            FontName = "font.txt";
        }

        public void InitializeHeadless()
        {
            instance = this;

            Settings = new ConfigFile();
            Settings.Open("settings.txt");
            RefreshResourceReplacements();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ResourceManager.Encoding = Encoding.UTF8;
        }

        protected override void OnProcess(float elapsed)
        {
            if (MusicMode != MusicModes.Off)
            {
                Key[] keys = DeviceManager.Keyboard.Keys;
                foreach (Key key in keys)
                {
                    if (key.IsVirtual && key.VirtualKey == SystemKey.F9)
                    {
                        ToggleMusicMode();
                        break;
                    }
                }
            }
        }

        protected override void OnClose()
        {
            // ensure section is created
            UserSettings.GetSection("", true);

            UserSettings[""].Set("music", GetMusicMode());
            UserSettings[""].Set("map_music", (int)MapMusicMode);
            if (Engine.SupportsFullscreenToggle)
                UserSettings[""].Set("fullscreen", Engine.IsFullscreen);
            UserSettings[""].Set("output_filtering", FormatOutputFiltering(Engine.OutputFiltering));
            UserSettings[""].Set("newgfx", IsNewGfx);
            UserSettings[""].Set("language", FormatLanguageMode(LanguageSelection));
            UserSettings[""].Set("controller_glyphs", FormatControllerGlyphMode(Engine.ControllerGlyphMode));
            UserSettings[""].Set("prompts", (int)UIHintVisibility);
            UserSettings.Save("user.txt");
        }

        public void CycleControllerGlyphMode()
        {
            Engine.ControllerGlyphMode = Engine.ControllerGlyphMode switch
            {
                ControllerGlyphMode.Auto => ControllerGlyphMode.Xbox,
                ControllerGlyphMode.Xbox => ControllerGlyphMode.PlayStation,
                ControllerGlyphMode.PlayStation => ControllerGlyphMode.Steam,
                ControllerGlyphMode.Steam => ControllerGlyphMode.Switch,
                _ => ControllerGlyphMode.Auto
            };
        }

        public void CycleUIHintVisibilityMode()
        {
            UIHintVisibility = UIHintVisibility == UIHintVisibilityMode.Hide
                ? UIHintVisibilityMode.Full
                : UIHintVisibilityMode.Hide;
        }

        static ControllerGlyphMode ParseControllerGlyphMode(string value) =>
            value.Trim().ToLowerInvariant() switch
            {
                "xbox" => ControllerGlyphMode.Xbox,
                "playstation" => ControllerGlyphMode.PlayStation,
                "steam" => ControllerGlyphMode.Steam,
                "switch" => ControllerGlyphMode.Switch,
                _ => ControllerGlyphMode.Auto
            };

        static string FormatControllerGlyphMode(ControllerGlyphMode mode) => mode switch
        {
            ControllerGlyphMode.Xbox => "xbox",
            ControllerGlyphMode.PlayStation => "playstation",
            ControllerGlyphMode.Steam => "steam",
            ControllerGlyphMode.Switch => "switch",
            _ => "auto"
        };

        public void CycleLanguageMode()
        {
            LanguageSelection = LanguageSelection switch
            {
                LanguageMode.Auto => LanguageMode.English,
                LanguageMode.English => LanguageMode.German,
                _ => LanguageMode.Auto
            };
            Language = ResolveLanguage(LanguageSelection);
        }

        public void SelectLanguage(string language)
        {
            LanguageSelection = language.Equals("de", StringComparison.OrdinalIgnoreCase)
                ? LanguageMode.German
                : LanguageMode.English;
            Language = ResolveLanguage(LanguageSelection);
        }

        static LanguageMode ParseLanguageMode(string value) => value.Trim().ToLowerInvariant() switch
        {
            "en" or "english" => LanguageMode.English,
            "de" or "german" => LanguageMode.German,
            _ => LanguageMode.Auto
        };

        static string FormatLanguageMode(LanguageMode mode) => mode switch
        {
            LanguageMode.English => "en",
            LanguageMode.German => "de",
            _ => "auto"
        };

        string ResolveLanguage(LanguageMode mode) => mode switch
        {
            LanguageMode.English => "en",
            LanguageMode.German => "de",
            _ => Engine.AutomaticLanguage
        };

        static string FormatOutputFiltering(OutputFiltering filtering) => filtering switch
        {
            OutputFiltering.NearestPoint => "point",
            OutputFiltering.Linear => "linear",
            OutputFiltering.Xbr2 => "smooth",
            _ => "sharp"
        };

        OutputFiltering GetGraphicsModeFiltering(bool newGfx,
            bool requireAvailableShaders = true)
        {
            if (Engine.ForceLinearOutputFiltering)
                return OutputFiltering.Linear;
            if (Engine.ForceNearestPointOutputFiltering)
                return OutputFiltering.NearestPoint;
            if (Engine.DisableShaders)
                return OutputFiltering.SharpBilinear;

            if (newGfx)
                return !requireAvailableShaders || Engine.SupportsXbr2Shader
                    ? OutputFiltering.Xbr2
                    : OutputFiltering.SharpBilinear;

            return !requireAvailableShaders || Engine.SupportsSharpBilinearShader
                ? OutputFiltering.SharpBilinearShader
                : OutputFiltering.SharpBilinear;
        }

        // internal use
        public bool IsInGame = false;
        public int InfoCity = -1;
        public int InventoryBackground = -1;
        public Room InventoryRoom = null;
        public PickItemList PickItems = null;

        public void SetScene(string scene, object? parameter = null,
            string? introScene = null)
        {
            if (introScene == null)
                SceneManager.SetScene(scene, parameter);
            else
                SceneManager.SetScene("ImageScene", new ImageSceneRequest(
                    introScene, scene, NextSceneParameter: parameter));
        }

        public void SetImageScene(string imageScene,
            string subtitleArgument = null, bool finishClient = false)
        {
            SceneManager.SetScene("ImageScene", new ImageSceneRequest(
                imageScene, SubtitleArgument: subtitleArgument,
                FinishClient: finishClient));
        }

        public int PreviousPlayerId = -1;
        public bool ShowManualOnNextWorldMap;
        public bool NewGui = false;

        public override bool IsNewGfx
        {
            get => base.IsNewGfx;
            set
            {
                OutputFiltering filtering = GetGraphicsModeFiltering(value);
                if (base.IsNewGfx == value && Engine.OutputFiltering == filtering)
                    return;

                Engine.OutputFiltering = filtering;
                base.IsNewGfx = value;
                RefreshNewGfx();
            }
        }

        #region Music
        public bool DisableMusic => !HasAmigaMusic && !HasDosMusic;
        public bool HasAmigaMusic { get; private set; }
        public bool HasDosMusic { get; private set; }
        private string? _lastPlayingSong;
        private string _mapPlaylistFile = "playlist_dos.txt";

        public enum MusicModes
        {
            Off = 0,
            Amiga = 1,
            Dos = 2,
            Remaster = 3
        }

        public MusicModes MusicMode { get; private set; } = MusicModes.Remaster;
        public Burntime.Platform.MapMusicMode MapMusicMode => Engine.MapMusicMode;

        public void SetMusicMode(string mode)
        {
            mode = mode?.ToLower();

            if (DisableMusic)
                MusicMode = MusicModes.Off;
            else if ((mode == "amiga" && HasAmigaMusic)
                || (!HasDosMusic && HasAmigaMusic))
                MusicMode = MusicModes.Amiga;
            else if (mode == "off")
                MusicMode = MusicModes.Off;
            else if (HasDosMusic)
                MusicMode = MusicModes.Remaster;
            else
                MusicMode = MusicModes.Off;

            // MusicModes.Off loads the DOS profile to ensure the jukebox works
            // even when the game starts with music disabled.
            if (!DisableMusic)
                LoadMusicProfile();
        }

        public string GetMusicMode() => MusicMode switch
        {
            MusicModes.Off => "off",
            MusicModes.Amiga => "amiga",
            _ => "remaster"
        };

        void SetMapMusicMode(string mode)
        {
            if (int.TryParse(mode, out int value) &&
                Enum.IsDefined(typeof(Burntime.Platform.MapMusicMode), value))
            {
                Engine.MapMusicMode = (Burntime.Platform.MapMusicMode)value;
            }
            else
            {
                Engine.MapMusicMode = Burntime.Platform.MapMusicMode.None;
            }
        }

        public void CycleMapMusicMode()
        {
            Engine.MapMusicMode = (Burntime.Platform.MapMusicMode)
                (((int)Engine.MapMusicMode + 1) % 3);
        }

        /// <summary>
        /// Toggle between Amiga and remaster.
        /// </summary>
        public void ToggleMusicMode()
        {
            if (DisableMusic) return;

            if (MusicMode == MusicModes.Amiga && HasDosMusic)
            {
                MusicMode = MusicModes.Remaster;
                Engine.Music.Enabled = true;
                LoadMusicProfile();
            }
            else if ((MusicMode == MusicModes.Dos || MusicMode == MusicModes.Remaster)
                && HasAmigaMusic)
            {
                MusicMode = MusicModes.Amiga;
                Engine.Music.Enabled = true;
                LoadMusicProfile();
            }
        }

        /// <summary>
        /// Cycle through Amiga, DOS, remaster and off.
        /// </summary>
        public void CycleMusicMode()
        {
            if (DisableMusic) return;

            if (MusicMode == MusicModes.Off && HasDosMusic)
            {
                MusicMode = MusicModes.Remaster;
                Engine.Music.Enabled = true;
                LoadMusicProfile();
                Engine.Music.Play(ResumeSongOrRadio());
            }
            else if ((MusicMode == MusicModes.Off && HasAmigaMusic)
                || (MusicMode == MusicModes.Remaster && HasAmigaMusic))
            {
                MusicMode = MusicModes.Amiga;
                Engine.Music.Enabled = true;
                LoadMusicProfile();
                if (Engine.Music.Playing is null)
                    Engine.Music.Play(ResumeSongOrRadio());
            }
            else if (MusicMode == MusicModes.Amiga
                || (MusicMode == MusicModes.Remaster && !HasAmigaMusic))
            {
                // we cycle over off mode, so we need to save the song to replay
                _lastPlayingSong = Engine.Music.Playing;
                MusicMode = MusicModes.Off;
                Engine.Music.Enabled = false;
                Engine.Music.Stop();
            }
        }

        string ResumeSongOrRadio() => _lastPlayingSong is not null && Engine.Music.CanPlay(_lastPlayingSong)
            ? _lastPlayingSong
            : "radio";

        void LoadMusicProfile()
        {
            bool amiga = MusicMode == MusicModes.Amiga;
            string profileFile = amiga ? "music_amiga.txt" : "music_dos.txt";
            string songlistFile = amiga ? "songs_amiga.txt" : "songs_dos.txt";
            _mapPlaylistFile = amiga ? "playlist_amiga.txt" : "playlist_dos.txt";

            var profile = new ConfigFile();
            if (profile.Open(profileFile))
            {
                ConfigSection settings = profile[""];
                string configuredSonglist = settings.GetString("songlist");
                string configuredPlaylist = settings.GetString("playlist");
                if (!string.IsNullOrWhiteSpace(configuredSonglist))
                    songlistFile = configuredSonglist;
                if (!string.IsNullOrWhiteSpace(configuredPlaylist))
                    _mapPlaylistFile = configuredPlaylist;
            }

            Engine.Music.LoadSonglist(songlistFile);
            Engine.Music.LoadPlaylist(_mapPlaylistFile);
        }

        #endregion

        public override string Language
        { 
            get => base.Language;
            set { if (base.Language != value) { base.Language = value; ResourceManager.ClearText(); Engine.ReloadGraphics(); } }
        }

        public Character SelectedCharacter => ((Player)GameState.CurrentPlayer).SelectedCharacter;
        public ClassicGame Game => GameState as ClassicGame;

        void RefreshNewGfx()
        {
            FontName = "font.txt";

            if (IsNewGfx)
            {
                FileSystem.AddPackage("newgfx", "game/classic_newgfx");
            }
            else
            {
                FileSystem.RemovePackage("newgfx");
            }
            RefreshResourceReplacements();

            Engine.ReloadGraphics();
            SceneManager.ResizeScene(reload: true);
        }

        public void RefreshResourceReplacements()
        {
            List<string> replacements = [];
            // Both packages provide this profile. In classic it remaps virtual
            // character-body ranges to frames that exist in the original RAW.
            if (FileSystem.ExistsFile("newgfx.txt"))
                replacements.Add("newgfx.txt");
            if (Engine?.OutputFiltering == OutputFiltering.Xbr2 &&
                FileSystem.ExistsFile("xbr2.txt"))
                replacements.Add("xbr2.txt");
            ResourceManager.SetResourceReplacements(replacements.ToArray());
        }
    }
}
