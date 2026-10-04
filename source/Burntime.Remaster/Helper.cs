using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Remaster
{
    class Helper
    {
        internal const int GrayBodyColorSet = 3;
        internal const uint GrayClothingRgb = 0xd4d4d4;
        static public DataID<ISprite> GetCharacterBody(int body, int colorSet)
        {
            string id = GetCharacterBodyResource(body, colorSet);
            return BurntimeClassic.Instance.ResourceManager.GetData(id, ResourceLoadType.LinkOnly);
        }

        internal static string GetCharacterBodyResource(int body, int colorSet)
        {
            int sprite = GetBodyId(body, colorSet == GrayBodyColorSet ? 2 : colorSet);
            if (sprite < 0) sprite = 0;
            return $"burngfxani@syssze.raw?{sprite}-{sprite + 15}" +
                (colorSet == GrayBodyColorSet ? "?;recolor=d4d4d4" : "");
        }

        static public int GetBodyId(int body, int color)
        {
            if (color < 0 || color >= 3)
                return -1;

            if (body == 3)
                return (14 + color) * 16;
            else if (body >= 0 && body < 3)
                return (body + 1) * 16 + 48 * color;

            return -1;
        }

        static public int GetSetBodyId(Data.BurnGfx.Save.CharacterType characterType)
        {
            return characterType switch
            {
                Data.BurnGfx.Save.CharacterType.Mercenary => 1,
                Data.BurnGfx.Save.CharacterType.Doctor => 0,
                Data.BurnGfx.Save.CharacterType.Technician => 2,
                Data.BurnGfx.Save.CharacterType.Boss => 3,
                _ => -1,
            };
        }

        static public int GetSetBodyId(CharClass charClass)
        {
            return charClass switch
            {
                CharClass.Mercenary => 1,
                CharClass.Doctor => 0,
                CharClass.Technician => 2,
                CharClass.Boss => 3,
                _ => -1,
            };
        }

        static public int GetColorFromSpriteId(int spriteId)
        {
            spriteId /= 16;

            if (spriteId > 16)
                return -1;

            if (spriteId >= 14)
                return spriteId - 14;

            if (spriteId >= 1 || spriteId <= 9)
                return (spriteId - 1) / 3;

            return -1;
        }
    }
}
