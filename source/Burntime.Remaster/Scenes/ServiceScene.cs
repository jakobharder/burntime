using Burntime.Data.BurnGfx;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Scenes
{
    class ServiceScene : Scene
    {
        public override bool UseDiagonalGamepadNavigation => true;

        readonly InventoryWindow inventory;
        readonly ItemGridWindow offer;
        readonly GuiFont font;
        readonly InventoryKeyboardNavigation keyboardNavigation;
        readonly Button exitButton;
        readonly Button actionButton;
        readonly InputPromptHandle actionPrompt;
        readonly Image doctorAnimation;
        readonly ItemGridTooltip itemTooltip;
        string[]? responseText;
        MapEntrance? entrance;
        RoomType serviceType;
        int lastAmount;
        bool serviceReady;

        BurntimeClassic Classic => (BurntimeClassic)app;
        Character ActiveCharacter => inventory.ActiveCharacter;
        // The same leader scopes both the visible inventory pages and group services.
        Character serviceLeader = null!;
        Character? doctorPatient;
        ICharacterCollection ServiceGroup => serviceLeader.GetGroup();

        public ServiceScene(Module app)
            : base(app)
        {
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            doctorAnimation = new Image(app);
            doctorAnimation.Position = new Vector2(211, 65);
            doctorAnimation.Background = "arzt.ani??p";
            doctorAnimation.Background.Animation.Speed = 6.5f;
            doctorAnimation.Background.Animation.IntervalMargin = 4;
            doctorAnimation.Background.Animation.Progressive = false;
            doctorAnimation.Hide();
            Windows += doctorAnimation;

            inventory = new InventoryWindow(app, InventorySide.Left, compact: app.SmallDeviceUi);
            inventory.Position = new Vector2(2, 5);
            inventory.LeftClickItemEvent += OnLeftClickItemInventory;
            Windows += inventory;

            exitButton = new Button(app);
            exitButton.Position = new Vector2(25, 183);
            exitButton.Text = app.ResourceManager.GetString("burn?354");
            exitButton.Font = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudText);
            exitButton.HoverFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudTextHover);
            exitButton.Command += OnButtonExit;
            exitButton.SetTextOnly();
            Windows += exitButton;

            actionButton = new Button(app);
            actionButton.Position = new Vector2(116, 183);
            actionButton.Text = app.ResourceManager.GetString("burn?369");
            actionButton.Font = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudText);
            actionButton.HoverFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.HudTextHover);
            actionButton.Command += OnButtonAction;
            actionButton.SetTextOnly();
            Windows += actionButton;

            offer = new ItemGridWindow(app);
            offer.Position = new Vector2(160, 165);
            offer.Spacing = app.SmallDeviceUi ? new Vector2(0, 1) : new Vector2(4, 4);
            offer.Grid = app.SmallDeviceUi ? new Vector2(1, 4) : new Vector2(4, 1);
            offer.LeftClickItemEvent += OnLeftClickItemOffer;
            offer.Prompts.HideInNewGfx = true;
            offer.Prompts.Add(InputAction.Primary, "@prompts?37",
                () => CanMoveFocusedItem(offer));
            Windows += offer;

            inventory.Grid.Prompts.HideInNewGfx = true;
            inventory.Grid.Prompts.Add(InputAction.Primary, "@prompts?36",
                () => CanMoveFocusedItem(inventory.Grid));

            itemTooltip = new ItemGridTooltip(app, () => inventory.ActiveCharacter, () => inventory);
            itemTooltip.AddGrid(inventory.Grid, ItemTooltipSide.Right,
                _ => GetTransferTooltip(inventory.Grid, "@prompts?36"));
            itemTooltip.AddGrid(offer, ItemTooltipSide.Left,
                _ => GetTransferTooltip(offer, "@prompts?37"));
            Windows += itemTooltip.Window;

            font = new GuiFont(BurntimeClassic.FontName, ClassicColors.LightGray);
            keyboardNavigation = new InventoryKeyboardNavigation(
                inventory, offer, OnButtonAction, OnButtonExit);
            inventory.Grid.MouseFocusChanged += keyboardNavigation.FocusFromMouse;
            offer.MouseFocusChanged += keyboardNavigation.FocusFromMouse;
            Windows += new InputPromptOverlay(app, Prompts,
                InputPromptColorScheme.Hud) { ScenePromptLeftMargin = app.SmallDeviceUi ? 88 : 6 };
            exitButton.Prompts.Add(InputAction.Back, "",
                new Vector2(exitButton.Size.x + 2, -2));
            actionPrompt = actionButton.Prompts.Add(new InputPrompt(InputAction.Action, "")
            {
                MouseControl = MouseButton.None,
                TouchControl = TouchControl.None
            },
                new Vector2(actionButton.Size.x + 2, -2));
            Prompts.Add(new InputPrompt(InputPattern.HorizontalPaging, "@prompts?16")
            {
                TouchPattern = app.SmallDeviceUi ? InputPattern.VerticalPaging : InputPattern.HorizontalPaging
            }, () => inventory.PageCount > 1);
            PositionElements();
        }

        ItemTooltipDetails GetTransferTooltip(ItemGridWindow source, GuiString label) =>
            app.IsNewGfx && CanMoveFocusedItem(source)
                ? new ItemTooltipDetails(new InputPrompt(InputAction.Primary, label)
                    { KeyboardControl = PreferredPrimaryKeyboardControl })
                : default;

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);
            PositionElements();
        }

        void PositionElements()
        {
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
            if (!app.SmallDeviceUi)
                return;
            inventory.Position = new Vector2(-Position.x, 7);
            offer.Position = new Vector2(168, 3) - Position;
            PositionFooter(exitButton, new Vector2(25, app.Engine.Resolution.Game.y / 2 + 56), 48);
            PositionFooter(actionButton, new Vector2(324, app.Engine.Resolution.Game.y / 2 + 56), 86);
        }

        void PositionFooter(Button button, Vector2 screenPosition, int width)
        {
            button.IsTextOnly = false;
            button.Layer = 20;
            button.Position = screenPosition - Position;
            button.TextHorizontalAlign = TextAlignment.Left;
            button.TextVerticalAlign = VerticalTextAlignment.Center;
            if (button == exitButton)
                button.Text = app.ResourceManager.GetString("prompts?17");
            button.Size = new Vector2(System.Math.Max(width, button.Font?.GetWidth(button.Text) ?? 0), 24);
        }

        protected override void OnActivateScene(object? parameter)
        {
            if (parameter is MapEntrance requestedEntrance)
                entrance = requestedEntrance;

            serviceReady = false;
            if (entrance is not { } currentEntrance || currentEntrance.RoomType is not
                (RoomType.Doctor or RoomType.Pub or RoomType.Restaurant))
            {
                actionButton.Hide();
                doctorAnimation.Hide();
                return;
            }

            serviceType = currentEntrance.RoomType;
            serviceLeader = Classic.SelectedCharacter;
            inventory.SetGroup(serviceLeader);
            responseText = null;
            offer.Clear();
            keyboardNavigation.Reset();
            lastAmount = -1;

            switch (serviceType)
            {
                case RoomType.Doctor:
                    Background = "arzt.pac";
                    Music = "doctor";
                    actionButton.Text = app.ResourceManager.GetString("burn?369");
                    doctorAnimation.Show();
                    break;
                case RoomType.Pub:
                    Background = currentEntrance.Background == 14
                        ? "scenes/bar.txt"
                        : "scenes/pub1.txt";
                    Music = "pub";
                    actionButton.Text = app.ResourceManager.GetString("burn?414");
                    doctorAnimation.Hide();
                    break;
                case RoomType.Restaurant:
                    Background = currentEntrance.Background == 22
                        ? "scenes/wirt.txt"
                        : "scenes/koch.txt";
                    Music = "diner";
                    actionButton.Text = app.ResourceManager.GetString("burn?415");
                    doctorAnimation.Hide();
                    break;
            }

            PositionElements();
            actionPrompt.UpdatePosition(new Vector2(actionButton.Size.x + 2, -2));
            actionButton.Show();
            serviceReady = true;
            if (serviceType == RoomType.Doctor)
                UpdateDoctorOfferText();
        }

        public override bool OnTouchTap(Vector2 position)
        {
            itemTooltip.HandleTouchTap(position + PositionOnScreen);
            return false;
        }

        public override bool OnInputAction(InputAction action) => keyboardNavigation.Handle(action);

        public override void OnUpdate(float elapsed)
        {
            itemTooltip.Update();
            if (serviceReady && serviceType == RoomType.Doctor && doctorPatient != ActiveCharacter)
                UpdateDoctorOfferText();
        }

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            if (responseText == null)
                return;

            int basex = 157 + 80;
            int basey = app.SmallDeviceUi ? 8 - Position.y : 4;
            for (int i = 0; i < responseText.Length; i++)
                font.DrawText(target, new Vector2(basex, basey + 9 * i),
                    responseText[i], TextAlignment.Center, VerticalTextAlignment.Top);
        }

        bool CanOffer(Item item) => serviceReady &&
            (serviceType != RoomType.Doctor || item.HealValue != 0);

        void OnOfferChanged()
        {
            if (serviceType == RoomType.Doctor)
            {
                UpdateDoctorOfferText();
                return;
            }

            lastAmount = -1;
            UpdateServiceText();
        }

        void CommitOffer()
        {
            switch (serviceType)
            {
                case RoomType.Doctor:
                    ActiveCharacter.Health = Classic.Game.RuleBook.CalculateDoctorResult(
                        ActiveCharacter.Health, offer);
                    UpdateDoctorText();
                    break;
                case RoomType.Pub:
                    lastAmount = Classic.Game.RuleBook.CalculatePubValue(offer);
                    UpdateServiceText();
                    ServiceGroup.Drink(serviceLeader, lastAmount);
                    break;
                case RoomType.Restaurant:
                    lastAmount = Classic.Game.RuleBook.CalculateRestaurantValue(offer);
                    UpdateServiceText();
                    ServiceGroup.Eat(serviceLeader, lastAmount);
                    break;
            }
        }

        void UpdateServiceText()
        {
            int value = serviceType == RoomType.Pub
                ? Classic.Game.RuleBook.CalculatePubValue(offer)
                : Classic.Game.RuleBook.CalculateRestaurantValue(offer);
            int baseLine = serviceType == RoomType.Pub ? 550 : 530;

            if (lastAmount == 0)
                baseLine += 6;
            else if (lastAmount > 0)
                baseLine += 3;
            else if (value == 0)
                baseLine += 9;

            TextHelper text = new TextHelper(app, "burn");
            text.AddArgument("|E", value);
            responseText = new[] { text[baseLine], text[baseLine + 1], text[baseLine + 2] };
        }

        void UpdateDoctorOfferText()
        {
            doctorPatient = ActiveCharacter;
            int health = ActiveCharacter.Health;
            int result = Classic.Game.RuleBook.CalculateDoctorResult(health, offer);
            int value = result - health;
            int cap = Classic.Game.RuleBook.Settings.DoctorHealthCap;
            int baseLine;

            if (health >= cap)
                baseLine = 67;
            else if (offer.Count == 0)
                baseLine = 64;
            else if (result >= cap)
                baseLine = 76;
            else if (value >= 20 && result > 60)
                baseLine = 73;
            else
                baseLine = 70;

            TextHelper text = new(app, "newburn");
            text.AddArgument("|E", value);
            responseText = new[] { text[baseLine], text[baseLine + 1], text[baseLine + 2] };
        }

        void UpdateDoctorText()
        {
            int value = Classic.Game.RuleBook.CalculateDoctorResult(
                ActiveCharacter.Health, offer) - ActiveCharacter.Health;
            int baseLine;

            if (value == 0)
                baseLine = 522;
            else if (ActiveCharacter.Health <= 45)
                baseLine = 516;
            else if (ActiveCharacter.Health <= 60)
                baseLine = 513;
            else if (ActiveCharacter.Health <= 95)
                baseLine = 510;
            else
                baseLine = 519;

            TextHelper text = new TextHelper(app, "burn");
            responseText = new[] { text[baseLine], text[baseLine + 1], text[baseLine + 2] };
        }

        bool CanMoveFocusedItem(ItemGridWindow source)
        {
            Item? focusedItem = source.FocusedItem;
            return focusedItem != null &&
                (source == offer
                    ? inventory.Grid.Count < inventory.Grid.MaxCount
                    : offer.Count < offer.MaxCount && CanOffer(focusedItem));
        }

        void OnButtonExit()
        {
            if (serviceReady)
                ServiceGroup.MoveItems(offer);
            app.SceneManager.PreviousScene();
        }

        void OnButtonAction()
        {
            if (!serviceReady)
                return;

            CommitOffer();
            offer.Clear();
            keyboardNavigation.ItemsChanged();
        }

        void OnLeftClickItemInventory(Framework.States.StateObject state)
        {
            if (state is not Item item || !CanOffer(item) || !offer.Add(item))
                return;

            inventory.Grid.Remove(item);
            ActiveCharacter.Items.Remove(item);
            inventory.RefreshCombatLoadout();
            OnOfferChanged();
            keyboardNavigation.ItemsChanged();
        }

        void OnLeftClickItemOffer(Framework.States.StateObject state)
        {
            if (!serviceReady || state is not Item item || !inventory.Grid.Add(item))
                return;

            ActiveCharacter.Items.Add(item);
            inventory.RefreshCombatLoadout();
            offer.Remove(item);
            OnOfferChanged();
            keyboardNavigation.ItemsChanged();
        }
    }
}
