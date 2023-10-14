using Microsoft.Xna.Framework.Graphics;
using Burntime.MonoGame;

namespace Burntime.Platform.Graphics
{
    class LoadingOverlay
    {
        float rotationSpeed = 1.5f;
        float rotationState;
        float fadeOutSpeed = 4;
        float fadeOutState = 1;
        float loadingDelay = 1;
        float loadingDelayState = 0;

        Texture loadingTexture;
        Vector2 _size;
        BurntimeGame _engine;

        public LoadingOverlay(BurntimeGame engine, Vector2 screenSize)
        {
            _size = screenSize;
            _engine = engine;
        }

        public void Render(float elapsedSeconds, SpriteBatch spriteBatch)
        {
            // loading render
            rotationState += rotationSpeed * elapsedSeconds;
            if (rotationState >= 1)
                rotationState -= (float)System.Math.Floor(rotationState);

            if (!_engine.isLoading)
            {
                if (_engine.loadingStack > 0 || _engine.ResourceManager.IsLoading)
                {
                    //loadingDelayState += loadingDelay * elapsedSeconds;
                    //if (loadingDelayState >= 1)
                    //{
                        _engine.isLoading = true;
                    //}
                }
            }
            else
            {
                if (_engine.loadingStack == 0 && !_engine.ResourceManager.IsLoading)
                    _engine.isLoading = false;
                loadingDelayState = 0;
                fadeOutState = 1;
            }

            if (fadeOutState > 0)
            {
                //SpriteRenderer.Begin(SpriteFlags.SortTexture | SpriteFlags.SortDepthFrontToBack | SpriteFlags.AlphaBlend);

                //SpriteRenderer.Transform = SlimDX.Matrix.Translation(size.x - 32, size.y - 32, 0);
                //SpriteRenderer.Transform = SlimDX.Matrix.RotationZ(-(float)System.Math.PI * rotationState * 2) * SpriteRenderer.Transform;
                //SpriteRenderer.Transform = SlimDX.Matrix.Scaling(1.5f, 1.5f, 1.5f) * SpriteRenderer.Transform;

                //SpriteRenderer.Draw(loadingTexture, new SlimDX.Vector3(16, 16, 0), new SlimDX.Vector3(0, 0, 0), new SlimDX.Color4(fadeOutState, 1, 1, 1));

                //SpriteRenderer.End();

                fadeOutState -= elapsedSeconds * fadeOutSpeed;
            }
        }

        public void Load(GraphicsDevice device)
        {
            //loadingTexture = Texture.FromStream(device, FileSystem.GetFile("loading.png"), Usage.None, Pool.Default);
        }

        public void Unload()
        {
            //loadingTexture.Dispose();
        }
    }
}
