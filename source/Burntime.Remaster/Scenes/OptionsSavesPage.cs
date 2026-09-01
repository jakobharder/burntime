using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Burntime.Classic.Scenes;

internal struct OptionFonts
{
    public GuiFont Disabled;

    public GuiFont Orange;
    public GuiFont Green;
    public GuiFont Blue;
}

internal class OptionsSavesPage : Container
{
    enum KeyboardArea
    {
        Slots,
        Actions
    }

    sealed class SaveInfo
    {
        public string FileName = "";
        public string DisplayName = "";
        public string? Version;
        public Dictionary<string, string>? Hints;
        public DateTime LastWriteTimeUtc;
        public bool MetadataLoaded;

        public bool IsValid => BurntimeClassic.IsSupportedSavegameVersion(Version) && Hints is not null;
        public bool IsAutosave => AutosaveManager.IsAutosave(FileName);
    }

    sealed class MetadataLoadResult
    {
        public int Generation;
        public string FileName = "";
        public DateTime LastWriteTimeUtc;
        public string? Version;
        public Dictionary<string, string>? Hints;
        public string? Error;
    }

    sealed class SaveRowButton : Button
    {
        public string SecondaryText = "";

        public SaveRowButton(Module app) : base(app)
        {
        }

        public override void OnRender(Platform.Graphics.RenderTarget target)
        {
            base.OnRender(target);
            if (string.IsNullOrEmpty(SecondaryText))
                return;

            bool highlighted = IsHover || IsKeyboardSelected;
            GuiFont? font = !IsEnabled && DisabledFont is not null
                ? DisabledFont
                : highlighted && HoverFont is not null ? HoverFont : Font;
            font?.DrawText(target, new Vector2(Size.x, 0), SecondaryText,
                Platform.Graphics.TextAlignment.Right,
                Platform.Graphics.VerticalTextAlignment.Top);
        }
    }

    readonly OptionFonts _fonts;

    readonly Button _load;
    readonly Button _save;
    readonly Button _delete;
    readonly Button _hintText;
    readonly Button _upIndicator;
    readonly Button _downIndicator;

    const int VISIBLE_SAVE_COUNT = 6;
    const int LIST_X = 38;
    const int LIST_Y = 58;
    // The action strip spans x=40..160. Rows begin at x=38, so 122 reaches
    // the same rightmost pixel of the black content area.
    const int LIST_WIDTH = 122;
    const int ROW_HEIGHT = 10;

    readonly SaveRowButton[] _saveRows = new SaveRowButton[VISIBLE_SAVE_COUNT];
    readonly Button[] _actionButtons;
    readonly List<SaveInfo> _saves = new();
    readonly Dictionary<string, SaveInfo> _saveInfos = new(StringComparer.OrdinalIgnoreCase);

    KeyboardArea _keyboardArea;
    // Keyboard navigation is transient; the marked entry remains the target of
    // load/save/delete until another row is explicitly confirmed or clicked.
    int _saveFocusIndex;
    int _markedIndex;
    int _scrollOffset;
    int _actionFocusIndex;
    int _metadataGeneration;
    int _metadataPreloadIndex;
    Task<MetadataLoadResult>? _metadataLoadTask;

    bool CanCreateSave => app.Server?.StateContainer is not null;
    int SaveEntryOffset => CanCreateSave ? 1 : 0;
    int EntryCount => _saves.Count + SaveEntryOffset;
    bool IsCreateSelected => IsCreateEntry(_markedIndex);
    SaveInfo? SelectedSave => GetSaveAtEntry(_markedIndex);

    public OptionsSavesPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;
        Size = new Vector2(320, 200);

        var saveButtons = new AutoAlignContainer(app)
        {
            // The hover details are centered in the 10-pixel strip below. Text-only
            // buttons draw from the top, so offset them by one pixel to share its baseline.
            Position = new Vector2(40, 124),
            Size = new Vector2(120, 10)
        };
        saveButtons.Windows += _load = new Button(app, OnLoad)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?382",
            IsTextOnly = true
        };
        saveButtons.Windows += _save = new Button(app, OnSave)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?383",
            IsTextOnly = true
        };
        saveButtons.Windows += _delete = new Button(app, OnDelete)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?384",
            IsTextOnly = true
        };
        Windows += saveButtons;

        Windows += _hintText = new Button(app)
        {
            Font = _fonts.Blue,
            Position = new Vector2(40, 124),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center,
            TextVerticalAlign = Platform.Graphics.VerticalTextAlignment.Top
        };
        // The action buttons are nested one layer deeper than this sibling. Keep
        // hover details above their strip so stale action pixels cannot cover them.
        _hintText.Layer += 2;

        Windows += _upIndicator = new Button(app, () => ScrollList(-1))
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            Position = new Vector2(LIST_X + LIST_WIDTH + 2, LIST_Y),
            Text = "<",
            IsTextOnly = true
        };
        Windows += _downIndicator = new Button(app, () => ScrollList(1))
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            Position = new Vector2(LIST_X + LIST_WIDTH + 2, LIST_Y + (VISIBLE_SAVE_COUNT - 1) * ROW_HEIGHT),
            Text = ">",
            IsTextOnly = true
        };

        _actionButtons = new[] { _load, _save, _delete };
        CreateSaveRows();
    }

    public override bool OnMouseWheel(Vector2 position, int delta)
    {
        var listBounds = new Rect(LIST_X, LIST_Y, LIST_WIDTH, VISIBLE_SAVE_COUNT * ROW_HEIGHT);
        if (!listBounds.PointInside(position) || EntryCount <= VISIBLE_SAVE_COUNT)
            return false;

        ScrollList(-System.Math.Sign(delta));

        return true;
    }

    bool ScrollList(int direction)
    {
        int maximumOffset = System.Math.Max(0, EntryCount - VISIBLE_SAVE_COUNT);
        int newOffset = System.Math.Clamp(_scrollOffset + direction, 0, maximumOffset);
        if (newOffset == _scrollOffset)
            return false;

        _scrollOffset = newOffset;
        RefreshVisibleRows();
        UpdateKeyboardFocus();
        return true;
    }

    public void SetKeyboardActive(bool active, bool resetFocus = false)
    {
        HasFocus = active;
        if (resetFocus)
        {
            _keyboardArea = KeyboardArea.Slots;
            _saveFocusIndex = EntryCount > 0 ? 0 : -1;
            EnsureFocusVisible();
            RefreshVisibleRows();
        }

        UpdateKeyboardFocus();
    }

    void UpdateKeyboardFocus()
    {
        bool keyboardFocus = HasFocus && app.LastInputMode != InputMode.Mouse;
        for (int i = 0; i < _saveRows.Length; i++)
        {
            int entryIndex = _scrollOffset + i;
            _saveRows[i].IsKeyboardSelected = keyboardFocus && _keyboardArea == KeyboardArea.Slots &&
                entryIndex == _saveFocusIndex && entryIndex < EntryCount;
        }

        for (int i = 0; i < _actionButtons.Length; i++)
            _actionButtons[i].IsKeyboardSelected = keyboardFocus && _keyboardArea == KeyboardArea.Actions && i == _actionFocusIndex;
    }

    int HoveredActionIndex => app.LastInputMode == InputMode.Mouse ? Array.FindIndex(_actionButtons,
        button => button.IsVisible && button.IsEnabled && button.IsHover) : -1;

    int HoveredEntryIndex
    {
        get
        {
            if (app.LastInputMode != InputMode.Mouse)
                return -1;

            int row = Array.FindIndex(_saveRows, button => button.IsVisible && button.IsHover);
            return row < 0 ? -1 : _scrollOffset + row;
        }
    }

    int FocusedActionIndex
    {
        get
        {
            int hovered = HoveredActionIndex;
            if (hovered >= 0)
                return hovered;

            return HasFocus && _keyboardArea == KeyboardArea.Actions && IsActionAvailable(_actionFocusIndex)
                ? _actionFocusIndex
                : -1;
        }
    }

    bool IsActionAvailable(int index) => index >= 0 && index < _actionButtons.Length &&
        _actionButtons[index].IsVisible && _actionButtons[index].IsEnabled;

    bool IsCreateEntry(int entryIndex) => CanCreateSave && entryIndex == 0;

    SaveInfo? GetSaveAtEntry(int entryIndex) => entryIndex >= SaveEntryOffset && entryIndex < EntryCount
        ? _saves[entryIndex - SaveEntryOffset]
        : null;

    int FindEntry(string? fileName)
    {
        if (fileName is null)
            return -1;

        int saveIndex = _saves.FindIndex(save =>
            save.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        return saveIndex < 0 ? -1 : saveIndex + SaveEntryOffset;
    }

    void MoveEntry(int direction)
    {
        int candidate = _saveFocusIndex + direction;
        if (candidate >= 0 && candidate < EntryCount)
        {
            _saveFocusIndex = candidate;
            EnsureFocusVisible();
            RefreshVisibleRows();
        }
        else if (direction > 0)
        {
            SelectFirstEnabledAction();
            return;
        }

        UpdateKeyboardFocus();
    }

    void EnsureFocusVisible()
    {
        if (_saveFocusIndex < _scrollOffset)
            _scrollOffset = _saveFocusIndex;
        else if (_saveFocusIndex >= _scrollOffset + VISIBLE_SAVE_COUNT)
            _scrollOffset = _saveFocusIndex - VISIBLE_SAVE_COUNT + 1;

        int maximumOffset = System.Math.Max(0, EntryCount - VISIBLE_SAVE_COUNT);
        _scrollOffset = System.Math.Clamp(_scrollOffset, 0, maximumOffset);
    }

    void SelectFirstEnabledAction()
    {
        SetActionVisibility(true);
        int preferred = IsActionAvailable(_actionFocusIndex)
            ? _actionFocusIndex
            : Array.FindIndex(_actionButtons, button => button.IsVisible && button.IsEnabled);
        if (preferred < 0)
            return;

        _actionFocusIndex = preferred;
        _keyboardArea = KeyboardArea.Actions;
        UpdateKeyboardFocus();
    }

    bool EnsureFocus()
    {
        if (_saveFocusIndex >= 0 && _saveFocusIndex < EntryCount)
            return true;

        if (EntryCount == 0)
            return false;

        _saveFocusIndex = 0;
        EnsureFocusVisible();
        RefreshVisibleRows();
        UpdateKeyboardFocus();
        return false;
    }

    bool MoveAction(int direction)
    {
        for (int candidate = _actionFocusIndex + direction;
             candidate >= 0 && candidate < _actionButtons.Length;
             candidate += direction)
        {
            if (IsActionAvailable(candidate))
            {
                _actionFocusIndex = candidate;
                UpdateKeyboardFocus();
                return true;
            }
        }

        return false;
    }

    public override bool OnInputAction(InputAction action)
    {
        if (_keyboardArea == KeyboardArea.Slots)
        {
            if (action == InputAction.MoveUp || action == InputAction.MoveDown)
            {
                EnsureFocus();
                MoveEntry(action == InputAction.MoveUp ? -1 : 1);
            }
            else if (action == InputAction.Primary)
            {
                if (!EnsureFocus())
                    return true;
                MarkEntry(_saveFocusIndex);
                if (IsCreateSelected)
                    OnSave();
                else
                    SelectFirstEnabledAction();
            }
            else
                return false;
            return true;
        }

        if (action == InputAction.MoveLeft || action == InputAction.MoveRight)
        {
            int direction = action == InputAction.MoveLeft ? -1 : 1;
            if (!MoveAction(direction) && direction > 0)
                return false;
            return true;
        }
        if (action == InputAction.MoveUp)
        {
            _keyboardArea = KeyboardArea.Slots;
            UpdateKeyboardFocus();
            return true;
        }
        if (action == InputAction.Primary)
        {
            int focusedAction = FocusedActionIndex;
            if (focusedAction >= 0)
                _actionButtons[focusedAction].OnButtonClick();
            return true;
        }

        return false;
    }

    public override void OnUpdate(float elapsed)
    {
        UpdateMetadataPreload();
        UpdateKeyboardFocus();
        int hoveredEntry = HoveredEntryIndex;
        int hoveredAction = HoveredActionIndex;

        // Mouse mode has no implicit selection, but retain the hovered row as the
        // anchor if input switches to keyboard or gamepad before the next update.
        if (app.LastInputMode == InputMode.Mouse)
        {
            _saveFocusIndex = hoveredEntry;
            _keyboardArea = hoveredAction >= 0 ? KeyboardArea.Actions : KeyboardArea.Slots;
        }

        bool keyboardPreview = app.LastInputMode != InputMode.Mouse && HasFocus &&
            _keyboardArea == KeyboardArea.Slots;
        int previewEntry = hoveredEntry >= 0
            ? hoveredEntry
            : keyboardPreview ? _saveFocusIndex : -1;
        bool showHint = previewEntry >= 0;
        SaveInfo? displayedSave = GetSaveAtEntry(previewEntry);

        if (showHint)
        {
            _hintText.Text = GetHintText(displayedSave);
            _hintText.IsVisible = !string.IsNullOrEmpty(_hintText.Text);
            SetActionVisibility(false);
        }
        else
        {
            _hintText.Text = "";
            _hintText.IsVisible = false;
            SetActionVisibility(true);
        }

        _save.IsEnabled = CanCreateSave;
        _load.IsEnabled = SelectedSave?.IsValid == true;
        _delete.IsEnabled = SelectedSave is { IsAutosave: false };

        if (hoveredAction >= 0 && hoveredAction != _actionFocusIndex)
        {
            _actionFocusIndex = hoveredAction;
            if (HasFocus && _keyboardArea == KeyboardArea.Actions)
                UpdateKeyboardFocus();
        }

        // A row preview deliberately hides the actions. Do not let keyboard action
        // recovery reveal them again on top of the hover details.
        if (!showHint && HasFocus && _keyboardArea == KeyboardArea.Actions && !IsActionAvailable(_actionFocusIndex))
            SelectFirstEnabledAction();

        base.OnUpdate(elapsed);
    }

    void SetActionVisibility(bool visible)
    {
        _load.IsVisible = visible && SelectedSave is not null;
        _save.IsVisible = visible;
        _delete.IsVisible = visible && SelectedSave is not null;
    }

    string GetHintText(SaveInfo? saveInfo)
    {
        if (saveInfo is null)
            return "";
        if (!saveInfo.IsValid)
            return app.ResourceManager.GetString("newburn?37");

        Dictionary<string, string> hints = saveInfo.Hints!;
        int difficulty = int.TryParse(hints.GetValueOrDefault("difficulty"), out int value)
            ? value
            : -1;
        string difficultyText = difficulty switch
        {
            0 => "I",
            1 => "II",
            2 => "III",
            _ => "?"
        };

        TextHelper campsText = new(app, "newburn");
        campsText.AddArgument("|A", hints.GetValueOrDefault("camps") ?? "");

        TextHelper details = new(app, "newburn");
        details.AddArgument("|D", difficultyText);
        details.AddArgument("|J", hints.GetValueOrDefault("days") ?? "");
        details.AddArgument("|C", campsText[35]);
        string result = details[54];
        return _fonts.Blue.GetWidth(result) <= _hintText.Size.x
            ? result
            : result.Replace(" - ", "-");
    }

    public void RefreshSaveGames(string? changed = null, bool resetSelection = false)
    {
        string? selectedFile = resetSelection ? null : SelectedSave?.FileName;
        string? focusedFile = resetSelection ? null : GetSaveAtEntry(_saveFocusIndex)?.FileName;
        string[] files = FileSystem.GetFileNames("saves/", ".sav");

        var refreshed = new List<SaveInfo>();
        foreach (string fileName in files)
        {
            DateTime timestamp = FileSystem.GetLastWriteTimeUtc("saves/" + fileName) ?? DateTime.MinValue;
            SaveInfo? saveInfo = _saveInfos.GetValueOrDefault(fileName);
            if (saveInfo is null || saveInfo.LastWriteTimeUtc != timestamp ||
                fileName.Equals(changed, StringComparison.OrdinalIgnoreCase))
            {
                saveInfo = new SaveInfo
                {
                    FileName = fileName,
                    DisplayName = GetDisplayName(fileName),
                    LastWriteTimeUtc = timestamp
                };
                _saveInfos[fileName] = saveInfo;
            }
            refreshed.Add(saveInfo);
        }

        _saves.Clear();
        _saves.AddRange(refreshed
            .OrderByDescending(save => save.LastWriteTimeUtc)
            .ThenBy(save => save.FileName, StringComparer.OrdinalIgnoreCase));
        _metadataGeneration++;
        _metadataPreloadIndex = 0;

        if (changed is not null)
        {
            selectedFile = changed;
            focusedFile = changed;
        }

        if (resetSelection)
        {
            _markedIndex = 0;
            _saveFocusIndex = 0;
        }
        else
        {
            _markedIndex = FindEntry(selectedFile);
            if (_markedIndex < 0)
                _markedIndex = 0;

            _saveFocusIndex = FindEntry(focusedFile);
            if (_saveFocusIndex < 0)
                _saveFocusIndex = _markedIndex;
        }

        EnsureFocusVisible();
        RefreshVisibleRows();
        _keyboardArea = KeyboardArea.Slots;
        UpdateKeyboardFocus();
    }

    void RefreshVisibleRows()
    {
        for (int row = 0; row < _saveRows.Length; row++)
        {
            int entryIndex = _scrollOffset + row;
            SaveRowButton button = _saveRows[row];
            button.IsVisible = entryIndex < EntryCount;

            if (!button.IsVisible)
                continue;

            SaveInfo? saveInfo = GetSaveAtEntry(entryIndex);
            if (IsCreateEntry(entryIndex))
            {
                button.Text = "@newburn?53";
                button.SecondaryText = "";
            }
            else
            {
                if (saveInfo is null)
                    continue;

                button.SecondaryText = GetTimestampText(saveInfo);
                button.Text = GetRowText(saveInfo, button.SecondaryText);
            }

            GuiFont normalFont = saveInfo?.IsValid == false ? _fonts.Disabled : _fonts.Green;
            button.Font = entryIndex == _markedIndex ? _fonts.Blue : normalFont;
        }

        _upIndicator.IsVisible = _scrollOffset > 0;
        _downIndicator.IsVisible = _scrollOffset + VISIBLE_SAVE_COUNT < EntryCount;
    }

    void UpdateMetadataPreload()
    {
        if (_metadataLoadTask is { IsCompleted: true })
        {
            MetadataLoadResult? result = null;
            try
            {
                result = _metadataLoadTask.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not inspect save metadata: {exception.Message}");
            }
            _metadataLoadTask = null;

            if (result?.Error is { Length: > 0 } error)
                Log.Warning($"Could not inspect save metadata for '{result.FileName}': {error}");

            if (result is not null && result.Generation == _metadataGeneration &&
                _saveInfos.TryGetValue(result.FileName, out SaveInfo? saveInfo) &&
                saveInfo.LastWriteTimeUtc == result.LastWriteTimeUtc)
            {
                saveInfo.Version = result.Version;
                saveInfo.Hints = result.Hints;
                saveInfo.MetadataLoaded = true;
                ReorderSaveGames();
            }
        }

        if (_metadataLoadTask is not null)
            return;

        SaveInfo? next = FindNextMetadataToLoad();
        if (next is null)
            return;

        int generation = _metadataGeneration;
        string fileName = next.FileName;
        DateTime lastWriteTimeUtc = next.LastWriteTimeUtc;
        var resourceManager = app.ResourceManager;
        _metadataLoadTask = Task.Run(() => LoadMetadata(resourceManager, generation,
            fileName, lastWriteTimeUtc));
    }

    SaveInfo? FindNextMetadataToLoad()
    {
        for (int row = 0; row < VISIBLE_SAVE_COUNT; row++)
        {
            SaveInfo? visible = GetSaveAtEntry(_scrollOffset + row);
            if (visible is { MetadataLoaded: false })
                return visible;
        }

        while (_metadataPreloadIndex < _saves.Count)
        {
            SaveInfo candidate = _saves[_metadataPreloadIndex++];
            if (!candidate.MetadataLoaded)
                return candidate;
        }
        return null;
    }

    static MetadataLoadResult LoadMetadata(Platform.Resource.IResourceManager resourceManager,
        int generation, string fileName, DateTime lastWriteTimeUtc)
    {
        var result = new MetadataLoadResult
        {
            Generation = generation,
            FileName = fileName,
            LastWriteTimeUtc = lastWriteTimeUtc
        };
        SaveGame? game = null;
        try
        {
            game = new SaveGame("saves/" + fileName);
            result.Version = game.Version;
            bool legacy = game.Version == BurntimeClassic.PreviousSavegameVersion;
            result.Hints = game.PeakInfo(resourceManager, includeDetails: legacy);
        }
        catch (Exception exception)
        {
            result.Error = exception.Message;
        }
        finally
        {
            game?.Close();
        }
        return result;
    }

    string GetRowText(SaveInfo saveInfo, string timestamp)
    {
        string name = (saveInfo.Hints?.GetValueOrDefault("player") ?? saveInfo.DisplayName).ToUpperInvariant();
        int availableWidth = LIST_WIDTH - _fonts.Green.GetWidth(timestamp) - 4;
        string displayName = FormatDisplayName(name, saveInfo.IsAutosave);
        while (name.Length > 1 && _fonts.Green.GetWidth(displayName) > availableWidth)
        {
            name = name[..^1];
            displayName = FormatDisplayName(name, saveInfo.IsAutosave);
        }
        return displayName;
    }

    void ReorderSaveGames()
    {
        string? selectedFile = SelectedSave?.FileName;
        string? focusedFile = GetSaveAtEntry(_saveFocusIndex)?.FileName;

        _saves.Sort((left, right) =>
        {
            int timestampOrder = GetSaveTimestampUtc(right).CompareTo(GetSaveTimestampUtc(left));
            return timestampOrder != 0
                ? timestampOrder
                : StringComparer.OrdinalIgnoreCase.Compare(left.FileName, right.FileName);
        });
        _metadataPreloadIndex = 0;

        if (selectedFile is not null)
            _markedIndex = FindEntry(selectedFile);
        if (focusedFile is not null)
            _saveFocusIndex = FindEntry(focusedFile);

        EnsureFocusVisible();
        RefreshVisibleRows();
        UpdateKeyboardFocus();
    }

    static string FormatDisplayName(string name, bool isAutosave) => isAutosave ? $"[{name}]" : name;

    static DateTime GetSaveTimestampUtc(SaveInfo saveInfo)
    {
        DateTime timestamp = saveInfo.LastWriteTimeUtc;
        // Legacy saves have no persisted timestamp. Fully peeking them rebuilds the
        // save hint and assigns the current time, so their file timestamp is the only
        // reliable date. Current saves persist the actual save time in their hint.
        if (saveInfo.Version != BurntimeClassic.PreviousSavegameVersion &&
            DateTime.TryParse(saveInfo.Hints?.GetValueOrDefault("datetime"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime savedAt))
        {
            timestamp = savedAt.ToUniversalTime();
        }
        return timestamp;
    }

    static string GetTimestampText(SaveInfo saveInfo)
    {
        DateTime timestamp = GetSaveTimestampUtc(saveInfo);
        return timestamp == DateTime.MinValue
        ? ""
        : timestamp.ToLocalTime().ToString("MMM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    static string GetDisplayName(string fileName)
    {
        string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
        int marker = name.LastIndexOf("-save", StringComparison.OrdinalIgnoreCase);
        if (marker > 0 && int.TryParse(name[(marker + 5)..], out _))
            return name[..marker];
        return name;
    }

    void CreateSaveRows()
    {
        for (int i = 0; i < _saveRows.Length; i++)
        {
            int row = i;
            _saveRows[i] = new SaveRowButton(app)
            {
                Position = new Vector2(LIST_X, LIST_Y + i * ROW_HEIGHT),
                Size = new Vector2(LIST_WIDTH, ROW_HEIGHT),
                Text = "",
                Font = _fonts.Green,
                HoverFont = _fonts.Orange,
                TextHorizontalAlign = Platform.Graphics.TextAlignment.Left,
                TextVerticalAlign = Platform.Graphics.VerticalTextAlignment.Top
            };
            _saveRows[i].Command += new CommandHandler(OnSelectRow, row);
            Windows += _saveRows[i];
        }
    }

    void OnSelectRow(int row)
    {
        int entryIndex = _scrollOffset + row;
        if (entryIndex < 0 || entryIndex >= EntryCount)
            return;

        _saveFocusIndex = entryIndex;
        _keyboardArea = KeyboardArea.Slots;
        MarkEntry(entryIndex);
        if (IsCreateSelected)
            OnSave();
    }

    void MarkEntry(int entryIndex)
    {
        _markedIndex = entryIndex;
        RefreshVisibleRows();
        UpdateKeyboardFocus();
    }

    void OnSave()
    {
        if (!CanCreateSave)
            return;

        SaveInfo? selectedSave = SelectedSave;
        bool createManualSave = IsCreateSelected || selectedSave?.IsAutosave == true;
        string fileName = createManualSave ? GetNextSaveFileName() : selectedSave!.FileName;
        var creation = new GameCreation(app as BurntimeClassic);
        creation.SaveGame("saves/" + fileName);

        RefreshSaveGames(fileName);
    }

    string GetNextSaveFileName()
    {
        string playerName = "player";
        if (app.Server?.StateContainer.Root is ClassicGame game)
        {
            Player? player = game.World?.Players.FirstOrDefault(candidate => candidate.Type == PlayerType.Human);
            if (!string.IsNullOrWhiteSpace(player?.Name))
                playerName = player.Name;
        }

        string slug = GetFileNameSlug(playerName);
        int maximum = 0;
        string prefix = slug + "-save";
        foreach (SaveInfo save in _saves)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(save.FileName);
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name[prefix.Length..], out int number))
                maximum = System.Math.Max(maximum, number);
        }

        string candidate;
        do
        {
            maximum++;
            candidate = prefix + maximum.ToString("D3", CultureInfo.InvariantCulture) + ".sav";
        }
        while (FileSystem.ExistsFile("saves/" + candidate));

        return candidate;
    }

    static string GetFileNameSlug(string name)
    {
        var characters = new List<char>(name.Length);
        bool lastWasSeparator = false;
        foreach (char character in name.ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                characters.Add(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && characters.Count > 0)
            {
                characters.Add('-');
                lastWasSeparator = true;
            }
        }

        while (characters.Count > 0 && characters[^1] == '-')
            characters.RemoveAt(characters.Count - 1);

        return characters.Count == 0 ? "player" : new string(characters.ToArray());
    }

    void OnLoad()
    {
        SaveInfo? saveInfo = SelectedSave;
        if (saveInfo?.IsValid != true)
            return;

        app.SceneManager.SetScene("WaitScene");
        app.SceneManager.BlockBlendIn();

        var creation = new GameCreation(app as BurntimeClassic);
        if (!creation.LoadGame("saves/" + saveInfo.FileName))
            app.SceneManager.PreviousScene();

        app.SceneManager.UnblockBlendIn();
    }

    void OnDelete()
    {
        SaveInfo? saveInfo = SelectedSave;
        if (saveInfo is null || saveInfo.IsAutosave)
            return;

        string deletedFile = saveInfo.FileName;
        FileSystem.RemoveFile("saves/" + deletedFile);
        _saveInfos.Remove(deletedFile);
        RefreshSaveGames();
    }
}
