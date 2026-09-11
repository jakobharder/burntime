using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster.Scenes
{
    class InventoryScene : Scene
    {
        public override bool UseDiagonalGamepadNavigation => true;

        InventoryWindow inventory;
        ItemGridWindow grid;
        GuiFont waterSourceFont;
        DialogWindow dialog;
        Button exitButton;
        Construction construction;
        Item item;
        ICharacterCollection group;
        Character leader;
        bool roomAreaActive;

        public InventoryScene(Module app)
            : base(app)
        {
            Background = "hint2.pac";
            Size = new Vector2(320, 200);
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            inventory = new InventoryWindow(app, InventorySide.Left);
            inventory.Position = new Vector2(2, 5);
            inventory.LeftClickItemEvent += OnLeftClickItemInventory;
            inventory.RightClickItemEvent += OnRightClickItemInventory;
            inventory.Grid.MouseFocusChanged += OnMouseFocusChanged;
            inventory.Grid.FocusEmptied += OnFocusEmptied;
            AddGridPrompts(inventory.Grid, true);
            Windows += inventory;

            exitButton = new Button(app);
            exitButton.Position = new Vector2(25, 183);
            exitButton.Text = app.ResourceManager.GetString("burn?354");
            exitButton.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(92, 92, 148));
            exitButton.HoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            exitButton.Command += OnButtonExit;
            exitButton.SetTextOnly();
            Windows += exitButton;

            waterSourceFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray);

            Windows += dialog = new DialogWindow(app)
            {
                Position = new Vector2(33, 20),
                PlayMusic = false
            };
            dialog.Hide();
            dialog.Layer += 55;
            dialog.WindowHide += new EventHandler(dialog_WindowHide);

            Windows += new InputPromptOverlay(app, Prompts,
                InputPromptColorScheme.Hud);
            Prompts.SuppressWhen(() => dialog.IsVisible);
            Prompts.Add(InputAction.Back, "@prompts?17");
            Prompts.Add(InputPattern.HorizontalPaging, "@prompts?30",
                () => inventory.PageCount > 1);
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        void dialog_WindowHide(object? sender, EventArgs e)
        {
            if (dialog.Result == ConversationActionType.Yes)
            {
                BurntimeClassic classic = app as BurntimeClassic;
                IItemCollection right = (classic.InventoryRoom == null) ? (IItemCollection)classic.PickItems : classic.InventoryRoom.Items;

                classic.Game.Constructions.Construct(construction, inventory.ActiveCharacter, right, item, classic.Game);

                inventory.OnSelectPage();

                // grid is not available if inventory is shown while on main map
                if (grid != null)
                {
                    grid.Clear();
                    grid.Add(right);
                }

                EnsureNonEmptyArea();
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

        bool CanTransferSelectedItem(ItemGridWindow source)
        {
            Item? focusedItem = source.FocusedItem;
            return focusedItem != null && grid != null &&
                (source == inventory.Grid
                    ? grid.Count < grid.MaxCount
                    : inventory.Grid.Count < inventory.Grid.MaxCount);
        }

        void AddGridPrompts(ItemGridWindow promptGrid, bool isInInventory)
        {
            promptGrid.Prompts.Add(InputAction.Primary,
                isInInventory ? "@prompts?39" : "@prompts?37",
                () => CanTransferSelectedItem(promptGrid));
            promptGrid.Prompts.AddDynamic(
                () => GetItemPrompt(promptGrid.FocusedItem, isInInventory),
                new(InputAction.Action, "@prompts?18"),
                new(InputAction.Action, "@prompts?19"),
                new(InputAction.Secondary, "@prompts?20"),
                new(InputAction.Secondary, "@prompts?21"),
                new(InputAction.Secondary, "@prompts?22"),
                new(InputAction.Secondary, "@prompts?23"),
                new(InputAction.Action, "@prompts?24"));
        }

        InputPrompt? GetItemPrompt(Item? selectedItem,
            bool isInInventory)
        {
            if (selectedItem == null)
                return null;
            BurntimeClassic classic = app as BurntimeClassic;
            if (selectedItem.FoodValue != 0)
            {
                if (!CanSupplyGroup(character => character.Food < character.MaxFood))
                    return null;
                return new(InputAction.Action, "@prompts?18");
            }
            if (selectedItem.WaterValue != 0)
            {
                if (!CanSupplyGroup(character => character.Water < character.MaxWater))
                    return null;
                return new(InputAction.Action, "@prompts?19");
            }
            if (selectedItem.Type.Full != null && selectedItem.Type.Full.WaterValue != 0)
            {
                if (isInInventory && classic.InventoryRoom?.IsWaterSource == true)
                    return new(InputAction.Action, "@prompts?24");
                return null;
            }

            if (isInInventory && selectedItem.IsSelectable)
            {
                bool isEquipped = inventory.ActiveCharacter.Weapon == selectedItem ||
                    inventory.ActiveCharacter.Protection == selectedItem;
                return new(InputAction.Secondary,
                    isEquipped ? "@prompts?22" : "@prompts?21");
            }

            IItemCollection? roomItems = classic.InventoryRoom != null
                ? classic.InventoryRoom.Items
                : classic.PickItems;
            if (roomItems != null && classic.Game.Constructions.HasConstruction(
                inventory.ActiveCharacter, roomItems, selectedItem))
                return new(InputAction.Secondary, "@prompts?20");

            return new(InputAction.Secondary, "@prompts?23");
        }

        bool CanSupplyGroup(Func<Character, bool> needsSupply)
        {
            foreach (Character character in group)
            {
                if (group.IsInRange(leader, character) && needsSupply(character))
                    return true;
            }

            return false;
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
                Music = classic.InventoryBackground switch
                {
                    0 => "room2",
                    1 or 2 => "room",
                    3 => "cave",
                    4 or 5 => "room_water",
                    6 => "well",
                    8 => "open_water",
                    7 => "cave_water",
                    _ => classic.InventoryRoom.IsWaterSource ? "open_water" : "room"
                };

                grid = new ItemGridWindow(app);
                grid.UnifiedSelection = true;
                grid.LockPositions = true;
                grid.DoubleLayered = !classic.InventoryRoom.IsWaterSource;
                grid.Position = new Vector2(160, classic.InventoryRoom.IsWaterSource ? 128 : 20);
                grid.Spacing = new Vector2(4, 4);
                grid.Grid = new Vector2(4, classic.InventoryRoom.IsWaterSource ? 2 : 5);
                if (app.IsNewGfx && classic.InventoryRoom.IsWaterSource)
                    grid.BackgroundColor = new PixelColor(128, 0, 0, 0);
                grid.Layer++;
                grid.LeftClickItemEvent += OnLeftClickItemRoom;
                grid.RightClickItemEvent += OnRightClickItemRoom;
                grid.MouseFocusChanged += OnMouseFocusChanged;
                grid.FocusEmptied += OnFocusEmptied;
                AddGridPrompts(grid, false);
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
                Music = "room";
                
                grid = new ItemGridWindow(app);
                grid.UnifiedSelection = true;
                grid.Position = new Vector2(170, 10);
                grid.Spacing = new Vector2(2, 2);
                grid.Grid = new Vector2(4, 5);
                grid.Layer++;
                grid.LeftClickItemEvent += OnLeftClickItemRoom;
                grid.RightClickItemEvent += OnRightClickItemRoom;
                grid.MouseFocusChanged += OnMouseFocusChanged;
                grid.FocusEmptied += OnFocusEmptied;
                AddGridPrompts(grid, false);
                Windows += grid;

                grid.Add(classic.PickItems);
            }
            else
                Music = "room";

            roomAreaActive = grid != null && grid.HasFocusableItems;
            inventory.Grid.ResetFocus();
            grid?.ResetFocus();
            UpdateActiveArea();
        }

        void OnMouseFocusChanged(ItemGridWindow focusedGrid)
        {
            roomAreaActive = grid != null && focusedGrid == grid;
            UpdateActiveArea();
        }

        void OnFocusEmptied(ItemGridWindow emptiedGrid, Vector2 previousPosition)
        {
            ItemGridWindow targetGrid = emptiedGrid == inventory.Grid ? grid : inventory.Grid;
            Vector2 direction = emptiedGrid == inventory.Grid ? new Vector2(1, 0) : new Vector2(-1, 0);
            if (targetGrid?.FocusEdge(direction, previousPosition) != true)
                return;

            roomAreaActive = targetGrid == grid;
            UpdateActiveArea();
        }

        void OnButtonExit()
        {
            app.SceneManager.PreviousScene();
        }

        public override bool OnVKeyPress(SystemKey key)
        {
            if (key != SystemKey.Escape)
                return false;

            OnButtonExit();
            return true;
        }

        public override bool OnInputAction(InputAction action)
        {
            if (action == InputAction.Back)
            {
                OnButtonExit();
                return true;
            }

            if (action == InputAction.LeftArea)
            {
                inventory.SelectAdjacentPage(-1);
                UpdateActiveArea();
                return true;
            }

            if (action == InputAction.RightArea)
            {
                inventory.SelectAdjacentPage(1);
                UpdateActiveArea();
                return true;
            }

            ItemGridWindow activeGrid = roomAreaActive && grid != null ? grid : inventory.Grid;
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
                Vector2? sourcePosition = activeGrid.FocusPosition;
                bool moved = activeGrid.MoveFocus(direction);
                if (!moved && direction.x == 0 && direction.y != 0 &&
                    activeGrid == inventory.Grid &&
                    inventory.SelectAdjacentPage(direction.y > 0 ? 1 : -1))
                {
                    if (sourcePosition.HasValue)
                        inventory.Grid.FocusPageEdge(direction, sourcePosition.Value);
                    UpdateActiveArea();
                }
                else if (!moved && direction.x != 0)
                {
                    ItemGridWindow targetGrid = roomAreaActive ? inventory.Grid : grid;
                    bool selectedTarget = sourcePosition.HasValue
                        ? targetGrid?.FocusEdge(direction, sourcePosition.Value) == true
                        : targetGrid?.EnsureFocus() == true;
                    if (selectedTarget)
                    {
                        roomAreaActive = !roomAreaActive;
                        UpdateActiveArea();
                    }
                }
                return true;
            }

            if (action is InputAction.Primary or InputAction.Secondary or
                InputAction.Action)
            {
                activeGrid.ActivateFocusedItem(action != InputAction.Primary);
                EnsureNonEmptyArea();
                return true;
            }

            return false;
        }

        void UpdateActiveArea()
        {
            inventory.Grid.FocusVisible = !roomAreaActive || grid == null;
            if (grid != null)
                grid.FocusVisible = roomAreaActive;
        }

        void EnsureNonEmptyArea()
        {
            ItemGridWindow activeGrid = roomAreaActive && grid != null ? grid : inventory.Grid;
            if (activeGrid.HasFocusableItems)
                return;

            ItemGridWindow otherGrid = roomAreaActive ? inventory.Grid : grid;
            if (otherGrid != null && otherGrid.HasFocusableItems)
                roomAreaActive = !roomAreaActive;

            UpdateActiveArea();
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
                    classic.Game.World.ActiveLocationObj.Source.RefillItem(item);

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

            EnsureNonEmptyArea();
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
            // refill water
            else if (item.Type.Full != null)
            {
                // fill up empty bottles
                if (classic.InventoryRoom != null && classic.InventoryRoom.IsWaterSource)
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
            // select weapon or protection
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

            EnsureNonEmptyArea();
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

            EnsureNonEmptyArea();
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

            EnsureNonEmptyArea();
        }
    }
}
