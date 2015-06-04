
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

namespace Burntime.Platform.Graphics
{
    public class ParticleEngine
    {
        List<Particle> particles = new List<Particle>();

        public void Clear()
        {
            particles.Clear();
        }

        public void Add(Particle particle)
        {
            particles.Add(particle);
        }

        public void Update(float elapsed)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                // update particle
                particles[i].Process(elapsed);

                // remove if not alive
                if (!particles[i].IsAlive)
                {
                    particles.RemoveAt(i);
                    i--;
                }
            }
        }

        public void Render(RenderTarget target)
        {
            foreach (Particle p in particles)
            {
                target.DrawSprite(p.Position - p.Sprite.Size / 2, p.Sprite, p.Alpha);
            }
        }
    }
}
