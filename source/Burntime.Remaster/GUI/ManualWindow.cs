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
    const int PageCount = 5;
    const int TabWidth = 56;
    const int TabsLeft = 10;
    const int TextLinesPerChunk = 4;
    const int TextLineHeight = 11;
    const int ChunkHeight = TextLinesPerChunk * TextLineHeight;
    const int FooterHeight = 30;
    const string ImageContinuation = "@image-continuation";
    const string LabelsResource = "manual?s5";

    readonly GuiFont _titleFont;
    readonly GuiFont _textFont;
    readonly GuiFont _mutedFont;
    readonly GuiFont _selectedFont;
    readonly TextHelper _uiText;
    readonly Button _exitButton;
    readonly Button _nextButton;
    InputPromptHandle? _exitPrompt;
    InputPromptHandle? _nextPagePrompt;
    ISprite? _goalFlag;
    ISprite? _goalCity;
    bool _restoreRenderMouse;
    bool _hasRenderMouseOverride;
    int _page;
    int _hoveredPage = -1;
    readonly int[] _textChunkScroll = new int[PageCount];
    Vector2 _mousePosition = new(-1, -1);

    public ManualWindow(Module app, Vector2 hostSize)
        : base(app)
    {
        Size = new Vector2(300, hostSize.y);
        Position = new Vector2((hostSize.x - Size.x) / 2, 0);
        // Map HUD elements reach layer 60. Keep the complete modal above them.
        Layer = 100;
        IsModal = true;
        HasFocus = true;
        CaptureAllMouseClicks = true;

        _titleFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.DialogText) { Borders = TextBorders.None };
        _textFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.LightGray) { Borders = TextBorders.None };
        _mutedFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.MenuText) { Borders = TextBorders.None };
        _selectedFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.MenuTextHover) { Borders = TextBorders.None };
        _uiText = new TextHelper(app, "manualui");

        _exitButton = CreateButton(_uiText[6], ExitFromButton);
        Windows += _exitButton;
        _nextButton = CreateButton(_uiText[7], () => MovePage(1));
        Windows += _nextButton;

        _exitPrompt = Prompts.Add(new InputPrompt(InputAction.Back, "")
        {
            MouseControl = MouseButton.Right
        },
            Vector2.Zero, showBackground: false, horizontalPadding: 0);
        _nextPagePrompt = Prompts.Add(new InputPrompt(InputAction.Primary, ""),
            Vector2.Zero, showBackground: false, horizontalPadding: 0);
        PositionFooter();
    }

    int ContentHeight => System.Math.Max(ChunkHeight,
        Size.y - 24 - FooterHeight);
    int FullyVisibleChunkCount => System.Math.Max(1, ContentHeight / ChunkHeight);
    int RenderedChunkCount => System.Math.Max(1,
        (ContentHeight + ChunkHeight - 1) / ChunkHeight);

    public void CenterIn(Vector2 hostSize)
    {
        Size = new Vector2(300, hostSize.y);
        Position = new Vector2((hostSize.x - Size.x) / 2, 0);
        PositionFooter();
    }

    Button CreateButton(GuiString text, System.Action command) => new(app, command)
    {
        Text = text,
        Font = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudText),
        HoverFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.HudTextHover),
        IsTextOnly = true
    };

    void ExitFromButton()
    {
        app.Engine.CenterMouse();
        Hide();
    }

    void PositionFooter()
    {
        const int horizontalMargin = 25;
        int y = Size.y - 17;
        _exitButton.Position = new Vector2(horizontalMargin, y);
        _nextButton.Position = new Vector2(
            Size.x - horizontalMargin - _nextButton.Size.x, y);
        _exitPrompt?.UpdatePosition(_exitButton.Position +
            new Vector2(_exitButton.Size.x + 2, -2));
        _nextPagePrompt?.UpdatePosition(_nextButton.Position +
            new Vector2(_nextButton.Size.x + 2, -2));
    }

    public void Open()
    {
        _page = 0;
        System.Array.Fill(_textChunkScroll, 0);
        Show();
    }

    public override void OnShow()
    {
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
        target.RenderRect(Vector2.Zero, Size, new PixelColor(176, 0, 0, 0),
            postFilter: true);
        for (int i = 0; i < PageCount; i++)
        {
            GuiFont font = i == _page ? _selectedFont :
                i == _hoveredPage ? _titleFont : _mutedFont;
            font.DrawText(target,
                new Vector2(TabsLeft + TabWidth * i + TabWidth / 2, 8),
                _uiText[i], TextAlignment.Center, VerticalTextAlignment.Top);
        }

        RenderTextPage(target);

        // _mutedFont.DrawText(target, new Vector2(Size.x / 2, Size.y - 10),
        //     _uiText[3],
        //     TextAlignment.Center, VerticalTextAlignment.Top);
    }

    void RenderTextPage(RenderTarget target)
    {
        string[] lines = app.ResourceManager.GetStrings($"manual?s{_page}");
        List<List<string>> chunks = BuildTextChunks(lines);
        int maximum = System.Math.Max(0, chunks.Count - FullyVisibleChunkCount);
        _textChunkScroll[_page] = System.Math.Clamp(_textChunkScroll[_page], 0, maximum);

        RenderTarget content = target.GetSubBuffer(new Rect(10, 24,
            Size.x - 20, ContentHeight));
        for (int row = 0; row < RenderedChunkCount; row++)
        {
            int index = _textChunkScroll[_page] + row;
            if (index >= chunks.Count)
                break;
            bool renderSpanningImage = row + 1 < RenderedChunkCount &&
                index + 1 < chunks.Count;
            RenderTextChunk(content, chunks[index], row * ChunkHeight + 2,
                renderSpanningImage);
        }
        RenderScrollBar(target, _textChunkScroll[_page], maximum,
            ContentHeight, chunks.Count * ChunkHeight);
    }

    List<List<string>> BuildTextChunks(string[] lines)
    {
        var chunks = new List<List<string>>();
        var current = new List<string>();
        int currentLineCount = 0;
        void Flush()
        {
            if (current.Count == 0)
                return;
            chunks.Add(current);
            current = new List<string>();
            currentLineCount = 0;
        }

        void AddImage(string line, int lineCount)
        {
            int remainingLines = TextLinesPerChunk - currentLineCount;
            if (currentLineCount > 0 && lineCount > remainingLines)
            {
                current.Add(line);
                chunks.Add(current);
                current = [];
                currentLineCount = lineCount - remainingLines;
                for (int i = 0; i < currentLineCount; i++)
                    current.Add(ImageContinuation);
                return;
            }

            current.Add(line);
            currentLineCount += lineCount;
            if (currentLineCount == TextLinesPerChunk)
                Flush();
        }

        bool skipExtendedOnly = false;
        bool useExtendedRules = BurntimeClassic.Instance.Game.Rules == RuleSet.Extended;
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

            bool isClassChapter = line is "@mercenary" or "@technician" or "@doctor";
            if (isClassChapter)
            {
                AddImage(line, 2);
                continue;
            }

            bool isGoalImage = line == "@flag" || line == "@city";
            if (isGoalImage)
            {
                AddImage(line, 2);
                continue;
            }

            if (line.StartsWith("@supply-example|"))
            {
                AddImage(line, 3);
                continue;
            }

            if (line.StartsWith("@entry|"))
            {
                string[] ids = line[7..].Split('|');
                if (ids.Any(BurntimeClassic.Instance.Game.ItemTypes.Contains))
                    AddImage(line, 3);
                continue;
            }

            if (line.StartsWith("@production|"))
            {
                AddImage(line, 3);
                continue;
            }

            if (line.StartsWith("@construction|"))
            {
                AddImage(line, 3);
                continue;
            }

            bool isClass = line.StartsWith("@mercenary|") ||
                line.StartsWith("@technician|") || line.StartsWith("@doctor|");
            if (isClass)
            {
                if (currentLineCount + 2 > TextLinesPerChunk)
                    Flush();
                current.Add(line);
                currentLineCount += 2;
                if (currentLineCount == TextLinesPerChunk)
                    Flush();
                continue;
            }
            if (line.StartsWith("@foods|"))
            {
                Flush();
                chunks.Add(new List<string> { line });
                continue;
            }
            int lineCount = line.StartsWith("#") ? 2 : 1;
            if (currentLineCount + lineCount > TextLinesPerChunk)
                Flush();
            current.Add(line);
            currentLineCount += lineCount;
            if (currentLineCount == TextLinesPerChunk)
                Flush();
        }

        Flush();
        return chunks;
    }

    void RenderTextChunk(RenderTarget target, List<string> chunk, int y,
        bool renderSpanningImage)
    {
        if (chunk.Count == 1 && TryRenderClassImage(target, chunk[0], y))
            return;
        if (chunk.Count == 1 && TryRenderSupplyExample(target, chunk[0], y))
            return;
        if (chunk.Count == 1 && TryRenderGoalLine(target, chunk[0], y))
            return;
        if (chunk.Count == 1 && TryRenderFoodLine(target, chunk[0], y))
            return;
        if (chunk.Count == 1 && TryRenderTierLine(target, chunk[0], y))
            return;
        if (chunk.Count == 1 && TryRenderContextItem(target, chunk[0], y))
            return;
        if (chunk.Count == 1 && TryRenderProductionLine(target, chunk[0], y))
            return;
        int lineOffset = 0;
        foreach (string entry in chunk)
        {
            if (TryRenderTierLine(target, entry,
                y + lineOffset * TextLineHeight))
            {
                lineOffset++;
                continue;
            }

            if (entry == ImageContinuation)
            {
                lineOffset++;
                continue;
            }

            int imageLineCount = GetImageLineCount(entry);
            if (imageLineCount > 0 &&
                lineOffset + imageLineCount > TextLinesPerChunk &&
                !renderSpanningImage)
            {
                lineOffset += imageLineCount;
                continue;
            }

            if (TryRenderSupplyExample(target, entry,
                y + lineOffset * TextLineHeight))
            {
                lineOffset += imageLineCount;
                continue;
            }

            if (TryRenderContextItem(target, entry,
                y + lineOffset * TextLineHeight))
            {
                lineOffset += imageLineCount;
                continue;
            }

            if (TryRenderProductionLine(target, entry,
                y + lineOffset * TextLineHeight))
            {
                lineOffset += imageLineCount;
                continue;
            }

            if (TryRenderConstructionLine(target, entry,
                y + lineOffset * TextLineHeight))
            {
                lineOffset += imageLineCount;
                continue;
            }

            if (TryRenderClassImage(target, entry,
                y + lineOffset * TextLineHeight))
            {
                lineOffset += 2;
                continue;
            }

            if (TryRenderGoalLine(target, entry,
                y + lineOffset * TextLineHeight))
            {
                lineOffset += 2;
                continue;
            }

            if (TryRenderClassLine(target, entry,
                y + lineOffset * TextLineHeight))
            {
                lineOffset += 2;
                continue;
            }

            bool header = entry.StartsWith("#");
            string line = header ? entry[1..] : entry;
            if (header)
            {
                int headingHeight = 2 * TextLineHeight;
                int headingOffset = (headingHeight - _titleFont.GetHeight()) / 2;
                _titleFont.DrawText(target,
                    new Vector2(6, y + lineOffset * TextLineHeight + headingOffset),
                    line, TextAlignment.Left, VerticalTextAlignment.Top);
                lineOffset += 2;
            }
            else
            {
                _textFont.DrawText(target,
                    new Vector2(6, y + lineOffset * TextLineHeight), line,
                    TextAlignment.Left, VerticalTextAlignment.Top);
                lineOffset++;
            }
        }
    }

    static int GetImageLineCount(string line)
    {
        if (line.StartsWith("@supply-example|") || line.StartsWith("@entry|") ||
            line.StartsWith("@production|") || line.StartsWith("@construction|"))
            return 3;
        return line is "@flag" or "@city" or "@mercenary" or "@technician" or
            "@doctor" ? 2 : 0;
    }

    bool TryRenderClassImage(RenderTarget target, string line, int y)
    {
        string spriteId = line switch
        {
            "@technician" => "syssze.raw?48",
            "@doctor" => "syssze.raw?16",
            "@mercenary" => "syssze.raw?32",
            _ => string.Empty
        };
        if (spriteId.Length == 0)
            return false;

        ISprite sprite = app.ResourceManager.GetImage(spriteId);
        target.DrawSprite(new Vector2((Size.x - 20 - sprite.Width) / 2, y),
            sprite);
        return true;
    }

    bool TryRenderSupplyExample(RenderTarget target, string line, int y)
    {
        const string prefix = "@supply-example|";
        if (!line.StartsWith(prefix))
            return false;

        string id = line[prefix.Length..];
        ClassicGame game = BurntimeClassic.Instance.Game;
        if (!game.ItemTypes.Contains(id))
            return true;

        ItemType item = game.ItemTypes[id];
        string description;
        if (id == "item_gas_mask")
        {
            description = "Provides gas protection.";
        }
        else
        {
            int value = item.WaterValue > 0 ? item.WaterValue : item.FoodValue;
            description = item.WaterValue > 0
                ? $"Carries {value} daily portions."
                : $"Provides {value} daily portions.";
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
        return true;
    }

    bool TryRenderGoalLine(RenderTarget target, string line, int y)
    {
        if (line == "@flag")
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
            int imageY = y + (2 * TextLineHeight - height) / 2;
            RenderTarget flagTarget = target.GetSubBuffer(
                new Rect(x, imageY, width, height));
            flagTarget.DrawSprite(new Vector2(-cropLeft, -cropTop),
                _goalFlag);
            return true;
        }

        if (line != "@city")
            return false;

        _goalCity ??= app.ResourceManager.GetImage("gfx/ui/manual_city.png");
        int cityX = (Size.x - 20 - _goalCity.Width) / 2;
        int cityY = y + (2 * TextLineHeight - _goalCity.Height) / 2;
        target.DrawSprite(new Vector2(cityX, cityY), _goalCity);
        return true;
    }

    public override void OnUpdate(float elapsed)
    {
        if (IsVisible && _page == 0)
            _goalFlag?.Update(elapsed);
    }

    bool TryRenderFoodLine(RenderTarget target, string line, int y)
    {
        const string prefix = "@foods|";
        if (!line.StartsWith(prefix))
            return false;

        ClassicGame game = BurntimeClassic.Instance.Game;
        string[] ids = line[prefix.Length..].Split('|');
        for (int column = 0; column < ids.Length && column < 2; column++)
        {
            if (!game.ItemTypes.Contains(ids[column]))
                continue;
            ItemType item = game.ItemTypes[ids[column]];
            int x = 6 + column * 139;
            target.DrawSprite(new Vector2(x, y), app.ResourceManager.GetImage(item.Sprite));
            _titleFont.DrawText(target, new Vector2(x + 35, y + 3), item.Title,
                TextAlignment.Left, VerticalTextAlignment.Top);
            string[] labels = app.ResourceManager.GetStrings(LabelsResource);
            _textFont.DrawText(target, new Vector2(x + 35, y + 13),
                labels[7].Replace("|A", item.FoodValue.ToString()),
                TextAlignment.Left, VerticalTextAlignment.Top);
        }
        return true;
    }

    bool TryRenderTierLine(RenderTarget target, string line, int y)
    {
        if (line != "@tiers")
            return false;
        int width = BurntimeClassic.Instance.Game.RuleBook.Settings.CombatTierWidth;
        for (int tier = 0; tier < 4; tier++)
        {
            int x = 6 + tier * 68;
            target.DrawSprite(new Vector2(x, y - 2), GetTierSprite(tier + 1));
            string range = tier == 3 ? $"{tier * width}+" :
                $"{tier * width}-{(tier + 1) * width - 1}";
            _textFont.DrawText(target, new Vector2(x + 10, y), range,
                TextAlignment.Left, VerticalTextAlignment.Top);
        }
        return true;
    }

    ISprite GetTierSprite(int tier) => app.ResourceManager.GetImage(
        $"pngsheet@gfx/ui/info_counter.png?{30 + tier}?8x12");

    bool TryRenderContextItem(RenderTarget target, string line, int y)
    {
        const string prefix = "@entry|";
        if (!line.StartsWith(prefix))
            return false;

        string[] ids = line[prefix.Length..].Split('|');
        ClassicGame game = BurntimeClassic.Instance.Game;
        if (ids.Length > 1)
        {
            for (int column = 0; column < ids.Length && column < 2; column++)
            {
                if (game.ItemTypes.Contains(ids[column]))
                    RenderCompactContextItem(target, game.ItemTypes[ids[column]],
                        new Vector2(column * 139, y), 139);
            }
            return true;
        }

        string id = ids[0];
        if (!game.ItemTypes.Contains(id))
            return true;
        RenderCompactContextItem(target, game.ItemTypes[id],
            new Vector2(0, y), target.Size.x);
        return true;
    }

    void RenderCompactContextItem(RenderTarget target, ItemType item,
        Vector2 position, int availableWidth)
    {
        ClassicGame game = BurntimeClassic.Instance.Game;
        Constructions.ConstructionInfo? recipe = game.Constructions.Recipes
            .FirstOrDefault(candidate => candidate.Result == item.ID);
        (string statistic, string detail) = GetContextItemText(item, recipe);
        bool normalDetail = item.DamageValues.Length > 0 ||
            item.ID is "item_hand_pump" or "item_industrial_pump";
        GuiFont detailFont = normalDetail ? _textFont : _mutedFont;
        ISprite sprite = app.ResourceManager.GetImage(item.Sprite);
        int tierMarkerWidth = item.DamageValues.Length > 0
            ? GetTierSprite(1).Width + 2
            : 0;
        int textWidth = System.Math.Max(_titleFont.GetWidth(item.Title),
            System.Math.Max(_textFont.GetWidth(statistic) + tierMarkerWidth,
                detailFont.GetWidth(detail) + tierMarkerWidth));
        int blockWidth = sprite.Width + 3 + textWidth;
        position.x += System.Math.Max(0, (availableWidth - blockWidth) / 2);

        string? ignored = null;
        DrawItem(target, item, position, ref ignored);

        Vector2 textPosition = new(position.x + sprite.Width + 3, position.y + 1);
        _titleFont.DrawText(target, textPosition,
            FitText(_titleFont, item.Title, availableWidth - sprite.Width - 3), TextAlignment.Left,
            VerticalTextAlignment.Top);

        _textFont.DrawText(target,
            new Vector2(textPosition.x, textPosition.y + 11),
            FitText(_textFont, statistic, availableWidth - sprite.Width - 3),
            TextAlignment.Left, VerticalTextAlignment.Top);

        detailFont.DrawText(target,
            new Vector2(textPosition.x, textPosition.y + 22),
            FitText(detailFont, detail, availableWidth - sprite.Width - 3),
            TextAlignment.Left, VerticalTextAlignment.Top);

        if (item.DamageValues.Length > 0)
        {
            int markerX = textPosition.x + _textFont.GetWidth(statistic) + 2;
            target.DrawSprite(new Vector2(markerX, textPosition.y + 9),
                GetTierSprite(1));
            markerX = textPosition.x + detailFont.GetWidth(detail) + 2;
            target.DrawSprite(new Vector2(markerX, textPosition.y + 20),
                GetTierSprite(4));
        }
    }

    bool TryRenderProductionLine(RenderTarget target, string line, int y)
    {
        const string prefix = "@production|";
        if (!line.StartsWith(prefix))
            return false;
        string[] ids = line[prefix.Length..].Split('|');
        if (ids.Length < 2)
            return true;

        ClassicGame game = BurntimeClassic.Instance.Game;
        if (!game.ItemTypes.Contains(ids[0]) ||
            !game.ItemTypes.Contains(ids[1]))
            return true;

        ItemType tool = game.ItemTypes[ids[0]];
        if (tool.Production is null)
            return true;

        int one = tool.Production.GetRate(1, 1).FoodPerDay;
        int maximum = tool.Production.GetRate(
            tool.Production.MaxToolCount, 1).FoodPerDay;
        string firstLine = $"{one} food per day with 1 trap";
        string secondLine = $"{maximum} food per day with " +
            $"{tool.Production.MaxToolCount} traps";
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
        return true;
    }

    bool TryRenderConstructionLine(RenderTarget target, string line, int y)
    {
        const string prefix = "@construction|";
        if (!line.StartsWith(prefix))
            return false;

        string resultId = line[prefix.Length..];
        ClassicGame game = BurntimeClassic.Instance.Game;
        if (!game.ItemTypes.Contains(resultId))
            return true;

        Constructions.ConstructionInfo? recipe = game.Constructions.Recipes
            .FirstOrDefault(candidate => candidate.Result == resultId);
        if (recipe is null)
            return true;

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
        return true;
    }

    bool TryRenderClassLine(RenderTarget target, string line, int y)
    {
        string spriteId = line switch
        {
            var value when value.StartsWith("@mercenary|") => "syssze.raw?32",
            var value when value.StartsWith("@technician|") => "syssze.raw?48",
            var value when value.StartsWith("@doctor|") => "syssze.raw?16",
            _ => string.Empty
        };
        if (spriteId.Length == 0)
            return false;

        int separator = line.IndexOf('|');
        target.DrawSprite(new Vector2(6, y), app.ResourceManager.GetImage(spriteId));
        string[] description = line[(separator + 1)..].Split('|');
        for (int i = 0; i < description.Length; i++)
            (i == 0 ? _titleFont : _textFont).DrawText(target,
                new Vector2(44, y + 3 + i * 10),
                description[i], TextAlignment.Left, VerticalTextAlignment.Top);
        return true;
    }

    sealed record ManualItem(ItemType Item, Constructions.ConstructionInfo? Recipe);

    (string Statistic, string Detail) GetContextItemText(ItemType item,
        Constructions.ConstructionInfo? recipe)
    {
        if (item.DefenseValue > 0)
            return ($"Prevents {item.DefenseValue}% damage.", string.Empty);
        if (item.ID is "item_hand_pump" or "item_industrial_pump")
        {
            const int sourceOutput = 2;
            int output = BurntimeClassic.Instance.Game.RuleBook.CalculateWaterOutput(
                sourceOutput, item.ID == "item_hand_pump",
                item.ID == "item_industrial_pump");
            return ($"Produces {output} water per day",
                $"at a {sourceOutput}-water source");
        }
        if (item.DamageValues.Length == 0)
            return (GetEquipmentStatistic(item),
                GetMaterialLine(new ManualItem(item, recipe)));

        ClassicGame game = BurntimeClassic.Instance.Game;
        int tierWidth = game.RuleBook.Settings.CombatTierWidth;
        CombatPreview first = RuleFormulas.OriginalCombatPreview(
            item.DamageValues, 0, tierWidth);
        CombatPreview last = RuleFormulas.OriginalCombatPreview(
            item.DamageValues, tierWidth * 3, tierWidth);
        return ($"{first.Minimum}-{first.Maximum} damage at tier 1",
            $"{last.Minimum}-{last.Maximum} damage at tier 4");
    }

    void RenderScrollBar(RenderTarget target, int offset, int maximum,
        int visibleUnits = 120, int totalUnits = 0)
    {
        if (maximum <= 0)
            return;
        int trackHeight = Size.y - FooterHeight - 28;
        totalUnits = totalUnits > 0 ? totalUnits : visibleUnits + maximum;
        int thumbHeight = System.Math.Max(10, trackHeight * visibleUnits / totalUnits);
        int thumbY = 26 + (trackHeight - thumbHeight) * offset / maximum;
        target.RenderRect(new Vector2(290, 26), new Vector2(2, trackHeight),
            new PixelColor(80, 108, 116, 168));
        target.RenderRect(new Vector2(290, thumbY), new Vector2(2, thumbHeight),
            new PixelColor(220, 240, 164, 56));
    }

    string GetMaterialLine(ManualItem entry)
    {
        string[] text = app.ResourceManager.GetStrings(LabelsResource);
        if (entry.Recipe is null)
            return string.Empty;

        ClassicGame game = BurntimeClassic.Instance.Game;
        string materials = string.Join(" + ", entry.Recipe.Items.Select(id =>
            game.ItemTypes[id].Title));
        bool anyCharacter = entry.Recipe.Classes[(int)CharClass.Boss] &&
            entry.Recipe.Classes[(int)CharClass.Mercenary] &&
            entry.Recipe.Classes[(int)CharClass.Technician] &&
            entry.Recipe.Classes[(int)CharClass.Doctor];
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

    void DrawItem(RenderTarget target, ItemType item, Vector2 position,
        ref string? hoveredTitle)
    {
        if (string.IsNullOrEmpty(item.Sprite))
            return;

        ISprite sprite = app.ResourceManager.GetImage(item.Sprite);
        target.DrawSprite(position, sprite);
        if (_mousePosition.x >= position.x && _mousePosition.x < position.x + 32 &&
            _mousePosition.y >= position.y && _mousePosition.y < position.y + 32)
        {
            hoveredTitle = item.Title;
        }
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
        if (button == MouseButton.Right)
        {
            Hide();
            return true;
        }

        if (button != MouseButton.Left)
            return true;

        if (_exitButton.Boundings.PointInside(position) ||
            _nextButton.Boundings.PointInside(position))
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
        _mousePosition = position;
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

    void MovePage(int direction) =>
        _page = (_page + direction + PageCount) % PageCount;

    static int GetTabAt(Vector2 position)
    {
        if (position.y < 2 || position.y >= 22 || position.x < TabsLeft ||
            position.x >= TabsLeft + TabWidth * PageCount)
            return -1;
        return (position.x - TabsLeft) / TabWidth;
    }

    void MoveTextScroll(int direction)
    {
        if (_page < _textChunkScroll.Length)
            _textChunkScroll[_page] = System.Math.Max(0,
                _textChunkScroll[_page] + direction);
    }
}
