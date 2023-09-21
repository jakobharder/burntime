using System;

namespace Burntime.Classic.Logic.Generation
{
    public class ClassicBurnGfxCreator : IGameObjectCreator
    {
        public void Create(ClassicGame game)
        {
            var gamdat = new Burntime.Data.BurnGfx.Save.SaveGame();
            gamdat.Open();
            LogicFactory.SetParameter("gamdat", gamdat);
        }
    }
}
