/*
 *  Burntime Platform
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform.Graphics
{
    public class StaticAnimationParticle : Particle
    {
        protected Vector2 position;
        protected Sprite sprite;

        public StaticAnimationParticle(Sprite sprite, Vector2 position)
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

        public override Sprite Sprite
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
