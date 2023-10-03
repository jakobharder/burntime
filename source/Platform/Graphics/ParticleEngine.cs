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
