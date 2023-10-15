using System;
using System.Collections.Generic;
using System.Text;

using SlimDX.Direct3D9;
using Burntime.Platform.IO;

namespace Burntime.Platform.Graphics
{
    class ErrorOverlay
    {
        Texture crashedTexture;
        Texture emptyTexture;
        Vector2 size;

        FadingHelper crashFade = new FadingHelper(1.0f);
        FadingHelper crashFadeBack = new FadingHelper(2.5f);
        FadingHelper crashWait = new FadingHelper(0.2f);

        Engine engine;

        public ErrorOverlay(Engine Engine, Vector2 ScreenSize)
        {
            size = ScreenSize;
            engine = Engine;
        }

        public void Render(GameTime RenderTime, SlimDX.Direct3D9.Sprite SpriteRenderer)
        {
            if (engine.crashed)
            {
                crashFade.FadeIn();
                crashFadeBack.FadeIn();
            }
            else
            {
                crashFade.FadeOut();
                crashFadeBack.FadeOut();
            }

            if (!crashFade.IsOut || engine.crashed)
            {
                crashFade.Update(RenderTime.Elapsed);
                crashFadeBack.Update(RenderTime.Elapsed);
                crashWait.Update(RenderTime.Elapsed);
                crashWait.FadeIn();

                SpriteRenderer.Begin(SpriteFlags.SortTexture | SpriteFlags.SortDepthFrontToBack | SpriteFlags.AlphaBlend);

                System.Drawing.Rectangle rc = new System.Drawing.Rectangle(0, 0, size.x, size.y);
                SpriteRenderer.Draw(emptyTexture, rc, new SlimDX.Vector3(0, 0, 0), new SlimDX.Vector3(0, 0, 0.001f), new SlimDX.Color4(crashFadeBack.State * 0.8f, 0, 0, 0));

                SpriteRenderer.Transform = SlimDX.Matrix.Translation(size.x / 2, size.y / 2, 0);
                SpriteRenderer.Transform = SlimDX.Matrix.Scaling(0.5f, 0.5f, 0.5f) * SpriteRenderer.Transform;

                SpriteRenderer.Draw(crashedTexture, new SlimDX.Vector3(128, 128, 0), new SlimDX.Vector3(0, 0, 0), new SlimDX.Color4(crashFade.State, 1, 1, 1));

                SpriteRenderer.End();
                if (crashWait.IsIn)
                {
                    engine.RecoverAfterCrash();
                    crashWait.State = 0;
                }
            }
        }

        public void Load(Device device)
        {
            crashedTexture = Texture.FromStream(device, FileSystem.GetFile("error.png"), Usage.None, Pool.Default);
            emptyTexture = new Texture(device, 1, 1, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
            SlimDX.DataRectangle dr = emptyTexture.LockRectangle(0, LockFlags.Discard);
            dr.Data.Write<uint>(0xffffffff);
            emptyTexture.UnlockRectangle(0);
        }

        public void Unload()
        {
            crashedTexture.Dispose();
            emptyTexture.Dispose();
        }
    }
}
