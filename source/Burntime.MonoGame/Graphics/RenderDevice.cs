using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Burntime.MonoGame.Graphics;

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
    public Texture2D WhiteTexture { get; private set; }

    event EventHandler DeviceReset;
    event EventHandler DeviceLost;

    //RenderToSurface renderToSurface;
    //Texture renderToTexture;

    SpriteBatch _spriteBatch;
    RenderTarget2D _intermediateTarget;
    RenderTarget2D _sharpIntermediateTarget;
    Effect _sharpBilinearEffect;
    Effect _xbr2Effect;
    Effect _xbr2AlphaEffect;
    public float? Xbr2IndividualDepth { get; set; }
    PlatformContentManager _shaderContentManager;
    public bool SharpBilinearAvailable => _sharpBilinearEffect is not null;
    public bool Xbr2Available => _xbr2Effect is not null && SharpBilinearAvailable;

    sealed class PlatformContentManager : ContentManager
    {
        readonly string _package;

        public PlatformContentManager(IServiceProvider services, string package)
            : base(services)
        {
            _package = package;
            RootDirectory = string.Empty;
        }

        protected override Stream OpenStream(string assetName)
        {
            Burntime.Platform.IO.File file =
                Burntime.Platform.IO.FileSystem.GetFile($"{_package}:{assetName}.xnb");
            if (file is null)
                throw new ContentLoadException($"Content file not found: {assetName}.xnb");
            return file.Stream;
        }
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

        Log.Info("Game resolution: " + _engine.Resolution.Game.x + "x" + _engine.Resolution.Game.y);
        Log.Info("Backbuffer resolution: " + _engine.Resolution.Native.x + "x" + _engine.Resolution.Native.y);
        Log.Info("Scale factor: " + _engine.Resolution.Scale.x.ToString("0.00") + "x" + _engine.Resolution.Scale.y.ToString("0.00"));
        Log.Info("Output scale: " + _engine.Resolution.OutputScale.ToString("0.00") +
            "x (" + _engine.OutputFiltering.ToString().ToLowerInvariant() + ")");

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
        OutputFiltering requestedOutputFiltering = _engine.OutputFiltering;
        SpriteFrame.EmptyTexture = new Texture2D(_engine.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
        SpriteFrame.EmptyTexture.SetData(new Color[] { Color.Black });
        WhiteTexture = new Texture2D(_engine.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
        WhiteTexture.SetData(new Color[] { Color.White });
        if (!_engine.DisableShaders)
        {
            _shaderContentManager = new PlatformContentManager(_engine.Services, "classic");
            _sharpBilinearEffect = LoadOptionalEffect("shaders/SharpBilinear");
            _xbr2Effect = LoadOptionalEffect("shaders/Xbr2");
            _xbr2AlphaEffect = LoadOptionalEffect("shaders/Xbr2Alpha");
        }

        if (_sharpBilinearEffect is null)
        {
            if (_engine.OutputFiltering is OutputFiltering.SharpBilinearShader or
                OutputFiltering.Xbr2)
                _engine.OutputFiltering = OutputFiltering.SharpBilinear;
            Log.Info(_engine.DisableShaders
                ? "Shader loading is disabled"
                : "Sharp bilinear shader is not installed; using software SHARP");
        }
        else if (_xbr2Effect is null)
        {
            if (_engine.OutputFiltering == OutputFiltering.Xbr2)
                _engine.OutputFiltering = _engine.DefaultOutputFiltering;
            Log.Info("XBR2 shader is not installed; XBR2 filtering is unavailable");
        }

        if (_engine.OutputFiltering != requestedOutputFiltering)
            _engine.RefreshResourceReplacements();

        if (_sharpBilinearEffect is null && _xbr2Effect is null)
        {
            _shaderContentManager?.Dispose();
            _shaderContentManager = null;
        }


        //spriteRenderer = new SlimDX.Direct3D9.Sprite(device);
        //lineRenderer = new Line(device);

        loadingOverlay.Load(_engine.GraphicsDevice);
        BlendOverlay.Load(_engine.GraphicsDevice);
        //errorOverlay.Load(device);

        //renderToSurface = new RenderToSurface(device, engine.Resolution.Game.x * renderScale, engine.Resolution.Game.y * renderScale, Format.X8R8G8B8);
        //renderToTexture = new Texture(device, engine.Resolution.Game.x * renderScale, engine.Resolution.Game.y * renderScale, 1, Usage.RenderTarget, Format.X8R8G8B8, Pool.Default);
    }

    Effect LoadOptionalEffect(string assetName)
    {
        if (!Burntime.Platform.IO.FileSystem.ExistsFile($"classic:{assetName}.xnb"))
            return null;
        try
        {
            return _shaderContentManager.Load<Effect>(assetName);
        }
        catch (Exception exception)
        {
            Log.Warning($"Could not load {assetName} shader: {exception.Message}");
            return null;
        }
    }

    void UnloadGraphicResources()
    {
        //resourceManager.ReleaseAll();

        SpriteFrame.EmptyTexture.Dispose();
        _shaderContentManager?.Dispose();
        _shaderContentManager = null;
        _sharpBilinearEffect = null;
        _xbr2Effect = null;
        _xbr2AlphaEffect = null;
        _sharpIntermediateTarget?.Dispose();
        _sharpIntermediateTarget = null;
        _intermediateTarget?.Dispose();
        _intermediateTarget = null;

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
        Xbr2IndividualDepth = null;
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
            // Render queues are complete snapshots. Graphics device changes (for
            // example toggling fullscreen) can stall this consumer while the game
            // thread keeps producing them. Replaying that backlog adds permanent
            // input latency, so discard stale snapshots and render the newest one.
            while (_renderQueue.Count > 0)
                _renderEntities = _renderQueue.Dequeue();
        }

        if (_renderEntities is null)
            return;

        foreach (RenderEntity entity in _renderEntities)
        {
            if (entity is SpriteEntity sprite)
            {
                if (sprite.SpriteFrame is not null &&
                    (sprite.SpriteFrame.IsLoaded || sprite.SpriteFrame.HasSystemCopy))
                {
                    _engine.ResourceManager.CreateTexture(sprite.SpriteFrame, this);
                }
            }
        }
    }

    /// <summary>
    /// Render queued objects.
    /// </summary>
    /// <param name="elapsedSeconds"></param>
    public void Render(float elapsedSeconds)
    {
        const float PIXEL_CORRECTION = 0.0001f;

        bool useRemasteredGraphics = _engine.UseRemasteredGraphics;
        bool useXbr2 = _engine.OutputFiltering == OutputFiltering.Xbr2 &&
            Xbr2Available;
        bool renderTextAfterXbr = useXbr2;
        EnsureIntermediateTarget(useRemasteredGraphics);
        _engine.GraphicsDevice.SetRenderTarget(_intermediateTarget);
        _engine.GraphicsDevice.Clear(Color.Black);

        Platform.Vector2f intermediateScale = useRemasteredGraphics
            ? _engine.Resolution.Scale
            : Platform.Vector2f.One;
        var transformMatrix = useRemasteredGraphics
            ? Matrix.CreateScale(new Vector3(intermediateScale.x + PIXEL_CORRECTION,
                intermediateScale.y + PIXEL_CORRECTION, 1))
            : Matrix.Identity;
        List<SpriteEntity> orderedSprites = (_renderEntities ?? new RenderEntityQueue())
            .OfType<SpriteEntity>()
            .Where(sprite => useRemasteredGraphics || !sprite.DirectToFramebuffer)
            .OrderBy(sprite => sprite.Position.Z)
            .ToList();
        int deferredSpriteIndex = -1;
        if (renderTextAfterXbr)
        {
            int firstPostFilterIndex = orderedSprites.FindIndex(sprite => sprite.PostFilter);
            int configuredLayerIndex = Xbr2IndividualDepth is float depth
                ? orderedSprites.FindIndex(sprite => sprite.Position.Z >= depth)
                : -1;

            deferredSpriteIndex = firstPostFilterIndex < 0
                ? configuredLayerIndex
                : configuredLayerIndex < 0
                    ? firstPostFilterIndex
                    : System.Math.Min(firstPostFilterIndex, configuredLayerIndex);
        }
        if (deferredSpriteIndex < 0)
            deferredSpriteIndex = orderedSprites.Count;

        //SlimDX.Matrix lineMatrix = SlimDX.Matrix.AffineTransformation2D(1, new SlimDX.Vector2(), 0, new SlimDX.Vector2());
        //// TODO engine scale
        ////lineMatrix = SlimDX.Matrix.Transformation2D(new SlimDX.Vector2(), 0, new SlimDX.Vector2(engine.Scale.x, engine.Scale.y), new SlimDX.Vector2(), 0, new SlimDX.Vector2());
        //lineMatrix = SlimDX.Matrix.Transformation2D(new SlimDX.Vector2(), 0, new SlimDX.Vector2(renderScale, renderScale), new SlimDX.Vector2(), 0, new SlimDX.Vector2());
        //lineMatrix = spriteRenderer.Transform;

        if (orderedSprites.Count != 0)
        {
            bool? linearFiltering = null;
            foreach (var sprite in orderedSprites.Take(deferredSpriteIndex))
            {
                // diposed texture links may remain in queue after direct3d reset, just skip them
                if ((sprite.Texture ?? sprite.SpriteFrame.Texture).IsDisposed)
                    continue;

                bool useLinearFiltering = sprite.LinearFiltering &&
                    (sprite.Factor.x * intermediateScale.x < 1 ||
                     sprite.Factor.y * intermediateScale.y < 1);

                if (linearFiltering != useLinearFiltering)
                {
                    if (linearFiltering.HasValue)
                        _spriteBatch.End();

                    linearFiltering = useLinearFiltering;
                    _spriteBatch.Begin(SpriteSortMode.Deferred,
                        Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied,
                        linearFiltering.Value ? SamplerState.LinearClamp : SamplerState.PointClamp,
                        null, null, null, transformMatrix);
                }

                // recompute position for not 1:1 sprite resolutions
                var position = new Microsoft.Xna.Framework.Vector2(sprite.Position.X, sprite.Position.Y);

                _spriteBatch.Draw(sprite.Texture ?? sprite.SpriteFrame.Texture,
                    position,
                    sourceRectangle: sprite.Rectangle,
                    sprite.Color,
                    rotation: 0,
                    Microsoft.Xna.Framework.Vector2.Zero,
                    (sprite.Factor).ToXna(),
                    SpriteEffects.None,
                    sprite.Position.Z);
            }

            if (linearFiltering.HasValue)
                _spriteBatch.End();
        }

        _spriteBatch.Begin(SpriteSortMode.FrontToBack, Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, transformMatrix);

        BlendOverlay.BlockFadeOut = _engine.IsLoading || BlendOverlay.Block;
        BlendOverlay.Update(elapsedSeconds);
        if (!renderTextAfterXbr)
            BlendOverlay.Render(_spriteBatch);
        if (_engine.MusicBlend)
            _engine.Music.Volume = 1 - BlendOverlay.BlendState;
        else
            _engine.Music.Volume = 1;

        if (_renderEntities != null)
        {
            foreach (var line in _renderEntities.OfType<LineEntity>())
                DrawLineBetween(line.Start, line.End, 2, line.Color);
        }

        //errorOverlay.Render(RenderTime, spriteRenderer);

        loadingOverlay.Render(elapsedSeconds, _spriteBatch);

        _spriteBatch.End();

        _engine.GraphicsDevice.SetRenderTarget(null);
        Texture2D presentationTexture = _intermediateTarget;
        if (_engine.OutputFiltering == OutputFiltering.SharpBilinear)
        {
            int horizontalScale = (_engine.Resolution.Native.x +
                _intermediateTarget.Width - 1) / _intermediateTarget.Width;
            int verticalScale = (_engine.Resolution.Native.y +
                _intermediateTarget.Height - 1) / _intermediateTarget.Height;
            int integerScale = System.Math.Max(1,
                System.Math.Min(horizontalScale, verticalScale));
            if (integerScale > 1)
            {
                EnsureSharpIntermediateTarget(integerScale);
                _engine.GraphicsDevice.SetRenderTarget(_sharpIntermediateTarget);
                _engine.GraphicsDevice.Clear(Color.Black);
                _spriteBatch.Begin(SpriteSortMode.Deferred,
                    Microsoft.Xna.Framework.Graphics.BlendState.Opaque,
                    SamplerState.PointClamp, null, null);
                _spriteBatch.Draw(_intermediateTarget,
                    new Rectangle(0, 0, _sharpIntermediateTarget.Width,
                        _sharpIntermediateTarget.Height), Color.White);
                _spriteBatch.End();
                _engine.GraphicsDevice.SetRenderTarget(null);
                presentationTexture = _sharpIntermediateTarget;
            }
        }
        else if (useXbr2)
        {
            EnsureSharpIntermediateTarget(2);
            _xbr2Effect.Parameters["TextureSize"].SetValue(
                new Microsoft.Xna.Framework.Vector2(_intermediateTarget.Width,
                    _intermediateTarget.Height));
            _engine.GraphicsDevice.SetRenderTarget(_sharpIntermediateTarget);
            _engine.GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin(SpriteSortMode.Deferred,
                Microsoft.Xna.Framework.Graphics.BlendState.Opaque,
                SamplerState.PointClamp, null, null, _xbr2Effect);
            _spriteBatch.Draw(_intermediateTarget,
                new Rectangle(0, 0, _sharpIntermediateTarget.Width,
                    _sharpIntermediateTarget.Height), Color.White);
            _spriteBatch.End();

            if (renderTextAfterXbr)
            {
                var postFilterTransform = Matrix.CreateScale(new Vector3(
                    intermediateScale.x * 2 + PIXEL_CORRECTION,
                    intermediateScale.y * 2 + PIXEL_CORRECTION, 1));
                bool pointBatchActive = false;
                foreach (var sprite in orderedSprites.Skip(deferredSpriteIndex))
                {
                    Texture2D texture = sprite.Texture ?? sprite.SpriteFrame.Texture;
                    if (texture.IsDisposed)
                        continue;

                    if (sprite.PostFilter || _xbr2AlphaEffect is null)
                    {
                        if (!pointBatchActive)
                        {
                            _spriteBatch.Begin(SpriteSortMode.Deferred,
                                Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied,
                                SamplerState.PointClamp, null, null, null,
                                postFilterTransform);
                            pointBatchActive = true;
                        }
                        _spriteBatch.Draw(texture,
                            new Microsoft.Xna.Framework.Vector2(sprite.Position.X,
                                sprite.Position.Y),
                            sprite.Rectangle, sprite.Color, 0,
                            Microsoft.Xna.Framework.Vector2.Zero, sprite.Factor.ToXna(),
                            SpriteEffects.None, sprite.Position.Z);
                        continue;
                    }

                    if (pointBatchActive)
                    {
                        _spriteBatch.End();
                        pointBatchActive = false;
                    }
                    _xbr2AlphaEffect.Parameters["TextureSize"].SetValue(
                        new Microsoft.Xna.Framework.Vector2(texture.Width, texture.Height));
                    _spriteBatch.Begin(SpriteSortMode.Immediate,
                        Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied,
                        SamplerState.PointClamp, null, null, _xbr2AlphaEffect,
                        postFilterTransform);
                    _spriteBatch.Draw(texture,
                        new Microsoft.Xna.Framework.Vector2(sprite.Position.X,
                            sprite.Position.Y),
                        sprite.Rectangle, sprite.Color, 0,
                        Microsoft.Xna.Framework.Vector2.Zero, sprite.Factor.ToXna(),
                        SpriteEffects.None, sprite.Position.Z);
                    _spriteBatch.End();
                }
                if (pointBatchActive)
                    _spriteBatch.End();

                _spriteBatch.Begin(SpriteSortMode.FrontToBack,
                    Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied,
                    SamplerState.PointClamp, null, null, null, postFilterTransform);
                BlendOverlay.Render(_spriteBatch);
                _spriteBatch.End();
            }
            _engine.GraphicsDevice.SetRenderTarget(null);
            presentationTexture = _sharpIntermediateTarget;
        }

        _engine.GraphicsDevice.Clear(Color.Black);
        int horizontalPresentationScale = _engine.Resolution.Native.x /
            presentationTexture.Width;
        int verticalPresentationScale = _engine.Resolution.Native.y /
            presentationTexture.Height;
        bool cleanIntegerShaderScale =
            _engine.OutputFiltering == OutputFiltering.SharpBilinearShader &&
            horizontalPresentationScale >= 2 &&
            horizontalPresentationScale == verticalPresentationScale &&
            presentationTexture.Width * horizontalPresentationScale ==
                _engine.Resolution.Native.x &&
            presentationTexture.Height * verticalPresentationScale ==
                _engine.Resolution.Native.y;
        bool useSharpBilinearShader = SharpBilinearAvailable &&
            (useXbr2 ||
             _engine.OutputFiltering == OutputFiltering.SharpBilinearShader &&
             !cleanIntegerShaderScale);
        if (useSharpBilinearShader)
        {
            _sharpBilinearEffect.Parameters["TextureSize"].SetValue(
                new Microsoft.Xna.Framework.Vector2(presentationTexture.Width,
                    presentationTexture.Height));
            _sharpBilinearEffect.Parameters["OutputSize"].SetValue(
                new Microsoft.Xna.Framework.Vector2(_engine.Resolution.Native.x,
                    _engine.Resolution.Native.y));
        }

        _spriteBatch.Begin(SpriteSortMode.Deferred,
            Microsoft.Xna.Framework.Graphics.BlendState.Opaque,
            _engine.OutputFiltering == OutputFiltering.NearestPoint ||
                cleanIntegerShaderScale
                ? SamplerState.PointClamp
                : SamplerState.LinearClamp,
            null, null, useSharpBilinearShader ? _sharpBilinearEffect : null);
        _spriteBatch.Draw(presentationTexture,
            new Rectangle(0, 0, _engine.Resolution.Native.x, _engine.Resolution.Native.y),
            Color.White);
        _spriteBatch.End();

        if (!useRemasteredGraphics)
        {
            var framebufferTransform = Matrix.CreateScale(new Vector3(
                _engine.Resolution.Native.x / (float)_engine.Resolution.Game.x + PIXEL_CORRECTION,
                _engine.Resolution.Native.y / (float)_engine.Resolution.Game.y + PIXEL_CORRECTION,
                1));
            _spriteBatch.Begin(SpriteSortMode.Deferred,
                Microsoft.Xna.Framework.Graphics.BlendState.NonPremultiplied,
                SamplerState.PointClamp, null, null, null, framebufferTransform);
            float visibility = 1 - BlendOverlay.BlendState;
            foreach (var sprite in (_renderEntities ?? new RenderEntityQueue())
                .OfType<SpriteEntity>()
                .Where(sprite => sprite.DirectToFramebuffer)
                .OrderBy(sprite => sprite.Position.Z))
            {
                Texture2D texture = sprite.Texture ?? sprite.SpriteFrame.Texture;
                if (texture.IsDisposed)
                    continue;
                Color color = new(
                    (byte)(sprite.Color.R * visibility),
                    (byte)(sprite.Color.G * visibility),
                    (byte)(sprite.Color.B * visibility),
                    sprite.Color.A);
                _spriteBatch.Draw(texture,
                    new Microsoft.Xna.Framework.Vector2(sprite.Position.X,
                        sprite.Position.Y),
                    sprite.Rectangle, color, 0,
                    Microsoft.Xna.Framework.Vector2.Zero, sprite.Factor.ToXna(),
                    SpriteEffects.None, sprite.Position.Z);
            }
            _spriteBatch.End();
        }
    }

    void EnsureIntermediateTarget(bool useRemasteredGraphics)
    {
        Platform.Vector2 targetSize = useRemasteredGraphics
            ? _engine.Resolution.BackBuffer
            : _engine.Resolution.Game;
        int width = targetSize.x;
        int height = targetSize.y;
        if (_intermediateTarget is not null &&
            _intermediateTarget.Width == width && _intermediateTarget.Height == height)
            return;

        _intermediateTarget?.Dispose();
        _intermediateTarget = new RenderTarget2D(_engine.GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None);
    }

    void EnsureSharpIntermediateTarget(int integerScale)
    {
        int width = _intermediateTarget.Width * integerScale;
        int height = _intermediateTarget.Height * integerScale;
        if (_sharpIntermediateTarget is not null &&
            _sharpIntermediateTarget.Width == width &&
            _sharpIntermediateTarget.Height == height)
            return;

        _sharpIntermediateTarget?.Dispose();
        _sharpIntermediateTarget = new RenderTarget2D(_engine.GraphicsDevice, width, height,
            false, SurfaceFormat.Color, DepthFormat.None);
    }

    public void DrawLineBetween(Microsoft.Xna.Framework.Vector3 startPos, Microsoft.Xna.Framework.Vector3 endPos, int thickness, Color color)
    {
        var distance = (int)Microsoft.Xna.Framework.Vector3.Distance(startPos, endPos);
        if (distance <= 0)
            return;

        var rotation = (float)System.Math.Atan2(endPos.Y - startPos.Y, endPos.X - startPos.X);
        var origin = new Microsoft.Xna.Framework.Vector2(0, thickness / 2);

        _spriteBatch.Draw(
            WhiteTexture,
            new Microsoft.Xna.Framework.Vector2(startPos.X, startPos.Y),
            null,
            color,
            rotation,
            origin,
            new Microsoft.Xna.Framework.Vector2(distance, thickness),
            SpriteEffects.None,
            startPos.Z);
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
