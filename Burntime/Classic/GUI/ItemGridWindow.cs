using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.States;

using Burntime.Classic.Logic;

namespace Burntime.Classic.GUI
{
    public class ItemGridWindow : Container, IItemCollection
    {
        Vector2 grid = new Vector2();
        bool doubleLayered = false;
        Vector2 spacing = new Vector2();
        Vector2 size = new Vector2(32, 32);
        ItemWindow[] itemWindows;
        IItemCollection mask;
        ItemList selection;
        Sprite maskSprite;
        Sprite selectionSprite;

        int[] gridPositions;
        bool lockPositions = false;
        public bool LockPositions
        {
            get { return lockPositions; }
            set { lockPositions = value; }
        }

        public LogicEvent LeftClickItemEvent;
        public LogicEvent RightClickItemEvent;

        List<Item> items = new List<Item>();
        public Vector2 Grid
        {
            get { return grid; }
            set { grid = value; RefreshWindows(); }
        }

        public Vector2 Spacing
        {
            get { return spacing; }
            set { spacing = value; RefreshWindows(); }
        }

        public bool DoubleLayered
        {
            get { return doubleLayered; }
            set { doubleLayered = value; RefreshWindows(); }
        }

        public int MaxCount
        {
            get { return grid.x * grid.y + (doubleLayered ? (grid.x - 1) * (grid.y - 1) : 0); }
        }

        public IItemCollection Mask
        {
            get { return mask; }
            set { mask = value; }
        }

        public ItemList Selection
        {
            get { return selection; }
        }

        public ItemGridWindow(Module App)
            : base(App)
        {
            maskSprite = App.ResourceManager.GetImage("gfx/grid.png");
            selectionSprite = App.ResourceManager.GetImage("inv.raw?3");
        }

        public override void OnRender(RenderTarget Target)
        {
            if (mask != null)
            {
                Target.Layer += 3;

                for (int i = 0; i < items.Count; i++)
                {
                    if (mask.Contains(items[i]))
                        Target.DrawSprite(itemWindows[i].Position, maskSprite);
                }


                Target.Layer -= 3;
            }

            if (selection != null && selection.Count > 0)
            {
                Target.Layer += 3;

                for (int i = 0; i < itemWindows.Length; i++)
                {
                    if (gridPositions[i] >= 0 && gridPositions[i] < items.Count &&
                        selection.Contains(items[gridPositions[i]]))
                        Target.DrawSprite(itemWindows[i].Position, selectionSprite);
                }


                Target.Layer -= 3;
            }
        }

        public void Clear()
        {
            items.Clear();
            RefreshContent();
            selection = app.GameState.Container.Create<ItemList>(StateObjectOptions.Temporary);
        }

        public bool Add(Item item)
        {
            if (items.Count >= MaxCount)
                return false;
            items.Add(item);

            if (lockPositions)
            {
                for (int i = 0; i < itemWindows.Length; i++)
                {
                    if (itemWindows[i].ItemID == "")
                    {
                        gridPositions[i] = items.Count - 1;
                        itemWindows[i].ItemID = item.ID;
                        break;
                    }
                }
            }
            else
                RefreshContent();
            
            return true;
        }

        public void Remove(Item item)
        {
            if (lockPositions)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == item)
                    {
                        for (int j = 0; j < itemWindows.Length; j++)
                        {
                            if (gridPositions[j] > i)
                                gridPositions[j]--;
                            else if (gridPositions[j] == i)
                            {
                                itemWindows[j].ItemID = "";
                                gridPositions[j] = -1;
                            }
                        }
                       
                        items.Remove(item);
                        break;
                    }
                }
            }
            else
            {
                items.Remove(item);
                RefreshContent();
            }
        }

        public int Count
        {
            get { return items.Count; }
        }

        public Item this[int index]
        {
            get { return items[index]; }
            set { items[index] = value; }
        }

        public bool Contains(Item item)
        {
            return items.Contains(item);
        }

        public void Update(Item item)
        {
            for (int i = 0; i < itemWindows.Length; i++)
            {
                if (itemWindows[i].ItemID != "" && items[gridPositions[i]] == item)
                {
                    itemWindows[i].ItemID = item.ID;
                }
            }
        }

        void RefreshWindows()
        {
            if (itemWindows != null)
            {
                foreach (Window wnd in itemWindows)
                    Windows -= wnd;
            }

            base.Size = grid * size + (grid + 1) * spacing;

            int count = grid.Count;
            if (doubleLayered && count > 0)
                count += (grid - 1).Count;
            if (count == 0)
            {
                itemWindows = null;
                gridPositions = null;
            }
            else if (itemWindows == null || itemWindows.Length != count)
            {
                itemWindows = new ItemWindow[count];
                gridPositions = new int[itemWindows.Length];
                foreach (Vector2 p in (Rect)grid)
                {
                    itemWindows[p.GetIndex(grid)] = new ItemWindow(app);
                    itemWindows[p.GetIndex(grid)].Position = p * (size + spacing);
                    itemWindows[p.GetIndex(grid)].LeftClickEvent += OnLeftClickItem;
                    itemWindows[p.GetIndex(grid)].RightClickEvent += OnRightClickItem;
                    Windows += itemWindows[p.GetIndex(grid)];
                }

                if (doubleLayered)
                {
                    foreach (Vector2 p in (Rect)(grid - 1))
                    {
                        itemWindows[p.GetIndex(grid - 1) + grid.Count] = new ItemWindow(app);
                        itemWindows[p.GetIndex(grid - 1) + grid.Count].Position = p * (size + spacing) + size / 2;
                        itemWindows[p.GetIndex(grid - 1) + grid.Count].LeftClickEvent += OnLeftClickItem;
                        itemWindows[p.GetIndex(grid - 1) + grid.Count].RightClickEvent += OnRightClickItem;
                        Windows += itemWindows[p.GetIndex(grid - 1) + grid.Count];
                    }
                }
            }

            RefreshContent();
        }

        void RefreshContent()
        {
            if (itemWindows == null)
                return;

            for (int i = 0; i < itemWindows.Length; i++)
            {
                if (items.Count > i)
                {
                    itemWindows[i].ItemID = items[i].ID;
                    gridPositions[i] = i;
                }
                else
                {
                    itemWindows[i].ItemID = "";
                    gridPositions[i] = -1;
                }
            }
        }

        void OnLeftClickItem(int index)
        {
            if (LeftClickItemEvent != null && itemWindows[index].ItemID != "")
            {
                LeftClickItemEvent.Execute(items[gridPositions[index]]);
            }
        }

        void OnRightClickItem(int index)
        {
            if (RightClickItemEvent != null && itemWindows[index].ItemID != "")
            {
                RightClickItemEvent.Execute(items[gridPositions[index]]);
            }
        }

        // IEnumerable interface implementation
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new ItemCollectionEnumerator(this);
        }
    }
}