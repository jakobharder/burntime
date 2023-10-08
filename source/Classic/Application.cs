﻿using Burntime.Classic.Logic;
using Burntime.Framework;
using Burntime.Platform;
using Burntime.Platform.IO;
using System;
using System.Text;

namespace Burntime.Classic
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

        // external use
        public override string Title { get { return "Burntime"; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(320, 200) }; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(480, 225), new Vector2(384, 240) }; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(400, 188), new Vector2(384, 240) }; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(640, 300), new Vector2(384, 240) }; } }

        public override int MaxVerticalResolution => 288;

        public bool IsWideScreen { get { return Engine.Resolution.Native.Ratio > 1.5f; } }

        // Burntime's ratio is 8:5. We need to scale height by 1.2 (320x200 where screens today would be multiple of 320x240).
        // But to get a clean tile resolution of 32x38 use 1.1875
        //public override Vector2f RatioCorrection => new(1, 1.0f / 32.0f * 38.0f);
        public override Vector2f RatioCorrection => new(1.0f/ 64.0f * 60.0f, 1.0f / 64.0f * 72.0f);

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
#warning TODO SlimDX/Mono Music
            //Engine.Music.Enabled = (!DisableMusic) & MusicPlayback;

            MouseImage = ResourceManager.GetImage("munt.raw");
            SceneManager.SetScene("IntroScene");
        }

        protected override void OnRun()
        {
            FileSystem.AddPackage("music", "game/classic_music");

            // set user folder to "burntime/" to get systems settings.txt for language code
            FileSystem.SetUserFolder("Burntime");

            Settings = new ConfigFile();
            Settings.Open("settings.txt");

            // set language code
            FileSystem.LocalizationCode = Settings["game"].GetString("language");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ResourceManager.Encoding = Encoding.GetEncoding(852); // DOS central europe

            // set user folder to game specific location
            FileSystem.SetUserFolder("Burntime/Classic");

            // reload settings
            Settings.Open("settings.txt");

            //Engine.Resolution.Native = new Vector2(512, 200);//Settings["system"].GetVector2("resolution");
#warning TODO SlimDX/Mono full screen
            //Engine.FullScreen = !Settings["system"].GetBool("windowmode");
            //Engine.UseTextureFilter = Settings["system"].GetBool("filter");

            // check music playback settings
            MusicPlayback = Settings["system"].GetBool("music");
            // check if ogg files are available
            DisableMusic = !FileSystem.ExistsFile("01_MUS 01_HSC.ogg"); //  || System.IntPtr.Size != 4

            bool useHighResFont = Settings["system"].GetBool("highres_font");

            // add newgfx package
            if (NewGfx)
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

        protected override void OnClose()
        {
            Settings["system"].Set("music", MusicPlayback);
            Settings.Save("settings.txt");
        }

        // internal use
        public bool IsInGame = false;
        public int InfoCity = -1;
        public int InventoryBackground = -1;
        public Room InventoryRoom = null;
        public String ImageScene = null;
        public PickItemList PickItems = null;
        public ActionAfterImageScene ActionAfterImageScene = ActionAfterImageScene.None;
        public bool MusicPlayback;
        public bool DisableMusic;
        public int PreviousPlayerId = -1;
        public bool NewGui = false;

#warning TODO make this a setting
        public bool NewGfx = true;

        public Character SelectedCharacter
        {
            get { return ((Player)GameState.CurrentPlayer).SelectedCharacter; }
        }

        public ClassicGame Game
        {
            get { return GameState as ClassicGame; }
        }

        public override void ToggleNewGfx()
        {
            NewGfx = !NewGfx;
            if (NewGfx)
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
                Engine.ReloadGraphics();
            }
            else
            {
                FileSystem.RemovePackage("newgfx");
                ResourceManager.SetResourceReplacement(null);
                Engine.ReloadGraphics();
            }
        }
    }
}
