using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic;
using System;

namespace Burntime.Remaster
{
    public class DialogWindow : Container
    {
        readonly static Vector2 FrameSize = new Vector2(208, 90);
        readonly static Vector2 FramePos = new Vector2(43, 10);

        FaceWindow face;
        GuiFont fontText;
        GuiFont fontOptions;
        GuiFont fontKeyChoice;
        GuiFont fontMouseChoice;

        Character character;
        Character self;
        public bool Ended;
        bool ready = false;
        int dlgoffset = 0;
        int dialogmode;
        int hover = -1;
        int selectedChoice = -1;

        public ConversationType Type { get; private set; }
        public bool PlayMusic { get; set; } = true;

        Conversation conversation;

        ConversationActionType result;
        public ConversationActionType Result
        {
            get { return result; }
        }

        public DialogWindow(Module app) 
            : base(app)
        {
            IsModal = true;
            Size = FramePos + FrameSize;

            face = new FaceWindow(app);
            face.DisplayOnly = true;
            Windows += face;

            fontText = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 164, 56));
            fontOptions = new GuiFont(BurntimeClassic.FontName, new PixelColor(108, 116, 168));
            fontKeyChoice = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
            fontMouseChoice = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));

            CaptureAllMouseMove = true;
        }

        public override void OnShow()
        {
            HasFocus = true;
            hover = -1;
            base.OnShow();

            if (PlayMusic)
                BurntimeClassic.Instance.Engine.Music.Play("talking");
        }

        public override void OnHide()
        {
            HasFocus = false;
            base.OnHide();

            if (PlayMusic)
                BurntimeClassic.Instance.Engine.Music.Stop();
        }

        public void SetCharacter(Character character, Conversation conversation, bool showFace = false)
        {
            result = ConversationActionType.None;
            self = null;
            this.character = character;
            this.conversation = conversation;
            if (showFace)
            {
                face.FaceID = character.FaceID;
            }

            dialogmode = (conversation.Text.Length < 3) ? 1 : 0;
            dlgoffset = 0;
            selectedChoice = FirstVisibleChoice();

            ready = true;
        }

        public void SetCharacter(Character self, Character character)
        {
            SetCharacter(self, character, ConversationType.Greeting);
        }

        public void SetCharacter(Character self, Character character, ConversationType type)
        {
            result = ConversationActionType.None;
            this.self = self;
            this.character = character;
            Type = type;
            face.FaceID = character.FaceID;

            conversation = character.Dialog.GetConversation(self, type);

            dialogmode = (conversation.Text.Length < 3) ? 1 : 0;
            dlgoffset = 0;
            selectedChoice = FirstVisibleChoice();

            ready = true;
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            if (hover == 0 && dialogmode == 0)
            {
                AdvanceText();
            }
            else if (hover != -1)
                SelectChoice(hover);

            return true;
        }

        public override bool OnVKeyPress(SystemKey key)
        {
            if (key == SystemKey.Escape)
            {
                result = ConversationActionType.Exit;
                Hide();
                return true;
            }

            if (dialogmode == 0)
            {
                if (key != SystemKey.Enter)
                    return false;

                AdvanceText();
                return true;
            }

            if (dialogmode != 1)
                return false;

            if (key == SystemKey.Up)
            {
                MoveSelection(-1);
                return true;
            }

            if (key == SystemKey.Down)
            {
                MoveSelection(1);
                return true;
            }

            if (key != SystemKey.Enter || selectedChoice == -1)
                return false;

            SelectChoice(selectedChoice);
            return true;
        }

        public override bool OnKeyPress(char key)
        {
            if (dialogmode != 1)
                return false;

            switch (char.ToLowerInvariant(key))
            {
                case 'w':
                    MoveSelection(-1);
                    return true;
                case 's':
                    MoveSelection(1);
                    return true;
                default:
                    return false;
            }
        }

        void AdvanceText()
        {
            dlgoffset += 2;
            if (dlgoffset + 2 >= conversation.Text.Length)
                dialogmode = 1;
        }

        int FirstVisibleChoice()
        {
            for (int i = 0; i < conversation.Choices.Length; i++)
                if (!string.IsNullOrEmpty(conversation.Choices[i].Text))
                    return i;

            return -1;
        }

        void MoveSelection(int direction)
        {
            if (selectedChoice == -1)
            {
                selectedChoice = FirstVisibleChoice();
                return;
            }

            for (int offset = 1; offset < conversation.Choices.Length; offset++)
            {
                int candidate = (selectedChoice + direction * offset + conversation.Choices.Length) % conversation.Choices.Length;
                if (!string.IsNullOrEmpty(conversation.Choices[candidate].Text))
                {
                    selectedChoice = candidate;
                    return;
                }
            }
        }

        void SelectChoice(int choice)
        {
            BurntimeClassic classic = app as BurntimeClassic;
            result = conversation.Choices[choice].Action.Type;

            dialogmode = 0;
            dlgoffset = 0;
            switch (conversation.Choices[choice].Action.Type)
            {
                case ConversationActionType.Talk:
                    conversation = character.Dialog.GetConversation(self, ConversationType.Talk);
                    break;
                case ConversationActionType.Trade:
                    Hide();
                    classic.Game.World.ActiveTraderObj = character as Trader;
                    app.SceneManager.SetScene("TraderScene");
                    break;
                case ConversationActionType.Yes:
                case ConversationActionType.No:
                case ConversationActionType.Exit:
                    Hide();
                    break;
                case ConversationActionType.HireRequirements:
                    conversation = character.Dialog.GetConversation(self, ConversationType.Hire);
                    break;
                case ConversationActionType.Profession:
                    conversation = character.Dialog.GetConversation(self, ConversationType.Profession);
                    break;
                case ConversationActionType.Hire:
                    Hire();
                    Hide();
                    break;
            }

            dialogmode = (conversation.Text.Length < 3) ? 1 : 0;
            selectedChoice = FirstVisibleChoice();
        }

        public override bool OnMouseMove(Vector2 position)
        {
            Vector2 Pos = position;

            TextHelper txt = new TextHelper(app, "burn");
            int textx = 55;

            hover = -1;

            if (dialogmode == 0)
            {
                int texty = 85;
                String line = txt[499];
                if (Pos.x >= textx && Pos.y >= texty && Pos.x < textx + fontText.GetWidth(line) && Pos.y < texty + 10)
                {
                    hover = 0;
                }
            }
            else if (dialogmode == 1)
            {
                int texty = 63;
                for (int i = 0; i < 3; i++)
                {
                    String line = conversation.Choices[i].Text;

                    if (Pos.x >= textx && Pos.y >= texty && Pos.x < textx + fontText.GetWidth(line) && Pos.y < texty + 10)
                    {
                        hover = i;
                    }
                    texty += 11;
                }
            }

            return base.OnMouseMove(Position);
        }

        public override void OnRender(RenderTarget target)
        {
            target.RenderRect(FramePos, FrameSize, new PixelColor(128, 0, 0, 0));

            base.OnRender(target);

            if (!ready)
                return;

            Vector2 textPos = new Vector2(43 + FrameSize.x / 2, 20);

            TextHelper txt = new TextHelper(app, "burn");

            for (int i = 0; i < 2; i++)
            {
                if (dlgoffset + i < conversation.Text.Length)
                {
                    String line = conversation.Text[dlgoffset + i];
                    Vector2 t = new Vector2(textPos);
                    t.x -= fontText.GetWidth(line) / 2 - 1;
                    fontText.DrawText(target, t, line, TextAlignment.Left, VerticalTextAlignment.Top);
                    textPos.y += 11;
                }
            }

            textPos.x = 55;

            if (dialogmode == 0)
            {
                textPos.y = 85;

                if (hover == 0)
                    fontMouseChoice.DrawText(target, textPos, txt[499], TextAlignment.Left, VerticalTextAlignment.Top);
                else
                    fontKeyChoice.DrawText(target, textPos, txt[499], TextAlignment.Left, VerticalTextAlignment.Top);
            }
            else
            {
                textPos.y = 63;

                for (int i = 0; i < 3; i++)
                {
                    String line = conversation.Choices[i].Text;

                    if (hover == i)
                        fontMouseChoice.DrawText(target, textPos, line, TextAlignment.Left, VerticalTextAlignment.Top);
                    else if (selectedChoice == i)
                        fontKeyChoice.DrawText(target, textPos, line, TextAlignment.Left, VerticalTextAlignment.Top);
                    else
                        fontOptions.DrawText(target, textPos, line, TextAlignment.Left, VerticalTextAlignment.Top);
                    textPos.y += 11;
                }
            }
        }

        void Hire()
        {
            BurntimeClassic classic = app as BurntimeClassic;
            Player boss = classic.Game.World.ActivePlayerObj;

            character.Hire(boss);
        }
    }
}
