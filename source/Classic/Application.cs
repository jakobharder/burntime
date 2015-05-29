
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Classic.Logic;

namespace Burntime.Classic
{
    enum ActionAfterImageScene
    {
        None,
        Trader,
        Doctor,
        Pub,
        Restaurant
    }

    class BurntimeClassic : Module
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

        public bool IsWideScreen { get { return Engine.Resolution.x / (float)Engine.Resolution.y > 1.5f; } }

        // burntime's 8:5 to 4:3 ratio correction
        public override float VerticalRatio { get { return 1.0f / 200.0f * 240.0f; } }

        public override System.Drawing.Icon Icon
        {
            get
            {
                return new System.Drawing.Icon(GetType(), "icon256.ico");
            }
        }

        public override void Start()
        {
            Engine.Music.Enabled = (!DisableMusic) & MusicPlayback;

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
            ResourceManager.Encoding = Encoding.GetEncoding(852); // DOS central europe

            // set user folder to game specific location
            FileSystem.SetUserFolder("Burntime/Classic");

            // reload settings
            Settings.Open("settings.txt");

            Engine.Resolution = Settings["system"].GetVector2("resolution");
            Engine.FullScreen = !Settings["system"].GetBool("windowmode");
            Engine.UseTextureFilter = Settings["system"].GetBool("filter");

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
