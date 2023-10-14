using System;

namespace Burntime.Remaster.Logic.Generation
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
