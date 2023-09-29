using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.States;
using Burntime.Classic.GUI;
using Burntime.Data.BurnGfx;
using Burntime.Classic.Logic;
using Burntime.Classic.Logic.Interaction;
using Burntime.Classic.Maps;

namespace Burntime.Classic
{
    public class LocationScene : Scene, IMapEntranceHandler, IInteractionHandler, ILogicNotifycationHandler
    {
        MapView view;
        MainUiOriginalWindow gui;
        MenuWindow menu;
        Image cursorAni;
        DialogWindow dialog;
        Maps.MapViewOverlayHoverText hoverInfo;
        Maps.MapViewOverlayCharacters charOverlay;

        private bool fightMode
        {
            get { if (view.Player == null) return false; return view.Player.FightMode; }
            set { if (view.Player != null) view.Player.FightMode = value; }
        }

        public LocationScene(Module App)
            : base(App)
        {
            Size = app.Engine.GameResolution;

            view = new MapView(this, App);
            view.Position = new Vector2(16, 0);
            view.Size = new Vector2(Size.x - 32, Size.y - 40);
            //view.Position = new Vector2(0, 0);
            //view.Size = new Vector2(Size.x, Size.y - 16);
            view.MouseClickEvent += OnMouseClickMap;
            view.Overlays.Add(new Maps.MapViewOverlayDroppedItems(App));
            view.Overlays.Add(charOverlay = new Maps.MapViewOverlayCharacters(App));
            view.Overlays.Add(hoverInfo = new Maps.MapViewOverlayHoverText(App));
            view.ClickObject += new EventHandler<ObjectArgs>(view_ClickObject);
            view.Scroll += new EventHandler<MapScrollArgs>(view_Scroll);
            Windows += view;

            menu = new MenuWindow(App);
            menu.Layer += 50;
            menu.Hide();
            Windows += menu;

            cursorAni = new Image(App);
            cursorAni.Background = "burngfxani@munt.raw?10-13";
            cursorAni.Background.Animation.Progressive = false;
            cursorAni.Layer += 59;
            Windows += cursorAni;

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
        }

        void dialog_WindowShow(object sender, EventArgs e)
        {
            hoverInfo.IsVisible = false;
            cursorAni.Hide();
        }

        void dialog_WindowHide(object sender, EventArgs e)
        {
            hoverInfo.IsVisible = true;
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
                ShowMenu(e.Position);
                return;
            }

            if (e.Object is Character)
            {
                if (fightMode)
                    AttackCharacter(e.Object as Character);
                else
                    ClickCharacter(e.Object as Character);
            }
            else if (!fightMode && e.Object is DroppedItem)
            {
                if (20 > (charOverlay.SelectedCharacter.Position - e.Object.MapPosition).Length)
                    OnMenuInventory();
                else
                    MoveCharacter(e.Object);
            }
        }

        void AttackCharacter(Character targetCharacter)
        {
            // only if not player owned
            if (view.Player != targetCharacter.Player)
            {
                if (30 > (charOverlay.SelectedCharacter.Position - targetCharacter.Position).Length)
                {
                    charOverlay.SelectedCharacter.Attack(targetCharacter);
                    charOverlay.SelectedCharacter.CancelAction();
                }
                else
                {
                    MoveCharacter(targetCharacter);
                }
            }
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

        public override bool OnVKeyPress(Keys key)
        {
            if (key == Keys.Pause)
            {
                app.SceneManager.SetScene("PauseScene");
                return true;
            }

            return false;
        }

        public override bool OnKeyPress(char key)
        {
            if (app.Settings["debug"].GetBool("enable_cheats"))
            {
                string[] items = null;

                switch (key)
                {
                    case '1':
                        items = app.Settings["debug"].GetStrings("insert_items_1");
                        break;
                    case '2':
                        items = app.Settings["debug"].GetStrings("insert_items_2");
                        break;
                    case '3':
                        items = app.Settings["debug"].GetStrings("insert_items_3");
                        break;
                }

                if (items != null)
                {
                    BurntimeClassic classic = app as BurntimeClassic;

                    foreach (string id in items)
                    {
                        Item item = classic.Game.ItemTypes.Generate(id);
                        classic.Game.World.ActiveLocationObj.Items.DropAt(item, classic.Game.World.ActivePlayerObj.Character.Position);
                    }
                }
            }

            return false;
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            if (button == MouseButton.Right)
            {
                ShowMenu(position);
            }
            return true;
        }

        public override void OnRender(RenderTarget Target)
        {
            if (app.MouseImage != null)
            {
                cursorAni.Position = app.DeviceManager.Mouse.Position + new Vector2(8, 11);

                int layer = Target.Layer;
                Target.Layer = gui.Layer - 1;
                Target.DrawSprite(app.DeviceManager.Mouse.Position, app.MouseImage);
                Target.Layer = layer;
            }
        }

        public override void OnUpdate(float Elapsed)
        {
            ClassicGame game = app.GameState as ClassicGame;
            game.World.Update(Elapsed);

            game.World.ActiveLocationObj.Update(Elapsed);
            game.World.ActivePlayerObj.Update(Elapsed);

            if (game.World.Time <= 0 || game.World.ActivePlayerObj.IsDead)
            {
                app.ActiveClient.Finish();
                app.SceneManager.SetScene("WaitScene");
            }

            if (charOverlay.SelectedCharacter.IsDead)
                view.Player.SelectGroup(view.Player.Group);
        }

        protected override void OnActivateScene(object parameter)
        {
            app.RenderMouse = false;
            app.MouseBoundings = view.Boundings;

            ClassicGame game = app.GameState as ClassicGame;

            if (BurntimeClassic.Instance.PreviousPlayerId != -1 &&
                BurntimeClassic.Instance.PreviousPlayerId != game.CurrentPlayerIndex)
            {
                // play player changed sound
                BurntimeClassic.Instance.Engine.Music.PlayOnce("06_MUS 06_HSC.ogg");
            }
            BurntimeClassic.Instance.PreviousPlayerId = game.CurrentPlayerIndex;

            view.Map = (MapData)game.World.ActiveLocationObj.Map.MapData;
            view.Location = game.World.ActiveLocationObj;
            view.Player = game.World.ActivePlayerObj;

            if (view.Player.RefreshScrollPosition)
                view.CenterTo(view.Player.Character.Position);
            else
                view.ScrollPosition = view.Player.LocationScrollPosition;
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
            app.RenderMouse = true;
            app.MouseBoundings = null;
            app.GameState.Container.RemoveNotifycationHandler(this);
        }

        void ShowMenu(Vector2 position)
        {
            menu.Clear();

            if (!view.Location.IsCity)
            {
                menu.AddLine("@burn?351", (CommandHandler)OnMenuInfo);
                if (fightMode)
                    menu.AddLine("@burn?350", (CommandHandler)OnMenuSpeak);
                else
                    menu.AddLine("@burn?352", (CommandHandler)OnMenuFight);
            }

            if (view.Player.Group.Count > 1)
            {
                if (!view.Player.SingleMode)
                {
                    menu.AddLine("@burn?358", (CommandHandler)OnMenuSingle);
                }
                else
                {
                    menu.AddLine("@burn?356", (CommandHandler)OnMenuAll);
                }
            }

            if (charOverlay.SelectedCharacter != view.Player.Character)
            {
                menu.AddLine("@burn?363", (CommandHandler)OnMenuDismiss);
                if (view.Player.Group.Contains(charOverlay.SelectedCharacter))
                {
                    menu.AddLine("@burn?364", (CommandHandler)OnMenuMakeCamp);
                }
                else
                {
                    menu.AddLine("@burn?365", (CommandHandler)OnMenuLeaveCamp);
                }
            }

            menu.AddLine("@burn?362", (CommandHandler)OnMenuMap);
            menu.AddLine("@burn?367", (CommandHandler)OnMenuInventory);
            menu.AddLine("@burn?357", (CommandHandler)OnMenuTurn);

            menu.Show(position, view.Boundings);
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

            charOverlay.SelectedCharacter.CancelAction();
            
            BurntimeClassic classic = app as BurntimeClassic;
            classic.InventoryBackground = -1;
            classic.InventoryRoom = null;
            classic.Game.World.ActiveLocationObj.Items.DropPosition = charOverlay.SelectedCharacter.Position;
            classic.PickItems = new PickItemList(classic.Game.World.ActiveLocationObj.Items, charOverlay.SelectedCharacter.Position, 20);
            app.SceneManager.SetScene("InventoryScene", charOverlay.SelectedCharacter);
        }

        public void OnMenuFight()
        {
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
                }
            }
            else
            {
                charOverlay.SelectedCharacter.JoinCamp();

                view.Location.Player = view.Player;
                BurntimeClassic.Instance.Engine.Music.PlayOnce("08_MUS 08_HSC.ogg");
            }
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
            if (!TryEnterRoom(entranceObject, charOverlay.SelectedCharacter))
            {
                charOverlay.SelectedCharacter.Mind.MoveToObject(new InteractionObject(entranceObject,
                   classic.Game.World.ActiveLocationObj.Rooms[Number].EntryCondition, this));
            }

            return true;
        }

        public void OnMouseClickMap(Vector2 position, MouseButton button)
        {
            charOverlay.SelectedCharacter.Mind.MoveToObject(null);
            charOverlay.SelectedCharacter.Path.MoveTo = position;
        }

        void MoveCharacter(IMapObject obj)
        {
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
                else if (!fightMode)
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
                        actor.Attack(ch);

                        actor.CancelAction();

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
            if (!condition.Process(charOverlay.SelectedCharacter))
                return false;

            if (view.Location.Player != null && view.Location.Player != view.Player)
                return true;

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

                Sprite sprite = (GuiImage)"burngfxani@syssze.raw?208-213";
                sprite.Animation.Speed = 10;
                view.Particles.Add(new StaticAnimationParticle(sprite, eventArgs.Attacker));
                view.Particles.Add(new StaticAnimationParticle(sprite.Clone(), eventArgs.Defender));
            }
        }
    }
}
