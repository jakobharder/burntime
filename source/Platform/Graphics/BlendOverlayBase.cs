using System.Threading;

namespace Burntime.Platform.Graphics
{
    public abstract class BlendOverlayBase
    {
        protected readonly FadingHelper _blendFade = new(2.5f);
        protected Vector2 _size;

        public bool BlockFadeOut { get; set; }
        public bool Block { get; set; }

        public bool IsBlended => _blendFade.State == 1;

        public float Speed
        {
            get => _blendFade.Speed;
            set => _blendFade.Speed = value;
        }
        public float BlendState => _blendFade.State;

        public BlendOverlayBase(Vector2 ScreenSize)
        {
            _size = ScreenSize;
        }

        public void FadeOut(bool wait = false)
        {
            _blendFade.FadeTo = 1;

            if (wait)
            {
                // block thread until full blend is reached
                Thread.Sleep(500 + (int)((1 - _blendFade.State) * 1000 / _blendFade.Speed));
            }
        }

        public void FadeIn()
        {
            if (_blendFade.State != 1)
            {
                // block thread until full blend is reached
                Thread.Sleep((int)((1 - _blendFade.State) * 1000 / _blendFade.Speed));
            }

            _blendFade.FadeTo = 0;
        }
    }
}
