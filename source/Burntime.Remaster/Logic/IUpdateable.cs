using System;

using Burntime.Platform;
using Burntime.Framework.States;

namespace Burntime.Remaster.Logic
{
    public interface IUpdateable
    {
        void Update(float Elapsed);
    }
}