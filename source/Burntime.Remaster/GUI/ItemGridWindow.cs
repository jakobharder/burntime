
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Burntime.Remaster.GUI
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
        ISprite maskSprite;
        ISprite selectionSprite;
        GuiFont selectionFont;
        int keyboardIndex = -1;
        Vector2? lastSelectionPosition;
        public bool KeyboardSelectionVisible { get; set; }
        public event Action<ItemGridWindow> MouseSelectionChanged;
        public event Action<ItemGridWindow, Vector2> SelectionEmptied;

        bool unifiedSelection;
        public bool UnifiedSelection
        {
            get { return unifiedSelection; }
            set
            {
                unifiedSelection = value;
                if (itemWindows != null)
                {
                    foreach (ItemWindow itemWindow in itemWindows)
                        itemWindow.ShowHoverText = !value;
                }
            }
        }

        bool hasLastMousePosition;
        bool mouseHasLeft;
        Vector2 lastMousePosition;

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

        public bool HasKeyboardItems => items.Count > 0;

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
            selectionFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));
            selectionFont.Borders = TextBorders.Screen;
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

            if (KeyboardSelectionVisible && IsValidKeyboardIndex(keyboardIndex))
            {
                Target.Layer += 5;
                Vector2 itemPosition = itemWindows[keyboardIndex].Position;
                RenderTarget bigger = Target.GetSubBuffer(new Rect(itemPosition - new Vector2(50, 50), new Vector2(132, 132)));
                string title = BurntimeClassic.Instance.Game.ItemTypes[itemWindows[keyboardIndex].ItemID].Title;
                selectionFont.DrawText(bigger, new Vector2(66, 41), title, TextAlignment.Center, VerticalTextAlignment.Top);
                Target.Layer -= 5;
            }
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

            for (int i = itemWindows?.Length - 1 ?? -1; i >= 0; i--)
            {
                if (IsValidKeyboardIndex(i) && itemWindows[i].Boundings.PointInside(position))
                {
                    keyboardIndex = i;
                    MouseSelectionChanged?.Invoke(this);
                    break;
                }
            }

            return base.OnMouseMove(position);
        }

        public override void OnMouseLeave()
        {
            mouseHasLeft = true;
            base.OnMouseLeave();
        }

        internal void SelectFromMouseClick(int index)
        {
            if (!UnifiedSelection || !IsValidKeyboardIndex(index))
                return;

            keyboardIndex = index;
            MouseSelectionChanged?.Invoke(this);
        }

        public void ResetKeyboardSelection()
        {
            keyboardIndex = -1;
            SelectFirstKeyboardItem();
        }

        public bool EnsureKeyboardSelection()
        {
            return IsValidKeyboardIndex(keyboardIndex) || SelectFirstKeyboardItem();
        }

        public bool MoveKeyboardSelection(Vector2 direction)
        {
            if (!IsValidKeyboardIndex(keyboardIndex))
                return SelectFirstKeyboardItem();

            Vector2 current = itemWindows[keyboardIndex].Position + size / 2;
            int selected = -1;
            int selectedScore = int.MaxValue;

            for (int i = 0; i < itemWindows.Length; i++)
            {
                if (!IsValidKeyboardIndex(i) || i == keyboardIndex)
                    continue;

                Vector2 candidate = itemWindows[i].Position + size / 2;
                Vector2 difference = candidate - current;

                // The offset room layer is a separate horizontal grid. Left and
                // right stay on the current layer and row; vertical movement is
                // what crosses between the interleaved layers.
                if (doubleLayered && direction.x != 0)
                {
                    bool currentSecondLayer = keyboardIndex >= grid.Count;
                    bool candidateSecondLayer = i >= grid.Count;
                    if (currentSecondLayer != candidateSecondLayer || candidate.y != current.y)
                        continue;
                }

                int forward = difference.x * direction.x + difference.y * direction.y;
                if (forward <= 0)
                    continue;

                int sideways = System.Math.Abs(difference.x * direction.y - difference.y * direction.x);
                int score = sideways * 1000 + forward;
                if (score < selectedScore)
                {
                    selected = i;
                    selectedScore = score;
                }
            }

            if (selected == -1)
                return false;

            keyboardIndex = selected;
            return true;
        }

        public bool SelectKeyboardEdge(Vector2 direction, Vector2 sourcePosition)
        {
            int selected = -1;
            int selectedScore = int.MaxValue;

            for (int i = 0; itemWindows != null && i < itemWindows.Length; i++)
            {
                if (!IsValidKeyboardIndex(i))
                    continue;

                Vector2 candidate = PositionOnScreen + itemWindows[i].Position + size / 2;
                int edge = direction.x > 0 ? candidate.x : -candidate.x;
                int rowDistance = System.Math.Abs(candidate.y - sourcePosition.y);
                int score = edge * 1000 + rowDistance;
                if (score < selectedScore)
                {
                    selected = i;
                    selectedScore = score;
                }
            }

            if (selected == -1)
                return false;

            keyboardIndex = selected;
            return true;
        }

        public Vector2? KeyboardSelectionPosition
        {
            get
            {
                if (IsValidKeyboardIndex(keyboardIndex))
                {
                    lastSelectionPosition = PositionOnScreen + itemWindows[keyboardIndex].Position + size / 2;
                    return lastSelectionPosition;
                }

                return lastSelectionPosition;
            }
        }

        public bool ActivateKeyboardItem(bool secondary)
        {
            if (!IsValidKeyboardIndex(keyboardIndex))
                return false;

            Vector2 previousPosition = itemWindows[keyboardIndex].Position;
            Item item = items[gridPositions[keyboardIndex]];
            if (secondary)
                RightClickItemEvent?.Execute(item);
            else
                LeftClickItemEvent?.Execute(item);

            if (!IsValidKeyboardIndex(keyboardIndex))
                SelectNearestKeyboardItem(previousPosition);
            return true;
        }

        bool SelectFirstKeyboardItem()
        {
            if (itemWindows == null)
                return false;

            for (int i = 0; i < itemWindows.Length; i++)
            {
                if (IsValidKeyboardIndex(i))
                {
                    keyboardIndex = i;
                    return true;
                }
            }

            keyboardIndex = -1;
            return false;
        }

        void SelectNearestKeyboardItem(Vector2 position)
        {
            keyboardIndex = -1;
            int nearestDistance = int.MaxValue;
            for (int i = 0; itemWindows != null && i < itemWindows.Length; i++)
            {
                if (!IsValidKeyboardIndex(i))
                    continue;

                Vector2 difference = itemWindows[i].Position - position;
                int distance = System.Math.Abs(difference.x) + System.Math.Abs(difference.y);
                if (distance < nearestDistance)
                {
                    keyboardIndex = i;
                    nearestDistance = distance;
                }
            }
        }

        bool IsValidKeyboardIndex(int index)
        {
            return itemWindows != null && gridPositions != null && index >= 0 && index < itemWindows.Length &&
                gridPositions[index] >= 0 && gridPositions[index] < items.Count &&
                !string.IsNullOrEmpty(itemWindows[index].ItemID);
        }

        public void Clear()
        {
            if (IsValidKeyboardIndex(keyboardIndex))
                lastSelectionPosition = PositionOnScreen + itemWindows[keyboardIndex].Position + size / 2;

            items.Clear();
            RefreshContent();
            selection = app.GameState.Container.Create<ItemList>(StateObjectOptions.Temporary);
            keyboardIndex = -1;
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

            if (!IsValidKeyboardIndex(keyboardIndex))
                SelectFirstKeyboardItem();
            
            return true;
        }

        public void Remove(Item item)
        {
            Vector2? removedSelectionPosition = items.Count == 1 && IsValidKeyboardIndex(keyboardIndex)
                ? PositionOnScreen + itemWindows[keyboardIndex].Position + size / 2
                : null;

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

            if (items.Count == 0 && removedSelectionPosition.HasValue)
                SelectionEmptied?.Invoke(this, removedSelectionPosition.Value);
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
                    var index = p.GetIndex(grid);
                    itemWindows[index] = new ItemWindow(app);
                    itemWindows[index].LeftClickEvent += OnLeftClickItem;
                    itemWindows[index].RightClickEvent += OnRightClickItem;
                    itemWindows[index].Position = p * (size + spacing);
                    itemWindows[index].ShowHoverText = !UnifiedSelection;
                    Windows += itemWindows[index];
                    itemWindows[index].Layer = this.Layer + 1;
                }

                if (doubleLayered)
                {
                    foreach (Vector2 p in (Rect)(grid - 1))
                    {
                        var index = p.GetIndex(grid - 1) + grid.Count;
                        itemWindows[index] = new ItemWindow(app);
                        itemWindows[index].LeftClickEvent += OnLeftClickItem;
                        itemWindows[index].RightClickEvent += OnRightClickItem;
                        itemWindows[index].Position = p * (size + spacing) + size / 2;
                        itemWindows[index].ShowHoverText = !UnifiedSelection;
                        Windows += itemWindows[index];
                        itemWindows[index].Layer = this.Layer + 2;
                    }
                }
            }

            RefreshContent();
            if (!IsValidKeyboardIndex(keyboardIndex))
                SelectFirstKeyboardItem();
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

        IEnumerator IEnumerable.GetEnumerator() => new ItemCollectionEnumerator(this);
        IEnumerator<Item> IEnumerable<Item>.GetEnumerator() => new ItemCollectionEnumerator(this);
    }
}
