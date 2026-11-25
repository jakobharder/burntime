using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster.GUI;

public partial class MenuWindow
{
    const int TouchHeaderHeight = 10;
    const int TouchBottomPadding = 4;
    const int TouchPanelGap = 8;
    bool touchLayout;
    int touchSectionStart = -1;
    int touchPanelWidth;
    int touchRowHeight;
    GuiFont? touchHeaderFont;
    GuiFont? touchMenuFont;
    ISprite? touchTop, touchMiddle, touchBottom;

    // One modal window owns both panels so dismissal, focus and touch capture
    // stay atomic; group actions do not require a second popup or another tap.
    public void BeginTouchGroup() => touchSectionStart = _menuEntries.Count;
    internal int TouchPanelCount => touchSectionStart > 0 && touchSectionStart < _menuEntries.Count ? 2 : 1;
    internal int TouchRowHeight => touchRowHeight;
    internal bool UsesTouchLayout => touchLayout;

    Rect TouchPlacementArea => placementBounds.HasValue
        ? placementBounds.Value.Intersect(app.Engine.Resolution.SafeArea - (Parent?.PositionOnScreen ?? Vector2.Zero))
        : app.Engine.Resolution.SafeArea - (Parent?.PositionOnScreen ?? Vector2.Zero);

    void LayoutTouchPanels()
    {
        Rect available = TouchPlacementArea;
        int rows = TouchPanelCount == 2 ? System.Math.Max(touchSectionStart, _menuEntries.Count - touchSectionStart) : _menuEntries.Count;
        touchRowHeight = System.Math.Max(1, System.Math.Min(25,
            (available.Height - TouchHeaderHeight - TouchBottomPadding) / System.Math.Max(1, rows)));
        int desiredWidth = 92;
        foreach (MenuItem entry in _menuEntries)
            desiredWidth = System.Math.Max(desiredWidth, _defaultFont.GetWidth(entry.Text) + 16);
        touchPanelWidth = System.Math.Max(1, System.Math.Min(desiredWidth,
            (available.Width - (TouchPanelCount - 1) * TouchPanelGap) / TouchPanelCount));
        Size = new Vector2(touchPanelWidth * TouchPanelCount + (TouchPanelCount - 1) * TouchPanelGap,
            TouchHeaderHeight + rows * touchRowHeight + TouchBottomPadding);
    }

    internal Rect TouchEntryBounds(int index)
    {
        bool second = TouchPanelCount == 2 && index >= touchSectionStart;
        int row = second ? index - touchSectionStart : index;
        return new Rect(second ? touchPanelWidth + TouchPanelGap : 0,
            TouchHeaderHeight + row * touchRowHeight, touchPanelWidth, touchRowHeight);
    }

    internal int TouchEntryAt(Vector2 position)
    {
        for (int i = 0; i < _menuEntries.Count; i++)
            if (TouchEntryBounds(i).PointInside(position) && !string.IsNullOrEmpty(_menuEntries[i].Text)) return i;
        return -1;
    }

    void RenderTouchPanels(RenderTarget target)
    {
        touchHeaderFont ??= new GuiFont("font-small.txt", ClassicColors.HudTextHover);
        touchMenuFont ??= new GuiFont(BurntimeClassic.FontName, ClassicColors.LightGray);
        for (int panel = 0; panel < TouchPanelCount; panel++)
        {
            int start = panel == 0 ? 0 : touchSectionStart;
            int count = TouchPanelCount == 1 ? _menuEntries.Count : panel == 0 ? touchSectionStart : _menuEntries.Count - touchSectionStart;
            Vector2 origin = new(panel * (touchPanelWidth + TouchPanelGap), 0);
            Vector2 size = new(touchPanelWidth, TouchHeaderHeight + count * touchRowHeight + TouchBottomPadding);
            // Reuse the original context-menu cap, sides and foot. Only the
            // middle section is stretched to accommodate larger touch rows.
            DrawMenuSkin(target, origin, size);
            string heading = app.ResourceManager.GetString(panel == 0 && FirstLineAction == InputAction.None ? "controls?22" : "prompts?35");
            touchHeaderFont.DrawText(target, origin + new Vector2(touchPanelWidth / 2, 2), heading, TextAlignment.Center);
            for (int i = start; i < start + count; i++)
            {
                Rect row = TouchEntryBounds(i);
                bool selected = (_touchPressedIndex >= 0 ? _touchPressedIndex : _focusIndex) == i;
                GuiFont font = selected ? _focusFont : touchMenuFont;
                string label = _menuEntries[i].Text;
                if (font.GetWidth(label) > touchPanelWidth - 12)
                {
                    while (label.Length > 0 && font.GetWidth(label + "...") > touchPanelWidth - 12)
                        label = label[..^1];
                    label += "...";
                }
                font.DrawText(target, new Vector2(row.Left + row.Width / 2, row.Top + row.Height / 2), label,
                    TextAlignment.Center, VerticalTextAlignment.Center);
            }
        }
    }
    void DrawMenuSkin(RenderTarget target, Vector2 origin, Vector2 size)
    {
        ISprite top = _topElement, middle = _middleElement, bottom = _bottomElement;
        if (!top.Touch() || !middle.Touch() || !bottom.Touch()) return;
        touchTop ??= top.Clone();
        touchMiddle ??= middle.Clone();
        touchBottom ??= bottom.Clone();
        Draw(top, touchTop, origin, size.x, top.Height);
        Draw(middle, touchMiddle, origin + new Vector2(0, TOP_HEIGHT), size.x, size.y - TOP_HEIGHT - 6, MIDDLE_HEIGHT);
        Draw(bottom, touchBottom, origin + new Vector2(0, size.y - 6), size.x, 6);

        void Draw(ISprite source, ISprite skin, Vector2 position, int width, int height, int? contentHeight = null)
        {
            // The remastered middle tile has transparent padding below its
            // original eleven-unit row. Stretch only that row's painted area.
            skin.Resolution = Vector2f.One;
            Vector2 pixels = skin.Size;
            if (contentHeight.HasValue)
                pixels.y = System.Math.Min(pixels.y,
                    (int)System.Math.Ceiling(contentHeight.Value / source.Resolution.y));
            skin.Resolution = new Vector2f(width / (float)System.Math.Max(1, pixels.x),
                height / (float)System.Math.Max(1, pixels.y));
            target.SelectSprite(skin);
            target.DrawSelectedSprite(position, new Rect(Vector2.Zero, pixels), PixelColor.White);
        }
    }

}
