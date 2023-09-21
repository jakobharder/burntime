using System.Collections.Generic;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Classic.Logic.Interaction
{
    public struct Construction
    {
        public Conversation Dialog;
        public Constructions.ConstructionInfo construction;
    }

    public class Constructions : DataObject
    {
        public class ConstructionInfo
        {
            public string Result;
            public string[] Items;
            public string[] Tools;
            public string[] MainItems;
            public int Dialog;
            public bool[] Classes;
        }
        
        int defaultDialog;
        Dictionary<string, List<ConstructionInfo>> constructions = new Dictionary<string, List<ConstructionInfo>>();

        public Constructions(ConfigFile file)
        {
            defaultDialog = file[""].GetInt("dialog");

            ConfigSection[] sections = file.GetAllSections();

            foreach (ConfigSection section in sections)
            {
                ConstructionInfo c = new ConstructionInfo();
                c.Result = section.Name;
                c.Dialog = section.GetInt("dialog");
                c.Items = section.GetStrings("items");
                c.Tools = section.GetStrings("tools");
                c.MainItems = section.GetStrings("main_items");
                c.Classes = new bool[(int)CharClass.Count];

                string[] classNames = section.GetStrings("class");

                foreach (string name in classNames)
                {
                    try
                    {
                        CharClass enumValue = (CharClass)System.Enum.Parse(typeof(CharClass), name, true);
                        c.Classes[(int)enumValue] = true;
                    }
                    catch
                    {
                        Burntime.Platform.Log.Warning("Class not found in construction: " + name + "!");
                        continue;
                    }
                }

                foreach (string item in c.MainItems)
                {
                    if (!constructions.ContainsKey(item))
                        constructions.Add(item, new List<ConstructionInfo>());

                    constructions[item].Add(c);
                }
            }
        }

        bool CanConstruct(IItemCollection primary, IItemCollection secondary, ConstructionInfo construction)
        {
            for (int i = 0; i < construction.Items.Length; i++)
            {
                if (!primary.Contains(construction.Items[i]) && !secondary.Contains(construction.Items[i]))
                    return false;
            }

            for (int i = 0; i < construction.Tools.Length; i++)
            {
                if (!primary.Contains(construction.Tools[i]) && !secondary.Contains(construction.Tools[i]))
                    return false;
            }

            return true;
        }

        bool MainItemsAvailable(IItemCollection primary, IItemCollection secondary, ConstructionInfo construction)
        {
            for (int i = 0; i < construction.MainItems.Length; i++)
            {
                if (primary.Contains(construction.MainItems[i]) || secondary.Contains(construction.MainItems[i]))
                    return true;
            }

            return false;
        }

        ConstructionInfo FindConstruction(IItemCollection primary, IItemCollection secondary, ItemType mainItem, CharClass charClass, out bool canBuild)
        {
            canBuild = false;
            ConstructionInfo best = null;

            if (constructions.ContainsKey(mainItem))
            {
                for (int i = 0; i < constructions[mainItem].Count; i++)
                {
                    ConstructionInfo construction = constructions[mainItem][i];
                    if (!construction.Classes[(int)charClass])
                        continue;

                    if (CanConstruct(primary, secondary, construction))
                    {
                        best = construction;
                        canBuild = true;
                        break;
                    }
                    else if (best == null && MainItemsAvailable(primary, secondary, construction))
                    {
                        best = construction;
                    }
                }
            }

            return best;
        }

        public void Construct(Construction construction, Character technician, IItemCollection roomItems, Item mainItem, ClassicGame world)
        {
            IItemCollection insert = technician.Items;
            if (!technician.Items.Contains(mainItem))
                insert = roomItems;

            for (int i = 0; i < construction.construction.Items.Length; i++)
            {
                Item item = technician.Items.Find(world.ItemTypes[construction.construction.Items[i]]);
                if (item != null)
                {
                    technician.Items.Remove(item);
                }
                else
                {
                    item = roomItems.Find(world.ItemTypes[construction.construction.Items[i]]);
                    roomItems.Remove(item);
                }
            }

            insert.Add(world.ItemTypes[construction.construction.Result].Generate());
        }

        public Construction GetConstruction(Character technician, IItemCollection roomItems, Item mainItem)
        {
            int index = 3; // TODO

            bool canBuild = false;

            ConstructionInfo construction = FindConstruction(technician.Items, roomItems, mainItem.Type, technician.Class, out canBuild);

            Conversation conv = new Conversation();

            if (construction == null)
            {
                conv.Text = ResourceManager.GetStrings("men_" + defaultDialog.ToString("D3") + "?s" + index);
            }
            else
            {
                conv.Text = ResourceManager.GetStrings("men_" + construction.Dialog.ToString("D3") + "?s" + index);

                string[] repl = new string[] { "|E", "|F", "|G", "|H" };

                for (int i = 0; i < conv.Text.Length; i++)
                {
                    conv.Text[i] = conv.Text[i].Replace("|I", BurntimeClassic.Instance.Game.ItemTypes[construction.Result].Text);
                    for (int j = 0; j < construction.Items.Length; j++)
                    {
                        conv.Text[i] = conv.Text[i].Replace(repl[j], BurntimeClassic.Instance.Game.ItemTypes[construction.Items[j]].Text);
                    }
                }
            }

            conv.Choices = new ConversationChoice[3];

            conv.Choices[0] = new ConversationChoice();
            conv.Choices[1] = new ConversationChoice();
            if (canBuild)
            {
                conv.Choices[1].Action = new ConversationAction(ConversationActionType.Yes);
                conv.Choices[1].Text = ResourceManager.GetString("burn?501").Replace("|I", BurntimeClassic.Instance.Game.ItemTypes[construction.Result].Text);
            }
            conv.Choices[2] = new ConversationChoice();
            conv.Choices[2].Action = new ConversationAction(ConversationActionType.No);
            conv.Choices[2].Text = ResourceManager.GetString("burn?503");

            Construction constr = new Construction();
            constr.Dialog = conv;
            constr.construction = construction;
            return constr;
        }
    }
}
