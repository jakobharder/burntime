using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework.GUI;

namespace Burntime.Framework
{
    public abstract class Scene : Container
    {
        string music;
        public string Music
        {
            get { return music; }
            set { music = value; }
        }

        public Scene(Module app)
            : base(app)
        {
            Layer = 0;
            HasFocus = true;
        }

        internal void ActivateScene()
        {
            ActivateScene(null);
        }

        internal void ActivateScene(object parameter)
        {
            OnResizeScreen();
            OnActivateScene(parameter);
#warning TODO SlimDX/Mono Music
            //if (music != null)
            //    app.Engine.Music.Play(music);
        }

        internal void InactivateScene()
        {
#warning TODO SlimDX/Mono Music
            //app.Engine.Music.Stop();

            OnInactivateScene();
        }

        protected virtual void OnActivateScene(object parameter)
        {
        }

        protected virtual void OnInactivateScene()
        {
        }
    }
}
