
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2011 Jakob Harder
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
*/
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Classic.GUI;
using Burntime.Classic.Logic;

namespace Burntime.Classic.Scenes
{
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
            Music = "10_MUS 10_HSC.ogg";
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

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
            exitButton.SetTextOnly();
            Windows += exitButton;

            acceptButton = new Button(App);
            acceptButton.Position = new Vector2(170, 183);
            acceptButton.Text = app.ResourceManager.GetString("burn?353");
            acceptButton.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            acceptButton.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            acceptButton.Command += OnButtonAccept;
            acceptButton.SetTextOnly();
            Windows += acceptButton;

            exchangeTop = new ExchangeWindow(App);
            inventoryTrader.Grid.Mask = exchangeTop.Grid;
            Windows += exchangeTop;

            exchangeBottom = new ExchangeWindow(App);
            inventory.Grid.Mask = exchangeBottom.Grid;
            Windows += exchangeBottom;

            temporarySpace = new ItemGridWindow(App);
            temporarySpace.Position = new Vector2(156, 0);
            temporarySpace.Spacing = new Vector2(0, 1);
            temporarySpace.Grid = new Vector2(1, 6);
            temporarySpace.LeftClickItemEvent += OnClickTemporarySpace;
            temporarySpace.RightClickItemEvent += OnClickTemporarySpace;
            Windows += temporarySpace;
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);

            BurntimeClassic classic = app as BurntimeClassic;
        }

        public override bool OnMouseMove(Vector2 Position)
        {
            InventorySide newside = (Position.x >= (side != InventorySide.Left ? 120 : 200)) ? InventorySide.Right : InventorySide.Left;

            if (newside != side)
            {
                if (newside == InventorySide.Left)
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
                side = newside;
            }

            return base.OnMouseMove(Position);
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
}
