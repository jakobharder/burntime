using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Burntime.Remaster.GUI;

internal enum ItemTooltipSide { Left, Right, Auto }

internal readonly record struct ItemTooltipDetails(
    InputPrompt? Prompt = null, GuiString? Status = null,
    bool StatusIsSuccess = false, bool StatusIsMuted = false);

// The popup belongs to the scene, not the grid: grids clip their children.
internal sealed class ItemGridTooltip
{
    const int MaxRecipeLines = 3;
    const int DamageText = 16, FoodText = 33, WaterText = 34;
    const int DefenseText = 36, GasText = 37, RadiationText = 38;
    const int ProductionText = 40, CapacityText = 41, RecipeText = 43;
    const int UnknownRecipeText = 44, UnknownRecipeCountText = 45;
    const int MoreRecipesText = 46;
    const int RemoteIntelText = 79, RestingSustenanceText = 80;
    const int CreatureDeterrentText = 81, MetalDetectionText = 82;
    const int LoadWithAmmunitionText = 83, LoadsWeaponsText = 84;
    const int ReachText = 87, ReachNameText = 88;
    readonly Module app;
    readonly Func<Character?> viewer;
    readonly List<Source> sources = [];
    ItemGridWindow? lastMouseSource;

    sealed record Source(ItemGridWindow Grid, ItemTooltipSide Side,
        Func<Item, ItemTooltipDetails>? Details);

    public TooltipWindow Window { get; }

    public ItemGridTooltip(Module app, Func<Character?> viewer)
    {
        this.app = app;
        this.viewer = viewer;
        Window = new TooltipWindow(app)
        {
            VerticalAlignment = PositionAlignment.Right,
            HeaderFont = new GuiFont(BurntimeClassic.FontName,
                ClassicColors.MenuTextHover) { Borders = TextBorders.None },
            Layer = 40
        };
        Window.Hide();
    }

    public void AddGrid(ItemGridWindow grid, ItemTooltipSide side = ItemTooltipSide.Auto,
        Func<Item, ItemTooltipDetails>? details = null)
    {
        grid.ShowHoverText = false;
        grid.MouseFocusChanged += OnMouseFocusChanged;
        sources.Add(new(grid, side, details));
    }

    public void ClearGrids()
    {
        foreach (Source source in sources)
            source.Grid.MouseFocusChanged -= OnMouseFocusChanged;
        sources.Clear();
        lastMouseSource = null;
        Window.Hide();
    }

    void OnMouseFocusChanged(ItemGridWindow grid) => lastMouseSource = grid;

    public void Update()
    {
        Source? source = sources.FirstOrDefault(source =>
            source.Grid == lastMouseSource && IsVisible(source.Grid) &&
            source.Grid.FocusedItem != null);
        source ??= sources.FirstOrDefault(source =>
            IsVisible(source.Grid) && source.Grid.FocusedItem != null);
        if (source == null || Window.Parent == null)
        {
            Window.Hide();
            return;
        }

        ItemGridWindow grid = source.Grid;
        Item? item = grid.FocusedItem;
        Vector2? focus = grid.FocusPosition;
        if (item == null || !focus.HasValue)
        {
            Window.Hide();
            return;
        }

        ItemTooltipSide side = source.Side == ItemTooltipSide.Auto
            ? grid.PositionOnScreen.x + grid.Size.x / 2 < app.Engine.Resolution.Game.x / 2
                ? ItemTooltipSide.Right : ItemTooltipSide.Left
            : source.Side;
        Window.HorizontalAlignment = side == ItemTooltipSide.Right
            ? PositionAlignment.Left : PositionAlignment.Right;
        Vector2 parentPosition = Window.Parent.PositionOnScreen;
        int edgeX = grid.PositionOnScreen.x - parentPosition.x +
            (side == ItemTooltipSide.Right ? grid.Size.x : 0);
        Window.Position = new Vector2(edgeX, focus.Value.y - parentPosition.y + 16);
        Window.Header = item.TooltipText;
        Window.Text = BuildItemFacts(item);
        ItemTooltipDetails details = source.Details?.Invoke(item) ?? default;
        Window.Prompt = details.Prompt;
        Window.Status = details.Status;
        Window.StatusIsSuccess = details.StatusIsSuccess;
        Window.StatusIsMuted = details.StatusIsMuted;
        Window.RefreshLayout();
        if (!Window.IsVisible)
            Window.Show();
    }

    static bool IsVisible(ItemGridWindow grid)
    {
        Window? window = grid;
        while (window != null)
        {
            if (!window.IsVisible)
                return false;
            window = window.Parent;
        }
        return true;
    }

    string BuildItemFacts(Item item)
    {
        List<string> lines = [];
        ClassicGame? game = app.GameState as ClassicGame;
        if (game == null)
            return string.Empty;

        if (item.Type.DamageValues.Length > 0 && viewer() is Character character)
        {
            var damage = game.RuleBook.GetWeaponPreview(character, item);
            string range = damage.Minimum == damage.Maximum
                ? damage.Minimum.ToString() : $"{damage.Minimum}-{damage.Maximum}";
            TextHelper text = new(app, "tooltip");
            text.AddArgument("{damage}", range);
            lines.Add(text.Get(DamageText));
        }

        if (item.Type.AttackRange > 0)
        {
            TextHelper text = new(app, "tooltip");
            int reachName = ReachNameText +
                (int)WeaponReachRules.FromRange(item.Type.AttackRange);
            text.AddArgument("{reach}",
                app.ResourceManager.GetString("tooltip", reachName));
            lines.Add(text.Get(ReachText));
        }

        if (item.FoodValue > 0 || item.WaterValue > 0)
        {
            TextHelper text = new(app, "tooltip");
            text.AddArgument("{food}", item.FoodValue);
            text.AddArgument("{water}", item.WaterValue);
            if (item.FoodValue > 0) lines.Add(text.Get(FoodText));
            if (item.WaterValue > 0) lines.Add(text.Get(WaterText));
        }

        int capacity = item.Type.Full?.WaterValue ?? 0;
        if (item.WaterValue == 0 && capacity > 0)
        {
            TextHelper text = new(app, "tooltip");
            text.AddArgument("{water}", capacity);
            lines.Add(text.Get(CapacityText));
        }

        if (item.Type.Production is Production production)
        {
            TextHelper text = new(app, "tooltip");
            text.AddArgument("{food}", production.GetRate(1, 1).FoodPerDay);
            lines.Add(text.Get(ProductionText));
        }

        TextHelper protection = new(app, "tooltip");
        if (item.DefenseValue > 0)
        {
            protection.AddArgument("{defense}", item.DefenseValue);
            lines.Add(protection.Get(DefenseText));
        }
        int gas = (int)((item.Type.GetProtection("gas")?.Rate ?? 0) * 100);
        int radiation = (int)((item.Type.GetProtection("radiation")?.Rate ?? 0) * 100);
        if (gas > 0)
        {
            protection.AddArgument("{gas}", gas);
            lines.Add(protection.Get(GasText));
        }
        if (radiation > 0)
        {
            protection.AddArgument("{radiation}", radiation);
            lines.Add(protection.Get(RadiationText));
        }

        if (item.Type.HasFunction(ItemFunction.RemoteIntel))
            lines.Add(app.ResourceManager.GetString("tooltip", RemoteIntelText));
        if (item.Type.HasFunction(ItemFunction.RestingSustenance))
            lines.Add(app.ResourceManager.GetString("tooltip", RestingSustenanceText));
        if (item.Type.HasFunction(ItemFunction.CreatureDeterrent))
            lines.Add(app.ResourceManager.GetString("tooltip", CreatureDeterrentText));
        if (item.Type.HasFunction(ItemFunction.MetalDetection))
            lines.Add(app.ResourceManager.GetString("tooltip", MetalDetectionText));

        string[] loadingResults = item.Type.Loads
            .Where(game.ItemTypes.Contains).Distinct().ToArray();
        HashSet<string> loadingResultSet = loadingResults.ToHashSet();
        if (loadingResults.Length > 0)
        {
            bool isUnloadedWeapon = loadingResults.Any(result =>
                game.ItemTypes[result].Empty == item.Type);
            if (isUnloadedWeapon)
            {
                lines.Add(app.ResourceManager.GetString("tooltip",
                    LoadWithAmmunitionText));
            }
            else
            {
                TextHelper text = new(app, "tooltip");
                text.AddArgument("{weapons}", string.Join(" / ", loadingResults
                    .Select(result => game.ItemTypes[result].LoadName)));
                lines.Add(text.Get(LoadsWeaponsText));
            }
        }

        var recipes = game.Constructions.GetRecipes(game).Where(recipe =>
            (recipe.Items.Contains(item.ID) || recipe.Tools.Contains(item.ID)) &&
            !loadingResultSet.Contains(recipe.Result))
            .ToArray();
        var known = recipes.Where(recipe => game.IsConstructionKnown(recipe.Result))
            .GroupBy(recipe => recipe.Result).Select(group => group.First()).ToArray();
        int unknown = recipes.Select(recipe => recipe.Result).Distinct().Count() - known.Length;
        bool moreKnown = known.Length > MaxRecipeLines;
        int knownLimit = unknown > 0 || moreKnown ? MaxRecipeLines - 1 : MaxRecipeLines;
        foreach (var recipe in known.Take(knownLimit))
        {
            TextHelper text = new(app, "tooltip");
            text.AddArgument("{recipe}", game.ItemTypes[recipe.Result].Title);
            lines.Add(text.Get(RecipeText));
        }
        if (unknown == 1)
            lines.Add(app.ResourceManager.GetString("tooltip", UnknownRecipeText));
        else if (unknown > 1)
        {
            TextHelper text = new(app, "tooltip");
            text.AddArgument("{count}", unknown);
            lines.Add(text.Get(UnknownRecipeCountText));
        }
        else if (moreKnown)
            lines.Add(app.ResourceManager.GetString("tooltip", MoreRecipesText));

        return string.Join('\n', lines);
    }
}
