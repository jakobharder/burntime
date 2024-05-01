using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Framework.States;
using Burntime.Platform.Resource;
using Burntime.Framework;

namespace Burntime.Remaster.Logic
{
    [Serializable]
    public class Dialog : StateObject, ITurnable
    {
        static StateLink<Dialog> lastDialog = null;
        StateLink<Character> parent;

        // save already talked player
        protected StateLinkList<Player> talkedPlayer;

        public Character Parent
        {
            get { return parent; }
            set { parent = value; }
        }

        public int MenFile = 0;

        protected override void InitInstance(object[] parameter)
        {
            talkedPlayer = container.CreateLinkList<Player>();
        }

        Conversation GetDismissConversation(Character boss)
        {
            int index = 3; // TODO

            Conversation conv = new Conversation();
            conv.Text = ResourceManager.GetStrings("men_502?s" + index);
            conv.Choices = new ConversationChoice[3];

            conv.Choices[0] = new ConversationChoice();
            conv.Choices[0].Action = new ConversationAction(ConversationActionType.None);
            conv.Choices[0].Text = "";
            conv.Choices[1] = new ConversationChoice();
            conv.Choices[1].Action = new ConversationAction(ConversationActionType.No);
            conv.Choices[1].Text = ResourceManager.GetString("burn?495");
            conv.Choices[2] = new ConversationChoice();
            conv.Choices[2].Action = new ConversationAction(ConversationActionType.Yes);
            conv.Choices[2].Text = ResourceManager.GetString("burn?496");

            return conv;
        }

        Conversation GetAbandonConversation(Character boss)
        {
            int index = 3; // TODO

            Conversation conv = new Conversation();
            conv.Text = ResourceManager.GetStrings("men_501?s" + index);
            conv.Choices = new ConversationChoice[3];

            conv.Choices[0] = new ConversationChoice();
            conv.Choices[0].Action = new ConversationAction(ConversationActionType.None);
            conv.Choices[0].Text = "";
            conv.Choices[1] = new ConversationChoice();
            conv.Choices[1].Action = new ConversationAction(ConversationActionType.No);
            conv.Choices[1].Text = ResourceManager.GetString("burn?494");
            conv.Choices[2] = new ConversationChoice();
            conv.Choices[2].Action = new ConversationAction(ConversationActionType.Yes);
            conv.Choices[2].Text = ResourceManager.GetString("burn?505");

            return conv;
        }

        Conversation GetCaptureConversation(Character boss)
        {
            int index = 3; // TODO

            Conversation conv = new Conversation();
            conv.Text = ResourceManager.GetStrings("men_500?s" + index);
            conv.Choices = new ConversationChoice[3];

            conv.Choices[0] = new ConversationChoice();
            conv.Choices[0].Action = new ConversationAction(ConversationActionType.None);
            conv.Choices[0].Text = "";
            conv.Choices[1] = new ConversationChoice();
            conv.Choices[1].Action = new ConversationAction(ConversationActionType.None);
            conv.Choices[1].Text = "";
            conv.Choices[2] = new ConversationChoice();
            conv.Choices[2].Action = new ConversationAction(ConversationActionType.Exit);
            conv.Choices[2].Text = ResourceManager.GetString("burn?490");

            return conv;
        }

        Conversation GetGreetingConversation(Character boss)
        {
            int index = (lastDialog == null || lastDialog.Object != this) ? 2 : 3;
            int file = MenFile;

            if (Parent.Player != null && Parent.Player != boss.Player)
                file = 510;

            Conversation conv = new Conversation();
            conv.Text = ResourceManager.GetStrings("men_" + file.ToString("D3") + "?s" + index);
            conv.Choices = new ConversationChoice[3];

            conv.Choices[0] = new ConversationChoice();
            conv.Choices[1] = new ConversationChoice();
            if (Parent.Class == CharClass.Trader && boss.IsPlayerCharacter && file != 510)
            {
                conv.Choices[0].Action = new ConversationAction(ConversationActionType.Trade);
                conv.Choices[0].Text = ResourceManager.GetString("burn?500");
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.Talk);
                conv.Choices[1].Text = ResourceManager.GetString("burn?492");
            }
            else if (Parent.Class != CharClass.Mutant && Parent.Class != CharClass.Dog && boss.IsPlayerCharacter && file != 510
                && boss.Player.Group.Count < Logic.Group.MAX_PEOPLE)
            {
                conv.Choices[0].Action = new ConversationAction(ConversationActionType.Talk);
                conv.Choices[0].Text = ResourceManager.GetString("burn?492");
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.HireRequirements);
                conv.Choices[1].Text = ResourceManager.GetString("burn?491");
            }
            else
            {
                conv.Choices[0].Action = new ConversationAction(ConversationActionType.None);
                conv.Choices[0].Text = "";
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.Talk);
                conv.Choices[1].Text = ResourceManager.GetString("burn?492");
            }
            conv.Choices[2] = new ConversationChoice();
            conv.Choices[2].Action = new ConversationAction(ConversationActionType.Exit);
            conv.Choices[2].Text = ResourceManager.GetString("burn?490");

            return conv;
        }

        Conversation GetTalkConversation(Character boss)
        {
            int index = 0;
            int file = MenFile;

            if (Parent.Player != null && Parent.Player != boss.Player)
                file = 510;

            Conversation conv = new Conversation();
            conv.Text = ResourceManager.GetStrings("men_" + file.ToString("D3") + "?s" + index);
            conv.Choices = new ConversationChoice[3];

            conv.Choices[0] = new ConversationChoice();
            conv.Choices[1] = new ConversationChoice();
            if (Parent.Class == CharClass.Trader && boss.IsPlayerCharacter && file != 510)
            {
                conv.Choices[0].Action = new ConversationAction(ConversationActionType.Trade);
                conv.Choices[0].Text = ResourceManager.GetString("burn?500");
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.Talk);
                conv.Choices[1].Text = ResourceManager.GetString("burn?492");
            }
            else if (Parent.Class != CharClass.Mutant && Parent.Class != CharClass.Dog && boss.IsPlayerCharacter && file != 510 && boss.Player.Group.Count < 5)
            {
                conv.Choices[0].Action = new ConversationAction(ConversationActionType.Talk);
                conv.Choices[0].Text = ResourceManager.GetString("burn?492");
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.HireRequirements);
                conv.Choices[1].Text = ResourceManager.GetString("burn?491");
            }
            else
            {
                conv.Choices[0].Action = new ConversationAction(ConversationActionType.None);
                conv.Choices[0].Text = "";
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.Talk);
                conv.Choices[1].Text = ResourceManager.GetString("burn?492");
            }
            conv.Choices[2] = new ConversationChoice();
            conv.Choices[2].Action = new ConversationAction(ConversationActionType.Exit);
            conv.Choices[2].Text = ResourceManager.GetString("burn?490");

            return conv;
        }


        public virtual Conversation GetConversation(Character boss, ConversationType type)
        {
            lastDialog = this;

            // change conversation to talk if met the first time
            if (!talkedPlayer.Contains(boss.Player))
            {
                talkedPlayer.Add(boss.Player);
                type = ConversationType.Talk;
            }
         
            switch (type)
            {
                case ConversationType.Dismiss: return GetDismissConversation(boss);
                case ConversationType.Abandon: return GetAbandonConversation(boss);
                case ConversationType.Capture: return GetCaptureConversation(boss);
                case ConversationType.Greeting: return GetGreetingConversation(boss);
                case ConversationType.Talk: return GetTalkConversation(boss);
            }

            int index = 0;
            Item hireItem = null;
            ItemType hireItemType = null;

            bool hire = false;
            if (type == ConversationType.Hire)
            {
                if (boss.Experience >= Parent.Experience * 0.66f)
                {
                    for (int i = 0; hireItem == null && i < Parent.HireItems.Count; i++)
                    {
                        hireItem = boss.Items.Find(Parent.HireItems[i]);
                    }

                    if (null != hireItem)
                    {
                        hire = true;
                        index = 4;
                        hireItemType = hireItem.Type;
                    }
                    else
                    {
                        hireItemType = Parent.HireItems[0];
                        index = 5;
                    }
                }
                else
                    index = 7;
            }
            else if (type == ConversationType.Profession)
            {
                for (int i = 0; hireItem == null && i < Parent.HireItems.Count; i++)
                {
                    hireItem = boss.Items.Find(Parent.HireItems[i]);
                }

                hire = true;
                index = 4;
                hireItemType = hireItem.Type;

                index = 6;
            }

            lastDialog = this;

            Conversation conv = new Conversation();
            conv.Text = ResourceManager.GetStrings("men_" + MenFile.ToString("D3") + "?s" + index);
            conv.Choices = new ConversationChoice[3];

            for (int i = 0; i < conv.Text.Length; i++)
            {
                conv.Text[i] = conv.Text[i].Replace("|A", ResourceManager.GetString("burn?431"));
                conv.Text[i] = conv.Text[i].Replace("|B", ResourceManager.GetString("burn?" + (40 + (int)Parent.Class)));
                if (hireItemType != null)
                    conv.Text[i] = conv.Text[i].Replace("|C", hireItemType.Text);
            }

            conv.Choices[0] = new ConversationChoice();
            conv.Choices[1] = new ConversationChoice();
            if (Parent.Class == CharClass.Trader && boss.IsPlayerCharacter)
            {
                conv.Choices[0].Action = new ConversationAction(ConversationActionType.Trade);
                conv.Choices[0].Text = ResourceManager.GetString("burn?500");
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.Talk);
                conv.Choices[1].Text = ResourceManager.GetString("burn?492");
            }
            else if (Parent.Class != CharClass.Mutant && Parent.Class != CharClass.Dog && boss.IsPlayerCharacter)
            {
                if (hire)
                {
                    conv.Choices[0].Action = new ConversationAction(ConversationActionType.Profession);
                    conv.Choices[0].Text = ResourceManager.GetString("burn?497");
                    conv.Choices[1].Action = new ConversationAction(ConversationActionType.Hire);
                    conv.Choices[1].Text = ResourceManager.GetString("burn?498").Replace("|C", hireItemType.Text);
                }
                else
                {
                    conv.Choices[0].Action = new ConversationAction(ConversationActionType.Talk);
                    conv.Choices[0].Text = ResourceManager.GetString("burn?492");
                    conv.Choices[1].Action = new ConversationAction(ConversationActionType.HireRequirements);
                    conv.Choices[1].Text = ResourceManager.GetString("burn?491");
                }
            }
            else
            {
                conv.Choices[0].Action = new ConversationAction(ConversationActionType.None);
                conv.Choices[0].Text = "";
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.Talk);
                conv.Choices[1].Text = ResourceManager.GetString("burn?492");
            }
            conv.Choices[2] = new ConversationChoice();
            conv.Choices[2].Action = new ConversationAction(ConversationActionType.Exit);
            conv.Choices[2].Text = ResourceManager.GetString("burn?490");

            return conv;
        }

        public virtual void Turn()
        {
        }
    }

    public class Conversation
    {
        public String[] Text;
        public ConversationChoice[] Choices;

        public static Conversation Simple(IResourceManager resourceManager, string firstLineId, string buttonId = "newburn?45")
        {
            return new()
            {
                Text = resourceManager.GetStrings(firstLineId),
                Choices = new ConversationChoice[3]
                    {
                        new ConversationChoice(),
                        new ConversationChoice(),
                        new ConversationChoice {
                            Action = new ConversationAction(ConversationActionType.Exit),
                            Text = resourceManager.GetString(buttonId)
                        }
                    }
            };
        }

        public static Conversation Simple(TextHelper text, int firstLineId, int buttonId = 45)
        {
            return new()
            {
                Text = text.GetStrings(firstLineId),
                Choices = new ConversationChoice[3]
                    {
                        new ConversationChoice(),
                        new ConversationChoice(),
                        new ConversationChoice {
                            Action = new ConversationAction(ConversationActionType.Exit),
                            Text = text.Get(buttonId)
                        }
                    }
            };
        }
    }

    public enum ConversationActionType
    {
        None,
        Talk,
        Trade,
        Exit,
        HireRequirements,
        Hire,
        Profession,
        Yes,
        No
    }

    public enum ConversationType
    {
        Talk,
        Greeting,
        Hire,
        Profession,
        Dismiss,
        Abandon,
        Capture
    }

    public class ConversationAction
    {
        public ConversationActionType Type;

        public ConversationAction(ConversationActionType Type)
        {
            this.Type = Type;
        }
    }

    public class ConversationChoice
    {
        public String Text = "";
        public ConversationAction Action = new ConversationAction(ConversationActionType.None);
    }
}