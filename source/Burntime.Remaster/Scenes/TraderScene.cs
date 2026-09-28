using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;
using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Remaster.Scenes;

class TraderScene : Scene
{
    public override bool UseDiagonalGamepadNavigation => true;

    enum KeyboardArea
    {
        Player,
        Trader,
        Temporary
    }

    InventoryWindow inventory;
    InventoryWindow inventoryTrader;
    InventorySide side = InventorySide.Left;
    Button exitButton;
    Button acceptButton;
    ExchangeWindow exchangeTop;
    ExchangeWindow exchangeBottom;
    ItemGridWindow temporarySpace;
    readonly ItemGridTooltip itemTooltip;
    KeyboardArea keyboardArea;
    Vector2? keyboardMousePosition;

    public TraderScene(Module App)
        : base(App)
    {
        Background = "gfx/trader_background.png";
        Music = "trader";

        inventory = new InventoryWindow(App, InventorySide.Left);
        inventory.Position = new Vector2(2, 5);
        inventory.LeftClickItemEvent += OnLeftClickItemInventory;
        inventory.RightClickItemEvent += OnRightClickItemInventory;
        inventory.Grid.MouseFocusChanged += OnMouseFocusChanged;
        inventory.Grid.FocusEmptied += OnFocusEmptied;
        AddTradeGridPrompts(inventory.Grid, playerSide: true);
        Windows += inventory;

        inventoryTrader = new InventoryWindow(App, InventorySide.Right);
        inventoryTrader.Position = new Vector2(154, 5);
        inventoryTrader.LeftClickItemEvent += OnLeftClickItemTrader;
        inventoryTrader.RightClickItemEvent += OnRightClickItemTrader;
        inventoryTrader.Grid.MouseFocusChanged += OnMouseFocusChanged;
        inventoryTrader.Grid.FocusEmptied += OnFocusEmptied;
        AddTradeGridPrompts(inventoryTrader.Grid, playerSide: false);
        Windows += inventoryTrader;

        exitButton = new Button(App);
        exitButton.Position = new Vector2(25, 183);
        exitButton.Text = app.ResourceManager.GetString("burn?354");
        exitButton.Font = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudText);
        exitButton.HoverFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudTextHover);
        exitButton.Command += OnButtonExit;
        exitButton.IsTextOnly = true;
        Windows += exitButton;

        acceptButton = new Button(App);
        acceptButton.Position = new Vector2(170, 183);
        acceptButton.Text = app.ResourceManager.GetString("burn?353");
        acceptButton.Font = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudText);
        acceptButton.HoverFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudTextHover);
        acceptButton.Command += OnButtonAccept;
        acceptButton.IsTextOnly = true;
        Windows += acceptButton;

        exchangeTop = new ExchangeWindow(App);
        inventoryTrader.Grid.Mask = exchangeTop.Grid;
        exchangeTop.LeftClickItemEvent += OnLeftClickItemTrader;
        exchangeTop.Grid.MouseFocusChanged += OnMouseFocusChanged;
        AddTradeGridPrompts(exchangeTop.Grid, playerSide: false);
        Windows += exchangeTop;

        exchangeBottom = new ExchangeWindow(App);
        inventory.Grid.Mask = exchangeBottom.Grid;
        exchangeBottom.LeftClickItemEvent += OnLeftClickItemInventory;
        exchangeBottom.Grid.MouseFocusChanged += OnMouseFocusChanged;
        AddTradeGridPrompts(exchangeBottom.Grid, playerSide: true);
        Windows += exchangeBottom;

        temporarySpace = new ItemGridWindow(App);
        temporarySpace.Position = new Vector2(156, 0);
        temporarySpace.Spacing = new Vector2(0, 1);
        temporarySpace.Grid = new Vector2(1, 6);
        temporarySpace.LeftClickItemEvent += OnClickTemporarySpace;
        temporarySpace.RightClickItemEvent += OnClickTemporarySpace;
        temporarySpace.UnifiedSelection = true;
        temporarySpace.MouseFocusChanged += OnMouseFocusChanged;
        temporarySpace.FocusEmptied += OnFocusEmptied;
        temporarySpace.Prompts.HideInNewGfx = true;
        temporarySpace.Prompts.Add(InputAction.Primary, "@prompts?46");
        Windows += temporarySpace;

        itemTooltip = new ItemGridTooltip(app, () => inventory.ActiveCharacter);
        itemTooltip.AddGrid(inventory.Grid, details: _ => GetTradeTooltip(inventory.Grid, true));
        itemTooltip.AddGrid(inventoryTrader.Grid, details: _ => GetTradeTooltip(inventoryTrader.Grid, false));
        itemTooltip.AddGrid(exchangeTop.Grid, details: _ => GetTradeTooltip(exchangeTop.Grid, false));
        itemTooltip.AddGrid(exchangeBottom.Grid, details: _ => GetTradeTooltip(exchangeBottom.Grid, true));
        itemTooltip.AddGrid(temporarySpace, details: _ => app.IsNewGfx
            ? new ItemTooltipDetails(new InputPrompt(InputAction.Primary, "@prompts?46")
                { KeyboardControl = PreferredPrimaryKeyboardControl }) : default);
        Windows += itemTooltip.Window;

        Windows += new InputPromptOverlay(app, Prompts,
            InputPromptColorScheme.Hud);
        exitButton.Prompts.Add(InputAction.Back, "",
            new Vector2(exitButton.Size.x + 2, -2));
        acceptButton.Prompts.Add(InputAction.Action, "",
            new Vector2(acceptButton.Size.x + 2, -2));

        Prompts.Add(InputPattern.HorizontalPaging, "@prompts?30",
            () => inventory.PageCount > 1);

        PositionElements();
    }

    Vector2 _lastPosition = Vector2.Zero;
    void PositionElements(Vector2? mousePosition = null, InventorySide? requestedSide = null)
    {
        if (app.Engine.Resolution.Game.x >= 450)
        {
            Size = new Vector2(470, 200);
            Position = (app.Engine.Resolution.Game - Size) / 2;

            inventory.Show();
            exitButton.Show();
            acceptButton.Show();
            inventoryTrader.Show();
            exchangeTop.Position = new Vector2(195, 1);
            exchangeBottom.Position = new Vector2(195, 101);
            temporarySpace.Show();

            acceptButton.Position = new Vector2(170, 183) + new Vector2(150, 0);
            inventoryTrader.Position = new Vector2(154, 5) + new Vector2(150, 0);

            side = InventorySide.None;
        }
        else
        {
            Size = new Vector2(320, 200);
            Position = (app.Engine.Resolution.Game - Size) / 2;

            acceptButton.Position = new Vector2(170, 183);
            inventoryTrader.Position = new Vector2(154, 5);

            InventorySide newside = requestedSide ??
                (((mousePosition ?? _lastPosition).x >= (side != InventorySide.Left ? 120 : 200))
                    ? InventorySide.Right
                    : InventorySide.Left);
            if (newside != side)
            {
                side = newside;

                if (side == InventorySide.Left)
                {
                    inventory.Show();
                    exitButton.Show();
                    acceptButton.Hide();
                    inventoryTrader.Hide();
                    exchangeTop.Position = new Vector2(195, 1);
                    exchangeBottom.Position = new Vector2(195, 101);
                    temporarySpace.Show();
                }
                else
                {
                    inventory.Hide();
                    exitButton.Hide();
                    acceptButton.Show();
                    inventoryTrader.Show();
                    exchangeTop.Position = new Vector2(2, 1);
                    exchangeBottom.Position = new Vector2(2, 101);
                    temporarySpace.Hide();
                }
            }
        }

        if (mousePosition.HasValue)
            _lastPosition = mousePosition.Value;

    }

    public override void OnResizeScreen(bool reload = false)
    {
        base.OnResizeScreen(reload);

        PositionElements();
    }

    public override void OnUpdate(float elapsed) => itemTooltip.Update();

    public override void OnRender(RenderTarget Target)
    {
        base.OnRender(Target);

        BurntimeClassic classic = app as BurntimeClassic;
    }

    public override bool OnMouseMove(Vector2 position)
    {
        if (keyboardMousePosition.HasValue && position == keyboardMousePosition.Value)
            return base.OnMouseMove(position);

        keyboardMousePosition = null;
        PositionElements(position);

        return base.OnMouseMove(position);
    }

    public override bool OnTouchTap(Vector2 position)
    {
        if (app.Engine.Resolution.Game.x >= 450 || !new Rect(Vector2.Zero, Size).PointInside(position))
            return false;
        bool exchangeSide = side == InventorySide.Left ? position.x >= 195 : position.x < 125;
        if (!exchangeSide) return false;
        keyboardMousePosition = null;
        keyboardArea = side == InventorySide.Left ? KeyboardArea.Trader : KeyboardArea.Player;
        PositionElements(position, side == InventorySide.Left ? InventorySide.Right : InventorySide.Left);
        return true;
    }

    protected override void OnActivateScene(object parameter)
    {
        BurntimeClassic classic = app as BurntimeClassic;
        inventory.SetGroup(classic.SelectedCharacter);
        inventoryTrader.SetGroup(classic.Game.World.ActiveTraderObj);
        exchangeTop.Title = classic.Game.World.ActiveTraderObj.Name;
        exchangeTop.ExchangeResult = ExchangeResult.Ng;
        exchangeBottom.Title = classic.Game.World.ActivePlayerObj.Name;
        exchangeBottom.ExchangeResult = ExchangeResult.None;

        temporarySpace.Clear();

        side = InventorySide.None;
        keyboardArea = KeyboardArea.Trader;
        inventory.Grid.ClearFocus();
        inventoryTrader.Grid.ClearFocus();
        temporarySpace.ClearFocus();
        exchangeTop.Grid.FocusVisible = false;
        exchangeBottom.Grid.FocusVisible = false;
        UpdateKeyboardArea();
    }

    void OnMouseFocusChanged(ItemGridWindow focusedGrid)
    {
        if (app.LastInputMode == InputMode.Touch &&
            (focusedGrid == exchangeTop.Grid || focusedGrid == exchangeBottom.Grid))
            return;
        keyboardArea = focusedGrid == inventoryTrader.Grid || focusedGrid == exchangeTop.Grid
            ? KeyboardArea.Trader
            : focusedGrid == temporarySpace
                ? KeyboardArea.Temporary
                : KeyboardArea.Player;
        UpdateKeyboardArea();
    }

    void OnFocusEmptied(ItemGridWindow emptiedGrid, Vector2 previousPosition)
    {
        ItemGridWindow targetGrid;
        KeyboardArea targetArea;
        Vector2 direction;

        if (emptiedGrid == inventory.Grid)
        {
            targetGrid = temporarySpace.HasFocusableItems ? temporarySpace : inventoryTrader.Grid;
            targetArea = temporarySpace.HasFocusableItems ? KeyboardArea.Temporary : KeyboardArea.Trader;
            direction = new Vector2(1, 0);
        }
        else if (emptiedGrid == inventoryTrader.Grid)
        {
            targetGrid = temporarySpace.HasFocusableItems ? temporarySpace : inventory.Grid;
            targetArea = temporarySpace.HasFocusableItems ? KeyboardArea.Temporary : KeyboardArea.Player;
            direction = new Vector2(-1, 0);
        }
        else
        {
            targetGrid = inventory.Grid;
            targetArea = KeyboardArea.Player;
            direction = new Vector2(-1, 0);
        }

        if (!targetGrid.FocusEdge(direction, previousPosition))
            return;

        keyboardArea = targetArea;
        UpdateKeyboardArea();
    }

    void OnButtonExit()
    {
        exchangeTop.Grid.Clear();
        exchangeBottom.Grid.Clear();

        app.SceneManager.PreviousScene();
    }

    void OnButtonAccept()
    {
        if (exchangeTop.ExchangeResult == ExchangeResult.Ng)
            return;

        BurntimeClassic classic = app as BurntimeClassic;

        // remove items in exchange place from parties
        foreach (Character chr in inventory.ActiveCharacter.GetGroup())
            chr.Items.Remove(exchangeBottom.Grid);
        classic.Game.World.ActiveTraderObj.Items.Remove(exchangeTop.Grid);

        // move items from exchange place to parties
        inventory.ActiveCharacter.GetGroup().MoveItems(exchangeTop.Grid);
        classic.Game.World.ActiveTraderObj.GetGroup().MoveItems(exchangeBottom.Grid);

        exchangeTop.Grid.Clear();
        exchangeBottom.Grid.Clear();

        inventory.OnSelectPage();
        inventoryTrader.OnSelectPage();

        exchangeTop.ExchangeResult = ExchangeResult.Ng;
    }

    public override bool OnVKeyPress(SystemKey key)
    {
        if (key == SystemKey.Escape)
        {
            OnButtonExit();
            return true;
        }

        if (key == SystemKey.Enter)
        {
            OnButtonAccept();
            return true;
        }

        return false;
    }

    public override bool OnInputAction(InputAction action)
    {
        if (action == InputAction.Back)
        {
            OnButtonExit();
            return true;
        }

        if (action == InputAction.Action)
        {
            OnButtonAccept();
            EnsureKeyboardArea();
            return true;
        }

        // Shoulders always page through the party, regardless of the focused area.
        if (action is InputAction.LeftArea or InputAction.RightArea)
        {
            inventory.SelectAdjacentPage(action == InputAction.LeftArea ? -1 : 1);
            UpdateKeyboardArea();
            return true;
        }

        Vector2 direction = action switch
        {
            InputAction.MoveUp => new Vector2(0, -1),
            InputAction.MoveDown => new Vector2(0, 1),
            InputAction.MoveLeft => new Vector2(-1, 0),
            InputAction.MoveRight => new Vector2(1, 0),
            InputAction.MoveUpLeft => new Vector2(-1, -1),
            InputAction.MoveUpRight => new Vector2(1, -1),
            InputAction.MoveDownLeft => new Vector2(-1, 1),
            InputAction.MoveDownRight => new Vector2(1, 1),
            _ => Vector2.Zero
        };
        if (direction != Vector2.Zero)
        {
            if (direction.x != 0 && inventory.Grid.FocusPosition == null &&
                inventoryTrader.Grid.FocusPosition == null && temporarySpace.FocusPosition == null)
            {
                keyboardArea = direction.x > 0 ? KeyboardArea.Trader : KeyboardArea.Player;
                ActiveKeyboardGrid.EnsureFocus();
                UpdateKeyboardArea();
                return true;
            }

            ItemGridWindow activeGrid = ActiveKeyboardGrid;
            Vector2? sourcePosition = activeGrid.FocusPosition;
            bool moved = activeGrid.MoveFocus(direction);
            InventoryWindow? activeInventory = keyboardArea switch
            {
                KeyboardArea.Player => inventory,
                KeyboardArea.Trader => inventoryTrader,
                _ => null
            };
            if (!moved && direction.x == 0 && direction.y != 0 && activeInventory != null &&
                activeInventory.SelectAdjacentPage(direction.y > 0 ? 1 : -1))
            {
                if (sourcePosition.HasValue)
                    activeInventory.Grid.FocusPageEdge(direction, sourcePosition.Value);
                UpdateKeyboardArea();
            }
            else if (!moved && direction.x != 0)
            {
                ItemGridWindow targetGrid = null;
                KeyboardArea targetArea = keyboardArea;
                if (!sourcePosition.HasValue && direction.x < 0 && keyboardArea == KeyboardArea.Trader)
                {
                    targetArea = KeyboardArea.Player;
                    targetGrid = inventory.Grid;
                }
                else if (!sourcePosition.HasValue && direction.x > 0 && keyboardArea == KeyboardArea.Player)
                {
                    targetArea = KeyboardArea.Trader;
                    targetGrid = inventoryTrader.Grid;
                }
                else if (direction.x > 0 && keyboardArea == KeyboardArea.Player)
                {
                    targetArea = temporarySpace.HasFocusableItems ? KeyboardArea.Temporary : KeyboardArea.Trader;
                    targetGrid = targetArea == KeyboardArea.Temporary ? temporarySpace : inventoryTrader.Grid;
                }
                else if (direction.x > 0 && keyboardArea == KeyboardArea.Temporary)
                {
                    targetArea = KeyboardArea.Trader;
                    targetGrid = inventoryTrader.Grid;
                }
                else if (direction.x < 0 && keyboardArea == KeyboardArea.Trader)
                {
                    targetArea = temporarySpace.HasFocusableItems ? KeyboardArea.Temporary : KeyboardArea.Player;
                    targetGrid = targetArea == KeyboardArea.Temporary ? temporarySpace : inventory.Grid;
                }
                else if (direction.x < 0 && keyboardArea == KeyboardArea.Temporary)
                {
                    targetArea = KeyboardArea.Player;
                    targetGrid = inventory.Grid;
                }

                bool selectedTarget = sourcePosition.HasValue
                    ? targetGrid?.FocusEdge(direction, sourcePosition.Value) == true
                    : targetGrid?.EnsureFocus() == true;
                bool canEnterEmptyInventory = targetArea != keyboardArea &&
                    targetArea is KeyboardArea.Player or KeyboardArea.Trader;
                if (selectedTarget || canEnterEmptyInventory)
                {
                    keyboardArea = targetArea;
                    UpdateKeyboardArea();
                }
            }
            return true;
        }

        if (action == InputAction.Primary || action == InputAction.Secondary)
        {
            ActiveKeyboardGrid.ActivateFocusedItem(action == InputAction.Secondary);
            EnsureKeyboardArea();
            return true;
        }

        return false;
    }

    ItemGridWindow ActiveKeyboardGrid => keyboardArea switch
    {
        KeyboardArea.Trader => inventoryTrader.Grid,
        KeyboardArea.Temporary => temporarySpace,
        _ => inventory.Grid
    };

    void MoveToNextKeyboardArea()
    {
        KeyboardArea start = keyboardArea;
        do
        {
            keyboardArea = keyboardArea switch
            {
                KeyboardArea.Player => KeyboardArea.Trader,
                KeyboardArea.Trader => KeyboardArea.Temporary,
                _ => KeyboardArea.Player
            };
        }
        while (!IsKeyboardAreaAvailable(keyboardArea) && keyboardArea != start);

        UpdateKeyboardArea();
    }

    void EnsureKeyboardArea()
    {
        // Player and trader inventories remain active even on an empty page:
        // the active side determines which set of character pages G cycles.
        // Temporary storage, unlike those inventories, has no pages of its own.
        if (keyboardArea == KeyboardArea.Temporary && !temporarySpace.HasFocusableItems)
            keyboardArea = KeyboardArea.Player;

        UpdateKeyboardArea();
    }

    bool IsKeyboardAreaAvailable(KeyboardArea area)
    {
        return area != KeyboardArea.Temporary || temporarySpace.HasFocusableItems;
    }

    void UpdateKeyboardArea()
    {
        inventory.Grid.FocusVisible = keyboardArea == KeyboardArea.Player;
        inventoryTrader.Grid.FocusVisible = keyboardArea == KeyboardArea.Trader;
        temporarySpace.FocusVisible = keyboardArea == KeyboardArea.Temporary;

        keyboardMousePosition = _lastPosition;
        PositionElements(requestedSide: keyboardArea == KeyboardArea.Trader
            ? InventorySide.Right
            : InventorySide.Left);
    }

    ItemTooltipDetails GetTradeTooltip(ItemGridWindow grid, bool playerSide)
    {
        GuiString? label = GetTradePrompt(grid, playerSide);
        if (!app.IsNewGfx || label == null)
            return default;
        return new ItemTooltipDetails(
            new InputPrompt(InputAction.Primary, label)
                { KeyboardControl = PreferredPrimaryKeyboardControl },
            SecondaryPrompt: CanShowMovePrompt(grid)
                ? new InputPrompt(InputAction.Secondary, "@prompts?45") : null);
    }

    void AddTradeGridPrompts(ItemGridWindow promptGrid, bool playerSide)
    {
        promptGrid.Prompts.HideInNewGfx = true;
        promptGrid.Prompts.AddDynamic(InputAction.Primary,
            () => GetTradePrompt(promptGrid, playerSide),
            playerSide ? "@prompts?40" : "@prompts?44",
            playerSide ? "@prompts?36" : "@prompts?37");
        if (promptGrid == inventory.Grid)
            promptGrid.Prompts.Add(InputAction.Secondary, "@prompts?45",
                () => CanShowMovePrompt(promptGrid));
    }

    GuiString? GetTradePrompt(ItemGridWindow source, bool playerSide)
    {
        if (source.FocusedItem == null)
            return null;
        return IsFocusedItemForTrade(source, playerSide)
            ? (playerSide ? "@prompts?40" : "@prompts?44")
            : (playerSide ? "@prompts?36" : "@prompts?37");
    }

    bool IsFocusedItemForTrade(ItemGridWindow source, bool playerSide)
    {
        Item? focusedItem = source.FocusedItem;
        return focusedItem != null && (playerSide
            ? exchangeBottom.Grid.Contains(focusedItem)
            : exchangeTop.Grid.Contains(focusedItem));
    }

    bool CanShowMovePrompt(ItemGridWindow source)
    {
        Item? focusedItem = source.FocusedItem;
        if (focusedItem == null || source != inventory.Grid)
            return false;
        if (app.LastInputMode != InputMode.Mouse)
            return true;
        return !exchangeBottom.Grid.Contains(focusedItem) &&
            temporarySpace.Count < temporarySpace.MaxCount;
    }

    void OnLeftClickItemInventory(Framework.States.StateObject State)
    {
        if (exchangeBottom.Grid.Contains(State as Item))
            exchangeBottom.Grid.Remove(State as Item);
        else
            exchangeBottom.Grid.Add(State as Item);

        exchangeTop.ExchangeResult = CheckTrade();
        EnsureKeyboardArea();
    }

    void OnRightClickItemInventory(Framework.States.StateObject state)
    {
        if (temporarySpace.MaxCount - temporarySpace.Count <= 0)
            return;

        Item item = state as Item;
        if (exchangeBottom.Grid.Contains(item))
            return;
        Vector2? previousPosition = inventory.Grid.Count == 1
            ? inventory.Grid.FocusPosition
            : null;
        inventory.ActiveCharacter.Items.Remove(item);
        inventory.OnSelectPage();
        temporarySpace.Add(item);
        if (previousPosition.HasValue)
            OnFocusEmptied(inventory.Grid, previousPosition.Value);
        EnsureKeyboardArea();
    }

    void OnLeftClickItemTrader(Framework.States.StateObject State)
    {
        if (exchangeTop.Grid.Contains(State as Item))
            exchangeTop.Grid.Remove(State as Item);
        else
            exchangeTop.Grid.Add(State as Item);

        exchangeTop.ExchangeResult = CheckTrade();
        EnsureKeyboardArea();
    }

    void OnRightClickItemTrader(Framework.States.StateObject State)
    {
    }

    void OnClickTemporarySpace(Framework.States.StateObject state)
    {
        if (inventory.Grid.MaxCount - inventory.Grid.Count <= 0)
            return;

        Item item = state as Item;
        inventory.ActiveCharacter.Items.Add(item);
        inventory.OnSelectPage();
        temporarySpace.Remove(item);
        EnsureKeyboardArea();
    }

    ExchangeResult CheckTrade()
    {
        BurntimeClassic classic = app as BurntimeClassic;
        ExchangeResult result = classic.Game.RuleBook.AcceptTrade(
            exchangeBottom.Grid,
            exchangeTop.Grid,
            classic.Game.World.Difficulty)
            ? ExchangeResult.Ok
            : ExchangeResult.Ng;
        if (exchangeTop.Grid.Count - exchangeBottom.Grid.Count > inventory.FreeSlots)
            result = ExchangeResult.Ng;
        if (exchangeBottom.Grid.Count - exchangeTop.Grid.Count > inventoryTrader.FreeSlots)
            result = ExchangeResult.Ng;

        return result;
    }
}
