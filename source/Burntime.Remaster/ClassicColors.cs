using Burntime.Platform;

namespace Burntime.Remaster;

public static class ClassicColors
{
    public static readonly PixelColor LightGray = new(212, 212, 212); // #D4D4D4 — Light gray
    public static readonly PixelColor Gray = new(184, 184, 184); // #B8B8B8 — Gray

    public static readonly PixelColor HudText = new(92, 92, 148); // #5C5C94 — Muted indigo
    public static readonly PixelColor HudTextHover = new(144, 160, 212); // #90A0D4 — Light periwinkle blue
    public static readonly PixelColor HudTextAccent = new(240, 120, 32); // #F07820 — Vivid orange
    public static readonly PixelColor HudWarning = new(252, 180, 56); // #FCB438 — Amber
    public static readonly PixelColor MapTargetText = new(184, 184, 184); // #B8B8B8 — Neutral gray

    public static readonly PixelColor MenuText = new(108, 116, 168); // #6C74A8 — Slate blue
    public static readonly PixelColor MenuTextHover = new(240, 64, 56); // #F04038 — Coral red
    public static readonly PixelColor DialogText = new(240, 164, 56); // #F0A438 — Golden orange
    public static readonly PixelColor InventoryText = new(128, 136, 192); // #8088C0 — Soft periwinkle

    public static readonly PixelColor OptionsDisabled = new(100, 100, 100); // #646464 — Dark gray
    public static readonly PixelColor OptionsRed = new(134, 44, 4); // #862C04 — Dark burnt orange
    public static readonly PixelColor OptionsRedHover = new(190, 77, 12); // #BE4D0C — Burnt orange
    public static readonly PixelColor OptionsBlueHover = new(109, 117, 170); // #6D75AA — Muted periwinkle
    public static readonly PixelColor OptionsGreen = new(0, 108, 0); // #006C00 — Dark green

    public static readonly PixelColor StatusSuccess = new(0, 156, 0); // #009C00 — Green
    public static readonly PixelColor StatusFailure = new(208, 0, 0); // #D00000 — Red
    public static readonly PixelColor StatusInactive = new(72, 72, 116); // #484874 — Dark indigo
}
