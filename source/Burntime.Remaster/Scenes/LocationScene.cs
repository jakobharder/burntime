using System;
using System.Collections.Generic;
using System.Linq;
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
        enum LocationInteractionMode
        {
            Auto,
            Talk,
            Fight
        }

        protected override bool UseGamepadDPadNavigation => false;

        public override InputAction ResolveInputAction(InputAction action) => action;

        const int PICKUP_DISTANCE = 20;
        const float NEXT_TURN_HOLD_TIME = 0.6f;
        const float CHARACTER_NAME_ANNOUNCEMENT_TIME = 1.5f;
        const float CHARACTER_CYCLE_DEBOUNCE_TIME = 0.15f;

        MapView view;
        MainUiOriginalWindow gui;
        MenuWindow menu;
        Image cursorAni;
        DialogWindow dialog;
        InputPromptHandle previousCharacterPrompt;
        InputPromptHandle nextCharacterPrompt;
        InputPromptHandle groupActionsPrompt;
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
        LocationInteractionMode interactionMode = LocationInteractionMode.Talk;
        bool cursorShowsFight;
        bool characterCycleLatched;
        float characterCycleDebounce;
        LocalCombatEncounter? combatEncounter;
        float combatRecovery;
        readonly ManualWindow manualWindow;

        public LocationScene(Module App)
            : base(App)
        {
            Size = app.Engine.Resolution.Game;

            view = new MapView(this, App);
            view.SetViewport(new Vector2(16, 0),
                new Vector2(Size.x - 32, Size.y - 40));
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

            InputPromptOverlay promptOverlay = new(app, Prompts,
                InputPromptColorScheme.Hud);
            Windows += promptOverlay;
            Windows += manualWindow = new ManualWindow(app, Size);
            manualWindow.WindowShow += (_, _) => view.RequireMouseEdgeScrollReentry();
            manualWindow.WindowHide += (_, _) => view.RequireMouseEdgeScrollReentry();
            Prompts.SuppressWhen(() => dialog.IsVisible || menu.IsVisible ||
                manualWindow.IsVisible);
            Prompts.Add(new InputPrompt(InputAction.Back, "...")
            {
                MouseControl = MouseButton.Right
            }, CanShowActionsMenuPrompt);
            view.Prompts.AddDynamic(InputAction.Primary,
                GetDirectInteractionPrompt,
                "@prompts?26", "@prompts?23", "@prompts?41",
                "@prompts?31", "@prompts?34");
            view.Prompts.AddDynamic(GetFightPrompt,
                new InputPrompt(InputAction.Action, "@prompts?38")
                {
                    MouseControl = MouseButton.Left
                },
                new InputPrompt(InputAction.Action, "@prompts?38")
                {
                    MouseControl = MouseButton.Right
                });

            previousCharacterPrompt = Prompts.Add(
                new InputPrompt(InputAction.LeftArea, ""),
                Vector2.Zero, CanShowCharacterPrompts,
                PositionAlignment.Right, PositionAlignment.Right);
            nextCharacterPrompt = Prompts.Add(
                new InputPrompt(InputAction.RightArea, ""),
                Vector2.Zero, CanShowCharacterPrompts,
                PositionAlignment.Left, PositionAlignment.Right,
                separator: " ");
            groupActionsPrompt = Prompts.Add(
                new InputPrompt(InputAction.Secondary, ""),
                Vector2.Zero, CanShowGroupActionsPrompt,
                PositionAlignment.Left, PositionAlignment.Right,
                separator: " ");
            UpdateCharacterPromptPositions();
        }

        private void View_ContextMenu(Vector2 position, MouseButton button)
        {
            if (IsAutoFightTarget(view.HoveredObject))
                return;
            ShowActionsMenu(position, true);
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Size = app.Engine.Resolution.Game;
            manualWindow?.CenterIn(Size);
            gui.SetMapRenderArea(view, Size);
            dialog.Position = view.Position + (view.Size - dialog.Size) / 2 -
                new Vector2(0, 10);
            app.MouseBoundings = view.Boundings;
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
                    view.Player.SelectGroup(view.Player.Party);
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
                if (IsAutoFightTarget(e.Object) && e.Object is Character target)
                    AttackCharacter(target);
                return;
            }

            if (e.Object is Character character)
            {
                if (interactionMode == LocationInteractionMode.Fight)
                    AttackCharacter(character);
                else
                    ClickCharacter(character);
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
            if (view.Location.IsCity)
                return;

            // only if not player owned
            if (view.Player != targetCharacter.Player)
                TryAttack(charOverlay.SelectedCharacter, targetCharacter);
        }

        bool TryAttack(Character attacker, Character defender)
        {
            if (attacker.IsDead || defender.IsDead || view.Location.IsCity)
                return false;
            if (combatRecovery > 0 || combatEncounter != null && !combatEncounter.IsComplete)
                return false;

            IEnumerable<Character> attackers = attacker.Player != null &&
                attacker.Player.Character == attacker && !attacker.Player.SingleMode
                ? attacker.Player.Party.ToArray()
                : new[] { attacker };
            combatEncounter = new LocalCombatEncounter(attackers, defender);
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
                if (CanTalkToCharacter(clickedCharacter))
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

            if (action == InputAction.PreviousTarget || action == InputAction.NextTarget)
            {
                if (app.LastInputMode == InputMode.Mouse)
                    app.LastInputMode = InputMode.Keyboard;
                nearbyAction.CycleTarget(action == InputAction.PreviousTarget ? -1 : 1);
                return true;
            }

            if (action == InputAction.Action)
            {
                if (app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad &&
                    CanShowFightPrompt() &&
                    nearbyAction.Object is Character target)
                {
                    AttackCharacter(target);
                }
                return true;
            }

            if (action == InputAction.Secondary)
            {
                ShowGroupMenu(view.Boundings.Center, false);
                return true;
            }

            if (action == InputAction.ToggleInteractionMode)
            {
                if (!view.Location.IsCity)
                    ToggleTalkFightMode();
                return true;
            }

            bool directionalInputActive = app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad;

            if (directionalInputActive && action == InputAction.Primary && nearbyAction.EntranceNumber != -1)
            {
                if (interactionMode == LocationInteractionMode.Fight)
                    OnMenuSpeak();
                OnClickEntrance(nearbyAction.EntranceNumber, MouseButton.Left);
                return true;
            }

            if (directionalInputActive && action == InputAction.Primary && nearbyAction.Object != null)
            {
                if (interactionMode == LocationInteractionMode.Fight)
                    OnMenuSpeak();
                view_ClickObject(view, new ObjectArgs(nearbyAction.Object,
                    nearbyAction.Object.MapPosition + view.ScrollPosition, MouseButton.Left));
                return true;
            }

            if (StartsAutomaticCameraFollow(action))
            {
                // A press should latch camera follow. Held movement also corrects
                // the camera below, but without this latch releasing the key
                // stopped an unfinished pan back to the selected character.
                followSelectedCharacter = true;
                return true;
            }

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

        internal static bool StartsAutomaticCameraFollow(InputAction action) =>
            action is InputAction.MoveUp or InputAction.MoveDown or
                InputAction.MoveLeft or InputAction.MoveRight;

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
            UpdateInteractionCursor();

            bool showInteractionMode = app.MouseInputVisible && !dialog.IsVisible &&
                !manualWindow.IsVisible &&
                ShouldShowMouseInteractionCursor();
            if (cursorAni.IsVisible != showInteractionMode)
                cursorAni.IsVisible = showInteractionMode;

            if (app.MouseImage != null)
            {
                cursorAni.Position = app.DeviceManager.Mouse.Position + new Vector2(8, 11);

                if (app.MouseInputVisible && !manualWindow.IsVisible)
                {
                    var layer = Target.Layer;
                    Target.Layer = gui.Layer - 1;
                    Target.DrawSprite(app.DeviceManager.Mouse.Position, app.MouseImage);
                    Target.Layer = layer;
                }
            }
        }

        bool ShouldShowMouseInteractionCursor()
        {
            if (interactionMode == LocationInteractionMode.Auto)
                return false;

            if (interactionMode == LocationInteractionMode.Fight)
            {
                if (view.ActiveEntrance >= 0 || view.HoveredObject is DroppedItem)
                    return false;

                return view.HoveredObject is not Character character ||
                    character.Player != view.Player;
            }

            return view.HoveredObject is Character talkTarget &&
                CanTalkToCharacter(talkTarget);
        }

        void UpdateInteractionCursor()
        {
            bool showFight = interactionMode == LocationInteractionMode.Fight;
            if (cursorShowsFight == showFight)
                return;

            cursorShowsFight = showFight;
            cursorAni.Background = showFight
                ? "burngfxani@munt.raw?14-17"
                : "burngfxani@munt.raw?10-13";
            cursorAni.Background.Animation.Progressive = false;
        }

        bool CanTalkToCharacter(Character character) =>
            character.Player != view.Player && character.Class != CharClass.Dog;

        public override void OnUpdate(float Elapsed)
        {
            characterCycleDebounce = System.Math.Max(0, characterCycleDebounce - Elapsed);
            if (characterCycleLatched &&
                !app.IsInputActionDown(InputAction.LeftArea) &&
                !app.IsInputActionDown(InputAction.RightArea))
            {
                characterCycleLatched = false;
            }

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
            combatRecovery = System.Math.Max(0, combatRecovery - Elapsed);
            combatEncounter?.Update(Elapsed);
            if (combatEncounter?.IsComplete == true)
            {
                combatEncounter = null;
                combatRecovery = 0.75f;
            }

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
                app.SceneManager.BlendMusicThroughNextBridge();
                app.SceneManager.SetScene("WaitScene");
            }

            if (charOverlay.SelectedCharacter.IsDead)
                view.Player.SelectGroup(view.Player.Party);
        }

        GuiString? GetDirectInteractionPrompt()
        {
            if (app.LastInputMode == InputMode.Mouse)
            {
                if (view.ActiveEntrance >= 0 &&
                    interactionMode != LocationInteractionMode.Fight)
                    return "@prompts?26";
                if (view.HoveredObject is DroppedItem)
                    return "@prompts?23";
                if (view.HoveredObject is Character hoveredCharacter &&
                    interactionMode != LocationInteractionMode.Fight)
                {
                    if (hoveredCharacter.Player == view.Player)
                        return hoveredCharacter == charOverlay.SelectedCharacter
                            ? "@prompts?41"
                            : "@prompts?31";
                    if (hoveredCharacter.Class != CharClass.Dog &&
                        !view.Player.Party.Contains(hoveredCharacter))
                        return "@prompts?34";
                }
                return null;
            }

            if (app.LastInputMode is not (InputMode.Keyboard or InputMode.Gamepad))
                return null;
            if (nearbyAction.EntranceNumber != -1)
                return "@prompts?26";
            if (nearbyAction.Object is DroppedItem)
                return "@prompts?23";
            if (nearbyAction.Object is Character primaryTarget)
            {
                if (primaryTarget.Player == view.Player)
                    return "@prompts?31";
                if (primaryTarget.Class != CharClass.Dog &&
                    !view.Player.Party.Contains(primaryTarget))
                    return "@prompts?34";
            }
            return null;
        }

        InputPrompt? GetFightPrompt()
        {
            if (!CanShowFightPrompt())
                return null;

            return new InputPrompt(InputAction.Action, "@prompts?38")
            {
                MouseControl = app.LastInputMode == InputMode.Mouse &&
                    interactionMode == LocationInteractionMode.Auto
                    ? MouseButton.Right
                    : MouseButton.Left
            };
        }

        bool CanShowFightPrompt()
        {
            if (combatRecovery > 0 || combatEncounter != null && !combatEncounter.IsComplete)
                return false;

            if (app.LastInputMode == InputMode.Mouse)
            {
                return (interactionMode is LocationInteractionMode.Auto or
                        LocationInteractionMode.Fight) &&
                    view.HoveredObject is Character hoveredCharacter &&
                    CanFightCharacter(hoveredCharacter);
            }
            return app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad &&
                nearbyAction.Object is Character target && CanFightCharacter(target);
        }

        bool CanShowActionsMenuPrompt() =>
            app.LastInputMode != InputMode.Mouse ||
            !IsAutoFightTarget(view.HoveredObject);

        bool IsAutoFightTarget(IMapObject? target) =>
            interactionMode == LocationInteractionMode.Auto &&
            target is Character character && CanFightCharacter(character);

        bool CanFightCharacter(Character character) =>
            !view.Location.IsCity && character.Player != view.Player;

        bool CanShowCharacterPrompts() =>
            app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad &&
            (view.Player.Party.Count > 1 ||
                (view.Player.SelectedCharacter != null &&
                    !view.Player.Party.Contains(view.Player.SelectedCharacter)));

        bool CanShowGroupActionsPrompt() =>
            app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad &&
            HasGroupMenuCommands();

        void UpdateCharacterPromptPositions()
        {
            const int portraitLeftOffset = -31;
            const int portraitWidth = 68;
            const int portraitGap = 2;
            const int bottomMargin = 6;
            int portraitLeft = app.Engine.Resolution.Game.x / 2 + portraitLeftOffset;
            int portraitRight = portraitLeft + portraitWidth;
            int baseline = app.Engine.Resolution.Game.y - bottomMargin;
            previousCharacterPrompt.UpdatePosition(new Vector2(
                portraitLeft - portraitGap, baseline));
            Vector2 nextPosition = new(portraitRight + portraitGap, baseline);
            nextCharacterPrompt.UpdatePosition(nextPosition);
            groupActionsPrompt.UpdatePosition(nextPosition);
        }

        void SelectAdjacentGroupCharacter(int direction)
        {
            var group = view.Player.Party;
            if (group.Count == 0)
                return;

            int targetIndex;
            if (!group.Contains(view.Player.SelectedCharacter))
            {
                // Camp selections are outside the party cycle. Either direction
                // returns to the boss, including when the party has no followers.
                targetIndex = 0;
            }
            else if (group.Count == 1)
                return;
            else if (!view.Player.SingleMode)
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
                // A held direction is sampled after discrete input. Do not let
                // an arrow key or gamepad stick cancel an attack accepted
                // earlier in this same frame. Once its strike has resolved, a
                // character may move without cancelling the rest of the group.
                bool completedAttack =
                    combatEncounter?.ReleaseAfterCompletedAttack(selectedCharacter) == true;
                if (selectedCharacter.IsCommittedToCombat)
                    return false;
                if (!completedAttack)
                    combatEncounter?.CancelOffense();
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
            interactionMode = LocationInteractionMode.Talk;
            combatEncounter?.Cancel();
            combatEncounter = null;
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
            view.Location.PlaceUnpositionedResidents();
            view.Player = game.World.ActivePlayerObj;
            RecoverUnwalkableCharacters(view.Location, view.Player);
            view.Player.Party.IgnoreRangeFilter = false;
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

            if (view.Location.IsCity &&
                interactionMode == LocationInteractionMode.Fight)
                OnMenuSpeak();
            UpdateInteractionCursor();
        }

        static void RecoverUnwalkableCharacters(Location location, Player player)
        {
            var mask = location.Map.Mask;
            foreach (Character character in location.Characters.Concat(player.Party).Distinct())
            {
                if (character.IsDead)
                    continue;

                Vector2 recoveredPosition = character.Path.GetSceneEntryPosition(
                    mask, character.Position);
                if (recoveredPosition == character.Position)
                    continue;

                character.Position = recoveredPosition;
                character.Path.Stop(recoveredPosition);
            }
        }

        protected override void OnInactivateScene()
        {
            combatEncounter?.Cancel();
            combatEncounter = null;
            view.Player?.SelectedCharacter?.CancelAction();
            manuallyMovedCharacter = null;
            app.RenderMouse = true;
            app.MouseBoundings = null;
            app.GameState.Container.RemoveNotifycationHandler(this);
        }

        void ShowActionsMenu(Vector2 position, bool openedByMouse)
        {
            groupMenuOpen = false;
            menu.FirstLineAction = InputAction.None;
            menu.Clear();

            void AddLine(GuiString text, Action command,
                InputShortcut shortcut = default)
            {
                menu.AddLine(text, new CommandHandler(command), shortcut);
            }

            // 0: interaction mode
            if (!view.Location.IsCity && openedByMouse)
            {
                if (interactionMode == LocationInteractionMode.Fight)
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
            AddLine("@manualui?5", manualWindow.Open);
            AddLine("@burn?357", OnMenuTurn, new(InputAction.NextTurn) { Hold = true });

            menu.Show(position, view.Boundings, openedByMouse);
        }

        bool HasGroupMenuCommands() =>
            charOverlay.SelectedCharacter != null &&
            (view.Player.Party.Count > 1 ||
                !view.Location.IsCity &&
                charOverlay.SelectedCharacter != view.Player.Character);

        void AddGroupMenuLines(Action<GuiString, Action> addLine)
        {
            if (view.Player.Party.Count > 1)
            {
                if (!view.Player.SingleMode)
                    addLine("@burn?358", OnMenuSingle);
                else
                    addLine("@burn?356", OnMenuAll);
            }

            // Cities do not support follower placement. Keep their menu limited
            // to the same group-selection command shown for the boss.
            if (!view.Location.IsCity &&
                charOverlay.SelectedCharacter != view.Player.Character)
            {
                bool inParty = view.Player.Party.Contains(charOverlay.SelectedCharacter);
                if (!inParty && view.Player.Party.Count < Logic.Group.MAX_PEOPLE)
                    addLine("@burn?365", OnMenuLeaveCamp);
                addLine("@burn?363", OnMenuDismiss);
                if (inParty)
                    addLine("@burn?364", OnMenuMakeCamp);
            }
        }

        void ShowGroupMenu(Vector2 position, bool openedByMouse)
        {
            if (!HasGroupMenuCommands())
                return;

            groupMenuOpen = true;
            menu.FirstLineAction = InputAction.Secondary;
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
                case InputAction.Secondary:
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
                        ToggleTalkFightMode();
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

            view.Player.Party.IgnoreRangeFilter = false;
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

            interactionMode = LocationInteractionMode.Fight;
            UpdateInteractionCursor();
        }

        public void OnMenuSpeak()
        {
            interactionMode = LocationInteractionMode.Talk;
            UpdateInteractionCursor();
        }

        void ToggleTalkFightMode()
        {
            if (interactionMode == LocationInteractionMode.Fight)
                OnMenuSpeak();
            else
                OnMenuFight();
        }

        public void OnMenuAll()
        {
            // set group selection to complete player group
            view.Player.SelectGroup(view.Player.Party);
        }

        public void OnMenuSingle()
        {
            // set group selection to selected character only
            view.Player.SelectGroup(view.Player.SelectedCharacter);
        }

        public void OnMenuDismiss()
        {
            dialog.SetCharacter(view.Player.Character, charOverlay.SelectedCharacter, ConversationType.Dismiss);
            dialog.Show();
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
                    GarrisonSelectedCharacter();
            }
            else
            {
                GarrisonSelectedCharacter();
            }
        }

        void GarrisonSelectedCharacter()
        {
            charOverlay.SelectedCharacter.JoinCamp();
            view.Player.SelectGroup(view.Player.Party);
            app.Engine.Music.PlaySound("sounds/camp.ogg");
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
            app.SceneManager.BlendMusicThroughNextBridge();
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
            if (Button == MouseButton.Right)
                return false;

            // do not enter when in fight mode
            if (interactionMode == LocationInteractionMode.Fight)
                return false;

            BurntimeClassic classic = app as BurntimeClassic;
            Location loc = classic.Game.World.ActiveLocationObj;

            MapEntrance entrance = loc.Map.Entrances[Number];

            EntranceObject entranceObject = new EntranceObject(entrance, Number);
            combatEncounter?.CancelOffense();
            EnsureAutomaticPath(charOverlay.SelectedCharacter);
            charOverlay.SelectedCharacter.Mind.MoveToObject(new InteractionObject(entranceObject,
                loc.Rooms[Number].EntryCondition, this));

            return true;
        }

        public void OnMouseClickMap(Vector2 position, MouseButton button)
        {
            combatEncounter?.CancelOffense();
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
                if (view.Player.Party.Contains(ch) || ch.Player == view.Player)
                {
                    return true;
                }
                else if (interactionMode != LocationInteractionMode.Fight ||
                    view.Location.IsCity)
                {
                    if (ch.Class != CharClass.Dog && !view.Player.Party.Contains(ch))
                    {
                        dialog.SetCharacter(actor, ch);
                        dialog.Show();

                        charOverlay.SelectedCharacter.CancelAction();
                    }

                    return true;
                }
                else
                {
                    if (!view.Player.Party.Contains(ch))
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

            view.Player.Party.IgnoreRangeFilter = !view.Player.SingleMode;
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
                    classic.Game.World.ActiveTraderObj = classic.Game.World.ActiveLocationObj.LocalTrader;
                    if (entrance.Background == 0x0D)
                        classic.SetScene("TraderScene", introScene: "scenes/film_10.txt");
                    else if (entrance.Background == 0x11)
                        classic.SetScene("TraderScene", introScene: "scenes/film_05.txt");
                    else
                        classic.SetScene("TraderScene");
                    break;
                case RoomType.Pub:
                    if (entrance.Background == 14)
                        classic.SetScene("ServiceScene", entrance,
                            introScene: "scenes/film_06.txt");
                    else
                        classic.SetScene("ServiceScene", entrance);
                    break;
                case RoomType.Restaurant:
                    classic.SetScene("ServiceScene", entrance);
                    break;
                case RoomType.Doctor:
                    classic.SetScene("ServiceScene", entrance);
                    break;
                case RoomType.Church:
                    classic.SetImageScene("scenes/church.txt");
                    break;
                case RoomType.Scene:
                    string imageScene = null;
                    switch (entrance.Background)
                    {
                        case 0x0A:
                        case 0x0B:
                        case 0x0C: imageScene = "scenes/film_" + (entrance.Background - 8).ToString("D2") + ".txt"; break;
                        case 0x10: imageScene = "scenes/film_08.txt"; break;
                        case 0x12: imageScene = "scenes/film_09.txt"; break;
                    }
                    if (imageScene != null)
                        classic.SetImageScene(imageScene);
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
                view.Particles.Add(new StaticAnimationParticle(sprite, eventArgs.Defender.Position));

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
