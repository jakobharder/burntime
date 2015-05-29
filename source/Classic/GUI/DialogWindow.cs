
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
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Classic.Logic;

namespace Burntime.Classic
{
    public class DialogWindow : Container
    {
        readonly static Vector2 FrameSize = new Vector2(208, 90);
        readonly static Vector2 FramePos = new Vector2(43, 10);

        FaceWindow face;
        GuiFont fontText;
        GuiFont fontOptions;
        GuiFont fontChoice;

        Character character;
        Character self;
        public bool Ended;
        bool ready = false;
        int dlgoffset = 0;
        int dialogmode;
        int hover = -1;

        ConversationType type;
        public ConversationType Type
        {
            get { return type; }
        }

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
            fontChoice = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));

            CaptureAllMouseMove = true;
        }

        public override void OnShow()
        {
            hover = -1;
            base.OnShow();

            BurntimeClassic.Instance.Engine.Music.Play("18_MUS 18_HSC.ogg");
        }

        public override void OnHide()
        {
            base.OnHide();

            BurntimeClassic.Instance.Engine.Music.Stop();
        }

        public void SetCharacter(Character character, Conversation conversation)
        {
            self = null;
            this.character = character;
            this.conversation = conversation;

            dialogmode = (conversation.Text.Length < 3) ? 1 : 0;
            dlgoffset = 0;

            ready = true;
        }

        public void SetCharacter(Character self, Character character)
        {
            SetCharacter(self, character, ConversationType.Greeting);
        }

        public void SetCharacter(Character self, Character character, ConversationType type)
        {
            this.self = self;
            this.character = character;
            this.type = type;
            face.FaceID = character.FaceID;

            BurntimeClassic classic = app as BurntimeClassic;

            conversation = character.Dialog.GetConversation(self, type);

            dialogmode = (conversation.Text.Length < 3) ? 1 : 0;
            dlgoffset = 0;

            ready = true;
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            if (hover == 0 && dialogmode == 0)
            {
                dlgoffset += 2;
                if (dlgoffset + 2 >= conversation.Text.Length)
                {
                    dialogmode = 1;
                }
            }
            else if (hover != -1)
            {
                BurntimeClassic classic = app as BurntimeClassic;
                result = conversation.Choices[hover].Action.Type;

                dialogmode = 0;
                dlgoffset = 0;
                switch (conversation.Choices[hover].Action.Type)
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

                dialogmode = (conversation.Text.Length <= 3) ? 1 : 0;
            }

            return true;
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
                    fontChoice.DrawText(target, textPos, txt[499], TextAlignment.Left, VerticalTextAlignment.Top);
                else
                    fontOptions.DrawText(target, textPos, txt[499], TextAlignment.Left, VerticalTextAlignment.Top);
            }
            else
            {
                textPos.y = 63;

                for (int i = 0; i < 3; i++)
                {
                    String line = conversation.Choices[i].Text;

                    if (hover == i)
                        fontChoice.DrawText(target, textPos, line, TextAlignment.Left, VerticalTextAlignment.Top);
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
