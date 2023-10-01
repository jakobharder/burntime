using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform.Graphics
{
    public class StaticAnimationParticle : Particle
    {
        protected Vector2 position;
        protected ISprite sprite;

        public StaticAnimationParticle(ISprite sprite, Vector2 position)
        {
            this.sprite = sprite;
            this.sprite.Animation.Endless = false;
            this.position = position;
        }

        public override Vector2 Position
        {
            get { return position; }
        }

        public override float Alpha
        {
            get { return 1.0f; }
        }

        public override ISprite Sprite
        {
            get { return sprite; }
        }

        public override bool IsAlive
        {
            get { return !sprite.Animation.End; }
        }

        public override void Process(float elapsed)
        {
            sprite.Animation.Update(elapsed);
        }
    }
}
