namespace Burntime.MonoGame.Graphics;

internal static class ConversionExtensions
{
    public static Microsoft.Xna.Framework.Vector2 ToXna(this Burntime.Platform.Vector2f vector)
    {
        return new Microsoft.Xna.Framework.Vector2(vector.x, vector.y);
    }

    public static Microsoft.Xna.Framework.Vector2 ToXna(this Burntime.Platform.Vector2 vector)
    {
        return new Microsoft.Xna.Framework.Vector2(vector.x, vector.y);
    }
}
