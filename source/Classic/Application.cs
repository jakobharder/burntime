using System;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Classic.Logic;
using Burntime.Platform.Resource;
using System.Collections.Generic;

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
        public override String Title { get { return "Burntime"; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(320, 200) }; } }
        public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(480, 225), new Vector2(384, 240) }; } }
        //public override Vector2[] Resolutions { get { return new Vector2[] { new Vector2(640, 300), new Vector2(384, 240) }; } }

        public bool IsWideScreen { get { return Engine.Resolution.x / (float)Engine.Resolution.y > 1.5f; } }

        // Burntime's ratio is 8:5. We need to scale height by 1.2 (320x200 where screens today would be multiple of 320x240).
        // But to get a clean tile resolution of 32x38 use 1.1875
        public override float VerticalRatio { get { return 1.0f / 32.0f * 38.0f; } }

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
#warning slimdx todo
            //Engine.Music.Enabled = (!DisableMusic) & MusicPlayback;

            MouseImage = ResourceManager.GetImage("munt.raw");
            SceneManager.SetScene("IntroScene");
        }

        protected override void OnInitialize()
        {


            base.OnInitialize();
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

            Engine.Resolution = Settings["system"].GetVector2("resolution");
#warning slimdx todo
            //Engine.FullScreen = !Settings["system"].GetBool("windowmode");
            //Engine.UseTextureFilter = Settings["system"].GetBool("filter");

            // check music playback settings
            MusicPlayback = Settings["system"].GetBool("music");
            // check if ogg files are available
            DisableMusic = !FileSystem.ExistsFile("01_MUS 01_HSC.ogg"); //  || System.IntPtr.Size != 4

            bool useHighResFont = Settings["system"].GetBool("highres_font");

            // add gfx packages
            /*if (Settings["system"].GetBool("2xgfx"))
            {
                FileSystem.AddPackage("gfx", "game/gfx");
                FileSystem.AddPackage("tiles", "game/tiles");
                ResourceManager.SetResourceReplacement("2xgfx.txt");
            }*/

            // add newgfx package
            if (Settings["system"].GetBool("newgfx"))
            {
                FileSystem.AddPackage("newgfx", "game/classic_newgfx");
                if (FileSystem.ExistsFile("newgfx.txt"))
                {
                    ResourceManager.SetResourceReplacement("newgfx.txt");

                    // use highres font anyway
                    useHighResFont = true;
                }
            }
            else if (DateTime.Now.Month == 12 && 
                (DateTime.Now.Day >= 24 && DateTime.Now.Day <= 31 || DateTime.Now.Day == 6))
            {
                ResourceManager.SetResourceReplacement("santa.txt");
            }

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
        public bool NewGfx = false;

        public Character SelectedCharacter
        {
            get { return ((Player)GameState.CurrentPlayer).SelectedCharacter; }
        }

        public ClassicGame Game
        {
            get { return GameState as ClassicGame; }
        }
    }
}
