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
        GuiFont fontFocusedChoice;

        Character character;
        Character self;
        public bool Ended;
        bool ready = false;
        int dlgoffset = 0;
        int textLinesPerPage = 2;
        int dialogmode;
        int focusChoiceIndex = -1;
        Vector2 lastMousePosition;
        bool hasLastMousePosition;
        bool mouseHasLeft;
        bool openedInMouseMode;
        bool directionalFocusActive;
        bool resumePlaylistMusic;
        int touchPressedChoice = -1;
        int touchActivationChoice = -1;
        bool touchReleasePending;

        public override bool PreserveMouseModeForDirectionalInput => openedInMouseMode;

        public ConversationType Type { get; private set; }
        public bool PlayMusic { get; set; } = true;

        Conversation conversation;

        ConversationActionType result;
        public ConversationActionType Result
        {
            get { return result; }
        }
        public int ResultChoice { get; private set; } = -1;

        public DialogWindow(Module app) 
            : base(app)
        {
            IsModal = true;
            Size = FramePos + FrameSize;

            face = new FaceWindow(app);
            face.DisplayOnly = true;
            Windows += face;

            fontText = new GuiFont(BurntimeClassic.FontName, ClassicColors.DialogText);
            fontOptions = new GuiFont(BurntimeClassic.FontName, ClassicColors.MenuText);
            fontFocusedChoice = new GuiFont(BurntimeClassic.FontName, ClassicColors.MenuTextHover);

            CaptureAllMouseMove = true;
        }

        public override void OnShow()
        {
            HasFocus = true;
            openedInMouseMode = app.LastInputMode == InputMode.Mouse;
            directionalFocusActive = false;
            lastMousePosition = app.DeviceManager.Mouse.Position - PositionOnScreen;
            hasLastMousePosition = true;
            mouseHasLeft = false;
            ResetFocus();
            base.OnShow();

            if (PlayMusic)
            {
                resumePlaylistMusic =
                    BurntimeClassic.Instance.MapMusicMode == MapMusicMode.List &&
                    BurntimeClassic.Instance.Engine.Music.IsPlayingFromPlaylist;
                if (resumePlaylistMusic)
                    BurntimeClassic.Instance.Engine.Music.RememberPlaylistSong();
                BurntimeClassic.Instance.Engine.Music.Play("talking");
            }
        }

        public override void OnHide()
        {
            HasFocus = false;
            base.OnHide();

            if (PlayMusic)
            {
                if (BurntimeClassic.Instance.MapMusicMode == MapMusicMode.List &&
                    resumePlaylistMusic)
                    BurntimeClassic.Instance.Engine.Music.PlayPlaylist();
                else if (BurntimeClassic.Instance.MapMusicMode == MapMusicMode.None)
                    BurntimeClassic.Instance.Engine.Music.Stop();
                resumePlaylistMusic = false;
            }
        }

        public void SetCharacter(Character character, Conversation conversation, bool showFace = false,
            int textLinesPerPage = 2)
        {
            result = ConversationActionType.None;
            ResultChoice = -1;
            self = null;
            this.character = character;
            this.conversation = conversation;
            this.textLinesPerPage = System.Math.Clamp(textLinesPerPage, 1, 3);
            face.IsVisible = showFace;
            if (showFace)
            {
                face.FaceID = character.FaceID;
            }

            dialogmode = conversation.Text.Length <= this.textLinesPerPage ? 1 : 0;
            dlgoffset = 0;
            focusChoiceIndex = FirstVisibleChoice();

            ready = true;
        }

        public void SetCharacter(Character self, Character character)
        {
            SetCharacter(self, character, ConversationType.Greeting);
        }

        public void SetCharacter(Character self, Character character, ConversationType type)
        {
            result = ConversationActionType.None;
            ResultChoice = -1;
            this.self = self;
            this.character = character;
            Type = type;
            face.IsVisible = true;
            face.FaceID = character.FaceID;

            conversation = character.Dialog.GetConversation(self, type);

            textLinesPerPage = 2;
            dialogmode = conversation.Text.Length <= textLinesPerPage ? 1 : 0;
            dlgoffset = 0;
            focusChoiceIndex = FirstVisibleChoice();

            ready = true;
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            int clickedChoice;
            if (app.LastInputMode == InputMode.Touch && touchReleasePending)
            {
                clickedChoice = touchActivationChoice;
                touchReleasePending = false;
            }
            else
                clickedChoice = app.LastInputMode == InputMode.Touch
                    ? TouchChoiceAt(position)
                    : ChoiceAt(position);
            if (clickedChoice == 0 && dialogmode == 0)
            {
                AdvanceText();
            }
            else if (clickedChoice != -1)
                SelectChoice(clickedChoice);

            return true;
        }

        public override void OnTouchPress(Vector2 position)
        {
            touchPressedChoice = TouchChoiceAt(position);
            touchActivationChoice = -1;
            touchReleasePending = false;
            focusChoiceIndex = touchPressedChoice;
        }

        public override void OnTouchRelease(Vector2 position, bool cancelled)
        {
            int releasedChoice = cancelled ? -1 : TouchChoiceAt(position);
            touchActivationChoice = releasedChoice == touchPressedChoice
                ? releasedChoice
                : -1;
            touchReleasePending = !cancelled;
            focusChoiceIndex = touchActivationChoice;
            touchPressedChoice = -1;
        }

        public override bool OnInputAction(InputAction action)
        {
            if (action == InputAction.Back)
            {
                result = ConversationActionType.Exit;
                Hide();
                return true;
            }

            if (dialogmode == 0)
            {
                if (action != InputAction.Primary)
                    return false;

                AdvanceText();
                return true;
            }

            if (dialogmode != 1)
                return false;

            if (action.IsUp())
            {
                directionalFocusActive = true;
                MoveFocus(-1);
                return true;
            }

            if (action.IsDown())
            {
                directionalFocusActive = true;
                MoveFocus(1);
                return true;
            }

            if (action != InputAction.Primary)
                return false;

            if (focusChoiceIndex == -1)
            {
                focusChoiceIndex = FirstVisibleChoice();
                return true;
            }

            SelectChoice(focusChoiceIndex);
            return true;
        }

        void AdvanceText()
        {
            dlgoffset += textLinesPerPage;
            if (dlgoffset + textLinesPerPage >= conversation.Text.Length)
                dialogmode = 1;
            ResetFocus();
        }

        int FirstVisibleChoice()
        {
            for (int i = 0; i < conversation.Choices.Length; i++)
                if (!string.IsNullOrEmpty(conversation.Choices[i].Text))
                    return i;

            return -1;
        }

        void MoveFocus(int direction)
        {
            if (focusChoiceIndex == -1)
                focusChoiceIndex = FirstVisibleChoice();
            if (focusChoiceIndex == -1)
                return;

            for (int offset = 1; offset < conversation.Choices.Length; offset++)
            {
                int candidate = (focusChoiceIndex + direction * offset + conversation.Choices.Length) % conversation.Choices.Length;
                if (!string.IsNullOrEmpty(conversation.Choices[candidate].Text))
                {
                    focusChoiceIndex = candidate;
                    return;
                }
            }
        }

        void SelectChoice(int choice)
        {
            BurntimeClassic classic = app as BurntimeClassic;
            ResultChoice = choice;
            result = conversation.Choices[choice].Action.Type;

            dialogmode = 0;
            dlgoffset = 0;
            switch (conversation.Choices[choice].Action.Type)
            {
                case ConversationActionType.Talk:
                    conversation = character.Dialog.GetConversation(self, ConversationType.Talk);
                    break;
                case ConversationActionType.Trade:
                    // Trade takes over the music after the scene fade. Keep the
                    // remembered map track paused instead of briefly resuming it
                    // in OnHide; returning from trade restores it normally.
                    resumePlaylistMusic = false;
                    Hide();
                    classic.Game.World.ActiveTraderObj = character as Trader;
                    classic.Game.World.ActivePlayerObj.Party.IgnoreRangeFilter =
                        !classic.Game.World.ActivePlayerObj.SingleMode;
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

            dialogmode = conversation.Text.Length <= textLinesPerPage ? 1 : 0;
            ResetFocus();
        }

        public override bool OnMouseMove(Vector2 position)
        {
            if (app.LastInputMode != InputMode.Mouse)
                return base.OnMouseMove(position);

            if (!mouseHasLeft && hasLastMousePosition && (position - lastMousePosition).Length <= 1)
                return base.OnMouseMove(position);

            lastMousePosition = position;
            hasLastMousePosition = true;
            mouseHasLeft = false;
            directionalFocusActive = false;

            if (position.x >= 0 && position.y >= 0 && position.x < Size.x && position.y < Size.y)
            {
                focusChoiceIndex = ChoiceAt(position);
            }

            return base.OnMouseMove(position);
        }

        public override void OnMouseLeave()
        {
            focusChoiceIndex = -1;
            mouseHasLeft = true;
            base.OnMouseLeave();
        }

        int ChoiceAt(Vector2 position)
        {
            Vector2 Pos = position;

            TextHelper txt = new TextHelper(app, "burn");
            int textx = 55;

            if (dialogmode == 0)
            {
                int texty = 85;
                String line = txt[499];
                if (Pos.x >= textx && Pos.y >= texty && Pos.x < textx + fontText.GetWidth(line) && Pos.y < texty + 10)
                    return 0;
            }
            else if (dialogmode == 1)
            {
                int texty = 63;
                for (int i = 0; i < 3; i++)
                {
                    String line = conversation.Choices[i].Text;

                    if (Pos.x >= textx && Pos.y >= texty && Pos.x < textx + fontText.GetWidth(line) && Pos.y < texty + 10)
                        return i;
                    texty += 11;
                }
            }

            return -1;
        }

        int TouchChoiceAt(Vector2 position)
        {
            TextHelper text = new(app, "burn");
            int selected = -1;
            float distance = float.MaxValue;

            void Consider(int choice, string line, int y)
            {
                if (string.IsNullOrEmpty(line))
                    return;
                Rect bounds = TouchHitTest.Expand(
                    new Rect(55, y, fontText.GetWidth(line), 10),
                    TouchHitTest.MinimumSize);
                float candidateDistance = (bounds.Center - position).Length;
                if (bounds.PointInside(position) && candidateDistance < distance)
                {
                    selected = choice;
                    distance = candidateDistance;
                }
            }

            if (dialogmode == 0)
                Consider(0, text[499], 85);
            else if (dialogmode == 1)
                for (int i = 0; i < conversation.Choices.Length; i++)
                    Consider(i, conversation.Choices[i].Text, 63 + 11 * i);

            return selected;
        }

        void ResetFocus()
        {
            touchPressedChoice = -1;
            touchActivationChoice = -1;
            touchReleasePending = false;
            focusChoiceIndex = app.LastInputMode == InputMode.Touch
                ? -1
                : openedInMouseMode && !directionalFocusActive
                ? ChoiceAt(lastMousePosition)
                : dialogmode == 0 ? 0 : FirstVisibleChoice();
        }

        public override void OnRender(RenderTarget target)
        {
            target.RenderRect(FramePos, FrameSize, new PixelColor(128, 0, 0, 0),
                postFilter: !face.IsVisible);

            base.OnRender(target);

            if (!ready)
                return;

            Vector2 textPos = new Vector2(43 + FrameSize.x / 2, 20);

            TextHelper txt = new TextHelper(app, "burn");

            for (int i = 0; i < textLinesPerPage; i++)
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

                if (focusChoiceIndex == 0)
                    fontFocusedChoice.DrawText(target, textPos, txt[499], TextAlignment.Left, VerticalTextAlignment.Top);
                else
                    fontOptions.DrawText(target, textPos, txt[499], TextAlignment.Left, VerticalTextAlignment.Top);
            }
            else
            {
                textPos.y = 63;

                for (int i = 0; i < 3; i++)
                {
                    String line = conversation.Choices[i].Text;

                    if (focusChoiceIndex == i)
                        fontFocusedChoice.DrawText(target, textPos, line, TextAlignment.Left, VerticalTextAlignment.Top);
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
            boss.SelectGroup(boss.Party);
        }
    }
}
