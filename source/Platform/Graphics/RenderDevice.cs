
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
using System.Collections.Generic;
using System.Text;
using System.Threading;
using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics
{
    class RenderEntityQueue : List<RenderEntity>
    {
    }

    class RenderDevice : IDisposable
    {
        RenderEntityQueue current;
        RenderEntityQueue render;
        Queue<RenderEntityQueue> queue = new Queue<RenderEntityQueue>();

        Engine engine;
        Resource.ResourceManager resourceManager;

        Direct3D direct3D;
        PresentParameters presentParams;
        Device device;
        SlimDX.Direct3D9.Sprite spriteRenderer;
        internal Texture emptyTexture;
        SlimDX.Direct3D9.Line lineRenderer;

        LoadingOverlay loadingOverlay;
        BlendOverlay blendOverlay;
        ErrorOverlay errorOverlay;

        bool requestStop = false;
        Thread renderThread;
        AutoResetEvent renderFinished;
        bool waitForReset;
        bool wasLost;
        bool blockBlend;
        bool deviceReadyForRender;
        RenderForm form;

        event EventHandler DeviceReset;
        event EventHandler DeviceLost;

        RenderToSurface renderToSurface;
        Texture renderToTexture;

        int renderScale = 2;

        public float BlendState
        {
            get { return blendOverlay.BlendState; }
        }

        public RenderDevice(Engine Engine)
        {
            resourceManager = Engine.ResourceManager;
            engine = Engine;
        }

        public bool Initialize(RenderForm Form)
        {
            direct3D = new Direct3D();
            this.form = Form;
            int adapter = direct3D.AdapterCount - 1;

            // check window mode
            if (!direct3D.CheckDeviceType(adapter, DeviceType.Hardware, Format.R5G6B5, Format.R5G6B5, true))
            {
                // not available
                System.Windows.Forms.MessageBox.Show("window mode not available");
            }

            // fullscreen mode
            if (!direct3D.CheckDeviceType(adapter, DeviceType.Hardware, Format.R5G6B5, Format.R5G6B5, false))
            {
                // not available
                System.Windows.Forms.MessageBox.Show("fullscreen not available");
            }

            presentParams = new PresentParameters();
            presentParams.BackBufferHeight = Form.ClientRectangle.Height;
            presentParams.BackBufferWidth = Form.ClientRectangle.Width;
            presentParams.DeviceWindowHandle = Form.Handle;
            presentParams.PresentationInterval = PresentInterval.One;
            presentParams.BackBufferCount = 1;
            presentParams.BackBufferFormat = Format.R5G6B5;

            // check back buffer format
            if (!direct3D.CheckDeviceFormat(adapter, DeviceType.Hardware, presentParams.BackBufferFormat,
                Usage.RenderTarget, ResourceType.Surface, Format.R5G6B5))
            {
                // not available
                System.Windows.Forms.MessageBox.Show("back buffer not available");
            }

            // check depth stencil format
            if (!direct3D.CheckDeviceFormat(adapter, DeviceType.Hardware, presentParams.BackBufferFormat,
                Usage.DepthStencil, ResourceType.Surface, Format.D16))
            {
                // not available
                System.Windows.Forms.MessageBox.Show("depth stencil not available");
            }

            // check depth stencil format match
            if (!direct3D.CheckDepthStencilMatch(adapter, DeviceType.Hardware, presentParams.BackBufferFormat,
                presentParams.BackBufferFormat, Format.D16))
            {
                // not available
                System.Windows.Forms.MessageBox.Show("depth stencil match not available");
            }
            presentParams.AutoDepthStencilFormat = Format.D16;
            presentParams.EnableAutoDepthStencil = true;
            
            presentParams.Windowed = !engine.FullScreen;
            if (!presentParams.Windowed)
            {
                presentParams.BackBufferWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
                presentParams.BackBufferHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
                presentParams.SwapEffect = SwapEffect.Flip;
                Form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

                engine.Resolution = new Vector2(presentParams.BackBufferWidth, presentParams.BackBufferHeight);

                Log.Info("Fullscreen: yes");
                Log.Info("Resolution: " + presentParams.BackBufferWidth + "x" + presentParams.BackBufferHeight);
            }
            else
            {
                presentParams.SwapEffect = SwapEffect.Discard;
                Log.Info("Fullscreen: no");
                Log.Info("Resolution: " + presentParams.BackBufferWidth + "x" + presentParams.BackBufferHeight);
            }

            Log.Info("Virtual resolution: " + engine.GameResolution.x + "x" + engine.GameResolution.y);
            Log.Info("Scale factor: " + engine.Scale.x.ToString("0.00") + "x" + engine.Scale.y.ToString("0.00"));
            Log.Info("Texture filter method: " + (engine.UseTextureFilter ? "GaussianQuad" : "NearestPoint"));

            device = new Device(direct3D, adapter, DeviceType.Hardware, Form.Handle, CreateFlags.SoftwareVertexProcessing | CreateFlags.Multithreaded, presentParams);

            renderToSurface = new RenderToSurface(device, engine.GameResolution.x * renderScale, engine.GameResolution.y * renderScale, Format.X8R8G8B8);
            renderToTexture = new Texture(device, engine.GameResolution.x * renderScale, engine.GameResolution.y * renderScale, 1, Usage.RenderTarget, Format.X8R8G8B8, Pool.Default);

            loadingOverlay = new LoadingOverlay(engine, new Vector2(presentParams.BackBufferWidth, presentParams.BackBufferHeight));
            blendOverlay = new BlendOverlay(new Vector2(presentParams.BackBufferWidth, presentParams.BackBufferHeight));
            errorOverlay = new ErrorOverlay(engine, new Vector2(presentParams.BackBufferWidth, presentParams.BackBufferHeight));

            ReloadGraphicResources();

            DeviceReset += new EventHandler(RenderDevice_DeviceReset);
            DeviceLost += new EventHandler(RenderDevice_DeviceLost);

            return true;
        }

        void RenderDevice_DeviceReset(object sender, EventArgs e)
        {
            if (Reset())
            {
                resourceManager.ReloadAll();
            }
        }

        void RenderDevice_DeviceLost(object sender, EventArgs e)
        {
            SlimDX.Result res = device.TestCooperativeLevel();
            if (res == ResultCode.DeviceNotReset)
            {
                if (Reset())
                {
                    resourceManager.ReloadAll();
                }
            }
        }

        public void RecoverAfterCrash()
        {
            blendOverlay.Blend = 1;
        }

        public bool Reset()
        {
            try
            {
                device.Reset(presentParams);
                ReloadGraphicResources();
                waitForReset = false;
                wasLost = false;
            }
            catch
            {
                waitForReset = false;
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            UnloadGraphicResources();
            resourceManager.Dispose();
            device.Dispose();
            direct3D.Dispose();
        }

        public void Start()
        {
            renderFinished = new AutoResetEvent(true);
            renderThread = new Thread(new ThreadStart(RenderThread));
            renderThread.IsBackground = true;
            renderThread.Start();
        }

        public void Stop()
        {
            requestStop = true;
            renderFinished.Set();
            renderThread.Join();
        }

        public Texture CreateTexture(int Width, int Height)
        {
            return new Texture(device, Width, Height, 1, Usage.Dynamic, Format.A8R8G8B8, Pool.Default);
        }

        void ReloadGraphicResources()
        {
            resourceManager.EmptyTexture = new Texture(device, 1, 1, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
            SlimDX.DataRectangle dr = resourceManager.EmptyTexture.LockRectangle(0, LockFlags.Discard);
            dr.Data.Write<uint>(0x00000000);
            resourceManager.EmptyTexture.UnlockRectangle(0);
            emptyTexture = new Texture(device, 1, 1, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
            dr = emptyTexture.LockRectangle(0, LockFlags.Discard);
            dr.Data.Write<uint>(0xffffffff);
            emptyTexture.UnlockRectangle(0);

            spriteRenderer = new SlimDX.Direct3D9.Sprite(device);
            lineRenderer = new Line(device);

            loadingOverlay.Load(device);
            blendOverlay.Load(device);
            errorOverlay.Load(device);

            renderToSurface = new RenderToSurface(device, engine.GameResolution.x * renderScale, engine.GameResolution.y * renderScale, Format.X8R8G8B8);
            renderToTexture = new Texture(device, engine.GameResolution.x * renderScale, engine.GameResolution.y * renderScale, 1, Usage.RenderTarget, Format.X8R8G8B8, Pool.Default);

            deviceReadyForRender = true;
        }

        void UnloadGraphicResources()
        {
            deviceReadyForRender = false;

            resourceManager.ReleaseAll();

            resourceManager.EmptyTexture.Dispose();
            emptyTexture.Dispose();

            spriteRenderer.Dispose();
            lineRenderer.Dispose();

            loadingOverlay.Unload();
            blendOverlay.Unload();
            errorOverlay.Unload();

            renderToSurface.Dispose();
            renderToTexture.Dispose();
        }

        public void Begin()
        {
            current = new RenderEntityQueue();
        }

        public void End()
        {
            lock (queue)
                queue.Enqueue(current);

            renderFinished.WaitOne();
        }

        public void AddEntity(RenderEntity Entity)
        {
            current.Add(Entity);
        }

        #region scene blend methods
        public float Blend
        {
            get { return blendOverlay.Blend; }
            set { blendOverlay.Blend = value; }
        }

        public bool IsBlended
        {
            get { return blendOverlay.BlendState == 1; }
        }

        public float BlendSpeed
        {
            get { return blendOverlay.Speed; }
            set { blendOverlay.Speed = value; }
        }

        public bool BlockBlend
        {
            get { return blockBlend; }
            set { blockBlend = value; }
        }

        public void WaitForBlend()
        {
            blendOverlay.WaitForBlend();
        }
        #endregion

        void RenderThread()
        {
            Thread.CurrentThread.Name = "RenderThread";

            GameTime renderTime = new GameTime();

            while (!requestStop)
            {
                renderTime.Refresh(25);

                if (waitForReset)
                {
                    Thread.Sleep(100);
                    continue;
                }

                if (deviceReadyForRender)
                {
                    try
                    {
                        // render to texture
                        renderToSurface.BeginScene(renderToTexture.GetSurfaceLevel(0), 
                            new Viewport(0, 0, engine.GameResolution.x * renderScale, engine.GameResolution.y * renderScale, 0f, 1f));
                        device.Clear(ClearFlags.Target, 0x000000, 5.0f, 0);                      
                        Render(renderTime);
                        renderToSurface.EndScene(Filter.Triangle);

                        // render texture to screen
                        device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, 0x00000000, 5.0f, 0);
                        device.BeginScene();
                        RenderTexture();
                        device.EndScene();

                        renderFinished.Set();
                    }
                    catch (Exception)
                    {
                    }

                    try
                    {
                        device.Present();
                    }
                    catch (Exception)
                    {
                        OnLostResetDevice();
                    }
                }
                else
                    OnLostResetDevice();
            }
        }

        void OnLostResetDevice()
        {
            deviceReadyForRender = false;

            SlimDX.Result res = device.TestCooperativeLevel();
            if (res == ResultCode.DeviceNotReset)
            {
                if (!wasLost)
                    UnloadGraphicResources();

                wasLost = true;

                waitForReset = true;
                engine.InvokeGUIThread(DeviceReset, this, null);

                return;
            }
            else if (res == ResultCode.DeviceLost)
            {
                Thread.Sleep(500);

                if (!wasLost)
                {
                    UnloadGraphicResources();
                    wasLost = true;
                }

                engine.InvokeGUIThread(DeviceLost, this, null);
                return;
            }
            else if (res != ResultCode.Success)
                throw new Exception();

            if (wasLost)
            {
                throw new Exception();
            }
        }

        void Render(GameTime RenderTime)
        {
            lock (queue)
            {
                if (queue.Count > 0)
                    render = queue.Dequeue();
            }

            spriteRenderer.Begin(SpriteFlags.SortTexture | SpriteFlags.SortDepthFrontToBack | SpriteFlags.AlphaBlend);

            //if (engine.UseTextureFilter)
            //{
            //    device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Anisotropic);
            //    device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Anisotropic);
            //    device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Anisotropic);
            //}
            //else
            {
                device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Point);
                device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Point);
                device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Point);
            }

            // TODO engine scale
            spriteRenderer.Transform = SlimDX.Matrix.Scaling(new SlimDX.Vector3(renderScale, renderScale, 1));

            SlimDX.Matrix lineMatrix = SlimDX.Matrix.AffineTransformation2D(1, new SlimDX.Vector2(), 0, new SlimDX.Vector2());
            // TODO engine scale
            //lineMatrix = SlimDX.Matrix.Transformation2D(new SlimDX.Vector2(), 0, new SlimDX.Vector2(engine.Scale.x, engine.Scale.y), new SlimDX.Vector2(), 0, new SlimDX.Vector2());
            lineMatrix = SlimDX.Matrix.Transformation2D(new SlimDX.Vector2(), 0, new SlimDX.Vector2(renderScale, renderScale), new SlimDX.Vector2(), 0, new SlimDX.Vector2());
            lineMatrix = spriteRenderer.Transform;
            lineRenderer.Begin();

            // application render
            engine.MainTarget.elapsed = RenderTime.Elapsed;

            float currentFactor = 1;

            if (render != null)
            {
                foreach (RenderEntity entity in render)
                {
                    if (entity is SpriteEntity)
                    {
                        SpriteEntity sprite = entity as SpriteEntity;
                        // diposed texture links may remain in queue after direct3d reset, just skip them
                        if (sprite.Texture.Disposed)
                            continue;

                        // if sprite resolution changed, then update transform matrix
                        if (currentFactor != sprite.Factor)
                        {
                            currentFactor = sprite.Factor;
                            // TODO engine scale
                            //spriteRenderer.Transform = SlimDX.Matrix.Scaling(new SlimDX.Vector3(engine.Scale.x * sprite.Factor, engine.Scale.y * sprite.Factor, 1));
                            spriteRenderer.Transform = SlimDX.Matrix.Scaling(new SlimDX.Vector3(renderScale * sprite.Factor, renderScale * sprite.Factor, 1));
                        }

                        // recompute position for not 1:1 sprite resolutions
                        SlimDX.Vector3 pos = sprite.Position;
                        if (currentFactor != 1)
                        {
                            pos.X /= currentFactor;
                            pos.Y /= currentFactor;
                        }

                        spriteRenderer.Draw(sprite.Texture, sprite.Rectangle, new SlimDX.Vector3(0, 0, 0), pos, sprite.Color);
                        
                    }
                    else if (entity is LineEntity)
                    {
                        LineEntity line = entity as LineEntity;
                        SlimDX.Vector3[] vec = new SlimDX.Vector3[] { line.Start, line.End };
                        SlimDX.Vector4[] v = SlimDX.Vector3.Transform(vec, ref lineMatrix);

                        SlimDX.Vector2[] vec2 = new SlimDX.Vector2[] { new SlimDX.Vector2(v[0].X, v[0].Y), new SlimDX.Vector2(v[1].X, v[1].Y) };
                        lineRenderer.Draw(vec2, line.Color);
                    }
                }
            }

            lineRenderer.End();

            blendOverlay.BlockFadeOut = engine.IsLoading || blockBlend;
            blendOverlay.Render(RenderTime, spriteRenderer);
            if (engine.MusicBlend)
            {
                engine.Music.Volume = 1 - BlendState;
            }
            else
            {
                engine.Music.Volume = 1;
            }

            spriteRenderer.End();

            errorOverlay.Render(RenderTime, spriteRenderer);

            loadingOverlay.Render(RenderTime, spriteRenderer);
        }

        void RenderTexture()
        {
            spriteRenderer.Begin(SpriteFlags.None);

            if (engine.UseTextureFilter)
            {
                device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Anisotropic);
                device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Anisotropic);
                device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Anisotropic);
            }
            else
            {
                device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Point);
                device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Point);
                device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Point);
            }

            spriteRenderer.Transform = SlimDX.Matrix.Scaling(new SlimDX.Vector3(engine.Scale.x / renderScale, engine.Scale.y / renderScale, 1));
            spriteRenderer.Draw(renderToTexture, new SlimDX.Color4(1, 1, 1));

            spriteRenderer.End();
        }
    }
}
