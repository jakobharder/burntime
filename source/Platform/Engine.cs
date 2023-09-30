using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

using Burntime.Platform.IO;
using SlimDX.Direct3D9;

namespace Burntime.Platform
{
    public class Engine
    {
        Graphics.RenderDevice device;
        internal Graphics.RenderDevice Device
        {
            get { return device; }
        }

        Music.MusicPlayback music;
        public Music.MusicPlayback Music
        {
            get { return music; }
        }

        const int VERTEX_STRIDE = 28;
        const VertexFormat VERTEX_FORMAT = VertexFormat.PositionRhw | VertexFormat.Texture1 | VertexFormat.Diffuse;

        const float MAX_LAYERS = 256;

        int resWidth = 640;
        int resHeight = 480;
        int gameWidth = 640;
        int gameHeight = 480;
        Vector2[] gameResolutions = new Vector2[] { new Vector2(640, 480) };
        float verticalRatio = 1;

        float layer;
        bool musicBlend = true;

        Vector2f scale;
        public Vector2f Scale
        {
            get { return scale; }
            set { scale = value; }
        }

        public Vector2 Resolution
        {
            set { SetResolution(value.x, value.y); }
            get { return new Vector2(resWidth, resHeight); }
        }

        public Vector2 GameResolution
        {
            get { return new Vector2(gameWidth, gameHeight); }
        }

        bool fullScreen;
        public bool FullScreen
        {
            get { return fullScreen; }
            set { fullScreen = value; }
        }

        bool useTextureFilter;
        public bool UseTextureFilter
        {
            get { return useTextureFilter; }
            set { useTextureFilter = value; }
        }

        float popInSpeed = 16.0f;

        // application
        IApplication app;
        internal IApplication Application
        {
            get { return app; }
        }

        RenderForm form;

        // process thread
        GameTime processTime;
        Thread processThread;
        bool requestExit = false;

        bool safeMode;
        public bool SafeMode
        {
            get { return safeMode; }
            set { safeMode = value; }
        }

        internal bool crashed = false;
        //bool crashCanRender = true;

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

        public int Layer
        {
            set { layer = MAX_LAYERS - value; }
            get { return (int)MAX_LAYERS - (int)layer; }
        }

        #region scene blending methods
        public float Blend
        {
            get { return device.Blend; }
            set { device.Blend = value; }
        }

        public bool IsBlended
        {
            get { return device.IsBlended; }
        }

        public float BlendSpeed
        {
            get { return device.BlendSpeed; }
            set { device.BlendSpeed = value; }
        }

        public bool BlockBlend
        {
            get { return device.BlockBlend; }
            set { device.BlockBlend = value; }
        }

        public void WaitForBlend()
        {
            device.WaitForBlend();
        }

        public bool MusicBlend
        {
            get { return musicBlend; }
            set { musicBlend = value; }
        }
        #endregion

        public void ReloadGraphics()
        {
            ResourceManager.ReleaseAll();
        }

        public void SetResolution(int width, int height)
        {
            resWidth = width;
            resHeight = height;

            // select best game resolution
            SelectBestGameResolution();
        }

        public void SetGameResolution(float verticalRatio, params Vector2[] size)
        {
            gameResolutions = size;
            this.verticalRatio = verticalRatio;

            // select best game resolution
            SelectBestGameResolution();
        }

        private bool IsCleanZoom(float zoom)
        {
            return zoom == 4 || zoom == 3 || zoom == 2 || zoom == 1 || zoom == 0.5f;
        }

        private void SelectBestGameResolution()
        {
            float bestRatio = -1000;
            float bestZoom = 1000;
            int bestIndex = 0;
            float realRatio = resWidth / (float)resHeight;

            for (int i = 0; i < gameResolutions.Length; i++)
            {
                float ratio = gameResolutions[i].x / (float)gameResolutions[i].y / verticalRatio;
                float zoom = resHeight / (float)gameResolutions[i].y;

                // select best ratio
                if (System.Math.Abs(ratio - realRatio) < System.Math.Abs(bestRatio - realRatio))
                {
                    bestRatio = ratio;
                    bestZoom = zoom;
                    bestIndex = i;
                }

                // if more than one resolutions with that ratio is available, choose best size (prefere too big than too small)
                else if (System.Math.Abs(ratio - realRatio) == System.Math.Abs(bestRatio - realRatio) &&
                    (IsCleanZoom(zoom) && !IsCleanZoom(bestZoom) || zoom <= 1 && bestZoom < zoom || zoom >= 1 && bestZoom < 1))
                {
                    bestZoom = zoom;
                    bestIndex = i;
                }
            }

            gameWidth = gameResolutions[bestIndex].x;
            gameHeight = gameResolutions[bestIndex].y;

            scale = new Vector2f();
            scale.x = (float)resWidth / (float)gameWidth;
            scale.y = (float)resHeight / (float)gameHeight;
        }

        #region render methods
        public void RenderRect(Vector2 pos, Vector2 size, int color)
        {
            Graphics.SpriteEntity entity = new Graphics.SpriteEntity();
            entity.Rectangle = new Rectangle(0, 0, size.x, size.y);
            entity.Color = new SlimDX.Color4(color);

            entity.Texture = device.emptyTexture;
            entity.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer));
            device.AddEntity(entity);
        }

        public void RenderFrame(Vector2 pos, Vector2 size, int color)
        {
            //Rectangle rc = new Rectangle(0, 0, size.x, size.y);
            //spriteRenderer.Draw(emptyTexture, rc, new SlimDX.Vector3(0, 0, 0), new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer)), new SlimDX.Color4(color));
        }

        public void RenderSprite(Graphics.Sprite sprite, Vector2 pos)
        {
            RenderSprite(sprite, pos, 1);
        }

        public void RenderSprite(Graphics.Sprite sprite, Vector2 pos, float alpha)
        {
            Graphics.SpriteEntity entity = new Graphics.SpriteEntity();
            entity.Rectangle = new Rectangle(0, 0, sprite.OriginalSize.x, sprite.OriginalSize.y);
            entity.Color = new SlimDX.Color4(alpha, 1, 1, 1);
            entity.Factor = sprite.Frame.Resolution;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - sprite.Frame.TimeStamp < (long)(Stopwatch.Frequency / popInSpeed) && popInSpeed != 0)
            {
                entity.Color.Alpha *= (now - sprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed;
            }

            if (sprite.Animation != null && sprite.Animation.Progressive && sprite.Frames != null)
            {
                entity.Texture = sprite.Frames[0].Texture;
                entity.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer) + 0.001f);
                device.AddEntity(entity);
            }

            Graphics.SpriteEntity entity2 = new Burntime.Platform.Graphics.SpriteEntity();
            entity2.Rectangle = entity.Rectangle;
            entity2.Color = entity.Color;
            entity2.Texture = sprite.Frame.Texture;
            entity2.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer));
            entity2.Factor = sprite.Frame.Resolution;
            device.AddEntity(entity2);
        }

        public void RenderSprite(Graphics.Sprite sprite, Vector2 pos, Vector2 srcPos, int srcWidth, int srcHeight)
        {
            //Rectangle rc = new Rectangle(srcX, srcY, srcWidth, srcHeight);
            //if (sprite.Animation != null && sprite.Animation.Progressive && sprite.Frames != null)
            //    spriteRenderer.Draw(sprite.Frames[0].Texture, rc, new SlimDX.Vector3(0, 0, 0), new SlimDX.Vector3(x, y, CalcZ(layer) + 0.001f), new SlimDX.Color4(1, 1, 1, 1));
            //spriteRenderer.Draw(sprite.Frame.Texture, rc, new SlimDX.Vector3(0, 0, 0), new SlimDX.Vector3(x, y, CalcZ(layer)), new SlimDX.Color4(1, 1, 1, 1));
        }

        public void RenderSprite(Graphics.Sprite sprite, Vector2 pos, Vector2 srcPos, int srcWidth, int srcHeight, int color)
        {
            //Rectangle rc = new Rectangle(srcX, srcY, srcWidth, srcHeight);
            //if (sprite.Animation != null && sprite.Animation.Progressive && sprite.Frames != null)
            //    spriteRenderer.Draw(sprite.Frames[0].Texture, rc, new SlimDX.Vector3(0, 0, 0), new SlimDX.Vector3(x, y, CalcZ(layer) + 0.001f), new SlimDX.Color4(color));
            //spriteRenderer.Draw(sprite.Frame.Texture, rc, new SlimDX.Vector3(0, 0, 0), new SlimDX.Vector3(x, y, CalcZ(layer)), new SlimDX.Color4(color));

            Graphics.SpriteEntity entity = new Graphics.SpriteEntity();
            entity.Rectangle = new Rectangle(srcPos.x, srcPos.y, srcWidth, srcHeight);
            entity.Color = new SlimDX.Color4(color);
            entity.Factor = sprite.Frame.Resolution;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - sprite.Frame.TimeStamp < (long)(Stopwatch.Frequency / popInSpeed) && popInSpeed != 0)
            {
                entity.Color.Alpha *= (now - sprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed;
            }

            if (sprite.Animation != null && sprite.Animation.Progressive && sprite.Frames != null)
            {
                entity.Texture = sprite.Frames[0].Texture;
                entity.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer) + 0.001f);
                device.AddEntity(entity);
            }

            Graphics.SpriteEntity entity2 = new Burntime.Platform.Graphics.SpriteEntity();
            entity2.Rectangle = entity.Rectangle;
            entity2.Color = entity.Color;
            entity2.Texture = sprite.Frame.Texture;
            entity2.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer));
            entity2.Factor = sprite.Frame.Resolution;
            device.AddEntity(entity2);
        }

        public void RenderLine(Vector2 start, Vector2 end, int color)
        {
            Graphics.LineEntity entity = new Burntime.Platform.Graphics.LineEntity();
            entity.Color = new SlimDX.Color4(color);
            entity.Start = new SlimDX.Vector3(start.x, start.y, CalcZ(layer));
            entity.End = new SlimDX.Vector3(end.x, end.y, CalcZ(layer));
            device.AddEntity(entity);
        }
        #endregion

        float CalcZ(float Layer)
        {
            return 0.01f + (Layer / MAX_LAYERS) * 0.9f;
        }

        public Engine()
        {
        }

        public void Start(IApplication App)
        {
            ConfigFile cfg = new ConfigFile();
            cfg.Open("system:settings.txt");

            popInSpeed = System.Math.Max(0, cfg["engine"].GetInt("delay_blend"));
            if (popInSpeed != 0)
                popInSpeed = 1000 / popInSpeed;
            safeMode = cfg["engine"].GetBool("safemode");

            // show debug dialog
            DebugForm debug = null;
            if (cfg["engine"].GetBool("debug"))
            {
                debug = new DebugForm();
                debug.Show();

                Debug.form = debug;
                Debug.SetInfo("safemode", safeMode ? "yes" : "no");
            }

            Log.Info("SafeMode: " + (safeMode ? "yes" : "no"));
            Log.Info("DebugMode: " + (cfg["engine"].GetBool("debug") ? "yes" : "no"));

            app = App;
            form = new RenderForm();
            form.Text = App.Title;
            form.engine = this;
            form.ClientSize = new Size(resWidth, resHeight);
            form.Icon = App.Icon;

            // add DEBUG to title in debug builds
#if (DEBUG)
            form.Text += " DEBUG";
#endif

            device = new Graphics.RenderDevice(this);
            if (!device.Initialize(form))
                return;

            mainTarget = new Graphics.RenderTarget(this, new Rect(new Vector2(), new Vector2(gameWidth, gameHeight)));
            music = new Burntime.Platform.Music.MusicPlayback(form);

            Log.Info("Start resource manager thread...");
            ResourceManager.Run();

            Log.Info("Start music playback thread...");
            music.RunThread();

            Log.Info("Start render thread...");
            // run render thread
            device.Start();
            device.BlendSpeed = cfg["engine"].GetFloat("scene_blend");

            Log.Info("Start game thread...");
            // run process thread
            processTime = new GameTime();
            processThread = new Thread(new ThreadStart(ProcessThread));
            processThread.IsBackground = true;
            processThread.Start();

            Log.Info("Start input thread...");
            // run application
            System.Windows.Forms.Application.Run(form);

            if (debug != null)
            {
                debug.Close();
                debug.Dispose();
            }

            Release();
        }

        //internal void Reset()
        //{
        //    if (waitForReset)
        //    {
        //        isLoading = true;

        //        renderer.Reset();

        //        ResourceManager.ReloadAll();

        //        isLoading = false;
        //        waitForReset = false;
        //    }
        //}

        internal void RecoverAfterCrash()
        {
            loadingStack = 0;

            try
            {
                ResourceManager.Reset();
                device.RecoverAfterCrash();
                app.Reset();
                //crashCanRender = true;
                crashed = false;
            }
            catch (Exception)
            {
                crashed = true;
            }
        }

        void Release()
        {
            music.StopThread();
            ResourceManager.Dispose();
            device.Dispose();

            form.Dispose();
        }

        void ProcessThread()
        {
            Thread.CurrentThread.Name = "ProcessThread";

            while (!requestExit)
            {
                processTime.Refresh(25);

                device.Begin();

                if (safeMode)
                {
                    try
                    {
                        if (!crashed)
                        {
                            app.Process(processTime.Elapsed);
                            mainTarget.elapsed = processTime.Elapsed;
                            app.Render(mainTarget);
                        }
                    }
                    catch (Exception e)
                    {
                        crashed = true;

                        File file = FileSystem.GetFile("system/errorlog.txt", FileOpenMode.NoPackage | FileOpenMode.Write);
                        file.Seek(0, SeekPosition.End);

                        System.IO.StreamWriter writer = new System.IO.StreamWriter(file);
                        writer.WriteLine();
                        writer.WriteLine("----------------------------------------------------------------------");
                        writer.WriteLine(System.DateTime.Now.ToLocalTime().ToString());
                        writer.WriteLine("exception: " + e.Message);
                        writer.WriteLine("trace:");
                        writer.Write(e.StackTrace);
                        writer.WriteLine();
                        writer.Close();

                        file.Close();
                    }
                }
                else
                {
                    app.Process(processTime.Elapsed);
                    mainTarget.elapsed = processTime.Elapsed;
                    app.Render(mainTarget);
                }

                device.End();
            }

            app.Close();
        }

        public void Close()
        {
            if (!requestExit)
                form.CloseForm();
        }

        internal void RequestClose()
        {
            requestExit = true;
            device.Stop();
        }

        internal DeviceManager DeviceManager = null;
        internal Resource.ResourceManager ResourceManager = null;
        
        Graphics.RenderTarget mainTarget;

        public Graphics.RenderTarget MainTarget
        {
            get { return mainTarget; }
        }

        internal void OnMouseMove(int x, int y)
        {
            DeviceManager.MouseMove(new Vector2((int)((float)x * gameWidth / form.ClientSize.Width), (int)((float)y * gameHeight / form.ClientSize.Height)));
        }

        internal void OnMouseClick(int x, int y, bool right)
        {
            DeviceManager.MouseClick(new Vector2((int)((float)x * gameWidth / form.ClientSize.Width), (int)((float)y * gameHeight / form.ClientSize.Height)), right ? MouseButton.Right : MouseButton.Left);
        }

        internal void OnMouseLeave()
        {
            DeviceManager.MouseLeave();
        }

        internal void OnKeyPress(char Key)
        {
            DeviceManager.KeyPress(Key);
        }

        internal void OnVKeyPress(Keys key)
        {
            DeviceManager.VKeyPress(key);
        }

        public void CenterMouse()
        {
            //Point pt = new Point(form.ClientSize.Width / 2, form.ClientSize.Height / 2);

            //Cursor.Position = form.PointToScreen(pt);

            //OnMouseMove(pt.X, pt.Y);

            form.CenterMouse();
        }

        internal protected void InvokeGUIThread(Delegate method, object sender, EventArgs e)
        {
            form.Invoke(method, new object[] { sender, e });
        }
    }
}
