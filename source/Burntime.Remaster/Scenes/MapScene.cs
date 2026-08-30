using System;
using System.Collections.Generic;
using Burntime.Data.BurnGfx;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster
{
    public class MapScene : Scene, IMapEntranceHandler
    {
        public override bool UseDiagonalGamepadNavigation => true;
        protected override bool UseGamepadDPadNavigation => false;

        public override InputAction ResolveInputAction(InputAction action) => action;

        public override bool TryGetInputAction(Key key, out InputAction action)
        {
            if (_cheatCommand.Length > 0)
            {
                if (key.IsVirtual && key.VirtualKey == SystemKey.Escape)
                    _cheatCommand = string.Empty;
                action = InputAction.None;
                return false;
            }

            if (key.IsVirtual && key.VirtualKey == SystemKey.Escape)
            {
                action = InputAction.Options;
                return true;
            }

            return base.TryGetInputAction(key, out action);
        }

        const float NEXT_TURN_HOLD_TIME = 0.6f;

        ClassicMapView view;
        IMapGuiWindow gui;
        MenuWindow menu;
        Image _cursorAni;
        readonly DialogWindow _dialog;
        readonly Maps.MapViewOverlaySelectedLocation _keyboardSelection;
        bool _followKeyboardSelection;
        bool _cameraPanActive;
        bool _followPlayerAfterPan;
        float _nextTurnHoldTime;
        bool _nextTurnTriggered;
        string _cheatCommand = string.Empty;
        bool _cheatDialogActive;

        private bool _infoMode
        {
            get { if (view.Player == null) return false; return view.Player.InfoMode; }
            set { if (view.Player != null) view.Player.InfoMode = value; }
        }

        bool _debugNoTravel = false;

        public MapScene(Module App)
            : base(App)
        {
            Size = app.Engine.Resolution.Game;
            BurntimeClassic classic = app as BurntimeClassic;

            gui = classic.NewGui ? new MainUiLeftWindow(App) : new MainUiOriginalWindow(App);

            view = new ClassicMapView(this, App);
            gui.SetMapRenderArea(view, Size);

            view.Overlays.Add(new Maps.MapViewOverlayFlags(app));
            view.Overlays.Add(new Maps.MapViewOverlayPlayer(app));
            view.Overlays.Add(new Maps.MapViewOverlayHoverText(app));
            view.Overlays.Add(_keyboardSelection = new Maps.MapViewOverlaySelectedLocation(app));
            view.Scroll += new EventHandler<MapScrollArgs>(view_Scroll);
            view.ContextMenu += View_OnContextMenu;
            Windows += view;

            menu = new MenuWindow(App);
            menu.Layer += 50;
            menu.AddLine("@burn?351", (CommandHandler)OnMenuInfo);
            menu.AddLine("@burn?367", (CommandHandler)OnMenuInventory);
            menu.AddLine("@burn?359", (CommandHandler)OnMenuStatistics);
            menu.AddLine("@burn?361", (CommandHandler)OnMenuOptions);
            menu.AddLine("@burn?357", (CommandHandler)OnMenuTurn);
            menu.Hide();
            Windows += menu;

            _cursorAni = new Image(App);
            _cursorAni.Background = "burngfxani@syst.raw?24-27";
            _cursorAni.Background.Animation.Progressive = false;
            _cursorAni.Layer += 59;
            Windows += _cursorAni;

            gui.Layer += classic.NewGui ? 40 : 60;
            Windows += gui;

            _dialog = new DialogWindow(app)
            {
                PlayMusic = false
            };
            _dialog.Position = view.Position + (view.Size - _dialog.Size) / 2 - new Vector2(0, 10);
            _dialog.Hide();
            _dialog.Layer += 55;
            _dialog.WindowHide += new EventHandler(OnDialogHidden);
            _dialog.WindowShow += new EventHandler(OnDialogShown);
            Windows += _dialog;
        }

        private void View_OnContextMenu(Vector2 position, MouseButton button)
        {
            menu.Show(position, view.Boundings, true);
        }

        void OnDialogShown(object? sender, EventArgs e)
        {
            _cursorAni.Hide();
            _keyboardSelection.IsVisible = false;
        }

        void OnDialogHidden(object? sender, EventArgs e)
        {
            _cursorAni.Show();
            _keyboardSelection.IsVisible = true;

            if (!_cheatDialogActive)
                return;

            _cheatDialogActive = false;
            if (_dialog.ResultChoice < 0)
                return;

            ClassicGame game = BurntimeClassic.Instance.Game;
            game.CheatsEnabled = true;

            switch (_dialog.ResultChoice)
            {
                case 0:
                    OnFastTravel();
                    break;
                case 1:
                    RefillPlayer();
                    break;
                case 2:
                    SpawnCheatItems();
                    break;
            }
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Size = app.Engine.Resolution.Game;
            gui.SetMapRenderArea(view, Size);
            app.MouseBoundings = view.Boundings;
        }

        void view_Scroll(object sender, MapScrollArgs e)
        {
            ClassicGame game = app.GameState as ClassicGame;
            game.World.ActivePlayerObj.MapScrollPosition = e.Offset;
        }

        public override bool OnKeyPress(char key)
        {
            if (app.GameState is not ClassicGame)
                return false;

            key = char.ToLowerInvariant(key);

            if (key == '/')
            {
                _cheatCommand = "/";
                return true;
            }

            if (_cheatCommand.Length > 0)
            {
                if (key == '\b')
                {
                    _cheatCommand = _cheatCommand.Length > 1
                        ? _cheatCommand[..^1]
                        : string.Empty;
                    return true;
                }

                _cheatCommand += key;
                if (_cheatCommand == "/petko")
                {
                    _cheatCommand = string.Empty;
                    ShowCheatDialog();
                    return true;
                }

                if (!"/petko".StartsWith(_cheatCommand, StringComparison.OrdinalIgnoreCase))
                    _cheatCommand = string.Empty;
                return true;
            }

            return base.OnKeyPress(key);
        }

        void ShowCheatDialog()
        {
            Conversation conversation = new()
            {
                Text = new[] { "Cheats", "" },
                Choices = new[]
                {
                    new ConversationChoice { Text = "Travel", Action = new ConversationAction(ConversationActionType.Exit) },
                    new ConversationChoice { Text = "Refill", Action = new ConversationAction(ConversationActionType.Exit) },
                    new ConversationChoice { Text = "Items", Action = new ConversationAction(ConversationActionType.Exit) }
                }
            };

            _cheatDialogActive = true;
            _dialog.SetCharacter(view.Player.Character, conversation);
            _dialog.Show();
        }

        void RefillPlayer()
        {
            view.Player.Character.Food = 9;
            view.Player.Character.Water = 5;
            view.Player.Character.Health = 100;
        }

        void SpawnCheatItems()
        {
            ClassicGame game = BurntimeClassic.Instance.Game;
            foreach (string setting in new[] { "insert_items_1", "insert_items_2", "insert_items_3" })
            {
                foreach (string id in app.Settings["debug"].GetStrings(setting))
                {
                    Item item = game.ItemTypes.Generate(id);
                    game.World.ActiveLocationObj.Items.DropAt(item, view.Player.Character.Position);
                }
            }
        }

        private void ToggleFastTravel()
        {
            if (!_debugNoTravel)
                OnFastTravel();
            else
                OnMenuTravel();
        }

        public override void OnRender(RenderTarget Target)
        {
            bool showInteractionMode = app.MouseInputVisible && !_dialog.IsVisible;
            if (_cursorAni.IsVisible != showInteractionMode)
                _cursorAni.IsVisible = showInteractionMode;

            if (app.MouseImage != null)
            {
                _cursorAni.Position = app.DeviceManager.Mouse.Position + new Vector2(8, 11);

                if (!BurntimeClassic.Instance.NewGui && app.MouseInputVisible)
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
            ResetHeldActionsIfReleased();
            UpdateCameraPan(Elapsed);

            ClassicGame game = app.GameState as ClassicGame;
            game.World.Update(Elapsed);

            if (game.World.Time <= 0)
            {
                app.ActiveClient.Finish();
                app.SceneManager.SetScene("WaitScene");
            }

            if (app.MouseInputVisible)
                _followPlayerAfterPan = false;
            else if (_followPlayerAfterPan)
            {
                Vector2 position = view.Map.Entrances[game.World.ActivePlayerObj.Location.Id].Area.Center;
                _followPlayerAfterPan = !view.FollowWithinMiddleThird(position, Elapsed);
            }
            else if (_followKeyboardSelection && _keyboardSelection.LocationNumber >= 0)
            {
                Vector2 position = view.Map.Entrances[_keyboardSelection.LocationNumber].Area.Center;
                _followKeyboardSelection = !view.FollowWithinMiddleThird(position, Elapsed);
            }

            SyncGamepadCursor();

            int selectedLocation = app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad &&
                _keyboardSelection.LocationNumber >= 0
                ? _keyboardSelection.LocationNumber
                : view.ActiveEntrance;
            var hoverLocation = selectedLocation >= 0 ? BurntimeClassic.Instance.Game.World.Locations[selectedLocation] : null;
            var player = BurntimeClassic.Instance.Game.World.ActivePlayerObj;
            gui.ExpectedTravelDays = hoverLocation is null ? 0 : player.GetTravelDays(player.Location, hoverLocation);
        }

        protected override void OnActivateScene(object parameter)
        {
            _nextTurnHoldTime = 0;
            _nextTurnTriggered = false;
            _cameraPanActive = false;
            _followPlayerAfterPan = false;

            if (!BurntimeClassic.Instance.NewGui)
            {
                app.RenderMouse = false;
            }
            app.MouseBoundings = view.Boundings;

            ClassicGame game = app.GameState as ClassicGame;

            if (BurntimeClassic.Instance.PreviousPlayerId != -1 &&
                BurntimeClassic.Instance.PreviousPlayerId != game.CurrentPlayerIndex)
            {
                // play player changed sound
                BurntimeClassic.Instance.Engine.Music.PlayOnce("sounds/change.ogg");
            }
            BurntimeClassic.Instance.PreviousPlayerId = game.CurrentPlayerIndex;

            view.Ways = (WayData)game.World.Ways.WayData;
            view.Map = (MapData)game.World.Map.MapData;
            view.Player = game.World.ActivePlayerObj;
            _keyboardSelection.LocationNumber = game.World.ActivePlayerObj.Location.Id;
            _followKeyboardSelection = false;
            //if (game.World.ActivePlayerObj.RefreshMapScrollPosition)
                view.CenterTo(view.Map.Entrances[game.World.ActivePlayerObj.Location].Area.Center);
            //else
            //    view.ScrollPosition = game.World.ActivePlayerObj.MapScrollPosition;
            gui.UpdatePlayer();

            game.World.ActivePlayerObj.OnMainMap = true;

            game.MainMapView = true;

            // refresh travel/info cursor
            if (_infoMode)
                OnMenuInfo();
            else
                OnMenuTravel();
        }

        protected override void OnInactivateScene()
        {
            if (!BurntimeClassic.Instance.NewGui)
            {
                app.RenderMouse = true;
            }
            app.MouseBoundings = null;
        }

        public override bool OnInputAction(InputAction action)
        {
            ClassicGame game = app.GameState as ClassicGame;

            if (action == InputAction.NextTurn)
                return true;

            if (action == InputAction.Back)
            {
                SetKeyboardSelection(game.World.ActivePlayerObj.Location.Id);
                _followKeyboardSelection = true;
                return true;
            }

            if (action == InputAction.Options)
            {
                OnMenuOptions();
                return true;
            }

            if (action == InputAction.Statistics)
            {
                OnMenuStatistics();
                return true;
            }

            if (action == InputAction.Inventory)
            {
                OnMenuInventory();
                return true;
            }

            if (action == InputAction.LocationInfo)
            {
                if (!game.World.ActiveLocationObj.IsCity)
                {
                    (app as BurntimeClassic).InfoCity = game.World.ActivePlayerObj.Location;
                    app.SceneManager.SetScene("InfoScene");
                }
                return true;
            }

            if (action == InputAction.GlobalAction)
            {
                menu.Show(view.Boundings.Center, view.Boundings, false);
                return true;
            }

            if (action == InputAction.LeftArea)
            {
                OnMenuTravel();
                return true;
            }

            if (action == InputAction.RightArea)
            {
                OnMenuInfo();
                return true;
            }

            if (action == InputAction.ToggleInteractionMode)
            {
                if (_infoMode)
                    OnMenuTravel();
                else
                    OnMenuInfo();
                return true;
            }

            Vector2 moveDirection = action switch
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
            if (moveDirection != Vector2.Zero)
            {
                _followPlayerAfterPan = false;
                SelectLocation(moveDirection);
                return true;
            }

            if (app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad &&
                action == InputAction.Primary)
            {
                int locationNumber = _keyboardSelection.LocationNumber;
                if (locationNumber == game.World.ActivePlayerObj.Location.Id)
                    app.SceneManager.SetScene("LocationScene");
                else if (locationNumber >= 0 &&
                    game.World.ActivePlayerObj.CanTravel(game.World.ActivePlayerObj.Location,
                        game.World.Locations[locationNumber]))
                    TravelToLocation(locationNumber);
                return true;
            }

            if (app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad &&
                action == InputAction.Secondary)
            {
                int locationNumber = _keyboardSelection.LocationNumber;
                if (locationNumber >= 0 &&
                    CanShowInfo(game.World.ActivePlayerObj, game.World.Locations[locationNumber]))
                {
                    BurntimeClassic.Instance.InfoCity = locationNumber;
                    app.SceneManager.SetScene("InfoScene");
                }
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

        void SelectLocation(Vector2 direction)
        {
            if (view.Map == null)
                return;

            Logic.Player player = BurntimeClassic.Instance.Game.World.ActivePlayerObj;
            int currentSelection = _keyboardSelection.LocationNumber;
            Vector2 current = currentSelection >= 0
                ? view.Map.Entrances[currentSelection].Area.Center
                : view.Map.Entrances[player.Location.Id].Area.Center;

            int selected = SelectLocationInDirection(current, currentSelection, direction, player);
            if (selected != -1)
            {
                SetKeyboardSelection(selected);
                _followKeyboardSelection = true;
            }
        }

        int SelectLocationInDirection(Vector2 current, int currentSelection, Vector2 requestedDirection,
            Logic.Player player)
        {
            Vector2[] directions =
            {
                new Vector2(0, -1),
                new Vector2(0, 1),
                new Vector2(-1, 0),
                new Vector2(1, 0),
                new Vector2(-1, -1),
                new Vector2(1, -1),
                new Vector2(-1, 1),
                new Vector2(1, 1)
            };
            var candidates = new List<(int Location, Vector2 Difference, float Distance)>();
            int locationCount = System.Math.Min(view.Map.Entrances.Length,
                BurntimeClassic.Instance.Game.World.Locations.Count);

            for (int i = 0; i < locationCount; i++)
            {
                Logic.Location location = BurntimeClassic.Instance.Game.World.Locations[i];
                if (i == currentSelection ||
                    (i != player.Location.Id && !CanShowInfo(player, location) &&
                        !player.CanTravel(player.Location, location)))
                    continue;

                Vector2 difference = view.Map.Entrances[i].Area.Center - current;
                if (difference != Vector2.Zero)
                    candidates.Add((i, difference, difference.Length));
            }

            var rankings = new List<int>[directions.Length];
            for (int d = 0; d < directions.Length; d++)
            {
                rankings[d] = new List<int>();
                for (int i = 0; i < candidates.Count; i++)
                {
                    Vector2 difference = candidates[i].Difference;
                    if (difference.x * directions[d].x + difference.y * directions[d].y > 0)
                        rankings[d].Add(i);
                }
                Vector2 direction = directions[d];
                rankings[d].Sort((left, right) =>
                    GetDirectionalScore(candidates[left].Difference, direction, candidates[left].Distance)
                        .CompareTo(GetDirectionalScore(candidates[right].Difference, direction,
                            candidates[right].Distance)));
            }

            int[] assignedCandidate = new int[directions.Length];
            Array.Fill(assignedCandidate, -1);
            int[] rankingIndex = new int[directions.Length];
            var candidateOwner = new Dictionary<int, int>();
            var pendingDirections = new Queue<int>();
            for (int d = 0; d < directions.Length; d++)
                pendingDirections.Enqueue(d);

            while (pendingDirections.Count > 0)
            {
                int directionIndex = pendingDirections.Dequeue();
                while (rankingIndex[directionIndex] < rankings[directionIndex].Count)
                {
                    int candidateIndex = rankings[directionIndex][rankingIndex[directionIndex]];
                    if (!candidateOwner.TryGetValue(candidateIndex, out int owner))
                    {
                        assignedCandidate[directionIndex] = candidateIndex;
                        candidateOwner[candidateIndex] = directionIndex;
                        break;
                    }

                    float challengerAlignment = GetAlignment(candidates[candidateIndex].Difference,
                        directions[directionIndex], candidates[candidateIndex].Distance);
                    float ownerAlignment = GetAlignment(candidates[candidateIndex].Difference,
                        directions[owner], candidates[candidateIndex].Distance);
                    if (challengerAlignment > ownerAlignment + 0.001f)
                    {
                        assignedCandidate[owner] = -1;
                        rankingIndex[owner]++;
                        pendingDirections.Enqueue(owner);

                        assignedCandidate[directionIndex] = candidateIndex;
                        candidateOwner[candidateIndex] = directionIndex;
                        break;
                    }

                    rankingIndex[directionIndex]++;
                }
            }

            int requestedIndex = 0;
            for (; requestedIndex < directions.Length; requestedIndex++)
            {
                if (directions[requestedIndex] == requestedDirection)
                    break;
            }
            if (requestedIndex == directions.Length || rankings[requestedIndex].Count == 0)
                return -1;

            int result = assignedCandidate[requestedIndex] >= 0
                ? assignedCandidate[requestedIndex]
                : rankings[requestedIndex][0];

            int bestRanked = rankings[requestedIndex][0];
            if (result != bestRanked)
            {
                float bestScore = GetDirectionalScore(candidates[bestRanked].Difference,
                    directions[requestedIndex], candidates[bestRanked].Distance);
                float resultScore = GetDirectionalScore(candidates[result].Difference,
                    directions[requestedIndex], candidates[result].Distance);

                // Keep a unique directional assignment only when it remains close
                // to the best combined distance-and-angle candidate.
                if (resultScore > bestScore * 1.05f)
                    result = bestRanked;
            }

            return candidates[result].Location;
        }

        static float GetAlignment(Vector2 difference, Vector2 direction, float distance)
        {
            return (difference.x * direction.x + difference.y * direction.y) /
                (distance * direction.Length);
        }

        static float GetDirectionalScore(Vector2 difference, Vector2 direction, float distance)
        {
            const float AngularWeight = 0.75f;
            double alignment = System.Math.Clamp(GetAlignment(difference, direction, distance), -1f, 1f);
            double normalizedAngle = System.Math.Acos(alignment) / (System.Math.PI / 4.0);
            return distance * (1f + AngularWeight * (float)(normalizedAngle * normalizedAngle));
        }

        bool CanShowInfo(Logic.Player player, Logic.Location location)
        {
            return location.Player == player ||
                !location.IsCity && location == player.Location && location.Player == null;
        }

        void SetKeyboardSelection(int locationNumber)
        {
            _keyboardSelection.LocationNumber = locationNumber;
        }

        void SyncGamepadCursor()
        {
            if (app.MouseInputVisible || view.Map == null || _keyboardSelection.LocationNumber < 0)
                return;

            int locationNumber = _keyboardSelection.LocationNumber;
            if (locationNumber >= view.Map.Entrances.Length)
                return;

            Vector2 mapPosition = view.Map.Entrances[locationNumber].Area.Center;
            app.DeviceManager.MouseMove(view.Boundings.Position + view.ScrollPosition + mapPosition);
        }

        public override bool OnHeldInputAction(InputAction action, float elapsed)
        {
            if (action == InputAction.NextTurn)
            {
                if (!_nextTurnTriggered)
                {
                    _nextTurnHoldTime += elapsed;
                    if (_nextTurnHoldTime >= NEXT_TURN_HOLD_TIME)
                    {
                        _nextTurnTriggered = true;
                        OnMenuTurn();
                    }
                }
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
            if (direction == Vector2.Zero)
                return false;

            return true;
        }

        void ResetHeldActionsIfReleased()
        {
            bool nextTurnDown = app.IsInputActionDown(InputAction.NextTurn);
            if (!nextTurnDown)
            {
                _nextTurnHoldTime = 0;
                _nextTurnTriggered = false;
            }
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
                if (_cameraPanActive)
                {
                    _cameraPanActive = false;
                    _followPlayerAfterPan = !app.MouseInputVisible;
                    _followKeyboardSelection = false;
                }
                return;
            }

            _cameraPanActive = true;
            _followPlayerAfterPan = false;
            _followKeyboardSelection = false;
            view.Pan(direction, elapsed);
        }

        public void OnMenuInfo()
        {
            _infoMode = true;
            _debugNoTravel = false;
            _cursorAni.Background = "burngfxani@syst.raw?20-23";
            _cursorAni.Background.Animation.Progressive = false;

            menu.RemoveLine(0);
            menu.AddLine(0, "@burn?360", (CommandHandler)OnMenuTravel);
        }

        public void OnMenuTravel()
        {
            _infoMode = false;
            _debugNoTravel = false;
            _cursorAni.Background = "burngfxani@syst.raw?24-27";
            _cursorAni.Background.Animation.Progressive = false;

            menu.RemoveLine(0);
            menu.AddLine(0, "@burn?351", (CommandHandler)OnMenuInfo);
        }

        public void OnFastTravel()
        {
            _infoMode = false;
            _debugNoTravel = true;
            _cursorAni.Background = "burngfxani@syst.raw?4-7";
            _cursorAni.Background.Animation.Progressive = false;

            menu.RemoveLine(0);
            menu.AddLine(0, "@burn?360", (CommandHandler)OnMenuTravel);
        }

        public void OnMenuInventory()
        {
            BurntimeClassic classic = app as BurntimeClassic;
            classic.InventoryBackground = -1;
            classic.InventoryRoom = null;
            classic.PickItems = null;
            app.SceneManager.SetScene("InventoryScene", classic.Game.World.ActivePlayerObj.Character);
        }

        public void OnMenuStatistics()
        {
            app.SceneManager.SetScene("StatisticsScene");
        }

        public void OnMenuOptions()
        {
            app.SceneManager.SetScene("OptionsScene");
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
            return app.ResourceManager.GetString("burn?" + Number);
        }

        public bool OnClickEntrance(int Number, MouseButton Button)
        {
            Logic.Player player = BurntimeClassic.Instance.Game.World.ActivePlayerObj;
            Logic.Location clickedLocation = BurntimeClassic.Instance.Game.World.Locations[Number];

            if (Button == MouseButton.Left)
            {
                if (_infoMode)
                {
                    // only show if current location or owned by player
                    if (clickedLocation.Player == player ||
                        (!clickedLocation.IsCity && Number == player.Location.Id && clickedLocation.Player == null))
                    {
                        BurntimeClassic.Instance.InfoCity = Number;
                        app.SceneManager.SetScene("InfoScene");
                    }
                    else
                        return false;
                }
                else
                {
                    TravelToLocation(Number);
                }
                return true;
            }
            return false;
        }

        void TravelToLocation(int locationNumber)
        {
            Logic.Player player = BurntimeClassic.Instance.Game.World.ActivePlayerObj;
            Logic.Location destination = BurntimeClassic.Instance.Game.World.Locations[locationNumber];

            if (_debugNoTravel)
            {
                player.Location = destination;
                player.Character.Position = destination.EntryPoint;
                player.RefreshScrollPosition = true;
                ToggleFastTravel();
            }

            if (player.Location == destination)
            {
                app.SceneManager.SetScene("LocationScene");
            }
            else if (player.Location.Neighbors.Contains(destination) &&
                player.CanTravel(player.Location, destination))
            {
                BurntimeClassic.Instance.Autosaves.SaveBeforeTravel();
                player.Travel(destination);
                OnMenuTurn();
            }
        }
    }
}
