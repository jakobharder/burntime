using Burntime.Platform.IO;
using Burntime.Platform.Graphics;
using Burntime.MonoGame;
using Burntime.MonoGame.Graphics;

namespace Burntime.Platform.Resource
{
    public class ResourceManager : ResourceManagerBase, IDisposable
    {
        readonly BurntimeGame _engine;

        public ResourceManager(BurntimeGame engine) : base(engine)
        {
            _engine = engine;
            engine.ResourceManager = this;
        }

        #region Font
        public override Font? GetFont(string filePath, PixelColor color)
        {
            return GetFont(filePath, color, PixelColor.Black);
        }

        public override Font? GetFont(string filePath, PixelColor color, PixelColor backColor)
        {
            ResourceInfoFont info;
            info.Name = filePath;
            if (backColor != PixelColor.Black)
            {
                info.Fore = color;
                info.Back = backColor;
            }
            else
            {
                info.Fore = PixelColor.White;
                info.Back = PixelColor.Black;
            }

            var font = new Font(this)
            {
                Info = new FontInfo
                {
                    Font = filePath,
                    ForeColor = color,
                    BackColor = backColor,
                    Colorize = color != PixelColor.Transparent,
                    UseBackColor = backColor != PixelColor.Black
                }
            };

            return LoadFont(font);
        }

        public override Font? LoadFont(Font font)
        {
            string fontFile = font.Info.Font == "font.txt" &&
                !_engine.UseRemasteredGraphics &&
                _engine.OutputFiltering == OutputFiltering.Xbr2
                    ? "font2x.txt"
                    : font.Info.Font;
            ResourceInfoFont info;
            info.Name = fontFile;
            info.Fore = font.Info.ForeColor;
            info.Back = font.Info.BackColor;

            lock (fonts)
            {
                bool isNewResource = !fonts.TryGetValue(info, out FontResource? resource);
                resource ??= new FontResource();

                font.Resource = resource;
                if (resource.IsLoaded)
                    return font;

                ResourceID id = fontFile;
                var replaced = GetReplacement(id);
                FilePath path = new FilePath(replaced?.Id?.ToString() ?? (string)id);
                if (!fontProcessors.ContainsKey(path.Extension))
                    return null;

                _engine.IncreaseLoadingCount();

                if (Activator.CreateInstance(fontProcessors[path.Extension].GetType()) is not IFontProcessor processor)
                    return null;
                processor.Process((string)path);
                Log.Debug("load \"" + path.FullPath + "\"");

                processor.Color = font.Info.ForeColor;
                processor.Shadow = font.Info.BackColor;

                SpriteFrame frame = new();
                lock (sprites)
                    MemoryUsage += frame.LoadFromProcessor(processor, keepSystemCopy: true);

                Sprite sprite = new Sprite(this, "", frame)
                {
                    Resolution = processor.Factor,
                    // Font filtering is selected later from the final physical scale.
                    LinearFiltering = true
                };

                resource.Load(sprite, processor.CharInfo, processor.Kerning, processor.Offset, processor.GlyphHeight);
                if (isNewResource)
                    fonts.Add(info, resource);

                _engine.DecreaseLoadingCount();
            }
            return font;
        }
        #endregion

        #region Sprites
        public override ISprite GetImage(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Delayed)
        {
            return GetSprite(id, loadType);
        }

        ISprite GetSprite(ResourceID id, ResourceLoadType LoadType)
        {
            Vector2f factor = Vector2f.One;
            ResourceID realid = (string)id;

            var replaced = GetReplacement(id);
            if (replaced is not null)
            {
                // if available return newid sprite right away
                if (sprites.TryGetValue(replaced.Id, out ISprite value))
                {
                    return value.Clone();
                }
                // otherwise check if newid is available, if not fallback to old id
                else if (CheckReplacementID(replaced.Id))
                {
                    id = replaced.Id;
                    factor = replaced.Factor;
                }
            }

            if (sprites.ContainsKey(id))
            {
                return sprites[id].Clone();
            }
            else
            {
                Sprite s;

                string format = id.Format;

                if (Log.DebugOut)
                {
                    string info = $"init \"{id}\"";
                    if (factor != Vector2f.One)
                        info += $" {System.Math.Round(factor.x * 100)}% x {System.Math.Round(factor.y * 100)}%";
                    if (realid != id)
                        info += $" ({realid})";
                    Log.Debug(info);
                }

                if (!spriteProcessors.ContainsKey(format))
                    return null;

                _engine.IncreaseLoadingCount();

                lock (this)
                {
                    ISpriteProcessor loader = GetSpriteProcessor(id, false);
                    if (loader is ISpriteAnimationProcessor)
                    {
                        ISpriteAnimationProcessor loaderAni = (ISpriteAnimationProcessor)loader;

                        loader.Process(id);

                        int count = loaderAni.FrameCount;

                        SpriteFrame[] frames = new SpriteFrame[loaderAni.FrameCount];
                        for (int i = 0; i < loaderAni.FrameCount; i++)
                        {
                            frames[i] = new SpriteFrame();
                        }

                        s = new Sprite(this, realid, frames, new SpriteAnimation(loaderAni.FrameCount));
                        s.LoadType = LoadType;

                        sprites.Add(id, s);
                    }
                    else
                    {
                        SpriteFrame si = new SpriteFrame();
                        s = new Sprite(this, realid, si);
                        s.LoadType = LoadType;

                        sprites.Add(id, s);
                    }
                }

                if (LoadType == ResourceLoadType.Now)
                    Reload(s, ResourceLoadType.Now);

                _engine.DecreaseLoadingCount();
                return s;
            }
        }

        private void Reload(Sprite Sprite, ResourceLoadType LoadType)
        {
            Sprite.internalFrames[0].IsLoading = true;

            if (LoadType == ResourceLoadType.Delayed)
            {
                delayLoader.Enqueue(Sprite);
                return;
            }

            ResourceID id = Sprite.ID;
            if (id.File == "" || !spriteProcessors.ContainsKey(id.Format))
                return;

            ResourceID realid = (string)id;

            // check replacements
            Sprite.internalFrames[0].Resolution = 1;
            if (replacement != null)
            {
                var replaced = GetReplacement(id);
                if (replaced is not null)
                {
                    if (CheckReplacementID(replaced.Id))
                    {
                        id = replaced.Id;
                        Sprite.internalFrames[0].Resolution = replaced.Factor;
                    }
                }
            }

            _engine.IncreaseLoadingCount();

            if (Log.DebugOut)
            {
                string info = Sprite.IsNew ? "load" : "reload";
                info += $" \"{id}\"";
                if (Sprite.internalFrames[0].Resolution != 1)
                    info += $" " + Log.FormatPercentage(Sprite.internalFrames[0].Resolution);
                if (realid.ToString() != id.ToString())
                    info += $" ({realid})";
                Log.Debug(info);
            }
            Sprite.IsNew = false;
            ISpriteProcessor loader = GetSpriteProcessor(id, true);
            loader.Process(id);

            if (loader is ISpriteAnimationProcessor loaderAni)
            {
                if (Sprite.internalFrames.Length != loaderAni.FrameCount)
                    Array.Resize(ref Sprite.internalFrames, loaderAni.FrameCount);
                Sprite.Animation ??= new SpriteAnimation(loaderAni.FrameCount);
                Sprite.Animation.Frame = System.Math.Min(loaderAni.FrameCount - 1, Sprite.Animation.Frame);
                Sprite.Animation.FrameCount = loaderAni.FrameCount;

                for (int i = 0; i < loaderAni.FrameCount; i++)
                {
                    if (!loaderAni.SetFrame(i))
                    {
                        Array.Resize(ref Sprite.internalFrames, loaderAni.FrameCount);
                        Sprite.Animation.FrameCount = i;
                        break;
                    }

                    Sprite.internalFrames[i] = Sprite.internalFrames[i] ?? new SpriteFrame();

                    lock (sprites)
                        MemoryUsage += Sprite.internalFrames[i].LoadFromProcessor(loader);
                    Sprite.internalFrames[i].Resolution = Sprite.internalFrames[0].Resolution;
                }
            }
            else
            {
                if (Sprite.Animation is not null)
                {
                    Sprite.Animation.Frame = 0;
                    Sprite.Animation.FrameCount = 1;
                }
                lock (sprites)
                    MemoryUsage += Sprite.internalFrames[0].LoadFromProcessor(loader);
            }

            _engine.DecreaseLoadingCount();
        }

        public override void Reload(ISprite sprite, ResourceLoadType loadType = ResourceLoadType.Delayed)
        {
            if (sprite is Sprite s)
                Reload(s, loadType);
        }
        #endregion

    }
}
