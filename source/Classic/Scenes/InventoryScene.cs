
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.States;
using Burntime.Classic.GUI;
using Burntime.Classic.Logic;
using Burntime.Classic.Logic.Interaction;

namespace Burntime.Classic.Scenes
{
    class InventoryScene : Scene
    {
        InventoryWindow inventory;
        ItemGridWindow grid;
        GuiFont waterSourceFont;
        DialogWindow dialog;
        Construction construction;
        Item item;
        ICharacterCollection group;
        Character leader;

        public InventoryScene(Module app)
            : base(app)
        {
            Background = "hint2.pac";
            Size = new Vector2(320, 200);
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

            inventory = new InventoryWindow(app, InventorySide.Left);
            inventory.Position = new Vector2(2, 5);
            inventory.LeftClickItemEvent += OnLeftClickItemInventory;
            inventory.RightClickItemEvent += OnRightClickItemInventory;
            Windows += inventory;

            Button button = new Button(app);
            button.Position = new Vector2(25, 183);
            button.Text = app.ResourceManager.GetString("burn?354");
            button.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            button.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            button.Command += OnButtonExit;
            button.SetTextOnly();
            Windows += button;

            waterSourceFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(72, 72, 76));

            dialog = new DialogWindow(app);
            dialog.Position = new Vector2(33, 20);
            dialog.Hide();
            dialog.Layer += 55;
            dialog.WindowHide += new EventHandler(dialog_WindowHide);
            Windows += dialog;
        }

        void dialog_WindowHide(object sender, EventArgs e)
        {
            if (dialog.Result == ConversationActionType.Yes)
            {
                BurntimeClassic classic = app as BurntimeClassic;
                IItemCollection right = (classic.InventoryRoom == null) ? (IItemCollection)classic.PickItems : classic.InventoryRoom.Items;

                classic.Game.Constructions.Construct(construction, inventory.ActiveCharacter, right, item, classic.Game);

                inventory.OnSelectPage();

                grid.Clear();
                grid.Add(right);
            }
        }

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            target.Layer += 10;

            BurntimeClassic classic = app as BurntimeClassic;

            if (classic.InventoryRoom != null && classic.InventoryRoom.IsWaterSource)
            {
                TextHelper txt = new TextHelper(app, "burn");
                txt.AddArgument("|C", classic.Game.World.ActiveLocationObj.Source.Reserve);
                waterSourceFont.DrawText(target, target.Size, txt[423]);

                Vector2 bar = new Vector2(14, classic.Game.World.ActiveLocationObj.Source.Reserve * 2);

                target.RenderRect(target.Size - new Vector2(2, 10) - bar, bar, new PixelColor(240, 64, 56));
            }

            target.Layer -= 10;
        }

        public override void OnUpdate(float elapsed)
        {
            ClassicGame game = app.GameState as ClassicGame;
            game.World.Update(elapsed);
        }

        protected override void OnActivateScene(object parameter)
        {
            leader = parameter as Character;
            group = leader.GetGroup();

            BurntimeClassic classic = app as BurntimeClassic;

            inventory.SetGroup(leader);

            if (classic.InventoryBackground == -1)
                Background = "hint2.pac";
            else
                Background = "raum_" + classic.InventoryBackground.ToString() + ".pac";
            Size = new Vector2(320, 200);

            if (grid != null)
            {
                Windows -= grid;
                grid = null;
            }

            if (classic.InventoryRoom != null)
            {
                Music = classic.InventoryRoom.IsWaterSource ? "22_MUS 22_HSC.ogg" : "04_MUS 04_HSC.ogg";

                grid = new ItemGridWindow(app);
                grid.LockPositions = true;
                grid.DoubleLayered = !classic.InventoryRoom.IsWaterSource;
                grid.Position = new Vector2(160, classic.InventoryRoom.IsWaterSource ? 128 : 20);
                grid.Spacing = new Vector2(4, 4);
                grid.Grid = new Vector2(4, classic.InventoryRoom.IsWaterSource ? 2 : 5);
                grid.Layer++;
                grid.LeftClickItemEvent += OnLeftClickItemRoom;
                grid.RightClickItemEvent += OnRightClickItemRoom;
                Windows += grid;

                grid.Add(classic.InventoryRoom.Items);

                // group drinks water
                if (classic.InventoryRoom.IsWaterSource)
                {
                    int r = group.Drink(leader, classic.Game.World.ActiveLocationObj.Source.Reserve);
                    classic.Game.World.ActiveLocationObj.Source.Reserve = r;
                }
            }
            else if (classic.PickItems != null)
            {
                Music = "04_MUS 04_HSC.ogg";
                
                grid = new ItemGridWindow(app);
                grid.Position = new Vector2(170, 10);
                grid.Spacing = new Vector2(2, 2);
                grid.Grid = new Vector2(4, 5);
                grid.Layer++;
                grid.LeftClickItemEvent += OnLeftClickItemRoom;
                grid.RightClickItemEvent += OnRightClickItemRoom;
                Windows += grid;

                grid.Add(classic.PickItems);
            }
            else
                Music = "04_MUS 04_HSC.ogg";
        }

        void OnButtonExit()
        {
            app.SceneManager.PreviousScene();
        }

        void OnLeftClickItemInventory(Framework.States.StateObject state)
        {
            BurntimeClassic classic = app as BurntimeClassic;

            if (classic.InventoryRoom != null)
            {
                if (classic.InventoryRoom.IsWaterSource && classic.InventoryRoom.Items.Count == 8)
                    return;
                if (!classic.InventoryRoom.IsWaterSource && classic.InventoryRoom.Items.Count == 32)
                    return;

                Item item = (Item)state;

                classic.InventoryRoom.Items.Add(item);
                inventory.ActiveCharacter.Items.Remove(item);

                // fill up empty bottles
                if (classic.InventoryRoom.IsWaterSource)
                {
                    if (item.Type.Full != null && item.Type.Full.WaterValue != 0)
                    {
                        if (classic.Game.World.ActiveLocationObj.Source.Reserve >= item.Type.Full.WaterValue)
                        {
                            classic.Game.World.ActiveLocationObj.Source.Reserve -= item.Type.Full.WaterValue;
                            item.MakeFull();
                        }
                    }
                }

                grid.Add(item);
                inventory.Grid.Remove(item);
            }
            else if (classic.PickItems != null)
            {
                classic.PickItems.Add(state as Item);
                inventory.ActiveCharacter.Items.Remove(state as Item);

                grid.Add(state as Item);
                inventory.Grid.Remove(state as Item);
            }
        }

        void OnRightClickItemInventory(Framework.States.StateObject state)
        {
            BurntimeClassic classic = app as BurntimeClassic;

            Item item = state as Item;
            // eat
            if (item.FoodValue != 0)
            {
                int left = group.Eat(leader, item.FoodValue);

                // remove item only if somebody actually ate
                if (left < item.FoodValue)
                {
                    if (item.Type.Empty != null)
                    {
                        item.MakeEmpty();
                        inventory.Grid.Update(item);
                    }
                    else
                    {
                        inventory.ActiveCharacter.Items.Remove(item);
                        inventory.Grid.Remove(item);
                    }
                }
            }
            // drink
            else if (item.WaterValue != 0)
            {
                int left = group.Drink(leader, item.WaterValue);

                // remove item only if somebody actually drank
                if (left < item.WaterValue)
                {
                    if (item.Type.Empty != null)
                    {
                        item.MakeEmpty();
                        inventory.Grid.Update(item);
                    }
                    else
                    {
                        inventory.ActiveCharacter.Items.Remove(item);
                        inventory.Grid.Remove(item);
                    }
                }
            }
            else if (item.Type.Full != null)
            {
                // fill up empty bottles
                if (classic.InventoryRoom.IsWaterSource)
                {
                    if (item.Type.Full != null && item.Type.Full.WaterValue != 0)
                    {
                        if (classic.Game.World.ActiveLocationObj.Source.Reserve >= item.Type.Full.WaterValue)
                        {
                            classic.Game.World.ActiveLocationObj.Source.Reserve -= item.Type.Full.WaterValue;
                            item.MakeFull();

                            // refresh item
                            inventory.Grid.Remove(item);
                            inventory.Grid.Add(item);
                        }
                    }
                }
            }
            else if (item.IsSelectable)
            {
                inventory.ActiveCharacter.SelectItem(item);
                inventory.Grid.Selection.Clear();
                if (inventory.ActiveCharacter.Weapon != null)
                    inventory.Grid.Selection.Add(inventory.ActiveCharacter.Weapon);
                if (inventory.ActiveCharacter.Protection != null)
                    inventory.Grid.Selection.Add(inventory.ActiveCharacter.Protection);
            }
            else //if (inventory.ActiveCharacter.Class == CharClass.Technician)
            {
                IItemCollection right = (classic.InventoryRoom == null) ? (IItemCollection)classic.PickItems : classic.InventoryRoom.Items;
                construction = classic.Game.Constructions.GetConstruction(inventory.ActiveCharacter, right, item);
                this.item = item;
                dialog.SetCharacter(inventory.ActiveCharacter, construction.Dialog);
                dialog.Show();
            }
        }

        void OnLeftClickItemRoom(Framework.States.StateObject state)
        {
            BurntimeClassic classic = app as BurntimeClassic;

            if (inventory.ActiveCharacter.Items.Count == 6)
                return;

            if (classic.InventoryRoom != null)
            {
                inventory.ActiveCharacter.Items.Add(state as Item);
                classic.InventoryRoom.Items.Remove(state as Item);

                inventory.Grid.Add(state as Item);
                grid.Remove(state as Item);
            }
            else if (classic.PickItems != null)
            {
                inventory.ActiveCharacter.Items.Add(state as Item);

                classic.PickItems.Remove(state as Item);

                inventory.Grid.Add(state as Item);
                grid.Remove(state as Item);
            }

            inventory.Grid.Selection.Clear();
            if (inventory.ActiveCharacter.Weapon != null)
                inventory.Grid.Selection.Add(inventory.ActiveCharacter.Weapon);
            if (inventory.ActiveCharacter.Protection != null)
                inventory.Grid.Selection.Add(inventory.ActiveCharacter.Protection);
        }

        void OnRightClickItemRoom(Framework.States.StateObject state)
        {
            BurntimeClassic classic = app as BurntimeClassic;
            Item item = state as Item;
            IItemCollection right = (classic.InventoryRoom == null) ? (IItemCollection)classic.PickItems : classic.InventoryRoom.Items;
            
            // eat
            if (item.FoodValue != 0)
            {
                int left = group.Eat(leader, item.FoodValue);

                // remove item only if somebody actually ate
                if (left < item.FoodValue)
                {
                    if (item.Type.Empty != null)
                    {
                        item.MakeEmpty();
                        grid.Update(item);
                    }
                    else
                    {
                        right.Remove(item);
                        grid.Remove(item);
                    }
                }
            }
            // drink
            else if (item.WaterValue != 0)
            {
                int left = group.Drink(leader, item.WaterValue);

                // remove item only if somebody actually drank
                if (left < item.WaterValue)
                {
                    if (item.Type.Empty != null)
                    {
                        item.MakeEmpty();
                        grid.Update(item);
                    }
                    else
                    {
                        right.Remove(item);
                        grid.Remove(item);
                    }
                }
            }
            else //if (inventory.ActiveCharacter.Class == CharClass.Technician)
            {
                construction = classic.Game.Constructions.GetConstruction(inventory.ActiveCharacter, right, item);
                this.item = item;
                dialog.SetCharacter(inventory.ActiveCharacter, construction.Dialog);
                dialog.Show();
            }
        }
    }
}
