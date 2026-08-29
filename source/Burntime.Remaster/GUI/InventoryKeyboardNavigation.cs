using System;
using Burntime.Framework;
using Burntime.Platform;

namespace Burntime.Remaster.GUI;

class InventoryKeyboardNavigation
{
    readonly InventoryWindow inventory;
    readonly ItemGridWindow roomGrid;
    readonly Action globalAction;
    readonly Action exitAction;
    bool roomAreaActive;

    public InventoryKeyboardNavigation(InventoryWindow inventory, ItemGridWindow roomGrid,
        Action globalAction, Action exitAction)
    {
        this.inventory = inventory;
        this.roomGrid = roomGrid;
        this.globalAction = globalAction;
        this.exitAction = exitAction;
    }

    public void Reset()
    {
        roomAreaActive = roomGrid.HasKeyboardItems;
        inventory.Grid.ResetKeyboardSelection();
        roomGrid.ResetKeyboardSelection();
        UpdateActiveArea();
    }

    public bool Handle(InputAction action)
    {
        if (action == InputAction.Back)
        {
            exitAction();
            return true;
        }

        if (action == InputAction.LeftArea)
        {
            inventory.SelectNextCharacter();
            UpdateActiveArea();
            return true;
        }

        if (action == InputAction.RightArea)
        {
            UpdateActiveArea();
            return true;
        }

        Vector2 direction = action switch
        {
            InputAction.MoveUp => new Vector2(0, -1),
            InputAction.MoveDown => new Vector2(0, 1),
            InputAction.MoveLeft => new Vector2(-1, 0),
            InputAction.MoveRight => new Vector2(1, 0),
            InputAction.MoveUpLeft => new Vector2(-1, -1),
            InputAction.MoveUpRight => new Vector2(1, -1),
            InputAction.MoveDownLeft => new Vector2(-1, 1),
            InputAction.MoveDownRight => new Vector2(1, 1),
            _ => Vector2.Zero
        };
        if (direction != Vector2.Zero)
        {
            ItemGridWindow activeGrid = ActiveGrid;
            Vector2? sourcePosition = activeGrid.KeyboardSelectionPosition;
            if (!activeGrid.MoveKeyboardSelection(direction) && direction.x != 0 && sourcePosition.HasValue)
            {
                ItemGridWindow targetGrid = roomAreaActive ? inventory.Grid : roomGrid;
                if (targetGrid.SelectKeyboardEdge(direction, sourcePosition.Value))
                {
                    roomAreaActive = !roomAreaActive;
                    UpdateActiveArea();
                }
            }
            return true;
        }

        if (action == InputAction.Primary)
        {
            ActiveGrid.ActivateKeyboardItem(false);
            EnsureNonEmptyArea();
            return true;
        }

        if (action == InputAction.GlobalAction)
        {
            globalAction();
            EnsureNonEmptyArea();
            return true;
        }

        return false;
    }

    public void ItemsChanged()
    {
        EnsureNonEmptyArea();
    }

    ItemGridWindow ActiveGrid => roomAreaActive ? roomGrid : inventory.Grid;

    void EnsureNonEmptyArea()
    {
        if (ActiveGrid.HasKeyboardItems)
        {
            UpdateActiveArea();
            return;
        }

        ItemGridWindow otherGrid = roomAreaActive ? inventory.Grid : roomGrid;
        if (otherGrid.HasKeyboardItems)
            roomAreaActive = !roomAreaActive;
        UpdateActiveArea();
    }

    void UpdateActiveArea()
    {
        inventory.Grid.KeyboardSelectionVisible = !roomAreaActive;
        roomGrid.KeyboardSelectionVisible = roomAreaActive;
    }
}
