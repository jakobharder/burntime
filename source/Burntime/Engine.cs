using System;
using System.Drawing;
using System.Diagnostics;
using System.Threading;

using Burntime.Platform.IO;
using SlimDX.Direct3D9;
using Burntime.Platform.Utils;
using Burntime.SlimDx.Graphics;
using Burntime.Platform.Graphics;

namespace Burntime.Platform
{
    public class Engine : IEngine, ILoadingCounter
    {
        RenderDevice device;
        internal RenderDevice Device
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

        float layer;
        bool musicBlend = true;

        public Resolution Resolution { get; } = new();

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
        public BlendOverlay BlendOverlay => device?.BlendOverlay;
        BlendOverlayBase IEngine.BlendOverlay => device?.BlendOverlay;

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

        #region render methods
        public void RenderRect(Vector2 pos, Vector2 size, uint color)
        {
            Graphics.SpriteEntity entity = new Graphics.SpriteEntity();
            entity.Rectangle = new Rectangle(0, 0, size.x, size.y);
            entity.Color = new SlimDX.Color4((int)color);
            entity.Texture = SpriteFrame.EmptyTexture;
            entity.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer));
            device.AddEntity(entity);
        }

        public void RenderSprite(ISprite sprite, Vector2 pos, float alpha = 1)
        {
            if (sprite is not SlimDx.Graphics.Sprite nativeSprite) return;

            Graphics.SpriteEntity entity = new Graphics.SpriteEntity();
            entity.Rectangle = new Rectangle(0, 0, nativeSprite.OriginalSize.x, nativeSprite.OriginalSize.y);
            entity.Color = new SlimDX.Color4(alpha, 1, 1, 1);
            entity.Factor = nativeSprite.Frame.Resolution;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - nativeSprite.Frame.TimeStamp < (long)(Stopwatch.Frequency / popInSpeed) && popInSpeed != 0)
            {
                entity.Color.Alpha *= (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed;
            }

            if (sprite.Animation != null && sprite.Animation.Progressive && nativeSprite.Frames != null)
            {
                entity.Texture = nativeSprite.Frames[0].Texture;
                entity.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer) + 0.001f);
                device.AddEntity(entity);
            }

            Graphics.SpriteEntity entity2 = new Burntime.Platform.Graphics.SpriteEntity();
            entity2.Rectangle = entity.Rectangle;
            entity2.Color = entity.Color;
            entity2.Texture = nativeSprite.Frame.Texture;
            entity2.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer));
            entity2.Factor = nativeSprite.Frame.Resolution;
            device.AddEntity(entity2);
        }

        public void RenderSprite(ISprite sprite, Vector2 pos, Vector2 srcPos, int srcWidth, int srcHeight, int color)
        {
            if (sprite is not SlimDx.Graphics.Sprite nativeSprite) return;

            Graphics.SpriteEntity entity = new Graphics.SpriteEntity();
            entity.Rectangle = new Rectangle(srcPos.x, srcPos.y, srcWidth, srcHeight);
            entity.Color = new SlimDX.Color4(color);
            entity.Factor = nativeSprite.Frame.Resolution;

            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (now - nativeSprite.Frame.TimeStamp < (long)(Stopwatch.Frequency / popInSpeed) && popInSpeed != 0)
            {
                entity.Color.Alpha *= (now - nativeSprite.Frame.TimeStamp) / (float)Stopwatch.Frequency * popInSpeed;
            }

            if (nativeSprite.Animation != null && nativeSprite.Animation.Progressive && nativeSprite.Frames != null)
            {
                entity.Texture = nativeSprite.Frames[0].Texture;
                entity.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer) + 0.001f);
                device.AddEntity(entity);
            }

            Graphics.SpriteEntity entity2 = new Burntime.Platform.Graphics.SpriteEntity();
            entity2.Rectangle = entity.Rectangle;
            entity2.Color = entity.Color;
            entity2.Texture = nativeSprite.Frame.Texture;
            entity2.Position = new SlimDX.Vector3(pos.x, pos.y, CalcZ(layer));
            entity2.Factor = nativeSprite.Frame.Resolution;
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
            form.ClientSize = new Size(Resolution.Native.x, Resolution.Native.y);
#warning slimdx todo
            //form.Icon = App.Icon;

            // add DEBUG to title in debug builds
#if (DEBUG)
            form.Text += " DEBUG";
#endif

            device = new RenderDevice(this);
            if (!device.Initialize(form))
                return;

            MainTarget = new Graphics.RenderTarget(this, new Rect(Vector2.Zero, Resolution.Game));
            music = new Burntime.Platform.Music.MusicPlayback(form);

            Log.Info("Start resource manager thread...");
            ResourceManager.Run();

            Log.Info("Start music playback thread...");
            music.RunThread();

            Log.Info("Start render thread...");
            // run render thread
            device.Start();
            BlendOverlay.Speed = cfg["engine"].GetFloat("scene_blend");

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
                            MainTarget.Elapsed = processTime.Elapsed;
                            app.Render(MainTarget);
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
                    MainTarget.Elapsed = processTime.Elapsed;
                    app.Render(MainTarget);
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

        public DeviceManager DeviceManager { get; set; }
        internal Resource.ResourceManager ResourceManager = null;
        
        public RenderTarget MainTarget { get; private set; }

        internal void OnMouseMove(int x, int y)
        {
            DeviceManager.MouseMove(new Vector2(
                (int)((float)x * Resolution.Game.x / form.ClientSize.Width), 
                (int)((float)y * Resolution.Game.y / form.ClientSize.Height)));
        }

        internal void OnMouseClick(int x, int y, bool right)
        {
            DeviceManager.MouseClick(new Vector2(
                (int)((float)x * Resolution.Game.x / form.ClientSize.Width), 
                (int)((float)y * Resolution.Game.y / form.ClientSize.Height)), 
                right ? MouseButton.Right : MouseButton.Left);
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
