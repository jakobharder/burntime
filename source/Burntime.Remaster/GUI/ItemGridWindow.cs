
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
        readonly ISprite focusSprite;
        readonly ISprite equippedSprite;
        GuiFont selectionFont;
        int focusIndex = -1;
        Vector2? lastFocusPosition;
        public bool FocusVisible { get; set; }
        public PixelColor? BackgroundColor { get; set; }
        public event Action<ItemGridWindow> MouseFocusChanged;
        public event Action<ItemGridWindow, Vector2> FocusEmptied;

        bool unifiedSelection;
        bool showHoverText = true;
        public bool ShowHoverText
        {
            get => showHoverText;
            set
            {
                showHoverText = value;
                if (itemWindows != null)
                    foreach (ItemWindow itemWindow in itemWindows)
                        itemWindow.ShowHoverText = value && !unifiedSelection;
            }
        }

        public bool UnifiedSelection
        {
            get { return unifiedSelection; }
            set
            {
                unifiedSelection = value;
                if (itemWindows != null)
                {
                    foreach (ItemWindow itemWindow in itemWindows)
                        itemWindow.ShowHoverText = showHoverText && !value;
                }
            }
        }

        bool hasLastMousePosition;
        bool mouseHasLeft;
        Vector2 lastMousePosition;
        const float MouseFocusHideDelay = 0.2f;
        bool mouseFocusActive;
        float mouseFocusHideRemaining;
        bool mouseFocusHidePending;

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

        public bool HasFocusableItems => items.Count > 0;

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
            focusSprite = App.ResourceManager.GetImage("gfx/ui/item_focus.png");
            equippedSprite = App.ResourceManager.GetImage("gfx/ui/item_equipped.png");
            selectionFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.MenuTextHover);
            selectionFont.Borders = TextBorders.Screen;
        }

        public override void OnRender(RenderTarget Target)
        {
            if (BackgroundColor.HasValue)
                Target.RenderRect(Vector2.Zero, Size, BackgroundColor.Value);

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
                        Target.DrawSprite(itemWindows[i].Position, equippedSprite);
                }


                Target.Layer -= 3;
            }

            bool showUIHints = app is not BurntimeClassic classic || classic.ShowUIHints;
            bool touchFocus = app.LastInputMode == InputMode.Touch && mouseFocusActive;
            if ((touchFocus || FocusVisible || showUIHints && mouseFocusActive) &&
                IsValidFocusIndex(focusIndex))
            {
                Target.Layer += 5;
                Vector2 itemPosition = itemWindows[focusIndex].Position;
                if (showUIHints || touchFocus)
                    Target.DrawSprite(itemPosition, focusSprite);
                else
                {
                    RenderTarget bigger = Target.GetSubBuffer(new Rect(
                        itemPosition - new Vector2(50, 50), new Vector2(132, 132)));
                    selectionFont.DrawText(bigger, new Vector2(66, 41),
                        itemWindows[focusIndex].TooltipText ?? "",
                        TextAlignment.Center, VerticalTextAlignment.Top);
                }
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
            bool foundFocus = false;

            for (int i = itemWindows?.Length - 1 ?? -1; i >= 0; i--)
            {
                if (IsValidFocusIndex(i) && itemWindows[i].Boundings.PointInside(position))
                {
                    foundFocus = true;
                    mouseFocusActive = true;
                    mouseFocusHidePending = false;
                    mouseFocusHideRemaining = 0;
                    focusIndex = i;
                    MouseFocusChanged?.Invoke(this);
                    break;
                }
            }

            if (!foundFocus && mouseFocusActive && !mouseFocusHidePending)
            {
                mouseFocusHidePending = true;
                mouseFocusHideRemaining = MouseFocusHideDelay;
            }

            return base.OnMouseMove(position);
        }

        public override void OnUpdate(float elapsed)
        {
            if (mouseFocusHidePending)
            {
                mouseFocusHideRemaining -= elapsed;
                if (mouseFocusHideRemaining <= 0)
                    ClearMouseFocus();
            }

            base.OnUpdate(elapsed);
        }

        public override void OnMouseLeave()
        {
            mouseHasLeft = true;
            ClearMouseFocus();
            base.OnMouseLeave();
        }

        void ClearMouseFocus()
        {
            mouseFocusActive = false;
            mouseFocusHidePending = false;
            mouseFocusHideRemaining = 0;
        }

        internal void FocusItem(int index)
        {
            if ((!UnifiedSelection && app.LastInputMode != InputMode.Touch) || !IsValidFocusIndex(index))
                return;

            mouseFocusActive = true;
            mouseFocusHidePending = false;
            mouseFocusHideRemaining = 0;
            focusIndex = index;
            MouseFocusChanged?.Invoke(this);
        }

        internal bool FocusItem(ItemWindow item)
        {
            bool wasFocused = IsFocused(item);
            FocusItem(Array.IndexOf(itemWindows, item));
            return wasFocused;
        }

        internal bool IsFocused(ItemWindow item) =>
            IsValidFocusIndex(focusIndex) && ReferenceEquals(itemWindows[focusIndex], item);

        public void ResetFocus()
        {
            ClearFocus();
            FocusFirstItem();
        }

        public void ClearFocus()
        {
            focusIndex = -1;
            lastFocusPosition = null;
            ClearMouseFocus();
        }

        public bool EnsureFocus()
        {
            return IsValidFocusIndex(focusIndex) || FocusFirstItem();
        }

        public bool MoveFocus(Vector2 direction)
        {
            if (!IsValidFocusIndex(focusIndex))
                return FocusFirstItem();

            Vector2 current = itemWindows[focusIndex].Position + size / 2;
            int selected = -1;
            int selectedScore = int.MaxValue;

            for (int i = 0; i < itemWindows.Length; i++)
            {
                if (!IsValidFocusIndex(i) || i == focusIndex)
                    continue;

                Vector2 candidate = itemWindows[i].Position + size / 2;
                Vector2 difference = candidate - current;

                // The offset room layer is a separate horizontal grid. Left and
                // right stay on the current layer and row; vertical movement is
                // what crosses between the interleaved layers.
                if (doubleLayered && direction.x != 0)
                {
                    bool currentSecondLayer = focusIndex >= grid.Count;
                    bool candidateSecondLayer = i >= grid.Count;
                    if (currentSecondLayer != candidateSecondLayer || candidate.y != current.y)
                        continue;
                }

                int forward = difference.x * direction.x + difference.y * direction.y;
                if (forward <= 0)
                    continue;

                int sideways = System.Math.Abs(difference.x * direction.y - difference.y * direction.x);
                // In an interleaved room grid, vertical movement should enter
                // the nearer offset layer instead of skipping over it to remain
                // in the same column of the current layer.
                int score = doubleLayered && direction.x == 0
                    ? forward * 1000 + sideways
                    : sideways * 1000 + forward;
                if (score < selectedScore)
                {
                    selected = i;
                    selectedScore = score;
                }
            }

            if (selected == -1)
                return false;

            focusIndex = selected;
            return true;
        }

        public bool FocusEdge(Vector2 direction, Vector2 sourcePosition)
        {
            int selected = -1;
            int selectedScore = int.MaxValue;

            for (int i = 0; itemWindows != null && i < itemWindows.Length; i++)
            {
                if (!IsValidFocusIndex(i))
                    continue;

                Vector2 candidate = PositionOnScreen + itemWindows[i].Position + size / 2;
                Vector2 difference = candidate - sourcePosition;
                int forward = difference.x * direction.x + difference.y * direction.y;
                if (forward <= 0)
                    continue;

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

            focusIndex = selected;
            return true;
        }

        public bool FocusPageEdge(Vector2 direction, Vector2 sourcePosition)
        {
            int selected = -1;
            int selectedScore = int.MaxValue;

            for (int i = 0; itemWindows != null && i < itemWindows.Length; i++)
            {
                if (!IsValidFocusIndex(i))
                    continue;

                Vector2 candidate = PositionOnScreen + itemWindows[i].Position + size / 2;
                // Moving down enters the top of the next page; moving up enters
                // the bottom of the previous page. Preserve the source column.
                int edge = direction.y > 0 ? candidate.y : -candidate.y;
                int columnDistance = System.Math.Abs(candidate.x - sourcePosition.x);
                int score = edge * 1000 + columnDistance;
                if (score < selectedScore)
                {
                    selected = i;
                    selectedScore = score;
                }
            }

            if (selected == -1)
                return false;

            focusIndex = selected;
            return true;
        }

        public Vector2? FocusPosition
        {
            get
            {
                if (IsValidFocusIndex(focusIndex))
                {
                    lastFocusPosition = PositionOnScreen + itemWindows[focusIndex].Position + size / 2;
                    return lastFocusPosition;
                }

                return lastFocusPosition;
            }
        }

        Item? FocusedItemAtIndex => IsValidFocusIndex(focusIndex)
            ? items[gridPositions[focusIndex]]
            : null;

        public Item? FocusedItem => app.LastInputMode switch
        {
            InputMode.Touch => FocusedItemAtIndex,
            InputMode.Mouse when mouseFocusActive => FocusedItemAtIndex,
            InputMode.Keyboard or InputMode.Gamepad when FocusVisible =>
                FocusedItemAtIndex,
            _ => null
        };

        protected override bool IsPromptActive(InputMode inputMode) => FocusedItem != null;

        public bool ActivateFocusedItem(bool secondary)
        {
            if (!IsValidFocusIndex(focusIndex))
                return false;

            Vector2 previousPosition = itemWindows[focusIndex].Position;
            Item item = items[gridPositions[focusIndex]];
            if (secondary)
                RightClickItemEvent?.Execute(item);
            else
                LeftClickItemEvent?.Execute(item);

            if (!IsValidFocusIndex(focusIndex))
                FocusNearestItem(previousPosition);
            return true;
        }

        bool FocusFirstItem()
        {
            if (itemWindows == null)
                return false;

            for (int i = 0; i < itemWindows.Length; i++)
            {
                if (IsValidFocusIndex(i))
                {
                    focusIndex = i;
                    return true;
                }
            }

            focusIndex = -1;
            return false;
        }

        void FocusNearestItem(Vector2 position)
        {
            focusIndex = -1;
            int nearestDistance = int.MaxValue;
            bool nearestIsOnSameRow = false;
            for (int i = 0; itemWindows != null && i < itemWindows.Length; i++)
            {
                if (!IsValidFocusIndex(i))
                    continue;

                Vector2 difference = itemWindows[i].Position - position;
                int distance = System.Math.Abs(difference.x) + System.Math.Abs(difference.y);
                bool isOnSameRow = difference.y == 0;
                if (focusIndex == -1 ||
                    isOnSameRow && !nearestIsOnSameRow ||
                    isOnSameRow == nearestIsOnSameRow && distance < nearestDistance)
                {
                    focusIndex = i;
                    nearestDistance = distance;
                    nearestIsOnSameRow = isOnSameRow;
                }
            }
        }

        bool IsValidFocusIndex(int index)
        {
            return itemWindows != null && gridPositions != null && index >= 0 && index < itemWindows.Length &&
                gridPositions[index] >= 0 && gridPositions[index] < items.Count &&
                !string.IsNullOrEmpty(itemWindows[index].ItemID);
        }

        public void Clear()
        {
            if (IsValidFocusIndex(focusIndex))
                lastFocusPosition = PositionOnScreen + itemWindows[focusIndex].Position + size / 2;

            items.Clear();
            RefreshContent();
            selection = app.GameState.Container.Create<ItemList>(StateObjectOptions.Temporary);
            focusIndex = -1;
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
                        itemWindows[i].Item = item;
                        break;
                    }
                }
            }
            else
                RefreshContent();

            if (!IsValidFocusIndex(focusIndex))
                FocusFirstItem();
            
            return true;
        }

        public void Remove(Item item)
        {
            Vector2? removedFocusPosition = items.Count == 1 && IsValidFocusIndex(focusIndex)
                ? PositionOnScreen + itemWindows[focusIndex].Position + size / 2
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

            if (items.Count == 0 && removedFocusPosition.HasValue)
                FocusEmptied?.Invoke(this, removedFocusPosition.Value);
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
                    itemWindows[i].Item = item;
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

            base.Size = grid.Count == 0
                ? Vector2.Zero
                : grid * size + (grid - Vector2.One) * spacing;

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
                    itemWindows[index].ShowHoverText = ShowHoverText && !UnifiedSelection;
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
                        itemWindows[index].ShowHoverText = ShowHoverText && !UnifiedSelection;
                        Windows += itemWindows[index];
                        itemWindows[index].Layer = this.Layer + 2;
                    }
                }
            }

            RefreshContent();
            if (!IsValidFocusIndex(focusIndex))
                FocusFirstItem();
        }

        void RefreshContent()
        {
            if (itemWindows == null)
                return;

            for (int i = 0; i < itemWindows.Length; i++)
            {
                if (items.Count > i)
                {
                    itemWindows[i].Item = items[i];
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
