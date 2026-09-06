using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.States;
using Burntime.Remaster.GUI;
using Burntime.Data.BurnGfx;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;
using Burntime.Remaster.Maps;

namespace Burntime.Remaster
{
    public class LocationScene : Scene, IMapEntranceHandler, IInteractionHandler, ILogicNotifycationHandler, IMapNavigationScene
    {
        protected override bool UseGamepadDPadNavigation => false;

        public override InputAction ResolveInputAction(InputAction action) => action;

        const int PICKUP_DISTANCE = 20;
        const float NEXT_TURN_HOLD_TIME = 0.6f;
        const float ATTACK_COOLDOWN_TIME = 0.5f;
        const float CHARACTER_NAME_ANNOUNCEMENT_TIME = 1.5f;
        const float CHARACTER_CYCLE_DEBOUNCE_TIME = 0.15f;

        MapView view;
        MainUiOriginalWindow gui;
        MenuWindow menu;
        Image cursorAni;
        DialogWindow dialog;
        InputPromptOverlay promptOverlay;
        InputPromptOverlay previousCharacterPromptOverlay;
        InputPromptOverlay nextCharacterPromptOverlay;
        Maps.MapViewOverlayHoverText hoverInfo;
        Maps.MapViewOverlayNearbyAction nearbyAction;
        Maps.MapViewOverlayCharacters charOverlay;
        Character manuallyMovedCharacter;
        float nextTurnHoldTime;
        bool nextTurnTriggered;
        bool cameraPanActive;
        bool followSelectedCharacter;
        Character lastSelectedCharacter;
        bool groupMenuOpen;
        bool characterCycleLatched;
        float characterCycleDebounce;
        float attackCooldownRemaining;

        private bool fightMode
        {
            get { if (view.Player == null) return false; return view.Player.FightMode; }
            set { if (view.Player != null) view.Player.FightMode = value; }
        }

        public LocationScene(Module App)
            : base(App)
        {
            Size = app.Engine.Resolution.Game;

            view = new MapView(this, App);
            view.Position = new Vector2(16, 0);
            view.Size = new Vector2(Size.x - 32, Size.y - 40);
            //view.Position = new Vector2(0, 0);
            //view.Size = new Vector2(Size.x, Size.y - 16);
            view.MouseClickEvent += OnMouseClickMap;
            view.Overlays.Add(new Maps.MapViewOverlayDroppedItems(App));
            view.Overlays.Add(charOverlay = new Maps.MapViewOverlayCharacters(App));
            view.Overlays.Add(hoverInfo = new Maps.MapViewOverlayHoverText(App));
            view.Overlays.Add(nearbyAction = new Maps.MapViewOverlayNearbyAction(App, hoverInfo));
            view.ClickObject += new EventHandler<ObjectArgs>(view_ClickObject);
            view.Scroll += new EventHandler<MapScrollArgs>(view_Scroll);
            view.ContextMenu += View_ContextMenu;
            Windows += view;

            menu = new MenuWindow(App);
            menu.Layer += 50;
            menu.ShortcutAction = OnMenuShortcut;
            menu.HeldShortcutAction = OnMenuHeldShortcut;
            menu.Hide();
            Windows += menu;

            cursorAni = new Image(App);
            cursorAni.Background = "burngfxani@munt.raw?10-13";
            cursorAni.Background.Animation.Progressive = false;
            cursorAni.Layer += 59;
            Windows += cursorAni;
            // Font rendering adds one layer internally. Starting two below the
            // cursor keeps both prompt backgrounds and text beneath it.
            menu.ExternalPromptLayer = cursorAni.Layer - 2;

            gui = new MainUiOriginalWindow(App);
            gui.Layer += 60;
            Windows += gui;

            dialog = new DialogWindow(app);
            //dialog.Position = new Vector2(33, 20);
            dialog.Position = view.Position + (view.Size - dialog.Size) / 2 - new Vector2(0, 10);
            dialog.Hide();
            dialog.Layer += 55;
            dialog.WindowHide += new EventHandler(dialog_WindowHide);
            dialog.WindowShow += new EventHandler(dialog_WindowShow);
            Windows += dialog;

            Windows += promptOverlay = new InputPromptOverlay(app);
            promptOverlay.AnchorToScreenBottomRight();

            Windows += previousCharacterPromptOverlay = new InputPromptOverlay(app)
            {
                HorizontalAlignment = PositionAlignment.Right,
                VerticalAlignment = PositionAlignment.Right
            };
            Windows += nextCharacterPromptOverlay = new InputPromptOverlay(app)
            {
                HorizontalAlignment = PositionAlignment.Left,
                VerticalAlignment = PositionAlignment.Right,
                Separator = " "
            };
            UpdateCharacterPromptPositions();
        }

        private void View_ContextMenu(Vector2 position, MouseButton button)
        {
            ShowActionsMenu(position, true);
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Size = app.Engine.Resolution.Game;
            view.Size = new Vector2(Size.x - 32, Size.y - 40);
            dialog.Position = view.Position + (view.Size - dialog.Size) / 2 - new Vector2(0, 10);
            gui.SetMapRenderArea(view, Size);
            app.MouseBoundings = view.Boundings;
            promptOverlay.AnchorToScreenBottomRight();
            UpdateCharacterPromptPositions();
        }

        void dialog_WindowShow(object sender, EventArgs e)
        {
            cursorAni.Hide();
        }

        void dialog_WindowHide(object sender, EventArgs e)
        {
            cursorAni.Show();

            if (dialog.Type == ConversationType.Dismiss)
            {
                if (dialog.Result == ConversationActionType.Yes)
                {
                    charOverlay.SelectedCharacter.Dismiss();
                    view.Player.SelectGroup(view.Player.Group);
                }
            }
            else if (dialog.Type == ConversationType.Abandon)
            {
                if (dialog.Result == ConversationActionType.Yes)
                    charOverlay.SelectedCharacter.LeaveCamp();
            }
        }

        void view_ClickObject(object sender, ObjectArgs e)
        {
            if (e.Button == MouseButton.Right)
            {
                ShowActionsMenu(e.Position, true);
                return;
            }

            if (e.Object is Character)
            {
                if (fightMode)
                    AttackCharacter(e.Object as Character);
                else
                    ClickCharacter(e.Object as Character);
            }
            else if (e.Object is DroppedItem)
            {
                if (PICKUP_DISTANCE > (charOverlay.SelectedCharacter.Position - e.Object.MapPosition).Length)
                    OnMenuInventory();
                else
                    MoveCharacter(e.Object);
            }
        }

        void AttackCharacter(Character targetCharacter)
        {
            if (view.Location.IsCity || attackCooldownRemaining > 0)
                return;

            // only if not player owned
            if (view.Player != targetCharacter.Player)
            {
                if (30 > (charOverlay.SelectedCharacter.Position - targetCharacter.Position).Length)
                {
                    TryAttack(charOverlay.SelectedCharacter, targetCharacter);
                }
                else
                {
                    MoveCharacter(targetCharacter);
                }
            }
        }

        bool TryAttack(Character attacker, Character defender)
        {
            if (attackCooldownRemaining > 0)
                return false;

            attacker.Attack(defender);
            attacker.CancelAction();
            attackCooldownRemaining = ATTACK_COOLDOWN_TIME;
            return true;
        }

        void ClickCharacter(Character clickedCharacter)
        {
            // select if player owned char in group or location
            if (clickedCharacter.Player == view.Player)
            {
                charOverlay.SelectedCharacter.CancelAction();
                if (!view.Player.SelectCharacter(clickedCharacter)) // false if already selected
                    OnMenuInventory();
            }
            else
            {
                // otherwise try to talk
                if (clickedCharacter.Class != CharClass.Dog && !view.Player.Group.Contains(clickedCharacter))
                {
                    if (30 > (charOverlay.SelectedCharacter.Position - clickedCharacter.Position).Length)
                    {
                        dialog.SetCharacter(charOverlay.SelectedCharacter, clickedCharacter);
                        dialog.Show();

                        charOverlay.SelectedCharacter.CancelAction();
                    }
                    else
                    {
                        MoveCharacter(clickedCharacter);
                        clickedCharacter.Mind.RequestToTalk();
                    }
                }
            }
        }

        void view_Scroll(object sender, MapScrollArgs e)
        {
            view.Player.LocationScrollPosition = e.Offset;
        }

        public override bool OnInputAction(InputAction action)
        {
            if (action == InputAction.NextTurn)
                return true;

            if (action == InputAction.Back)
            {
                ShowActionsMenu(view.Boundings.Center,
                    app.LastInputMode == InputMode.Mouse);
                return true;
            }

            if (action == InputAction.WorldMap)
            {
                OnMenuMap();
                return true;
            }

            if (action == InputAction.Options)
            {
                app.SceneManager.SetScene("OptionsScene");
                return true;
            }

            if (action == InputAction.Statistics)
            {
                app.SceneManager.SetScene("StatisticsScene");
                return true;
            }

            if (action == InputAction.Inventory)
            {
                OnMenuInventory();
                return true;
            }

            if (action == InputAction.LocationInfo)
            {
                OnMenuInfo();
                return true;
            }

            if (action == InputAction.LeftArea || action == InputAction.RightArea)
            {
                if (app.LastInputMode == InputMode.Gamepad)
                {
                    if (characterCycleLatched || characterCycleDebounce > 0)
                        return true;

                    characterCycleLatched = true;
                    characterCycleDebounce = CHARACTER_CYCLE_DEBOUNCE_TIME;
                }
                SelectAdjacentGroupCharacter(action == InputAction.LeftArea ? -1 : 1);
                return true;
            }

            if (action == InputAction.SceneAction)
            {
                ShowGroupMenu(view.Boundings.Center, false);
                return true;
            }

            if (action == InputAction.ToggleInteractionMode)
            {
                if (app.LastInputMode == InputMode.Mouse && !view.Location.IsCity)
                {
                    if (fightMode)
                        OnMenuSpeak();
                    else
                        OnMenuFight();
                }
                return true;
            }

            bool directionalInputActive = app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad;

            if (directionalInputActive && action == InputAction.Primary && nearbyAction.EntranceNumber != -1)
            {
                OnMenuSpeak();
                OnClickEntrance(nearbyAction.EntranceNumber, MouseButton.Left);
                return true;
            }

            if (directionalInputActive && action == InputAction.Primary && nearbyAction.Object != null)
            {
                OnMenuSpeak();
                view_ClickObject(view, new ObjectArgs(nearbyAction.Object,
                    nearbyAction.Object.MapPosition + view.ScrollPosition, MouseButton.Left));
                return true;
            }

            if (directionalInputActive && action == InputAction.Secondary)
            {
                if (nearbyAction.Object is Character target)
                    AttackCharacter(target);
                return true;
            }

            if (action is InputAction.MoveUp or InputAction.MoveDown or InputAction.MoveLeft or InputAction.MoveRight)
                return true;

            Vector2 direction = action switch
            {
                InputAction.PanCameraUp => new Vector2(0, -1),
                InputAction.PanCameraDown => new Vector2(0, 1),
                InputAction.PanCameraLeft => new Vector2(-1, 0),
                InputAction.PanCameraRight => new Vector2(1, 0),
                _ => Vector2.Zero
            };
            if (direction != Vector2.Zero)
                return true;

            return false;
        }

        public override bool OnHeldInputAction(InputAction action, float elapsed)
        {
            if (action == InputAction.NextTurn)
            {
                if (!nextTurnTriggered)
                {
                    nextTurnHoldTime += elapsed;
                    if (nextTurnHoldTime >= NEXT_TURN_HOLD_TIME)
                    {
                        nextTurnTriggered = true;
                        OnMenuTurn();
                    }
                }
                return true;
            }

            if (action is InputAction.MoveUp or InputAction.MoveDown or InputAction.MoveLeft or InputAction.MoveRight)
                return true;

            Vector2 direction = action switch
            {
                InputAction.PanCameraUp => new Vector2(0, -1),
                InputAction.PanCameraDown => new Vector2(0, 1),
                InputAction.PanCameraLeft => new Vector2(-1, 0),
                InputAction.PanCameraRight => new Vector2(1, 0),
                _ => Vector2.Zero
            };
            if (direction == Vector2.Zero)
                return false;

            return true;
        }

        public override void OnRender(RenderTarget Target)
        {
            app.Engine.Xbr2IndividualLayer = gui.Layer;

            bool showInteractionMode = app.MouseInputVisible && !dialog.IsVisible;
            if (cursorAni.IsVisible != showInteractionMode)
                cursorAni.IsVisible = showInteractionMode;

            if (app.MouseImage != null)
            {
                cursorAni.Position = app.DeviceManager.Mouse.Position + new Vector2(8, 11);

                if (app.MouseInputVisible)
                {
                    var layer = Target.Layer;
                    Target.Layer = gui.Layer - 1;
                    Target.DrawSprite(app.DeviceManager.Mouse.Position, app.MouseImage);
                    Target.Layer = layer;
                }
            }
        }

        public override void OnUpdate(float Elapsed)
        {
            attackCooldownRemaining = System.Math.Max(0,
                attackCooldownRemaining - Elapsed);
            characterCycleDebounce = System.Math.Max(0, characterCycleDebounce - Elapsed);
            if (characterCycleLatched &&
                !app.IsInputActionDown(InputAction.LeftArea) &&
                !app.IsInputActionDown(InputAction.RightArea))
            {
                characterCycleLatched = false;
            }

            UpdatePromptOverlay();
            ResetNextTurnHoldIfReleased();
            UpdateCameraPan(Elapsed);
            hoverInfo.ShowAllEntrances = app.IsInputActionDown(InputAction.ShowEntrances);

            ClassicGame game = app.GameState as ClassicGame;
            Character selectedCharacter = game.World.ActivePlayerObj.SelectedCharacter;
            if (selectedCharacter != lastSelectedCharacter)
            {
                lastSelectedCharacter = selectedCharacter;
                gui.UpdatePlayer();
                followSelectedCharacter = !app.MouseInputVisible;
            }

            bool manualMovementActive = UpdateManualCharacterMovement();
            game.World.Update(Elapsed);

            game.World.ActiveLocationObj.Update(Elapsed);
            game.World.ActivePlayerObj.Update(Elapsed);

            if (app.MouseInputVisible)
                followSelectedCharacter = false;

            if (followSelectedCharacter && selectedCharacter != null)
                followSelectedCharacter = !view.FollowWithinMiddleThird(
                    selectedCharacter.Position, Elapsed);
            else if (manualMovementActive && charOverlay.SelectedCharacter != null)
                view.FollowWithinMiddleThird(charOverlay.SelectedCharacter.Position, Elapsed);

            SyncGamepadCursor();

            if (game.World.Time <= 0 || game.World.ActivePlayerObj.IsDead)
            {
                app.ActiveClient.Finish();
                app.SceneManager.SetScene("WaitScene");
            }

            if (charOverlay.SelectedCharacter.IsDead)
                view.Player.SelectGroup(view.Player.Group);
        }

        void UpdatePromptOverlay()
        {
            if (dialog.IsVisible || menu.IsVisible)
            {
                promptOverlay.SetPrompts();
                SetCharacterPrompts(false);
                return;
            }

            if (app.LastInputMode == InputMode.Mouse)
            {
                SetCharacterPrompts(false);
                List<InputPrompt> mousePrompts = [];
                GuiString? mousePrimaryLabel = null;
                if (view.ActiveEntrance >= 0 && !fightMode)
                {
                    mousePrimaryLabel = "@prompts?26";
                }
                else if (view.HoveredObject is DroppedItem)
                {
                    mousePrimaryLabel = "@prompts?23";
                }
                else if (view.HoveredObject is Character hoveredCharacter)
                {
                    if (fightMode && hoveredCharacter.Player != view.Player)
                        mousePrimaryLabel = "@prompts?38";
                    else if (!fightMode && hoveredCharacter.Player == view.Player)
                        mousePrimaryLabel = hoveredCharacter == charOverlay.SelectedCharacter
                            ? "@prompts?41"
                            : "@prompts?31";
                    else if (!fightMode && hoveredCharacter.Class != CharClass.Dog &&
                        !view.Player.Group.Contains(hoveredCharacter))
                        mousePrimaryLabel = "@prompts?34";
                }

                if (mousePrimaryLabel != null)
                {
                    mousePrompts.Add(new(InputAction.Primary, mousePrimaryLabel)
                    {
                        PreferredMouseControl = MouseButton.Left
                    });
                }
                mousePrompts.Add(new(InputAction.Back, "...")
                {
                    PreferredMouseControl = MouseButton.Right
                });
                promptOverlay.SetPrompts(mousePrompts.ToArray());
                return;
            }

            List<InputPrompt> prompts = [];
            SetCharacterPrompts(app.LastInputMode is (InputMode.Keyboard or InputMode.Gamepad) &&
                view.Player.Group.Count > 1);
            if (app.LastInputMode is (InputMode.Keyboard or InputMode.Gamepad))
            {
                GuiString? primaryLabel = null;
                if (nearbyAction.EntranceNumber != -1)
                    primaryLabel = "@prompts?26";
                else if (nearbyAction.Object is DroppedItem)
                    primaryLabel = "@prompts?23";
                else if (nearbyAction.Object is Character primaryTarget)
                {
                    if (primaryTarget.Player == view.Player)
                        primaryLabel = "@prompts?31";
                    else if (primaryTarget.Class != CharClass.Dog &&
                        !view.Player.Group.Contains(primaryTarget))
                        primaryLabel = "@prompts?34";
                }
                if (primaryLabel != null)
                {
                    prompts.Add(new(InputAction.Primary, primaryLabel)
                    {
                        PreferredKeyboardControl = new Key(' '),
                        PreferredGamepadControl = GamepadControl.A
                    });
                }
            }
            if (!view.Location.IsCity && nearbyAction.Object is Character target &&
                target.Player != view.Player)
                prompts.Add(new(InputAction.Secondary, "@prompts?38"));
            prompts.Add(new(InputAction.Back, "...")
            {
                PreferredKeyboardControl = new Key(SystemKey.Escape),
                PreferredGamepadControl = GamepadControl.B
            });

            promptOverlay.SetPrompts(prompts.ToArray());
        }

        void SetCharacterPrompts(bool visible)
        {
            if (!visible)
            {
                previousCharacterPromptOverlay.SetPrompts();
                nextCharacterPromptOverlay.SetPrompts();
                return;
            }

            previousCharacterPromptOverlay.SetPrompts(
                new InputPrompt(InputAction.LeftArea, "")
                {
                    PreferredGamepadControl = GamepadControl.LeftShoulder
                });
            nextCharacterPromptOverlay.SetPrompts(
                new InputPrompt(InputAction.RightArea, "")
                {
                    PreferredGamepadControl = GamepadControl.RightShoulder
                },
                new InputPrompt(InputAction.SceneAction, "")
                {
                    PreferredGamepadControl = GamepadControl.Y
                });
            UpdateCharacterPromptPositions();
        }

        void UpdateCharacterPromptPositions()
        {
            const int portraitGap = 2;
            const int bottomMargin = 6;
            Rect portrait = gui.PlayerFaceBounds;
            int baseline = app.Engine.Resolution.Game.y - bottomMargin;
            previousCharacterPromptOverlay.Position = new Vector2(
                portrait.Left - portraitGap, baseline);
            nextCharacterPromptOverlay.Position = new Vector2(
                portrait.Right + portraitGap, baseline);
        }

        void SelectAdjacentGroupCharacter(int direction)
        {
            var group = view.Player.Group;
            if (group.Count <= 1)
                return;

            int targetIndex;
            if (!view.Player.SingleMode)
            {
                // All is a separate state. LB selects the singular boss while
                // RB starts the follower cycle at its first member.
                targetIndex = direction > 0 ? 1 : 0;
            }
            else
            {
                int currentIndex = 0;
                for (int i = 0; i < group.Count; i++)
                {
                    if (group[i] == view.Player.SelectedCharacter)
                    {
                        currentIndex = i;
                        break;
                    }
                }
                targetIndex = (currentIndex + direction + group.Count) % group.Count;
            }

            Character target = group[targetIndex];

            // Normalize the previous singular selection first so it resumes its
            // follower role before another character becomes player-controlled.
            view.Player.SelectGroup(group);
            if (target == view.Player.Character)
                view.Player.SelectGroup(target);
            else
                view.Player.SelectCharacter(target);

            nearbyAction.AnnounceCharacter(target, CHARACTER_NAME_ANNOUNCEMENT_TIME);
            followSelectedCharacter = true;
        }

        void SyncGamepadCursor()
        {
            if (app.MouseInputVisible)
                return;

            Vector2? mapPosition = nearbyAction.Position;
            if (!mapPosition.HasValue && charOverlay.SelectedCharacter != null)
                mapPosition = charOverlay.SelectedCharacter.Position;
            if (!mapPosition.HasValue)
                return;

            app.DeviceManager.MouseMove(view.Boundings.Position + view.ScrollPosition + mapPosition.Value);
        }

        void ResetNextTurnHoldIfReleased()
        {
            if (app.IsInputActionDown(InputAction.NextTurn))
                return;

            nextTurnHoldTime = 0;
            nextTurnTriggered = false;
        }

        void UpdateCameraPan(float elapsed)
        {
            Vector2 direction = Vector2.Zero;
            foreach (InputAction action in app.InputManager.ActionsDown)
            {
                direction += action switch
                {
                    InputAction.PanCameraUp => new Vector2(0, -1),
                    InputAction.PanCameraDown => new Vector2(0, 1),
                    InputAction.PanCameraLeft => new Vector2(-1, 0),
                    InputAction.PanCameraRight => new Vector2(1, 0),
                    _ => Vector2.Zero
                };
            }

            if (direction == Vector2.Zero)
            {
                if (cameraPanActive)
                {
                    cameraPanActive = false;
                    followSelectedCharacter = !app.MouseInputVisible;
                }
                return;
            }

            cameraPanActive = true;
            followSelectedCharacter = false;
            view.Pan(direction, elapsed);
        }

        bool UpdateManualCharacterMovement()
        {
            Character selectedCharacter = charOverlay.SelectedCharacter;
            if (selectedCharacter == null)
                return false;

            Vector2 direction = Vector2.Zero;
            foreach (InputAction action in app.InputManager.ActionsDown)
            {
                direction += action switch
                {
                    InputAction.MoveUp => new Vector2(0, -1),
                    InputAction.MoveDown => new Vector2(0, 1),
                    InputAction.MoveLeft => new Vector2(-1, 0),
                    InputAction.MoveRight => new Vector2(1, 0),
                    _ => Vector2.Zero
                };
            }

            if (manuallyMovedCharacter != null && manuallyMovedCharacter != selectedCharacter)
            {
                if (manuallyMovedCharacter.Path is PathFinding.ManualPath previousPath)
                    previousPath.Direction = Vector2f.Zero;
                manuallyMovedCharacter = null;
            }

            if (direction != Vector2.Zero)
            {
                if (selectedCharacter.Path is not PathFinding.ManualPath)
                {
                    selectedCharacter.CancelAction();
                    selectedCharacter.Path = app.GameState.Container.Create<PathFinding.ManualPath>();
                    selectedCharacter.Path.MoveTo = selectedCharacter.Position;
                }

                manuallyMovedCharacter = selectedCharacter;
                ((PathFinding.ManualPath)selectedCharacter.Path).Direction = direction;
                return true;
            }
            else if (selectedCharacter.Path is PathFinding.ManualPath)
            {
                ((PathFinding.ManualPath)selectedCharacter.Path).Direction = Vector2f.Zero;
                manuallyMovedCharacter = null;
            }

            return false;
        }

        void EnsureAutomaticPath(Character character)
        {
            if (character.Path is PathFinding.ComplexPath)
                return;

            character.Path = app.GameState.Container.Create<PathFinding.ComplexPath>();
            character.Path.MoveTo = character.Position;
        }

        protected override void OnActivateScene(object parameter)
        {
            nextTurnHoldTime = 0;
            nextTurnTriggered = false;
            cameraPanActive = false;
            followSelectedCharacter = false;
            characterCycleLatched = false;
            characterCycleDebounce = 0;

            app.RenderMouse = false;
            app.MouseBoundings = view.Boundings;

            ClassicGame game = app.GameState as ClassicGame;

            if (BurntimeClassic.Instance.PreviousPlayerId != -1 &&
                BurntimeClassic.Instance.PreviousPlayerId != game.CurrentPlayerIndex)
            {
                // play player changed sound
                BurntimeClassic.Instance.Engine.Music.PlaySound("sounds/change.ogg");
            }
            BurntimeClassic.Instance.PreviousPlayerId = game.CurrentPlayerIndex;

            view.Map = (MapData)game.World.ActiveLocationObj.Map.MapData;
            view.Location = game.World.ActiveLocationObj;
            view.Player = game.World.ActivePlayerObj;
            view.Player.Group.IgnoreRangeFilter = false;
            lastSelectedCharacter = view.Player.SelectedCharacter;

            if (view.Player.RefreshScrollPosition)
                view.CenterTo(view.Player.SelectedCharacter.Position);
            else
                view.ScrollPosition = view.Player.LocationScrollPosition;

            // Returning from another scene may restore or otherwise reposition the
            // camera without going through UpdateCameraPan. In directional input
            // modes, resume the same automatic follow used after manual camera pan.
            followSelectedCharacter = !app.MouseInputVisible;
            gui.UpdatePlayer();

            view.Player.OnMainMap = false;

            game.MainMapView = false;

            app.GameState.Container.AddNotifycationHandler(this);

            // refresh speak/fight mode
            if (view.Location.IsCity && fightMode)
                fightMode = false;

            if (fightMode)
                OnMenuFight();
            else
                OnMenuSpeak();
        }

        protected override void OnInactivateScene()
        {
            view.Player?.SelectedCharacter?.CancelAction();
            manuallyMovedCharacter = null;
            app.RenderMouse = true;
            app.MouseBoundings = null;
            app.GameState.Container.RemoveNotifycationHandler(this);
        }

        void ShowActionsMenu(Vector2 position, bool openedByMouse)
        {
            groupMenuOpen = false;
            menu.AlternatePrimaryAction = InputAction.None;
            menu.Clear();

            void AddLine(GuiString text, Action command,
                InputShortcut shortcut = default)
            {
                menu.AddLine(text, new CommandHandler(command), shortcut);
            }

            // 0: interaction mode
            if (!view.Location.IsCity && openedByMouse)
            {
                if (fightMode)
                    AddLine("@burn?350", OnMenuSpeak, new(InputAction.ToggleInteractionMode));
                else
                    AddLine("@burn?352", OnMenuFight, new(InputAction.ToggleInteractionMode));
            }

            void AddMapLine() => AddLine("@burn?362", OnMenuMap, new(InputAction.WorldMap)
            {
                PreferredKeyboardControl = new Key('v'),
                PreferredMouseKeyboardControl = new Key('m')
            });
            void AddInventoryLine() => AddLine("@burn?367", OnMenuInventory,
                new(InputAction.Inventory)
                {
                    PreferredKeyboardControl = new Key('e'),
                    PreferredMouseKeyboardControl = new Key('i')
                });
            void AddInfoLine() => AddLine("@burn?351", OnMenuInfo,
                new(InputAction.LocationInfo)
                {
                    PreferredGamepadControl = GamepadControl.DPadRight
                });

            if (openedByMouse)
            {
                if (view.Location.IsCity)
                {
                    AddInventoryLine();
                    AddGroupMenuLines((text, command) => AddLine(text, command));
                    AddMapLine();
                }
                else
                {
                    AddInfoLine();
                    AddInventoryLine();
                    AddMapLine();
                    AddGroupMenuLines((text, command) => AddLine(text, command));
                }
            }
            else
            {
                AddMapLine();
                AddInventoryLine();
                if (!view.Location.IsCity)
                    AddInfoLine();
            }

            AddLine("@burn?361", () => app.SceneManager.SetScene("OptionsScene"),
                new(InputAction.Options));
            AddLine("@burn?357", OnMenuTurn, new(InputAction.NextTurn) { Hold = true });

            menu.Show(position, view.Boundings, openedByMouse);
        }

        bool HasGroupMenuCommands() =>
            charOverlay.SelectedCharacter != null &&
            (view.Player.Group.Count > 1 || charOverlay.SelectedCharacter != view.Player.Character);

        void AddGroupMenuLines(Action<GuiString, Action> addLine)
        {
            if (view.Player.Group.Count > 1)
            {
                if (!view.Player.SingleMode)
                    addLine("@burn?358", OnMenuSingle);
                else
                    addLine("@burn?356", OnMenuAll);
            }

            if (charOverlay.SelectedCharacter != view.Player.Character)
            {
                addLine("@burn?363", OnMenuDismiss);
                if (view.Player.Group.Contains(charOverlay.SelectedCharacter))
                    addLine("@burn?364", OnMenuMakeCamp);
                else if (view.Player.Group.Count < Logic.Group.MAX_PEOPLE)
                    addLine("@burn?365", OnMenuLeaveCamp);
            }
        }

        void ShowGroupMenu(Vector2 position, bool openedByMouse)
        {
            if (!HasGroupMenuCommands())
                return;

            groupMenuOpen = true;
            menu.AlternatePrimaryAction = InputAction.SceneAction;
            menu.Clear();
            AddGroupMenuLines((text, command) =>
                menu.AddLine(text, new CommandHandler(command)));

            menu.Show(position, view.Boundings, openedByMouse);
        }

        bool OnMenuShortcut(InputAction action)
        {
            if (groupMenuOpen)
                return false;

            switch (action)
            {
                case InputAction.SceneAction:
                    if (!HasGroupMenuCommands())
                        return true;
                    menu.Hide();
                    ShowGroupMenu(view.Boundings.Center, false);
                    return true;
                case InputAction.Inventory:
                    menu.Hide();
                    OnMenuInventory();
                    return true;
                case InputAction.Statistics:
                    menu.Hide();
                    app.SceneManager.SetScene("StatisticsScene");
                    return true;
                case InputAction.Options:
                    menu.Hide();
                    app.SceneManager.SetScene("OptionsScene");
                    return true;
                case InputAction.WorldMap:
                    menu.Hide();
                    OnMenuMap();
                    return true;
                case InputAction.LocationInfo:
                    menu.Hide();
                    OnMenuInfo();
                    return true;
                case InputAction.ToggleInteractionMode:
                    if (app.LastInputMode != InputMode.Mouse)
                        return false;
                    menu.Hide();
                    if (!view.Location.IsCity)
                    {
                        if (fightMode)
                            OnMenuSpeak();
                        else
                            OnMenuFight();
                    }
                    return true;
                case InputAction.NextTurn:
                    return true;
                default:
                    return false;
            }
        }

        bool OnMenuHeldShortcut(InputAction action, float elapsed)
        {
            if (groupMenuOpen)
                return false;

            bool handled = action == InputAction.NextTurn && OnHeldInputAction(action, elapsed);
            if (nextTurnTriggered)
                menu.Hide();
            return handled;
        }

        public void OnMenuInfo()
        {
            BurntimeClassic game = app as BurntimeClassic;
            if (game.Game.World.ActiveLocationObj.IsCity)
                return;
            // check camp or location
            game.InfoCity = game.Game.World.ActivePlayerObj.Location;
            app.SceneManager.SetScene("InfoScene");
        }

        public void OnMenuInventory()
        {
            if (charOverlay.SelectedCharacter.IsDead)
                return;

            view.Player.Group.IgnoreRangeFilter = false;
            charOverlay.SelectedCharacter.CancelAction();

            BurntimeClassic classic = app as BurntimeClassic;
            classic.InventoryBackground = -1;
            classic.InventoryRoom = null;
            classic.Game.World.ActiveLocationObj.Items.DropPosition = charOverlay.SelectedCharacter.Position;
            classic.PickItems = new PickItemList(classic.Game.World.ActiveLocationObj.Items, charOverlay.SelectedCharacter.Position, PICKUP_DISTANCE);
            app.SceneManager.SetScene("InventoryScene", charOverlay.SelectedCharacter);
        }

        public void OnMenuFight()
        {
            if (view.Location.IsCity)
            {
                OnMenuSpeak();
                return;
            }

            fightMode = true;
            cursorAni.Background = "burngfxani@munt.raw?14-17";
            cursorAni.Background.Animation.Progressive = false;
        }

        public void OnMenuSpeak()
        {
            fightMode = false;
            cursorAni.Background = "burngfxani@munt.raw?10-13";
            cursorAni.Background.Animation.Progressive = false;
        }

        public void OnMenuAll()
        {
            // set group selection to complete player group
            view.Player.SelectGroup(view.Player.Group);
        }

        public void OnMenuSingle()
        {
            // set group selection to selected character only
            view.Player.SelectGroup(view.Player.SelectedCharacter);
        }

        public void OnMenuDismiss()
        {
            if (charOverlay.SelectedCharacter.IsLastInCamp)
            {
                dialog.SetCharacter(view.Player.Character, charOverlay.SelectedCharacter, ConversationType.Dismiss);
                dialog.Show();
                return;
            }

            charOverlay.SelectedCharacter.Dismiss();
            view.Player.SelectGroup(view.Player.Group);
        }

        public void OnMenuMakeCamp()
        {
            if (view.Location.Player != null)
            {
                if (view.Location.Player != view.Player)
                {
                    dialog.SetCharacter(view.Player.Character, charOverlay.SelectedCharacter, ConversationType.Capture);
                    dialog.Show();
                }
                else
                {
                    charOverlay.SelectedCharacter.JoinCamp();
                    SelectBossAfterKeyboardGarrison();
                }
            }
            else
            {
                charOverlay.SelectedCharacter.JoinCamp();
                SelectBossAfterKeyboardGarrison();

                view.Location.Player = view.Player;
                BurntimeClassic.Instance.Engine.Music.PlaySound("sounds/camp.ogg");
            }
        }

        void SelectBossAfterKeyboardGarrison()
        {
            if (app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad)
                view.Player.SelectGroup(view.Player.Group);
        }

        public void OnMenuLeaveCamp()
        {
            if (charOverlay.SelectedCharacter.IsLastInCamp)
            {
                dialog.SetCharacter(view.Player.Character, charOverlay.SelectedCharacter, ConversationType.Abandon);
                dialog.Show();
                return;
            }

            charOverlay.SelectedCharacter.LeaveCamp();
        }

        public void OnMenuMap()
        {
            app.SceneManager.SetScene("MapScene");
        }

        public void OnMenuTurn()
        {
            app.SceneManager.SetScene("WaitScene");
            app.SceneManager.BlockBlendIn();
            app.ActiveClient.Finish();
            app.SceneManager.UnblockBlendIn();
        }

        public String GetEntranceTitle(int Number)
        {
            BurntimeClassic classic = app as BurntimeClassic;
            Location loc = classic.Game.World.ActiveLocationObj;
            if (loc.AreEntrancesBlockedFor(view.Player))
                return app.ResourceManager.GetString("newburn?103");
            return app.ResourceManager.GetString(loc.Map.Entrances[Number].TitleId);
        }

        public bool OnClickEntrance(int Number, MouseButton Button)
        {
            // do not enter when in fight mode
            if (fightMode)
                return false;

            BurntimeClassic classic = app as BurntimeClassic;
            Location loc = classic.Game.World.ActiveLocationObj;

            MapEntrance entrance = loc.Map.Entrances[Number];

            EntranceObject entranceObject = new EntranceObject(entrance, Number);
            EnsureAutomaticPath(charOverlay.SelectedCharacter);
            charOverlay.SelectedCharacter.Mind.MoveToObject(new InteractionObject(entranceObject,
                loc.Rooms[Number].EntryCondition, this));

            return true;
        }

        public void OnMouseClickMap(Vector2 position, MouseButton button)
        {
            EnsureAutomaticPath(charOverlay.SelectedCharacter);
            charOverlay.SelectedCharacter.Mind.MoveToObject(null);
            charOverlay.SelectedCharacter.Path.MoveTo = position;
        }

        void MoveCharacter(IMapObject obj)
        {
            EnsureAutomaticPath(charOverlay.SelectedCharacter);
            charOverlay.SelectedCharacter.Mind.MoveToObject(new InteractionObject(obj, this));
        }

        bool IInteractionHandler.HandleInteraction(IMapObject obj, Character actor)
        {
            if (actor.IsDead)
                return false;

            if (obj is DroppedItem)
            {
                OnMenuInventory();
                return true;
            }
            else if (obj is Character)
            {
                Character ch = (Character)obj;

                if (view.Player.Group.Contains(ch) || ch.Player == view.Player)
                {
                    return true;
                }
                else if (!fightMode || view.Location.IsCity)
                {
                    if (ch.Class != CharClass.Dog && !view.Player.Group.Contains(ch))
                    {
                        dialog.SetCharacter(actor, ch);
                        dialog.Show();

                        charOverlay.SelectedCharacter.CancelAction();
                    }

                    return true;
                }
                else
                {
                    if (!view.Player.Group.Contains(ch))
                    {
                        TryAttack(actor, ch);

                        return true;
                    }
                }
            }
            else if (obj is EntranceObject)
            {
                return TryEnterRoom((EntranceObject)obj, actor);
            }

            return false;
        }

        private bool TryEnterRoom(EntranceObject entranceObject, Character chr)
        {
            MapEntrance entrance = entranceObject.Data;
            int number = entranceObject.Number;
            BurntimeClassic classic = BurntimeClassic.Instance;

            Condition condition = classic.Game.World.ActiveLocationObj.Rooms[number].EntryCondition;
            if (!condition.Process(charOverlay.SelectedCharacter, out Conversation? hint))
            {
                if (hint is not null)
                {
                    dialog.SetCharacter(chr, hint, true);
                    dialog.Show();
                }
                return false;
            }

            if (view.Location.AreEntrancesBlockedFor(view.Player))
                return true;

            view.Player.Group.IgnoreRangeFilter = !view.Player.SingleMode;
            charOverlay.SelectedCharacter.CancelAction();

            switch (entrance.RoomType)
            {
                case RoomType.Normal:
                case RoomType.WaterSource:
                    classic.InventoryBackground = entrance.Background;
                    classic.InventoryRoom = classic.Game.World.ActiveLocationObj.Rooms[number];
                    app.SceneManager.SetScene("InventoryScene", charOverlay.SelectedCharacter);
                    break;
                case RoomType.Rope:
                    classic.InventoryBackground = 3;
                    classic.InventoryRoom = classic.Game.World.ActiveLocationObj.Rooms[number];
                    app.SceneManager.SetScene("InventoryScene", charOverlay.SelectedCharacter);
                    break;
                case RoomType.Trader:
                    classic.ImageScene = null;
                    classic.ActionAfterImageScene = ActionAfterImageScene.Trader;
                    classic.Game.World.ActiveTraderObj = classic.Game.World.ActiveLocationObj.LocalTrader;
                    switch (entrance.Background)
                    {
                        case 0x0D: classic.ImageScene = "film_10.pac"; break;
                        case 0x11: classic.ImageScene = "film_05.pac"; break;
                    }
                    if (classic.ImageScene != null)
                        app.SceneManager.SetScene("ImageScene");
                    break;
                case RoomType.Pub:
                    classic.InventoryBackground = entrance.Background;
                    if (entrance.Background == 14)
                    {
                        classic.ImageScene = "film_06.pac";
                        classic.ActionAfterImageScene = ActionAfterImageScene.Pub;
                        app.SceneManager.SetScene("ImageScene");
                    }
                    else
                        app.SceneManager.SetScene("PubScene");
                    break;
                case RoomType.Restaurant:
                    classic.InventoryBackground = entrance.Background;
                    app.SceneManager.SetScene("RestaurantScene");
                    break;
                case RoomType.Doctor:
                    app.SceneManager.SetScene("DoctorScene");
                    break;
                case RoomType.Church:
                    app.SceneManager.SetScene("ChurchScene");
                    break;
                case RoomType.Scene:
                    classic.ImageScene = null;
                    classic.ActionAfterImageScene = ActionAfterImageScene.None;
                    switch (entrance.Background)
                    {
                        case 0x0A:
                        case 0x0B:
                        case 0x0C: classic.ImageScene = "film_" + (entrance.Background - 8).ToString("D2") + ".pac"; break;
                        case 0x10: classic.ImageScene = "film_08.pac"; break;
                        case 0x12: classic.ImageScene = "film_09.pac"; break;
                    }
                    if (classic.ImageScene != null)
                        app.SceneManager.SetScene("ImageScene");
                    break;
            }

            return true;
        }

        void ILogicNotifycationHandler.Handle(ILogicNotifycation notify)
        {
            if (notify is AttackEvent)
            {
                AttackEvent eventArgs = (AttackEvent)notify;

                var sprite = (GuiImage)"burngfxani@syssze.raw?208-213";
                sprite.Animation.Speed = 20;
                view.Particles.Add(new StaticAnimationParticle(sprite, eventArgs.Attacker.Position));
                view.Particles.Add(new StaticAnimationParticle(sprite.Clone(), eventArgs.Defender.Position));

                // play sounds only for human player interactions
                if (eventArgs.Attacker.Player?.Type != PlayerType.Human &&
                    eventArgs.Defender.Player?.Type != PlayerType.Human)
                    return;

                if ((eventArgs.Attacker.IsDead && eventArgs.Attacker.Class != CharClass.Dog)
                    || (eventArgs.Defender.IsDead && eventArgs.Defender.Class != CharClass.Dog))
                {
                    app.Engine.Music.PlaySound("sounds/hit-die.ogg");
                }
                else if ((!eventArgs.Attacker.IsDead && eventArgs.Attacker.Class == CharClass.Dog)
                    || (!eventArgs.Defender.IsDead && eventArgs.Defender.Class == CharClass.Dog))
                {
                    app.Engine.Music.PlaySound("sounds/hit-barf.ogg");
                }
                else
                {
                    app.Engine.Music.PlaySound("sounds/hit.ogg");
                }
            }
        }
    }
}
