using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Interaction;
using Burntime.Remaster.Logic.Rules;
using System.Collections.Generic;
using System.Linq;

namespace Burntime.Remaster.GUI;

/// <summary>
/// Text-only field manual displayed as a modal overlay.
/// </summary>
public sealed class ManualWindow : Container
{
    readonly bool _setupNotes;
    int PageCount => _setupNotes ? 3 : 5;
    int TabWidth => _setupNotes ? 93 : 56;
    const int TabsLeft = 10;
    int TextLineHeight => _textFont.LineHeight;
    const int FooterHeight = 30;
    const string LabelsResource = "manual?s5";

    enum ManualEntryKind
    {
        Text,
        Heading,
        Subheading,
        Tiers,
        ClassImage,
        SupplyExample,
        Goal,
        ContextItem,
        Production,
        Construction
    }

    sealed record ManualEntry(ManualEntryKind Kind, string Payload, int LineCount);
    sealed record ManualPageLayout(ManualEntry[] Entries, int TotalLineCount);

    readonly GuiFont _titleFont;
    readonly GuiFont _textFont;
    readonly GuiFont _subheadingFont;
    readonly GuiFont _mutedFont;
    readonly GuiFont _detailFont;
    readonly InputControlLabelRenderer _exitControlRenderer;
    readonly GuiFont _selectedFont;
    readonly TextHelper _uiText;
    readonly Button _exitButton;
    ISprite? _goalFlag;
    ISprite? _goalCity;
    bool _restoreRenderMouse;
    bool _hasRenderMouseOverride;
    int _page;
    int _hoveredPage = -1;
    readonly int[] _textScroll;
    readonly ManualPageLayout?[] _pageLayouts;
    float _touchScrollPixels;
    readonly KineticScroll _touchMomentum = new();
    const float ScrollRepeatDelay = 0.3f;
    const float ScrollRepeatInterval = 0.06f;
    bool _heldScrollUp;
    bool _heldScrollDown;
    int _scrollDirection;
    float _scrollRepeatRemaining = ScrollRepeatDelay;

    public ManualWindow(Module app, Vector2 hostSize, bool setupNotes = false)
        : base(app)
    {
        _setupNotes = setupNotes;
        _textScroll = new int[PageCount];
        _pageLayouts = new ManualPageLayout?[PageCount];
        Size = new Vector2(300, hostSize.y);
        Position = new Vector2((hostSize.x - Size.x) / 2, 0);
        // Map HUD elements reach layer 60. Keep the complete modal above them.
        Layer = 100;
        IsModal = true;
        HasFocus = true;
        CaptureAllMouseClicks = true;

        _titleFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.DialogText) { Borders = TextBorders.None };
        _textFont = new GuiFont("font-small.txt",
            ClassicColors.LightGray) { Borders = TextBorders.None };
        _subheadingFont = new GuiFont("font-small.txt",
            ClassicColors.DialogText) { Borders = TextBorders.None };
        _mutedFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.MenuText) { Borders = TextBorders.None };
        _detailFont = new GuiFont("font-small.txt",
            ClassicColors.MenuText) { Borders = TextBorders.None };
        _selectedFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.MenuTextHover) { Borders = TextBorders.None };
        _uiText = new TextHelper(app, "manualui");

        _exitButton = CreateButton(_uiText[6], Hide);
        Windows += _exitButton;
        _exitControlRenderer = new InputControlLabelRenderer(app, _exitButton.Font,
            brackets: false, glyphTint: ClassicColors.MenuText);
        PositionFooter();
    }

    int ContentHeight => System.Math.Max(TextLineHeight,
        Size.y - 24 - FooterHeight);
    int VisibleLineCount
    {
        get
        {
            int fontHeight = System.Math.Max(_titleFont.GetHeight(),
                _textFont.GetHeight());
            return System.Math.Max(1,
                1 + (ContentHeight - 2 - fontHeight) / TextLineHeight);
        }
    }
    public void CenterIn(Vector2 hostSize)
    {
        Size = new Vector2(300, hostSize.y);
        Position = new Vector2((hostSize.x - Size.x) / 2, 0);
        PositionFooter();
    }

    Button CreateButton(GuiString text, System.Action command) => new(app, command)
    {
        Text = text,
        Font = _mutedFont,
        HoverFont = _titleFont,
        IsTextOnly = true
    };

    void PositionFooter()
    {
        int y = Size.y - 17;
        // Include the inline key/controller glyph when centering the exit control.
        var control = InputControlDisplay.Resolve(app, app.LastInputMode,
            new InputPrompt(InputAction.Back, ""));
        int promptWidth = control.IsEmpty || app.LastInputMode == InputMode.Mouse ||
            (app is BurntimeClassic classic && !classic.ShowUIHints)
            ? 0 : _exitControlRenderer.Measure(control) + 2;
        _exitButton.Position = new Vector2((Size.x - _exitButton.Size.x - promptWidth) / 2, y);

    }

    public void Open()
    {
        _page = 0;
        System.Array.Fill(_textScroll, 0);
        Show();
    }

    public override void OnShow()
    {
        _touchScrollPixels = 0;
        _touchMomentum.Stop();
        _heldScrollUp = _heldScrollDown = false;
        _scrollDirection = 0;
        _scrollRepeatRemaining = ScrollRepeatDelay;
        if (_hasRenderMouseOverride)
            return;

        _restoreRenderMouse = app.RenderMouse;
        _hasRenderMouseOverride = true;
        app.RenderMouse = true;
    }

    public override void OnHide()
    {
        if (!_hasRenderMouseOverride)
            return;

        app.RenderMouse = _restoreRenderMouse;
        _hasRenderMouseOverride = false;
    }

    public override void OnRender(RenderTarget target)
    {
        PositionFooter();
        target.RenderRect(Vector2.Zero, Size, new PixelColor(176, 0, 0, 0),
            postFilter: true);
        for (int i = 0; i < PageCount; i++)
        {
            GuiFont font = i == _page ? _selectedFont :
                i == _hoveredPage ? _titleFont : _mutedFont;
            font.DrawText(target,
                new Vector2(TabsLeft + TabWidth * i + TabWidth / 2, 8),
                _setupNotes ? app.ResourceManager.GetString($"setupnotes?{i}") : _uiText[i],
                TextAlignment.Center, VerticalTextAlignment.Top);
        }

        RenderTextPage(target);
        if (app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad &&
            (app is not BurntimeClassic classic || classic.ShowUIHints))
        {
            // Keep the manual's shortcut color independent of the host scene's overlay palette.
            var control = InputControlDisplay.Resolve(app, app.LastInputMode,
                new InputPrompt(InputAction.Back, ""));
            _exitControlRenderer.Draw(target,
                _exitButton.Position + new Vector2(_exitButton.Size.x + 2, 0), control);
        }
    }

    void RenderTextPage(RenderTarget target)
    {
        ManualPageLayout layout = GetPageLayout(_page);
        int totalLineCount = layout.TotalLineCount;
        int maximum = System.Math.Max(0, totalLineCount - VisibleLineCount);
        _textScroll[_page] = System.Math.Clamp(_textScroll[_page], 0, maximum);

        RenderTarget content = target.GetSubBuffer(new Rect(10, 24,
            Size.x - 20, ContentHeight));
        int line = 0;
        foreach (ManualEntry entry in layout.Entries)
        {
            int entryStart = line;
            int entryEnd = entryStart + entry.LineCount;
            line = entryEnd;
            if (entryStart < _textScroll[_page] ||
                entryEnd > _textScroll[_page] + VisibleLineCount)
                continue;
            RenderEntry(content, entry,
                2 + (entryStart - _textScroll[_page]) * TextLineHeight);
        }
        RenderScrollBar(target, _textScroll[_page], maximum,
            VisibleLineCount, totalLineCount);
    }

    ManualPageLayout GetPageLayout(int page)
    {
        if (_pageLayouts[page] is ManualPageLayout cached)
            return cached;

        string[] lines = _setupNotes
            ? (page == 0 ? SetupPatchNotes.Read() :
                app.ResourceManager.GetStrings($"setupnotes?s{page}"))
            : app.ResourceManager.GetStrings($"manual?s{page}");
        lines = ManualTextLayout.Wrap(lines, Size.x - 30,
            _textFont.GetWidth, _titleFont.GetWidth);
        ManualEntry[] entries = BuildEntries(lines).ToArray();
        var layout = new ManualPageLayout(entries,
            entries.Sum(entry => entry.LineCount));
        _pageLayouts[page] = layout;
        return layout;
    }

    List<ManualEntry> BuildEntries(string[] lines)
    {
        var entries = new List<ManualEntry>();
        bool skipExtendedOnly = false;
        bool useExtendedRules = !_setupNotes && BurntimeClassic.Instance.Game.Rules == RuleSet.Extended;
        int lastContentLine = lines.Length - 1;
        while (lastContentLine >= 0 && lines[lastContentLine].Length == 0)
            lastContentLine--;

        foreach (string line in lines.Take(lastContentLine + 1))
        {
            if (line == "@extended-only-begin")
            {
                skipExtendedOnly = !useExtendedRules;
                continue;
            }
            if (line == "@extended-only-end")
            {
                skipExtendedOnly = false;
                continue;
            }
            if (skipExtendedOnly)
                continue;

            ManualEntry? entry = ParseEntry(line);
            if (entry != null)
            {
                if (_setupNotes && entry.Kind == ManualEntryKind.Heading)
                {
                    int previous = entries.Count - 1;
                    while (previous >= 0 && entries[previous].Kind == ManualEntryKind.Text &&
                        entries[previous].Payload.Length == 0)
                        previous--;
                    if (previous >= 0 && entries[previous].Kind == ManualEntryKind.Heading)
                        entries.RemoveRange(previous + 1, entries.Count - previous - 1);
                }
                entries.Add(entry);
            }
        }

        return entries;
    }

    ManualEntry? ParseEntry(string line)
    {
        if (line == "@tiers")
            return new(ManualEntryKind.Tiers, string.Empty, 1);
        if (line is "@mercenary" or "@technician" or "@doctor")
            return new(ManualEntryKind.ClassImage, line,
                SpriteLineCount(GetClassSpriteId(line)));
        if (line is "@flag" or "@city")
            return new(ManualEntryKind.Goal, line, GoalLineCount(line));
        if (line.StartsWith("@supply-example|"))
            return new(ManualEntryKind.SupplyExample, line[16..],
                ItemLineCount([line[16..]]));
        if (line.StartsWith("@entry|"))
        {
            string[] ids = line[7..].Split('|');
            if (!ids.Any(BurntimeClassic.Instance.Game.ItemTypes.Contains))
                return null;
            return new(ManualEntryKind.ContextItem, line[7..], ItemLineCount(ids));
        }
        if (line.StartsWith("@production|"))
        {
            string[] ids = line[12..].Split('|');
            return new(ManualEntryKind.Production, line[12..],
                ItemLineCount(ids.Take(1)));
        }
        if (line.StartsWith("@construction|"))
            return new(ManualEntryKind.Construction, line[14..],
                ConstructionLineCount(line[14..]));
        if (line.StartsWith("##"))
            return new(ManualEntryKind.Subheading, line[2..], 1);
        if (line.StartsWith("#"))
            return new(ManualEntryKind.Heading, line[1..], PixelLineCount((_setupNotes ? 1 : 2) * _titleFont.LineHeight));
        return new(ManualEntryKind.Text, line, 1);
    }

    int ItemLineCount(IEnumerable<string> ids)
    {
        ClassicGame game = BurntimeClassic.Instance.Game;
        int height = ids.Where(game.ItemTypes.Contains)
            .Select(id => game.ItemTypes[id].Sprite)
            .Where(sprite => !string.IsNullOrEmpty(sprite))
            .Select(sprite => app.ResourceManager.GetImage(sprite).Height)
            .DefaultIfEmpty(3 * TextLineHeight)
            .Max();
        return PixelLineCount(System.Math.Max(3 * TextLineHeight, height));
    }

    int GoalLineCount(string source)
    {
        if (source == "@city")
        {
            _goalCity ??= app.ResourceManager.GetImage("gfx/ui/manual_city.png");
            return PixelLineCount(_goalCity.Height);
        }

        _goalFlag ??= BurntimeClassic.Instance.Game.World.ActivePlayerObj.Flag.Object.Clone();
        int cropTop = System.Math.Max(0,
            (int)System.MathF.Round(_goalFlag.Height / 36f));
        int cropBottom = System.Math.Max(0,
            (int)System.MathF.Round(_goalFlag.Height * 8f / 36f));
        return PixelLineCount(_goalFlag.Height - cropTop - cropBottom);
    }

    int SpriteLineCount(string spriteId) => PixelLineCount(
        app.ResourceManager.GetImage(spriteId).Height);

    int ConstructionLineCount(string resultId)
    {
        ClassicGame game = BurntimeClassic.Instance.Game;
        Constructions.ConstructionInfo? recipe = game.Constructions.GetRecipes(game)
            .FirstOrDefault(candidate => candidate.Result == resultId);
        IEnumerable<string> ids = recipe == null
            ? [resultId]
            : recipe.Items.Append(resultId);
        return ItemLineCount(ids);
    }

    int PixelLineCount(int height) => System.Math.Max(1,
        (height + TextLineHeight - 1) / TextLineHeight);

    static string GetClassSpriteId(string source) => source switch
    {
        var value when value.StartsWith("@technician") => "syssze.raw?48",
        var value when value.StartsWith("@doctor") => "syssze.raw?16",
        _ => "syssze.raw?32"
    };

    void RenderEntry(RenderTarget target, ManualEntry entry, int y)
    {
        switch (entry.Kind)
        {
            case ManualEntryKind.Text:
                _textFont.DrawText(target, new Vector2(6, y), entry.Payload,
                    TextAlignment.Left, VerticalTextAlignment.Top);
                break;
            case ManualEntryKind.Heading:
                int headingOffset = (entry.LineCount * TextLineHeight - _titleFont.GetHeight()) / 2;
                _titleFont.DrawText(target, new Vector2(6, y + headingOffset),
                    entry.Payload, TextAlignment.Left, VerticalTextAlignment.Top);
                break;
            case ManualEntryKind.Subheading:
                _subheadingFont.DrawText(target, new Vector2(6, y), entry.Payload,
                    TextAlignment.Left, VerticalTextAlignment.Top);
                break;
            case ManualEntryKind.Tiers:
                RenderTierLine(target, y);
                break;
            case ManualEntryKind.ClassImage:
                RenderClassImage(target, entry.Payload, y);
                break;
            case ManualEntryKind.SupplyExample:
                RenderSupplyExample(target, entry.Payload, y);
                break;
            case ManualEntryKind.Goal:
                RenderGoalLine(target, entry.Payload, entry.LineCount, y);
                break;
            case ManualEntryKind.ContextItem:
                RenderContextItem(target, entry.Payload.Split('|'), y);
                break;
            case ManualEntryKind.Production:
                RenderProductionLine(target, entry.Payload.Split('|'), y);
                break;
            case ManualEntryKind.Construction:
                RenderConstructionLine(target, entry.Payload, y);
                break;
        }
    }

    void RenderClassImage(RenderTarget target, string source, int y)
    {
        string spriteId = GetClassSpriteId(source);
        ISprite sprite = app.ResourceManager.GetImage(spriteId);
        target.DrawSprite(new Vector2((Size.x - 20 - sprite.Width) / 2, y),
            sprite);
    }

    void RenderSupplyExample(RenderTarget target, string id, int y)
    {
        ClassicGame game = BurntimeClassic.Instance.Game;
        if (!game.ItemTypes.Contains(id))
            return;

        ItemType item = game.ItemTypes[id];
        string[] labels = app.ResourceManager.GetStrings(LabelsResource);
        string description;
        if (id == "item_gas_mask")
        {
            description = labels[10];
        }
        else
        {
            int value = item.WaterValue > 0 ? item.WaterValue : item.FoodValue;
            description = item.WaterValue > 0
                ? labels[11].Replace("|A", value.ToString())
                : labels[12].Replace("|A", value.ToString());
        }

        ISprite sprite = app.ResourceManager.GetImage(item.Sprite);
        int textWidth = System.Math.Max(_titleFont.GetWidth(item.Title),
            _textFont.GetWidth(description));
        int blockWidth = sprite.Width + 3 + textWidth;
        int x = (target.Size.x - blockWidth) / 2;

        target.DrawSprite(new Vector2(x, y), sprite);
        _titleFont.DrawText(target, new Vector2(x + sprite.Width + 3, y + 4), item.Title,
            TextAlignment.Left, VerticalTextAlignment.Top);
        _textFont.DrawText(target, new Vector2(x + sprite.Width + 3, y + 15), description,
            TextAlignment.Left, VerticalTextAlignment.Top);
    }

    void RenderGoalLine(RenderTarget target, string source, int lineCount, int y)
    {
        if (source == "@flag")
        {
            _goalFlag ??= BurntimeClassic.Instance.Game.World.ActivePlayerObj.Flag.Object.Clone();
            int cropLeft = System.Math.Max(0,
                (int)System.MathF.Round(_goalFlag.Width / 30f));
            int cropTop = System.Math.Max(0,
                (int)System.MathF.Round(_goalFlag.Height / 36f));
            int cropRight = System.Math.Max(0,
                (int)System.MathF.Round(_goalFlag.Width * 3f / 30f));
            int cropBottom = System.Math.Max(0,
                (int)System.MathF.Round(_goalFlag.Height * 8f / 36f));
            int width = _goalFlag.Width - cropLeft - cropRight;
            int height = _goalFlag.Height - cropTop - cropBottom;
            int x = (Size.x - 20 - width) / 2;
            int imageY = y + (lineCount * TextLineHeight - height) / 2;
            RenderTarget flagTarget = target.GetSubBuffer(
                new Rect(x, imageY, width, height));
            flagTarget.DrawSprite(new Vector2(-cropLeft, -cropTop),
                _goalFlag);
            return;
        }

        _goalCity ??= app.ResourceManager.GetImage("gfx/ui/manual_city.png");
        int cityX = (Size.x - 20 - _goalCity.Width) / 2;
        int cityY = y + (lineCount * TextLineHeight - _goalCity.Height) / 2;
        target.DrawSprite(new Vector2(cityX, cityY), _goalCity);
    }

    public override void OnUpdate(float elapsed)
    {
        if (IsVisible && _page == 0)
            _goalFlag?.Update(elapsed);

        ApplyTouchScroll(_touchMomentum.Update(elapsed));

        int direction = (_heldScrollDown ? 1 : 0) - (_heldScrollUp ? 1 : 0);
        _heldScrollUp = _heldScrollDown = false;
        if (direction == 0 || direction != _scrollDirection)
            _scrollRepeatRemaining = ScrollRepeatDelay;
        _scrollDirection = direction;
        if (direction == 0)
            return;

        _scrollRepeatRemaining -= elapsed;
        while (_scrollRepeatRemaining <= 0)
        {
            MoveTextScroll(direction);
            _scrollRepeatRemaining += ScrollRepeatInterval;
        }
    }

    public override bool OnHeldInputAction(InputAction action, float elapsed)
    {
        if (action.IsUp())
            _heldScrollUp = true;
        else if (action.IsDown())
            _heldScrollDown = true;
        else
            return false;
        return true;
    }

    void RenderTierLine(RenderTarget target, int y)
    {
        int width = BurntimeClassic.Instance.Game.RuleBook.Settings.CombatTierWidth;
        for (int tier = 0; tier < 4; tier++)
        {
            int x = 6 + tier * 68;
            string range = tier == 3 ? $"{tier * width}+" :
                $"{tier * width}-{(tier + 1) * width - 1}";
            _textFont.DrawText(target, new Vector2(x, y), $"~d{tier + 1} {range}",
                TextAlignment.Left, VerticalTextAlignment.Top);
        }
    }

    void RenderContextItem(RenderTarget target, string[] ids, int y)
    {
        ClassicGame game = BurntimeClassic.Instance.Game;
        if (ids.Length > 1)
        {
            for (int column = 0; column < ids.Length && column < 2; column++)
            {
                if (game.ItemTypes.Contains(ids[column]))
                    RenderCompactContextItem(target, game.ItemTypes[ids[column]],
                        new Vector2(column * 139, y), 139);
            }
            return;
        }

        string id = ids[0];
        if (!game.ItemTypes.Contains(id))
            return;
        RenderCompactContextItem(target, game.ItemTypes[id],
            new Vector2(0, y), target.Size.x);
    }

    void RenderCompactContextItem(RenderTarget target, ItemType item,
        Vector2 position, int availableWidth)
    {
        ClassicGame game = BurntimeClassic.Instance.Game;
        Constructions.ConstructionInfo? recipe = game.Constructions.GetRecipes(game)
            .FirstOrDefault(candidate => candidate.Result == item.ID);
        (string statistic, string detail) = GetContextItemText(item, recipe);
        bool normalDetail = item.DamageValues.Length > 0 ||
            item.ID is "item_hand_pump" or "item_industrial_pump";
        GuiFont detailFont = normalDetail ? _textFont : _detailFont;
        ISprite sprite = app.ResourceManager.GetImage(item.Sprite);
        string statisticText = item.DamageValues.Length > 0
            ? $"{statistic} ~d1"
            : statistic;
        string detailText = item.DamageValues.Length > 0
            ? $"{detail} ~d4"
            : detail;
        int textWidth = System.Math.Max(_titleFont.GetWidth(item.Title),
            System.Math.Max(_textFont.GetWidth(statisticText),
                detailFont.GetWidth(detailText)));
        int blockWidth = sprite.Width + 3 + textWidth;
        position.x += System.Math.Max(0, (availableWidth - blockWidth) / 2);

        target.DrawSprite(position, sprite);

        Vector2 textPosition = new(position.x + sprite.Width + 3, position.y + 1);
        int availableTextWidth = availableWidth - sprite.Width - 3;
        string fittedStatistic = item.DamageValues.Length > 0
            ? FitText(_textFont, statistic,
                availableTextWidth - _textFont.GetWidth(" ~d1")) + " ~d1"
            : FitText(_textFont, statistic, availableTextWidth);
        string fittedDetail = item.DamageValues.Length > 0
            ? FitText(detailFont, detail,
                availableTextWidth - detailFont.GetWidth(" ~d4")) + " ~d4"
            : FitText(detailFont, detail, availableTextWidth);
        _titleFont.DrawText(target, textPosition,
            FitText(_titleFont, item.Title, availableTextWidth), TextAlignment.Left,
            VerticalTextAlignment.Top);

        _textFont.DrawText(target,
            new Vector2(textPosition.x, textPosition.y + _titleFont.LineHeight),
            fittedStatistic,
            TextAlignment.Left, VerticalTextAlignment.Top);

        detailFont.DrawText(target,
            new Vector2(textPosition.x, textPosition.y + _titleFont.LineHeight + _textFont.LineHeight),
            fittedDetail,
            TextAlignment.Left, VerticalTextAlignment.Top);
    }

    void RenderProductionLine(RenderTarget target, string[] ids, int y)
    {
        if (ids.Length < 2)
            return;

        ClassicGame game = BurntimeClassic.Instance.Game;
        if (!game.ItemTypes.Contains(ids[0]) ||
            !game.ItemTypes.Contains(ids[1]))
            return;

        ItemType tool = game.ItemTypes[ids[0]];
        if (tool.Production is null)
            return;

        int one = tool.Production.GetRate(1, 1).FoodPerDay;
        int maximum = tool.Production.GetRate(
            tool.Production.MaxToolCount, 1).FoodPerDay;
        string[] labels = app.ResourceManager.GetStrings(LabelsResource);
        string firstLine = labels[13].Replace("|A", one.ToString());
        string secondLine = labels[14]
            .Replace("|A", maximum.ToString())
            .Replace("|B", tool.Production.MaxToolCount.ToString());
        ISprite toolSprite = app.ResourceManager.GetImage(tool.Sprite);
        int textWidth = System.Math.Max(_titleFont.GetWidth(tool.Title),
            System.Math.Max(_textFont.GetWidth(firstLine),
                _textFont.GetWidth(secondLine)));
        int blockWidth = toolSprite.Width + 3 + textWidth;
        int x = (target.Size.x - blockWidth) / 2;
        target.DrawSprite(new Vector2(x, y), toolSprite);
        int textX = x + toolSprite.Width + 3;
        _titleFont.DrawText(target, new Vector2(textX, y + 1), tool.Title,
            TextAlignment.Left, VerticalTextAlignment.Top);
        _textFont.DrawText(target, new Vector2(textX, y + 12), firstLine,
            TextAlignment.Left, VerticalTextAlignment.Top);
        _textFont.DrawText(target, new Vector2(textX, y + 23), secondLine,
            TextAlignment.Left, VerticalTextAlignment.Top);
    }

    void RenderConstructionLine(RenderTarget target, string resultId, int y)
    {
        ClassicGame game = BurntimeClassic.Instance.Game;
        if (!game.ItemTypes.Contains(resultId))
            return;

        Constructions.ConstructionInfo? recipe = game.Constructions.GetRecipes(game)
            .FirstOrDefault(candidate => candidate.Result == resultId);
        if (recipe is null)
            return;

        List<ISprite> materials = recipe.Items
            .Where(game.ItemTypes.Contains)
            .Select(id => app.ResourceManager.GetImage(game.ItemTypes[id].Sprite))
            .ToList();
        ISprite result = app.ResourceManager.GetImage(game.ItemTypes[resultId].Sprite);
        int colonWidth = _textFont.GetWidth(":");
        int plusWidth = _textFont.GetWidth("+");
        int width = result.Width + 4 + colonWidth + 4 +
            materials.Sum(sprite => sprite.Width) +
            System.Math.Max(0, materials.Count - 1) * (plusWidth + 8);
        int x = (target.Size.x - width) / 2;
        target.DrawSprite(new Vector2(x, y), result);
        x += result.Width + 4;
        _textFont.DrawText(target, new Vector2(x, y + 11), ":",
            TextAlignment.Left, VerticalTextAlignment.Top);
        x += colonWidth + 4;
        for (int i = 0; i < materials.Count; i++)
        {
            if (i > 0)
            {
                _textFont.DrawText(target, new Vector2(x + 4, y + 11), "+",
                    TextAlignment.Left, VerticalTextAlignment.Top);
                x += plusWidth + 8;
            }
            target.DrawSprite(new Vector2(x, y), materials[i]);
            x += materials[i].Width;
        }
    }

    (string Statistic, string Detail) GetContextItemText(ItemType item,
        Constructions.ConstructionInfo? recipe)
    {
        string[] labels = app.ResourceManager.GetStrings(LabelsResource);
        if (item.DefenseValue > 0)
            return (labels[15].Replace("|A", item.DefenseValue.ToString()),
                string.Empty);
        if (item.ID is "item_hand_pump" or "item_industrial_pump")
        {
            const int sourceOutput = 2;
            int output = BurntimeClassic.Instance.Game.RuleBook.CalculateWaterOutput(
                sourceOutput, item.ID == "item_hand_pump",
                item.ID == "item_industrial_pump");
            return (labels[16].Replace("|A", output.ToString()),
                labels[17].Replace("|B", sourceOutput.ToString()));
        }
        if (item.DamageValues.Length == 0)
            return (GetEquipmentStatistic(item),
                GetMaterialLine(recipe));

        ClassicGame game = BurntimeClassic.Instance.Game;
        int tierWidth = game.RuleBook.Settings.CombatTierWidth;
        CombatPreview first = RuleFormulas.OriginalCombatPreview(
            item.DamageValues, 0, tierWidth);
        CombatPreview last = RuleFormulas.OriginalCombatPreview(
            item.DamageValues, tierWidth * 3, tierWidth);
        return (labels[18]
                .Replace("|A", first.Minimum.ToString())
                .Replace("|B", first.Maximum.ToString()),
            labels[19]
                .Replace("|A", last.Minimum.ToString())
                .Replace("|B", last.Maximum.ToString()));
    }

    void RenderScrollBar(RenderTarget target, int offset, int maximum,
        int visibleLines, int totalLines)
    {
        if (maximum <= 0)
            return;
        int trackHeight = Size.y - FooterHeight - 28;
        int thumbHeight = System.Math.Max(10,
            trackHeight * visibleLines / totalLines);
        int thumbY = 26 + (trackHeight - thumbHeight) * offset / maximum;
        target.RenderRect(new Vector2(290, 26), new Vector2(2, trackHeight),
            new PixelColor(80, 108, 116, 168));
        target.RenderRect(new Vector2(290, thumbY), new Vector2(2, thumbHeight),
            new PixelColor(220, 240, 164, 56));
    }

    string GetMaterialLine(Constructions.ConstructionInfo? recipe)
    {
        string[] text = app.ResourceManager.GetStrings(LabelsResource);
        if (recipe is null)
            return string.Empty;

        ClassicGame game = BurntimeClassic.Instance.Game;
        string materials = string.Join(" + ", recipe.Items.Select(id =>
            game.ItemTypes[id].Title));
        bool anyCharacter = recipe.Classes[(int)CharClass.Boss] &&
            recipe.Classes[(int)CharClass.Mercenary] &&
            recipe.Classes[(int)CharClass.Technician] &&
            recipe.Classes[(int)CharClass.Doctor];
        return anyCharacter ? text[6] : text[4].Replace("|M", materials);
    }

    string FitText(GuiFont font, string text, int width)
    {
        if (font.GetWidth(text) <= width)
            return text;
        while (text.Length > 0 && font.GetWidth(text + "...") > width)
            text = text[..^1];
        return text.TrimEnd() + "...";
    }

    string GetEquipmentStatistic(ItemType item)
    {
        ClassicGame game = BurntimeClassic.Instance.Game;
        string[] labels = app.ResourceManager.GetStrings(LabelsResource);
        if (item.DamageValues.Length > 0)
            return string.Empty;

        if (item.Production is not null)
        {
            int one = item.Production.GetRate(1, 1).FoodPerDay;
            int maximum = item.Production.GetRate(item.Production.MaxToolCount, 1).FoodPerDay;
            return labels[1]
                .Replace("|A", one.ToString())
                .Replace("|B", maximum.ToString());
        }

        if (item.ID is "item_hand_pump" or "item_industrial_pump")
        {
            const int assumedSource = 2;
            int output = game.RuleBook.CalculateWaterOutput(assumedSource,
                item.ID == "item_hand_pump", item.ID == "item_industrial_pump");
            return labels[2].Replace("|B", output.ToString());
        }

        if (item.WaterValue > 0)
            return labels[8].Replace("|A", item.WaterValue.ToString());

        if (item.DefenseValue > 0)
            return labels[9].Replace("|A", item.DefenseValue.ToString());

        if (item.Protection.Length > 0)
        {
            int gas = (int)((item.GetProtection("gas")?.Rate ?? 0) * 100);
            int radiation = (int)((item.GetProtection("radiation")?.Rate ?? 0) * 100);
            return labels[3]
                .Replace("|A", gas.ToString())
                .Replace("|B", radiation.ToString());
        }

        return string.Empty;
    }

    public override bool OnInputAction(InputAction action)
    {
        if (action == InputAction.Back)
        {
            Hide();
            return true;
        }

        if (action.IsUp() || action.IsDown())
        {
            int direction = action.IsUp() ? -1 : 1;
            MoveTextScroll(direction);
            return true;
        }

        if (action.IsLeft() || action == InputAction.LeftArea)
        {
            MovePage(-1);
            return true;
        }

        if (action.IsRight() || action == InputAction.RightArea ||
            action == InputAction.Primary)
        {
            MovePage(1);
            return true;
        }

        return false;
    }

    public override bool OnMouseClick(Vector2 position, MouseButton button)
    {
        if (button != MouseButton.Left)
            return true;

        if (_exitButton.Boundings.PointInside(position))
            return true;

        if (position.y >= 2 && position.y < 22)
        {
            int tab = GetTabAt(position);
            if (tab >= 0)
                _page = tab;
        }

        return true;
    }

    public override bool OnMouseMove(Vector2 position)
    {
        _hoveredPage = GetTabAt(position);
        return true;
    }

    public override void OnMouseLeave()
    {
        _hoveredPage = -1;
    }

    public override bool OnMouseWheel(Vector2 position, int delta)
    {
        MoveTextScroll(delta > 0 ? -1 : 1);
        return true;
    }

    public override bool OnTouchScroll(Vector2 position, Vector2 delta)
    {
        if (!new Rect(10, 24, Size.x - 20, ContentHeight).PointInside(position))
            return false;
        _touchMomentum.Stop();
        ApplyTouchScroll(-delta.y);
        return true;
    }

    public override void OnTouchScrollEnd(Vector2 position, Vector2f velocity) =>
        _touchMomentum.Release(-velocity.y);

    public override void OnTouchPress(Vector2 position)
    {
        _touchMomentum.Stop();
    }

    void ApplyTouchScroll(float pixels)
    {
        _touchScrollPixels += pixels;
        int lineHeight = System.Math.Max(1, TextLineHeight);
        int lines = (int)(_touchScrollPixels / lineHeight);
        if (lines == 0)
            return;

        _touchScrollPixels -= lines * lineHeight;
        if (!MoveTextScroll(lines))
        {
            _touchScrollPixels = 0;
            _touchMomentum.Stop();
        }
    }

    void MovePage(int direction)
    {
        _touchMomentum.Stop();
        _touchScrollPixels = 0;
        _page = (_page + direction + PageCount) % PageCount;
    }

    int GetTabAt(Vector2 position)
    {
        if (position.y < 2 || position.y >= 22 || position.x < TabsLeft ||
            position.x >= TabsLeft + TabWidth * PageCount)
            return -1;
        return (position.x - TabsLeft) / TabWidth;
    }

    int MaximumTextScroll()
        => System.Math.Max(0,
            GetPageLayout(_page).TotalLineCount - VisibleLineCount);

    bool MoveTextScroll(int direction)
    {
        int newScroll = System.Math.Clamp(_textScroll[_page] + direction,
            0, MaximumTextScroll());
        if (newScroll == _textScroll[_page])
            return false;
        _textScroll[_page] = newScroll;
        return true;
    }
}
