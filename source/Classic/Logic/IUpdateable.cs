using System;

using Burntime.Platform;
using Burntime.Framework.States;

namespace Burntime.Classic.Logic
{
    public interface IUpdateable
    {
        void Update(float Elapsed);
    }
}