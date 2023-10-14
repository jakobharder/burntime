using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Framework;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster
{
    class Helper
    {
        static public DataID<ISprite> GetCharacterBody(int body, int colorSet)
        {
            int sprite = (body + 1) * 16 + 48 * colorSet;
            return BurntimeClassic.Instance.ResourceManager.GetData("burngfxani@syssze.raw?" + sprite + "-" + (sprite + 15));
        }

        static public int GetSetBodyId(Burntime.Data.BurnGfx.Save.CharacterType characterType)
        {
            switch (characterType)
            {
                case Burntime.Data.BurnGfx.Save.CharacterType.Mercenary:
                    return 1;
                case Burntime.Data.BurnGfx.Save.CharacterType.Technician:
                case Burntime.Data.BurnGfx.Save.CharacterType.Doctor:
                    return 0;
                default:
                    return 2;
            }
        }
    }
}
