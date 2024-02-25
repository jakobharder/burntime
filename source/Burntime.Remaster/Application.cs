using Burntime.Remaster.Logic;
using Burntime.Framework;
using Burntime.Platform;
using Burntime.Platform.IO;
using System;
using System.Text;
using Burntime.Remaster;
using static Burntime.Remaster.BurntimeClassic;
using System.Diagnostics;

namespace Burntime.Remaster
{
    public enum ActionAfterImageScene
    {
        None,
        Trader,
        Doctor,
        Pub,
        Restaurant
    }

    public class BurntimeClassic : Module
    {
        public static new BurntimeClassic Instance
        {
            get { return (BurntimeClassic)instance; }
        }

        public static string SavegameVersion = "0.1.2";
        public static string FontName = "font.txt";

        public static readonly PixelColor LightGray = new(212, 212, 212);
        public static readonly PixelColor Gray = new(184, 184, 184);

        private static string? _version;
        public static string Version
        {
            get
            {
                if (_version is null)
                    _version = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(System.AppContext.BaseDirectory, "burntime.exe")).ProductVersion ?? "?";
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

        public override System.Drawing.Icon Icon
        {
            get
            {
                return new System.Drawing.Icon(GetType(), "icon256.ico");
            }
        }

        public BurntimeClassic()
        {
            FindClassesFromAssembly(typeof(BurntimeClassic).Assembly);
        }

        public override void Start()
        {
            Engine.Music.Enabled = (!DisableMusic) && (MusicMode != MusicModes.Off);

            MouseImage = ResourceManager.GetImage("munt.raw");

            SceneManager.SetScene(string.IsNullOrEmpty(FileSystem.LocalizationCode) ? "LanguageScene" : "IntroScene");
        }

        protected override void OnRun()
        {
            // set user folder to "burntime/" to get systems settings.txt for language code
            FileSystem.SetUserFolder("Burntime");

            Settings = new ConfigFile();
            Settings.Open("settings.txt");

            // set user folder to game specific location
            FileSystem.SetUserFolder("Burntime");

            // read user settings
            UserSettings = new ConfigFile();
            UserSettings.Open("user.txt");
            FileSystem.LocalizationCode = UserSettings[""].GetString("language");
            Engine.IsFullscreen = UserSettings[""].GetBool("fullscreen", false);
            base.IsNewGfx = UserSettings[""].GetBool("newgfx", true);

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

            bool useHighResFont = Settings["system"].GetBool("highres_font");

            // add newgfx package
            if (IsNewGfx)
            {
                FileSystem.AddPackage("newgfx", "game/classic_newgfx");
                if (FileSystem.ExistsFile("newgfx.txt"))
                {
                    ResourceManager.SetResourceReplacement("newgfx.txt");

                    // use highres font anyway
                    useHighResFont = true;
                }
            }
#warning TODO Santa for NewGfx (only)
            //else if (DateTime.Now.Month == 12 && 
            //    (DateTime.Now.Day >= 24 && DateTime.Now.Day <= 31 || DateTime.Now.Day == 6))
            //{
            //    ResourceManager.SetResourceReplacement("santa.txt");
            //}

            // set highres font
            if (useHighResFont)
            {
                if (FileSystem.ExistsFile("highres-font.txt"))
                    FontName = "highres-font.txt";
            }
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
            UserSettings[""].Set("fullscreen", Engine.IsFullscreen);
            UserSettings[""].Set("newgfx", IsNewGfx);
            UserSettings[""].Set("language", FileSystem.LocalizationCode);
            UserSettings.Save("user.txt");
        }

        // internal use
        public bool IsInGame = false;
        public int InfoCity = -1;
        public int InventoryBackground = -1;
        public Room InventoryRoom = null;
        public String ImageScene = null;
        public PickItemList PickItems = null;
        public ActionAfterImageScene ActionAfterImageScene = ActionAfterImageScene.None;

        public int PreviousPlayerId = -1;
        public bool NewGui = false;

        public override bool IsNewGfx
        {
            get => base.IsNewGfx;
            set { base.IsNewGfx = value; RefreshNewGfx(); }
        }

        #region Music
        public bool DisableMusic => !HasAmigaMusic && !HasDosMusic;
        public bool HasAmigaMusic { get; private set; }
        public bool HasDosMusic { get; private set; }
        private string _lastPlayingSong;

        public enum MusicModes
        {
            Off = 0,
            Amiga = 1,
            Dos = 2,
            Remaster = 3
        }

        public MusicModes MusicMode { get; private set; } = MusicModes.Remaster;

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

            //if (MusicMode != MusicModes.Off)
            // MusicModes.Off loads songs_dos to ensure jukebox working even when started with off
            if (!DisableMusic)
                Engine.Music.LoadSonglist(MusicMode == MusicModes.Amiga ? "songs_amiga.txt" : "songs_dos.txt");
        }

        public string GetMusicMode() => MusicMode switch
        {
            MusicModes.Off => "off",
            MusicModes.Amiga => "amiga",
            _ => "remaster"
        };

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
                Engine.Music.LoadSonglist("songs_dos.txt");
            }
            else if ((MusicMode == MusicModes.Dos || MusicMode == MusicModes.Remaster)
                && HasAmigaMusic)
            {
                MusicMode = MusicModes.Amiga;
                Engine.Music.Enabled = true;
                Engine.Music.LoadSonglist("songs_amiga.txt");
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
                Engine.Music.LoadSonglist("songs_dos.txt");
                Engine.Music.Play(_lastPlayingSong ?? "radio");
            }
            else if ((MusicMode == MusicModes.Off && HasAmigaMusic)
                || (MusicMode == MusicModes.Remaster && HasAmigaMusic))
            {
                MusicMode = MusicModes.Amiga;
                Engine.Music.Enabled = true;
                Engine.Music.LoadSonglist("songs_amiga.txt");
                if (Engine.Music.Playing is null)
                    Engine.Music.Play(_lastPlayingSong ?? "radio");
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
            if (IsNewGfx)
            {
                FileSystem.AddPackage("newgfx", "game/classic_newgfx");
                if (FileSystem.ExistsFile("newgfx.txt"))
                {
                    ResourceManager.SetResourceReplacement("newgfx.txt");

                    // use highres font anyway
                    if (FileSystem.ExistsFile("highres-font.txt"))
                        FontName = "highres-font.txt";
                }
                else
                {
                    ResourceManager.SetResourceReplacement(null);
                }
            }
            else
            {
                FileSystem.RemovePackage("newgfx");
                ResourceManager.SetResourceReplacement(null);
            }

            Engine.ReloadGraphics();
            SceneManager.ResizeScene();
        }
    }
}
