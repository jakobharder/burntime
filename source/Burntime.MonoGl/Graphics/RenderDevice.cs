using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Burntime.MonoGl.Graphics;

public class RenderDevice : IDisposable
{
    RenderEntityQueue current;
    RenderEntityQueue _renderEntities;
    readonly Queue<RenderEntityQueue> _renderQueue = new();

    readonly BurntimeGame _engine;
    readonly ResourceManager resourceManager;

    //SlimDX.Direct3D9.Line lineRenderer;

    LoadingOverlay loadingOverlay;
    public BlendOverlay BlendOverlay { get; private set; }
    //ErrorOverlay errorOverlay;

    bool requestStop = false;
    Thread renderThread;
    AutoResetEvent renderFinished;
    bool waitForReset;
    bool wasLost;
    bool deviceReadyForRender;

    event EventHandler DeviceReset;
    event EventHandler DeviceLost;

    //RenderToSurface renderToSurface;
    //Texture renderToTexture;

    int renderScale = 2;
    SpriteBatch _spriteBatch;

    public float BlendState
    {
        get { return 1;/* return blendOverlay.BlendState;*/ }
    }

    public RenderDevice(BurntimeGame Engine)
    {
        resourceManager = Engine.ResourceManager;
        _engine = Engine;
    }

    public bool Initialize()
    {
        _spriteBatch = new SpriteBatch(_engine.GraphicsDevice);

        //presentParams.Windowed = !engine.FullScreen;
        //if (!presentParams.Windowed)
        //{
        //    presentParams.BackBufferWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        //    presentParams.BackBufferHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
        //    presentParams.SwapEffect = SwapEffect.Flip;
        //    Form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;

        //    engine.Resolution.Native = new Vector2(presentParams.BackBufferWidth, presentParams.BackBufferHeight);

        //    Log.Info("Fullscreen: yes");
        //    Log.Info("Resolution: " + presentParams.BackBufferWidth + "x" + presentParams.BackBufferHeight);
        //}
        //else
        //{
        //    presentParams.SwapEffect = SwapEffect.Discard;
        //    Log.Info("Fullscreen: no");
        //    Log.Info("Resolution: " + presentParams.BackBufferWidth + "x" + presentParams.BackBufferHeight);
        //}

        Log.Info("Virtual resolution: " + _engine.Resolution.Game.x + "x" + _engine.Resolution.Game.y);
        Log.Info("Scale factor: " + _engine.Resolution.Scale.x.ToString("0.00") + "x" + _engine.Resolution.Scale.y.ToString("0.00"));

        //renderToSurface = new RenderToSurface(device, engine.Resolution.Game.x * renderScale, engine.Resolution.Game.y * renderScale, Format.X8R8G8B8);
        //renderToTexture = new Texture(device, engine.Resolution.Game.x * renderScale, engine.Resolution.Game.y * renderScale, 1, Usage.RenderTarget, Format.X8R8G8B8, Pool.Default);

        loadingOverlay = new LoadingOverlay(_engine, _engine.Resolution.Native);
        BlendOverlay = new BlendOverlay(_engine.Resolution.Native);
        //errorOverlay = new ErrorOverlay(engine, new Vector2(presentParams.BackBufferWidth, presentParams.BackBufferHeight));

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
        //SlimDX.Result res = device.TestCooperativeLevel();
        //if (res == ResultCode.DeviceNotReset)
        //{
        //    if (Reset())
        //    {
        //        resourceManager.ReloadAll();
        //    }
        //}
    }

    public void RecoverAfterCrash()
    {
        BlendOverlay.FadeOut();
    }

    public bool Reset()
    {
        //try
        //{
        //    device.Reset(presentParams);
        //    ReloadGraphicResources();
        //    waitForReset = false;
        //    wasLost = false;
        //}
        //catch
        //{
        //    waitForReset = false;
        //    return false;
        //}

        return true;
    }

    public void Dispose()
    {
        UnloadGraphicResources();
        //resourceManager.Dispose();
        //device.Dispose();
        //direct3D.Dispose();
    }

    public Texture2D CreateTexture(int Width, int Height)
    {
        return new Texture2D(_engine.GraphicsDevice, Width, Height, false, SurfaceFormat.Color);
    }

    void ReloadGraphicResources()
    {
        SpriteFrame.EmptyTexture = new Texture2D(_engine.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
        SpriteFrame.EmptyTexture.SetData(new Color[] { Color.Black });

        //spriteRenderer = new SlimDX.Direct3D9.Sprite(device);
        //lineRenderer = new Line(device);

        loadingOverlay.Load(_engine.GraphicsDevice);
        BlendOverlay.Load(_engine.GraphicsDevice);
        //errorOverlay.Load(device);

        //renderToSurface = new RenderToSurface(device, engine.Resolution.Game.x * renderScale, engine.Resolution.Game.y * renderScale, Format.X8R8G8B8);
        //renderToTexture = new Texture(device, engine.Resolution.Game.x * renderScale, engine.Resolution.Game.y * renderScale, 1, Usage.RenderTarget, Format.X8R8G8B8, Pool.Default);

        deviceReadyForRender = true;
    }

    void UnloadGraphicResources()
    {
        deviceReadyForRender = false;

        //resourceManager.ReleaseAll();

        SpriteFrame.EmptyTexture.Dispose();

        //spriteRenderer.Dispose();
        //lineRenderer.Dispose();

        loadingOverlay.Unload();
        BlendOverlay.Unload();
        //errorOverlay.Unload();

        //renderToSurface.Dispose();
        //renderToTexture.Dispose();
    }

    public void Begin()
    {
        current = new RenderEntityQueue();
    }

    public void End()
    {
        lock (_renderQueue)
            _renderQueue.Enqueue(current);
    }

    public void AddEntity(RenderEntity Entity)
    {
        current.Add(Entity);
    }

    void OnLostResetDevice()
    {
        //deviceReadyForRender = false;

        //SlimDX.Result res = device.TestCooperativeLevel();
        //if (res == ResultCode.DeviceNotReset)
        //{
        //    if (!wasLost)
        //        UnloadGraphicResources();

        //    wasLost = true;

        //    waitForReset = true;
        //    engine.InvokeGUIThread(DeviceReset, this, null);

        //    return;
        //}
        //else if (res == ResultCode.DeviceLost)
        //{
        //    Thread.Sleep(500);

        //    if (!wasLost)
        //    {
        //        UnloadGraphicResources();
        //        wasLost = true;
        //    }

        //    engine.InvokeGUIThread(DeviceLost, this, null);
        //    return;
        //}
        //else if (res != ResultCode.Success)
        //    throw new Exception();

        //if (wasLost)
        //{
        //    throw new Exception();
        //}
    }

    /// <summary>
    /// Create render thread data for queued objects.
    /// </summary>
    public void Update()
    {
        lock (_renderQueue)
        {
            if (_renderQueue.Count > 0)
                _renderEntities = _renderQueue.Dequeue();
        }

        if (_renderEntities is null)
            return;

        foreach (RenderEntity entity in _renderEntities)
        {
            if (entity is SpriteEntity sprite)
            {
                if (sprite.SpriteFrame is not null && sprite.SpriteFrame.IsLoaded)
                    sprite.SpriteFrame.CreateTexture(this);
            }
        }
    }

    /// <summary>
    /// Render queued objects.
    /// </summary>
    /// <param name="elapsedSeconds"></param>
    public void Render(float elapsedSeconds)
    {
        _spriteBatch.Begin(SpriteSortMode.FrontToBack, Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied, SamplerState.PointClamp);

        //SlimDX.Matrix lineMatrix = SlimDX.Matrix.AffineTransformation2D(1, new SlimDX.Vector2(), 0, new SlimDX.Vector2());
        //// TODO engine scale
        ////lineMatrix = SlimDX.Matrix.Transformation2D(new SlimDX.Vector2(), 0, new SlimDX.Vector2(engine.Scale.x, engine.Scale.y), new SlimDX.Vector2(), 0, new SlimDX.Vector2());
        //lineMatrix = SlimDX.Matrix.Transformation2D(new SlimDX.Vector2(), 0, new SlimDX.Vector2(renderScale, renderScale), new SlimDX.Vector2(), 0, new SlimDX.Vector2());
        //lineMatrix = spriteRenderer.Transform;

        if (_renderEntities != null)
        {
            foreach (RenderEntity entity in _renderEntities)
            {
                if (entity is SpriteEntity sprite)
                {
                    // diposed texture links may remain in queue after direct3d reset, just skip them
                    if ((sprite.Texture ?? sprite.SpriteFrame.Texture).IsDisposed)
                        continue;

                    // recompute position for not 1:1 sprite resolutions
                    var pos = new Microsoft.Xna.Framework.Vector2(sprite.Position.X, sprite.Position.Y);
                    pos.X *= _engine.Resolution.Scale.x;
                    pos.Y *= _engine.Resolution.Scale.y;

                    _spriteBatch.Draw(sprite.Texture ?? sprite.SpriteFrame.Texture, pos, sprite.Rectangle, sprite.Color, 0, 
                        Microsoft.Xna.Framework.Vector2.Zero,
                        new Microsoft.Xna.Framework.Vector2(sprite.Factor * _engine.Resolution.Scale.x, sprite.Factor * _engine.Resolution.Scale.y), 
                        SpriteEffects.None, sprite.Position.Z);
                }
            }
        }

        BlendOverlay.BlockFadeOut = _engine.IsLoading || BlendOverlay.Block;
        BlendOverlay.Render(elapsedSeconds, _spriteBatch);
        //if (engine.MusicBlend)
        //{
        //    engine.Music.Volume = 1 - BlendState;
        //}
        //else
        //{
        //    engine.Music.Volume = 1;
        //}

        //spriteRenderer.End();
        //if (render != null)
        //{
        //    lineRenderer.Begin();
        //    foreach (var line in render.OfType<LineEntity>())
        //    {
        //        SlimDX.Vector3[] vec = new SlimDX.Vector3[] { line.Start, line.End };
        //        SlimDX.Vector4[] v = SlimDX.Vector3.Transform(vec, ref lineMatrix);

        //        SlimDX.Vector2[] vec2 = new SlimDX.Vector2[] { new SlimDX.Vector2(v[0].X, v[0].Y), new SlimDX.Vector2(v[1].X, v[1].Y) };
        //        lineRenderer.Draw(vec2, line.Color);
        //    }
        //    lineRenderer.End();
        //}

        //errorOverlay.Render(RenderTime, spriteRenderer);

        loadingOverlay.Render(elapsedSeconds, _spriteBatch);

        _spriteBatch.End();
    }

    void RenderTexture()
    {
        //spriteRenderer.Begin(SpriteFlags.None);

        //if (engine.UseTextureFilter)
        //{
        //    device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Anisotropic);
        //    device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Anisotropic);
        //    device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Anisotropic);
        //}
        //else
        //{
        //    device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Point);
        //    device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Point);
        //    device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Point);
        //}

        //spriteRenderer.Transform = SlimDX.Matrix.Scaling(new SlimDX.Vector3(engine.Resolution.Scale.x / renderScale, engine.Resolution.Scale.y / renderScale, 1));
        //spriteRenderer.Draw(renderToTexture, new SlimDX.Color4(1, 1, 1));

        //spriteRenderer.End();
    }
}
