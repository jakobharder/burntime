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
    InventoryWindow inventory;
    InventoryWindow inventoryTrader;
    InventorySide side = InventorySide.Left;
    Button exitButton;
    Button acceptButton;
    ExchangeWindow exchangeTop;
    ExchangeWindow exchangeBottom;
    ItemGridWindow temporarySpace;

    public TraderScene(Module App)
        : base(App)
    {
        Background = "hint1.pac";
        Music = "trader";

        inventory = new InventoryWindow(App, InventorySide.Left);
        inventory.Position = new Vector2(2, 5);
        inventory.LeftClickItemEvent += OnLeftClickItemInventory;
        inventory.RightClickItemEvent += OnRightClickItemInventory;
        Windows += inventory;

        inventoryTrader = new InventoryWindow(App, InventorySide.Right);
        inventoryTrader.Position = new Vector2(154, 5);
        inventoryTrader.LeftClickItemEvent += OnLeftClickItemTrader;
        inventoryTrader.RightClickItemEvent += OnRightClickItemTrader;
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
        Windows += temporarySpace;

        PositionElements();
    }

    Vector2 _lastPosition = Vector2.Zero;
    void PositionElements(Vector2? mousePosition = null)
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

            InventorySide newside = ((mousePosition ?? _lastPosition).x >= (side != InventorySide.Left ? 120 : 200)) ? InventorySide.Right : InventorySide.Left;
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

    void OnLeftClickItemInventory(Framework.States.StateObject State)
    {
        if (exchangeBottom.Grid.Contains(State as Item))
            exchangeBottom.Grid.Remove(State as Item);
        else
            exchangeBottom.Grid.Add(State as Item);

        exchangeTop.ExchangeResult = CheckTrade();
    }

    void OnRightClickItemInventory(Framework.States.StateObject state)
    {
        if (temporarySpace.MaxCount - temporarySpace.Count <= 0)
            return;

        Item item = state as Item;
        if (exchangeBottom.Grid.Contains(item))
            return;
        inventory.ActiveCharacter.Items.Remove(item);
        inventory.OnSelectPage();
        temporarySpace.Add(item);
    }

    void OnLeftClickItemTrader(Framework.States.StateObject State)
    {
        if (exchangeTop.Grid.Contains(State as Item))
            exchangeTop.Grid.Remove(State as Item);
        else
            exchangeTop.Grid.Add(State as Item);

        exchangeTop.ExchangeResult = CheckTrade();
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
