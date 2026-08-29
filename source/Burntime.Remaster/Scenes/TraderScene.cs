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
        inventory.Grid.MouseSelectionChanged += OnMouseSelectionChanged;
        inventory.Grid.SelectionEmptied += OnSelectionEmptied;
        Windows += inventory;

        inventoryTrader = new InventoryWindow(App, InventorySide.Right);
        inventoryTrader.Position = new Vector2(154, 5);
        inventoryTrader.LeftClickItemEvent += OnLeftClickItemTrader;
        inventoryTrader.RightClickItemEvent += OnRightClickItemTrader;
        inventoryTrader.Grid.MouseSelectionChanged += OnMouseSelectionChanged;
        inventoryTrader.Grid.SelectionEmptied += OnSelectionEmptied;
        Windows += inventoryTrader;

        exitButton = new Button(App);
        exitButton.Position = new Vector2(25, 183);
        exitButton.Text = app.ResourceManager.GetString("burn?354");
        exitButton.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
        exitButton.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
        exitButton.Command += OnButtonExit;
        exitButton.IsTextOnly = true;
        Windows += exitButton;

        acceptButton = new Button(App);
        acceptButton.Position = new Vector2(170, 183);
        acceptButton.Text = app.ResourceManager.GetString("burn?353");
        acceptButton.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
        acceptButton.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
        acceptButton.Command += OnButtonAccept;
        acceptButton.IsTextOnly = true;
        Windows += acceptButton;

        exchangeTop = new ExchangeWindow(App);
        inventoryTrader.Grid.Mask = exchangeTop.Grid;
        exchangeTop.LeftClickItemEvent += OnLeftClickItemTrader;
        Windows += exchangeTop;

        exchangeBottom = new ExchangeWindow(App);
        inventory.Grid.Mask = exchangeBottom.Grid;
        exchangeBottom.LeftClickItemEvent += OnLeftClickItemInventory;
        Windows += exchangeBottom;

        temporarySpace = new ItemGridWindow(App);
        temporarySpace.Position = new Vector2(156, 0);
        temporarySpace.Spacing = new Vector2(0, 1);
        temporarySpace.Grid = new Vector2(1, 6);
        temporarySpace.LeftClickItemEvent += OnClickTemporarySpace;
        temporarySpace.RightClickItemEvent += OnClickTemporarySpace;
        temporarySpace.UnifiedSelection = true;
        temporarySpace.MouseSelectionChanged += OnMouseSelectionChanged;
        temporarySpace.SelectionEmptied += OnSelectionEmptied;
        Windows += temporarySpace;

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

    public override void OnResizeScreen()
    {
        base.OnResizeScreen();

        PositionElements();
    }

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
        inventory.Grid.ResetKeyboardSelection();
        inventoryTrader.Grid.ResetKeyboardSelection();
        temporarySpace.ResetKeyboardSelection();
        exchangeTop.Grid.KeyboardSelectionVisible = false;
        exchangeBottom.Grid.KeyboardSelectionVisible = false;
        UpdateKeyboardArea();
    }

    void OnMouseSelectionChanged(ItemGridWindow selectedGrid)
    {
        keyboardArea = selectedGrid == inventoryTrader.Grid
            ? KeyboardArea.Trader
            : selectedGrid == temporarySpace
                ? KeyboardArea.Temporary
                : KeyboardArea.Player;
        UpdateKeyboardArea();
    }

    void OnSelectionEmptied(ItemGridWindow emptiedGrid, Vector2 previousPosition)
    {
        ItemGridWindow targetGrid;
        KeyboardArea targetArea;
        Vector2 direction;

        if (emptiedGrid == inventory.Grid)
        {
            targetGrid = temporarySpace.HasKeyboardItems ? temporarySpace : inventoryTrader.Grid;
            targetArea = temporarySpace.HasKeyboardItems ? KeyboardArea.Temporary : KeyboardArea.Trader;
            direction = new Vector2(1, 0);
        }
        else if (emptiedGrid == inventoryTrader.Grid)
        {
            targetGrid = temporarySpace.HasKeyboardItems ? temporarySpace : inventory.Grid;
            targetArea = temporarySpace.HasKeyboardItems ? KeyboardArea.Temporary : KeyboardArea.Player;
            direction = new Vector2(-1, 0);
        }
        else
        {
            targetGrid = inventory.Grid;
            targetArea = KeyboardArea.Player;
            direction = new Vector2(-1, 0);
        }

        if (!targetGrid.SelectKeyboardEdge(direction, previousPosition))
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

        if (action == InputAction.GlobalAction)
        {
            OnButtonAccept();
            EnsureKeyboardArea();
            return true;
        }

        if (action == InputAction.LeftArea)
        {
            inventory.SelectNextCharacter();
            UpdateKeyboardArea();
            return true;
        }

        if (action == InputAction.RightArea)
        {
            inventoryTrader.SelectNextCharacter();
            UpdateKeyboardArea();
            return true;
        }

        Vector2 direction = action switch
        {
            InputAction.MoveUp => new Vector2(0, -1),
            InputAction.MoveDown => new Vector2(0, 1),
            InputAction.MoveLeft => new Vector2(-1, 0),
            InputAction.MoveRight => new Vector2(1, 0),
            _ => Vector2.Zero
        };
        if (direction != Vector2.Zero)
        {
            ItemGridWindow activeGrid = ActiveKeyboardGrid;
            Vector2? sourcePosition = activeGrid.KeyboardSelectionPosition;
            if (!activeGrid.MoveKeyboardSelection(direction) && direction.x != 0)
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
                    targetArea = temporarySpace.HasKeyboardItems ? KeyboardArea.Temporary : KeyboardArea.Trader;
                    targetGrid = targetArea == KeyboardArea.Temporary ? temporarySpace : inventoryTrader.Grid;
                }
                else if (direction.x > 0 && keyboardArea == KeyboardArea.Temporary)
                {
                    targetArea = KeyboardArea.Trader;
                    targetGrid = inventoryTrader.Grid;
                }
                else if (direction.x < 0 && keyboardArea == KeyboardArea.Trader)
                {
                    targetArea = temporarySpace.HasKeyboardItems ? KeyboardArea.Temporary : KeyboardArea.Player;
                    targetGrid = targetArea == KeyboardArea.Temporary ? temporarySpace : inventory.Grid;
                }
                else if (direction.x < 0 && keyboardArea == KeyboardArea.Temporary)
                {
                    targetArea = KeyboardArea.Player;
                    targetGrid = inventory.Grid;
                }

                bool selectedTarget = sourcePosition.HasValue
                    ? targetGrid?.SelectKeyboardEdge(direction, sourcePosition.Value) == true
                    : targetGrid?.EnsureKeyboardSelection() == true;
                if (selectedTarget)
                {
                    keyboardArea = targetArea;
                    UpdateKeyboardArea();
                }
            }
            return true;
        }

        if (action == InputAction.Primary || action == InputAction.Secondary)
        {
            ActiveKeyboardGrid.ActivateKeyboardItem(action == InputAction.Secondary);
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
        if (keyboardArea == KeyboardArea.Temporary && !temporarySpace.HasKeyboardItems)
            keyboardArea = KeyboardArea.Player;

        UpdateKeyboardArea();
    }

    bool IsKeyboardAreaAvailable(KeyboardArea area)
    {
        return area != KeyboardArea.Temporary || temporarySpace.HasKeyboardItems;
    }

    void UpdateKeyboardArea()
    {
        inventory.Grid.KeyboardSelectionVisible = keyboardArea == KeyboardArea.Player;
        inventoryTrader.Grid.KeyboardSelectionVisible = keyboardArea == KeyboardArea.Trader;
        temporarySpace.KeyboardSelectionVisible = keyboardArea == KeyboardArea.Temporary;

        keyboardMousePosition = _lastPosition;
        PositionElements(requestedSide: keyboardArea == KeyboardArea.Trader
            ? InventorySide.Right
            : InventorySide.Left);
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
            ? inventory.Grid.KeyboardSelectionPosition
            : null;
        inventory.ActiveCharacter.Items.Remove(item);
        inventory.OnSelectPage();
        temporarySpace.Add(item);
        if (previousPosition.HasValue)
            OnSelectionEmptied(inventory.Grid, previousPosition.Value);
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
        int player = exchangeBottom.Grid.GetTradeValue();
        int trader = exchangeTop.Grid.GetTradeValue();
        ExchangeResult result = (player >= trader && exchangeBottom.Grid.Count > 0) ? ExchangeResult.Ok : ExchangeResult.Ng;
        if (exchangeTop.Grid.Count - exchangeBottom.Grid.Count > inventory.FreeSlots)
            result = ExchangeResult.Ng;
        if (exchangeBottom.Grid.Count - exchangeTop.Grid.Count > inventoryTrader.FreeSlots)
            result = ExchangeResult.Ng;

        return result;
    }
}
