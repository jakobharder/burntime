using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

using Burntime.Platform.Graphics;

namespace Burntime.Platform
{
    public interface IApplication
    {
        String Title { get; }
        Vector2[] Resolutions { get; }
        Icon Icon { get; }

        void Render(IRenderTarget Target);
        void Process(float Elapsed);
        void Reset();
        void Close();
    }
}
