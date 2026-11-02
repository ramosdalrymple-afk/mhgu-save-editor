using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public string ApplicationVersion => typeof(MainViewModel).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "Development";
    public string ApplicationTitle => $"MHGU Save Editor · {ApplicationVersion}" +
        (ApplicationVersion.Contains('-') ? " · Early Access" : string.Empty);
    public string HunterArtworkKey => "artwork:hunter";
    public string VillageArtworkKey => "artwork:village";
    public bool HasGameVisuals => Services.GameVisuals.IsAvailable;
    private ItemCatalog _catalog;
    private readonly QuestCatalog _questCatalog;
    private readonly EquipmentCatalog _equipCatalog;
    private SaveContainer? _save;
    private CharacterSlot? _slot;
    private string? _path;
    private bool _syncing;
    private readonly Func<IReadOnlyList<string>> _emulatorProbe;
    private readonly Func<string, SaveContainer, SaveResult> _saveWriter;
    [ObservableProperty] private int _selectedSection;
    public bool IsProfileSection => SelectedSection == 0;
    public bool IsItemsSection => SelectedSection == 1;
    public bool IsEquipmentSection => SelectedSection == 2;
    public bool IsQuestsSection => SelectedSection == 3;
    public bool IsUnlocksSection => SelectedSection == 4;
    partial void OnSelectedSectionChanged(int value)
    {
        OnPropertyChanged(nameof(IsProfileSection));
        OnPropertyChanged(nameof(IsItemsSection));
        OnPropertyChanged(nameof(IsEquipmentSection));
        OnPropertyChanged(nameof(IsQuestsSection));
        OnPropertyChanged(nameof(IsUnlocksSection));
    }
    public ObservableCollection<EquipmentPick> EquipmentMatches { get; } = [];
    [ObservableProperty] private string _equipmentQuery = string.Empty;
    [ObservableProperty] private EquipmentPick? _selectedEquipmentPick;
    partial void OnEquipmentQueryChanged(string value) => RefreshEquipmentMatches();
    partial void OnSelectedEquipmentPickChanged(EquipmentPick? value)
    {
        if (value is not null) AddEquipId = value.Id;
    }
    private void RefreshEquipmentMatches()
    {
        var previousId = AddEquipId;
        EquipmentMatches.Clear();
        if (!ValidInteger(AddEquipType, 1, 24)) return;
        foreach (var entry in _equipCatalog.Search(EquipmentQuery, (int)AddEquipType, 5000)) EquipmentMatches.Add(entry);
        SelectedEquipmentPick = EquipmentMatches.FirstOrDefault(e => e.Id == previousId) ?? EquipmentMatches.FirstOrDefault();
        if (SelectedEquipmentPick is null) AddEquipId = 0;
    }
    public ObservableCollection<CharacterChoice> Characters { get; } = [];
    public System.Collections.Generic.IReadOnlyList<EquipmentTypeInfo> EquipmentTypes => _equipCatalog.Types;
    [ObservableProperty] private EquipmentTypeInfo? _selectedAddEquipmentType;
    partial void OnSelectedAddEquipmentTypeChanged(EquipmentTypeInfo? value)
    {
        if (value is not null) AddEquipType = value.Code;
    }
    [ObservableProperty] private CharacterChoice? _selectedCharacter;
    [ObservableProperty] private bool _hasUnsavedChanges;
    [ObservableProperty] private string _saveState = "No save open";
    [ObservableProperty] private string _boxStat = "—";
    [ObservableProperty] private string _equipStat = "—";
    [ObservableProperty] private string _questStat = "—";
    [ObservableProperty] private decimal _editTransmogId;
    [ObservableProperty] private decimal _editTransmogLevel = 1;
    [ObservableProperty] private decimal _editKinsectId;
    [ObservableProperty] private decimal _editKinsectLevel = 1;
    public bool IsArmor => EditEquipType >= 1 && EditEquipType <= 5;
    public bool IsTalisman => EditEquipType == EquipmentSlot.TalismanType;
    public bool IsGlaive => EditEquipType == EquipmentSlot.InsectGlaiveType;
    public int EditEquipMaxLevel => ValidInteger(EditEquipType, 1, 24) && ValidInteger(EditEquipId, 0, 65535)
        ? _equipCatalog.MaxLevel((int)EditEquipType, (int)EditEquipId) : 1;
    partial void OnEditEquipIdChanged(decimal value)
    {
        OnPropertyChanged(nameof(EditEquipMaxLevel));
        OnPropertyChanged(nameof(CanUseEquipmentMaximum));
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        TrackEditorChange(e.PropertyName);
        if (e.PropertyName == nameof(IsLoaded)) OnPropertyChanged(nameof(CanAddEquipment));
        if (!_syncing && IsLoaded && e.PropertyName is nameof(HunterName) or nameof(Zenny)
            or nameof(Wycademy) or nameof(HrPoints) or nameof(VillageStar) or nameof(HubStar) or nameof(QuestCounter))
            HasUnsavedChanges = true;
    }

    partial void OnHasUnsavedChangesChanged(bool value) => SaveState = value ? "Unsaved changes" : IsLoaded ? "All changes saved" : "No save open";
    partial void OnEditEquipTypeChanged(decimal value)
    {
        OnPropertyChanged(nameof(IsArmor));
        OnPropertyChanged(nameof(IsTalisman));
        OnPropertyChanged(nameof(IsGlaive));
        OnPropertyChanged(nameof(EditEquipMaxLevel));
        OnPropertyChanged(nameof(CanUseEquipmentMaximum));
    }

    partial void OnSelectedCharacterChanged(CharacterChoice? value)
    {
        if (_syncing || value is null || _save is null || value.Index == _slot?.Index) return;
        try
        {
            if (!ApplyPendingEditors()) throw new ArgumentException(Status);
            ApplyCharacterFields();
            ActivateSlot(_save.Slots[value.Index]);
            Status = $"Editing {value.Label}. Changes in other characters are kept until you save.";
        }
        catch (ArgumentException ex)
        {
            _syncing = true;
            SelectedCharacter = Characters.FirstOrDefault(c => c.Index == _slot?.Index);
            _syncing = false;
            Status = "Fix the current character before switching: " + ex.Message;
        }
    }

    public MainViewModel() : this(EmulatorGuard.RunningEmulators, (path, save) => SaveWriter.Write(path, save)) { }

    internal MainViewModel(Func<IReadOnlyList<string>> emulatorProbe,
        Func<string, SaveContainer, SaveResult> saveWriter)
    {
        _emulatorProbe = emulatorProbe;
        _saveWriter = saveWriter;
        _catalog = ItemCatalog.LoadEmbedded();
        _questCatalog = QuestCatalog.LoadEmbedded();
        _equipCatalog = EquipmentCatalog.LoadEmbedded();
        InitializeEquipmentEditors();
        EquipTypeNames.Add("All");
        foreach (var type in _equipCatalog.Types) EquipTypeNames.Add(type.Name);
        QuestCategories.Add("All");
        foreach (var category in Enumerable.Range(1, QuestCatalog.QuestCount).Select(i => _questCatalog.Get(i).Category).Distinct())
            QuestCategories.Add(category);
        SelectedAddEquipmentType = EquipmentTypes.First(type => type.Code == (int)AddEquipType);
        ItemNamesStatus = $"Item names: {_catalog.Count:N0} embedded";
        QuestStatus = $"Quest list: {_questCatalog.Count:N0} named quests embedded";
        EquipStatus = $"Equipment names: {_equipCatalog.NameCount:N0} embedded";
        RefreshEquipmentMatches();
    }

    // ---- file / status ---------------------------------------------------------------
    [ObservableProperty] private string _status = "Open an MHGU save (the file named \"system\") to start.";
    [ObservableProperty] private string _filePath = "No file loaded";
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private string _emulatorWarning = string.Empty;
    [ObservableProperty] private string _itemNamesStatus;

    // ---- character -------------------------------------------------------------------
    [ObservableProperty] private string _hunterName = "-";
    [ObservableProperty] private string _rankLine = "-";
    [ObservableProperty] private string _playTimeLine = "-";
    [ObservableProperty] private string _itemsLine = "-";
    [ObservableProperty] private string _equipmentLine = "-";
    [ObservableProperty] private string _questLine = "-";
    [ObservableProperty] private string _unlockLine = "-";

    [ObservableProperty] private decimal _zenny;
    [ObservableProperty] private decimal _wycademy;
    [ObservableProperty] private decimal _hrPoints;
    [ObservableProperty] private decimal _villageStar;
    [ObservableProperty] private decimal _hubStar;
    [ObservableProperty] private decimal _questCounter;

    // ---- item box / pouch ------------------------------------------------------------
    public ObservableCollection<ItemRow> BoxRows { get; } = [];
    public ObservableCollection<ItemRow> PouchRows { get; } = [];
    public ObservableCollection<ItemRow> BoxView { get; } = [];
    public ObservableCollection<ItemRow> PouchView { get; } = [];

    [ObservableProperty] private bool _hideEmpty = true;
    [ObservableProperty] private string _itemFilter = string.Empty;
    [ObservableProperty] private ItemRow? _selectedBox;
    [ObservableProperty] private ItemRow? _selectedPouch;
    [ObservableProperty] private decimal _editBoxId;
    [ObservableProperty] private decimal _editBoxCount;
    [ObservableProperty] private decimal _editPouchId;
    [ObservableProperty] private decimal _editPouchCount;
    [ObservableProperty] private decimal _addItemId = 9;
    [ObservableProperty] private decimal _addItemCount = 1;

    // ---- quests ----------------------------------------------------------------------
    public ObservableCollection<QuestRow> QuestRows { get; } = [];
    public ObservableCollection<QuestRow> QuestView { get; } = [];
    public ObservableCollection<string> QuestCategories { get; } = [];

    [ObservableProperty] private string _questFilter = string.Empty;
    [ObservableProperty] private string _questCategory = "All";
    [ObservableProperty] private QuestRow? _selectedQuest;
    [ObservableProperty] private string _questStatus;
    public bool HasQuestSelection => SelectedQuest is not null;
    public bool HasBoxSelection => SelectedBox is not null;
    public bool HasPouchSelection => SelectedPouch is not null;

    // ---- equipment -------------------------------------------------------------------
    public ObservableCollection<EquipmentRow> EquipRows { get; } = [];
    public ObservableCollection<EquipmentRow> EquipView { get; } = [];
    public ObservableCollection<string> EquipTypeNames { get; } = [];

    [ObservableProperty] private string _equipFilter = string.Empty;
    [ObservableProperty] private string _equipTypeFilter = "All";
    [ObservableProperty] private bool _hideEmptyEquip = true;
    [ObservableProperty] private EquipmentRow? _selectedEquip;
    [ObservableProperty] private string _equipStatus;

    [ObservableProperty] private decimal _editEquipType;
    [ObservableProperty] private decimal _editEquipId;
    [ObservableProperty] private decimal _editEquipLevel = 1;
    [ObservableProperty] private decimal _editEquipDeco1;
    [ObservableProperty] private decimal _editEquipDeco2;
    [ObservableProperty] private decimal _editEquipDeco3;
    [ObservableProperty] private decimal _editEquipSkill1;
    [ObservableProperty] private decimal _editEquipSkillPoints1;
    [ObservableProperty] private decimal _editEquipSkill2;
    [ObservableProperty] private decimal _editEquipSkillPoints2;
    [ObservableProperty] private decimal _editEquipSlots;
    [ObservableProperty] private decimal _editEquipCharm = 353;

    [ObservableProperty] private decimal _addEquipType = 7;
    [ObservableProperty] private decimal _addEquipId = 1;
    [ObservableProperty] private decimal _addEquipLevel = 1;
    [ObservableProperty] private string _addEquipName = "Petrified Blade";

    // ---- item name search --------------------------------------------------------------
    public ObservableCollection<ItemPick> ItemNameMatches { get; } = [];
    [ObservableProperty] private string _itemNameQuery = string.Empty;
    [ObservableProperty] private ItemPick? _selectedItemPick;

    // ---- loading ---------------------------------------------------------------------

    public void Load(string path)
    {
        try
        {
            if (Directory.Exists(path))
                path = Path.Combine(path, File.Exists(Path.Combine(path, "system")) ? "system" : "system_backup");
            var save = SaveContainer.Load(path);
            var slot = save.Slots.FirstOrDefault(candidate => candidate.InUse && candidate.Index == save.LastSlot)
                       ?? save.Slots.FirstOrDefault(candidate => candidate.InUse)
                       ?? throw new SaveFormatException("No character slot is in use in this save.");
            _save = save;
            _path = path;
            FilePath = path;
            IsLoaded = true;
            _syncing = true;
            Characters.Clear();
            foreach (var character in save.Slots.Where(c => c.InUse))
                Characters.Add(new CharacterChoice(character.Index, $"Slot {character.Index + 1} · {character.Name}"));
            SelectedCharacter = Characters.First(c => c.Index == slot.Index);
            _syncing = false;
            ActivateSlot(slot);
            HasUnsavedChanges = false;
            SaveState = "All changes saved";
            UpdateEmulatorWarning();

            Status = $"Loaded {new FileInfo(path).Length:N0} bytes. Editing slot {slot.Index + 1} of 3.";
        }
        catch (Exception ex) when (ex is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            Status = "Could not open save: " + ex.Message;
        }
    }

    private void ActivateSlot(CharacterSlot slot)
    {
        _syncing = true;
        HasEquipmentDraft = HasBoxDraft = HasPouchDraft = false;
        _slot = slot;
        SelectedBox = null;
        SelectedPouch = null;
        SelectedEquip = null;
        SelectedQuest = null;
        BoxRows.Clear();
        PouchRows.Clear();
        for (var i = 0; i < slot.ItemBox.Count; i++) BoxRows.Add(new ItemRow(slot.ItemBox, i, _catalog));
        for (var i = 0; i < slot.Pouch.Count; i++) PouchRows.Add(new ItemRow(slot.Pouch, i, _catalog));
        FillQuestRows(slot);
        FillEquipRows(slot);
        RefreshFilters();
        RefreshQuestFilters();
        RefreshEquipFilters();
        SyncFromModel();
        _syncing = false;
    }

    private void SyncFromModel()
    {
        if (_slot is null)
        {
            return;
        }

        HunterName = _slot.Name;
        Zenny = _slot.Zenny;
        Wycademy = _slot.WycademyPoints;
        HrPoints = _slot.HrPoints;
        VillageStar = _slot.VillageStar;
        HubStar = _slot.HubStar;
        QuestCounter = _slot.QuestCounter;
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        if (_slot is null)
        {
            return;
        }

        if (_save?.IsDirty == true) HasUnsavedChanges = true;
        var slot = _slot;
        BoxStat = $"{slot.ItemBox.UsedCount():N0} / 2,300";
        EquipStat = $"{slot.EquipmentBoxUsed:N0} / 2,000";
        QuestStat = $"{slot.QuestsClearedCount:N0} / 1,509";
        RankLine = $"HR {slot.HunterRank}   ({slot.HrPoints:N0} HR points)";
        PlayTimeLine = $"{FormatPlayTime(slot.PlayTimeSeconds)}   ({slot.PlayTimeSeconds:N0} s)";
        ItemsLine = $"{slot.ItemBox.UsedCount()} of {slot.ItemBox.Count} box slots used, " +
                    $"{slot.Pouch.UsedCount()} pouch slots, {slot.QuestsClearedCount} quests cleared";
        EquipmentLine = $"{slot.EquipmentBoxUsed} of {EquipmentBox.Capacity} equipment records, quest counter {slot.QuestCounter}";
        QuestLine = $"Cleared {slot.ClearedQuests.SetCount()} / 1509   ·   seen {slot.SeenQuests.SetCount()}   ·   " +
                    $"failed {slot.FailedQuests.SetCount()}   ·   quest sets {slot.CompletedQuestSets.SetCount()} / 100";
        UnlockLine = $"Materials unlocked: {slot.ItemsObtained.SetCount():N0} / 2990   ·   " +
                     $"awards {slot.Awards.SetCount()} / 132 · Hunter Arts {slot.UnlockedHunterArtCount} / 178";
    }

    private void RefreshFilters()
    {
        if (!_refreshingViews && HasBoxDraft && SelectedBox is not null
            && !TryApplyItem(SelectedBox, EditBoxId, EditBoxCount, "box")) return;
        if (!_refreshingViews && HasPouchDraft && SelectedPouch is not null
            && !TryApplyItem(SelectedPouch, EditPouchId, EditPouchCount, "pouch")) return;
        var wasRefreshing = _refreshingViews;
        _refreshingViews = true;
        try
        {
            var box = SelectedBox;
            var pouch = SelectedPouch;
            FillView(BoxRows, BoxView);
            FillView(PouchRows, PouchView);
            SelectedBox = box is not null && BoxView.Contains(box) ? box : null;
            SelectedPouch = pouch is not null && PouchView.Contains(pouch) ? pouch : null;

            void FillView(ObservableCollection<ItemRow> source, ObservableCollection<ItemRow> view)
            {
                var filter = ItemFilter.Trim();
                view.Clear();
                foreach (var row in source)
                {
                    if (HideEmpty && row.IsEmpty)
                    {
                        continue;
                    }

                    if (filter.Length > 0 &&
                        !row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                        !row.Id.ToString().Equals(filter, StringComparison.OrdinalIgnoreCase) &&
                        !row.SlotNumber.ToString().Equals(filter, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    view.Add(row);
                }
            }
        }
        finally { _refreshingViews = wasRefreshing; }
        OnPropertyChanged(nameof(HasBoxSelection));
        OnPropertyChanged(nameof(HasPouchSelection));
    }

    partial void OnHideEmptyChanged(bool value) => RefreshFilters();

    partial void OnItemFilterChanged(string value) => RefreshFilters();

    partial void OnSelectedBoxChanged(ItemRow? oldValue, ItemRow? newValue)
    {
        OnPropertyChanged(nameof(HasBoxSelection));
        if (_syncing || _refreshingViews || _selectionGuard) return;
        if (HasBoxDraft && oldValue is not null && oldValue != newValue)
        {
            _selectionGuard = true;
            var applied = TryApplyItem(oldValue, EditBoxId, EditBoxCount, "box");
            if (!applied) SelectedBox = oldValue;
            _selectionGuard = false;
            if (!applied) return;
        }
        _loadingEditors = true;
        EditBoxId = newValue?.Id ?? 0;
        EditBoxCount = newValue?.Count ?? 0;
        HasBoxDraft = false;
        _loadingEditors = false;
    }

    partial void OnSelectedPouchChanged(ItemRow? oldValue, ItemRow? newValue)
    {
        OnPropertyChanged(nameof(HasPouchSelection));
        if (_syncing || _refreshingViews || _selectionGuard) return;
        if (HasPouchDraft && oldValue is not null && oldValue != newValue)
        {
            _selectionGuard = true;
            var applied = TryApplyItem(oldValue, EditPouchId, EditPouchCount, "pouch");
            if (!applied) SelectedPouch = oldValue;
            _selectionGuard = false;
            if (!applied) return;
        }
        _loadingEditors = true;
        EditPouchId = newValue?.Id ?? 0;
        EditPouchCount = newValue?.Count ?? 0;
        HasPouchDraft = false;
        _loadingEditors = false;
    }

    // ---- quests ----------------------------------------------------------------------

    private void FillQuestRows(CharacterSlot slot)
    {
        QuestRows.Clear();

        for (var index = 1; index <= QuestCatalog.QuestCount; index++)
        {
            var row = new QuestRow(slot, _questCatalog.Get(index));
            row.ClearedChanged += OnQuestRowChanged;
            QuestRows.Add(row);

        }
    }

    private void OnQuestRowChanged(object? sender, EventArgs e)
    {
        if (sender is QuestRow row && SelectedQuest == row)
        {
            QuestStatus = $"Quest {row.Index}: {row.Name} — {(row.IsCleared ? "cleared" : "not cleared")}";
        }

        RefreshSummary();
    }

    private void RefreshQuestFilters()
    {
        var selected = SelectedQuest;
        var filter = QuestFilter.Trim();
        QuestView.Clear();

        foreach (var row in QuestRows)
        {
            if (QuestCategory != "All" && !row.Category.Equals(QuestCategory, StringComparison.Ordinal))
            {
                continue;
            }

            if (filter.Length > 0 &&
                !row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !row.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !row.Index.ToString().Equals(filter, StringComparison.Ordinal))
            {
                continue;
            }

            QuestView.Add(row);
        }
        SelectedQuest = selected is not null && QuestView.Contains(selected) ? selected : null;
    }

    partial void OnQuestFilterChanged(string value) => RefreshQuestFilters();

    partial void OnQuestCategoryChanged(string value) => RefreshQuestFilters();

    partial void OnSelectedQuestChanged(QuestRow? value)
    {
        OnPropertyChanged(nameof(HasQuestSelection));
        if (value is null)
        {
            return;
        }

        QuestStatus = $"Quest {value.Index}: {value.Name} — " +
                      $"{(value.IsCleared ? "cleared" : "not cleared")}, {(value.IsSeen ? "seen" : "unseen")}";
    }

    // ---- equipment -------------------------------------------------------------------

    private void FillEquipRows(CharacterSlot slot)
    {
        EquipRows.Clear();

        for (var index = 0; index < slot.EquipmentBox.Count; index++)
        {
            var row = new EquipmentRow(slot.EquipmentBox, index, _equipCatalog);
            row.PieceChanged += OnEquipmentRowChanged;
            EquipRows.Add(row);
        }
    }

    private void OnEquipmentRowChanged(object? sender, EventArgs e)
    {
        if (sender is EquipmentRow row && SelectedEquip == row)
        {
            EquipStatus = $"Slot {row.SlotNumber}: {row.EditorTitle}";
        }

        RefreshSummary();
    }

    private void RefreshEquipFilters()
    {
        if (!_refreshingViews && HasEquipmentDraft && SelectedEquip is not null
            && !TryApplyEquipment(SelectedEquip)) return;
        var wasRefreshing = _refreshingViews;
        _refreshingViews = true;
        try
        {
            var selected = SelectedEquip;
            var filter = EquipFilter.Trim();
            EquipView.Clear();

            foreach (var row in EquipRows)
            {
                if (HideEmptyEquip && row.IsEmpty)
                {
                    continue;
                }

                if (EquipTypeFilter != "All" && !row.TypeName.Equals(EquipTypeFilter, StringComparison.Ordinal))
                {
                    continue;
                }

                if (filter.Length > 0 &&
                    !row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !row.TypeName.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !row.Id.ToString().Equals(filter, StringComparison.Ordinal) &&
                    !row.SlotNumber.ToString().Equals(filter, StringComparison.Ordinal))
                {
                    continue;
                }

                EquipView.Add(row);
            }
            SelectedEquip = selected is not null && EquipView.Contains(selected) ? selected : null;
        }
        finally { _refreshingViews = wasRefreshing; }
        OnPropertyChanged(nameof(HasEquipmentSelection));
        OnPropertyChanged(nameof(CanUseEquipmentMaximum));
    }

    partial void OnEquipFilterChanged(string value) => RefreshEquipFilters();

    partial void OnEquipTypeFilterChanged(string value) => RefreshEquipFilters();

    partial void OnHideEmptyEquipChanged(bool value) => RefreshEquipFilters();

    partial void OnSelectedEquipChanged(EquipmentRow? oldValue, EquipmentRow? newValue)
    {
        if (_syncing || _refreshingViews || _selectionGuard) return;
        if (HasEquipmentDraft && oldValue is not null && oldValue != newValue)
        {
            _selectionGuard = true;
            var applied = TryApplyEquipment(oldValue);
            if (!applied) SelectedEquip = oldValue;
            _selectionGuard = false;
            if (!applied) return;
        }
        LoadEquipmentEditor(newValue);
    }

    private void LoadEquipmentEditor(EquipmentRow? value)
    {
        OnPropertyChanged(nameof(HasEquipmentSelection));
        OnPropertyChanged(nameof(CanUseEquipmentMaximum));
        if (value is null || value.IsEmpty)
        {
            HasEquipmentDraft = false;
            EquipStatus = value is null ? "Select equipment to edit, or use Add equipment." : "This slot is empty. Use Add equipment to create a piece.";
            return;
        }
        _loadingEditors = true;
        var piece = value.Piece;
        EditTransmogId = piece.TransmogId;
        EditTransmogLevel = piece.TransmogLevel;
        EditKinsectId = piece.KinsectId;
        EditKinsectLevel = Math.Max(1, piece.KinsectLevel);
        EditEquipType = piece.Type;
        EditEquipId = piece.Id;
        EditEquipLevel = piece.Level;
        EditEquipDeco1 = piece.Deco1;
        EditEquipDeco2 = piece.Deco2;
        EditEquipDeco3 = piece.Deco3;
        EditEquipSkill1 = piece.Skill1;
        EditEquipSkillPoints1 = piece.SkillPoints1;
        EditEquipSkill2 = piece.Skill2;
        EditEquipSkillPoints2 = piece.SkillPoints2;
        EditEquipSlots = piece.SlotCount;
        EditEquipCharm = piece.CharmItem == 0 ? 353 : piece.CharmItem;
        HasEquipmentDraft = false;
        _loadingEditors = false;
        EquipmentPane = 0;
        EquipStatus = $"Slot {value.SlotNumber}: {value.EditorTitle}. Edit the fields and Apply, or Save changes.";
        OnPropertyChanged(nameof(CanUseEquipmentMaximum));
    }

    partial void OnItemNameQueryChanged(string value)
    {
        ItemNameMatches.Clear();
        foreach (var (id, name) in _catalog.Search(value, 40))
        {
            ItemNameMatches.Add(new ItemPick(id, name));
        }
    }

    partial void OnSelectedItemPickChanged(ItemPick? value)
    {
        if (value is null)
        {
            return;
        }

        AddItemId = value.Id;
        Status = $"Item id set to {value.Id} ({value.Name}). Press Add to put it in the list.";
    }

    // ---- commands --------------------------------------------------------------------

    [RelayCommand]
    private void Save()
    {
        if (_save is null || _slot is null || _path is null)
        {
            Status = "Nothing to save yet.";
            return;
        }

        UpdateEmulatorWarning();
        var running = _emulatorProbe();
        if (running.Count > 0)
        {
            Status = $"Refusing to write: {string.Join(", ", running)} is running. Close it first.";
            return;
        }

        try
        {
            if (!ApplyPendingEditors()) return;
            ApplyCharacterFields();
            var result = _saveWriter(_path, _save);
            Status = $"Wrote {result.Written.Count} file(s), {result.Backups.Count} backup(s). " +
                     "Load the character in game to confirm.";
            HasUnsavedChanges = false;
            SaveState = "All changes saved";
            RefreshSummary();
            EquipStatus = "Saved equipment and inventory changes to disk.";
            _syncing = true;
            Characters.Clear();
            foreach (var character in _save.Slots.Where(c => c.InUse))
                Characters.Add(new CharacterChoice(character.Index, $"Slot {character.Index + 1} · {character.Name}"));
            SelectedCharacter = Characters.First(c => c.Index == _slot.Index);
            _syncing = false;
        }
        catch (Exception ex) when (ex is SaveFormatException or IOException or UnauthorizedAccessException
                                       or ArgumentException)
        {
            Status = "Write failed: " + ex.Message;
        }
    }

    public void Revert()
    {
        if (_path is not null) Load(_path);
    }

    private void ApplyCharacterFields()
    {
        if (_slot is null)
        {
            throw new InvalidOperationException("No save loaded.");
        }

        var name = (HunterName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new ArgumentException("Hunter name cannot be empty.");
        }

        if (Encoding.UTF8.GetByteCount(name) > 31)
        {
            throw new ArgumentException("Hunter name must fit in 31 UTF-8 bytes.");
        }

        if (name.Contains('\0')) throw new ArgumentException("Hunter name cannot contain a null character.");
        RequireInteger(Zenny, 0, CharacterSlot.PointCap, "Zenny");
        RequireInteger(Wycademy, 0, CharacterSlot.PointCap, "Wycademy points");
        RequireInteger(HrPoints, 0, uint.MaxValue, "HR points");
        RequireInteger(VillageStar, 1, 10, "Village stars");
        RequireInteger(HubStar, 0, 13, "Hub stars");
        RequireInteger(QuestCounter, 0, ushort.MaxValue, "Quest counter");
        if (_slot.Name != name) _slot.Name = name;
        if (_slot.Zenny != Zenny) _slot.Zenny = (int)Zenny;
        if (_slot.WycademyPoints != Wycademy) _slot.WycademyPoints = (int)Wycademy;
        if (_slot.HrPoints != HrPoints) _slot.HrPoints = (uint)HrPoints;
        if (_slot.VillageStar != VillageStar) _slot.VillageStar = (ushort)VillageStar;
        if (_slot.HubStar != HubStar) _slot.HubStar = (ushort)HubStar;
        if (_slot.QuestCounter != QuestCounter) _slot.QuestCounter = (ushort)QuestCounter;
    }

    [RelayCommand]
    private void MaxZenny()
    {
        Zenny = CharacterSlot.PointCap;
        Status = "Zenny set to the cap. Press Save to write.";
    }

    [RelayCommand]
    private void MaxPoints()
    {
        Wycademy = CharacterSlot.PointCap;
        Status = "Wycademy points set to the cap. Press Save to write.";
    }

    [RelayCommand]
    private void ApplySelectedBox() => TryApplyItem(SelectedBox, EditBoxId, EditBoxCount, "box");

    [RelayCommand]
    private void ApplySelectedPouch() => TryApplyItem(SelectedPouch, EditPouchId, EditPouchCount, "pouch");

    private bool TryApplyItem(ItemRow? row, decimal id, decimal count, string listName)
    {
        if (row is null)
        {
            Status = $"Select a {listName} slot first.";
            return false;
        }

        if (!ValidInteger(id, 0, PackedItemList.MaxId) || !ValidInteger(count, 0, PackedItemList.MaxCount))
        {
            Status = "Use a whole item ID from 0–4095 and count from 0–127.";
            return false;
        }
        if (id == 0)
        {
            row.Id = 0;
            row.Count = 0;
            Status = $"Slot {row.SlotNumber} cleared.";
        }
        else
        {
            row.Id = (int)id;
            row.Count = Math.Max(1, (int)count);
            Status = $"Slot {row.SlotNumber} set to {_catalog.NameOf(row.Id)} x{row.Count}.";
        }

        _loadingEditors = true;
        if (listName == "box" && ReferenceEquals(row, SelectedBox))
        {
            EditBoxId = row.Id;
            EditBoxCount = row.Count;
        }
        if (listName == "pouch" && ReferenceEquals(row, SelectedPouch))
        {
            EditPouchId = row.Id;
            EditPouchCount = row.Count;
        }
        _loadingEditors = false;
        if (listName == "box") HasBoxDraft = false; else HasPouchDraft = false;
        RefreshFilters();
        RefreshSummary();
        return true;
    }

    [RelayCommand]
    private void ClearSelectedBox()
    {
        if (SelectedBox is null)
        {
            return;
        }

        HasBoxDraft = false;
        var row = SelectedBox;
        row.Id = 0;
        row.Count = 0;
        EditBoxId = 0;
        EditBoxCount = 0;
        HasBoxDraft = false;
        RefreshFilters();
        RefreshSummary();
        Status = $"Slot {row.SlotNumber} cleared.";
    }

    [RelayCommand]
    private void ClearSelectedPouch()
    {
        if (SelectedPouch is null)
        {
            return;
        }

        HasPouchDraft = false;
        var row = SelectedPouch;
        row.Id = 0;
        row.Count = 0;
        EditPouchId = 0;
        EditPouchCount = 0;
        HasPouchDraft = false;
        RefreshFilters();
        RefreshSummary();
        Status = $"Pouch slot {row.SlotNumber} cleared.";
    }

    [RelayCommand]
    private void AddToBox() => AddItem(_slot?.ItemBox, SelectedBoxSetter, "box");

    [RelayCommand]
    private void AddToPouch() => AddItem(_slot?.Pouch, SelectedPouchSetter, "pouch");

    private void SelectedBoxSetter(ItemRow row) => SelectedBox = row;

    private void SelectedPouchSetter(ItemRow row) => SelectedPouch = row;

    private void AddItem(PackedItemList? list, Action<ItemRow> select, string listName)
    {
        if (list is null)
        {
            Status = "Load a save first.";
            return;
        }

        if (!ValidInteger(AddItemId, 1, PackedItemList.MaxId) || !ValidInteger(AddItemCount, 1, PackedItemList.MaxCount))
        {
            Status = "Enter a whole item ID from 1–4095 and count from 1–127.";
            return;
        }

        var count = Math.Clamp((int)AddItemCount, 1, PackedItemList.MaxCount);
        if (!ApplyPendingEditors()) return;
        var index = list.Add(new ItemSlot((int)AddItemId, count));
        if (index < 0)
        {
            Status = $"The {listName} is full.";
            return;
        }

        var row = (listName == "box" ? BoxRows : PouchRows)[index];
        row.Refresh();
        ItemFilter = string.Empty;
        RefreshFilters();
        select(row);
        RefreshSummary();
        Status = $"Added {_catalog.NameOf(row.Id)} x{row.Count} to {listName} slot {row.SlotNumber}.";
    }

    [RelayCommand]
    private void UnlockAllMaterials()
    {
        if (_slot is null)
        {
            return;
        }

        _slot.ItemsObtained.SetRange(1, 2990);
        RefreshSummary();
        Status = "All item ids 1-2990 marked as obtained (materials known to the Smithy). Press Save.";
    }

    [RelayCommand]
    private void GrantAllAwards()
    {
        if (_slot is null)
        {
            return;
        }

        _slot.Awards.SetAll();
        RefreshSummary();
        Status = "All 132 Guild Card awards granted. Press Save.";
    }

    [RelayCommand]
    private void UnlockAllArts()
    {
        if (_slot is null)
        {
            return;
        }

        _slot.UnlockAllHunterArts();
        RefreshSummary();
        Status = "All Hunter Arts unlocked. Press Save.";
    }

    [RelayCommand]
    private void ClearSelectedQuest()
    {
        if (SelectedQuest is null)
        {
            QuestStatus = "Select a quest first.";
            return;
        }

        SetQuestState([SelectedQuest], true, $"quest {SelectedQuest.Index}");
    }

    [RelayCommand]
    private void UnclearSelectedQuest()
    {
        if (SelectedQuest is null)
        {
            QuestStatus = "Select a quest first.";
            return;
        }

        SetQuestState([SelectedQuest], false, $"quest {SelectedQuest.Index}");
    }

    [RelayCommand]
    private void ClearVisibleQuests()
    {
        if (QuestView.Count == 0)
        {
            QuestStatus = "No quests match the current filter.";
            return;
        }

        SetQuestState(QuestView.ToList(), true, $"{QuestView.Count} shown quest(s)");
    }

    [RelayCommand]
    private void UnclearVisibleQuests()
    {
        if (QuestView.Count == 0)
        {
            QuestStatus = "No quests match the current filter.";
            return;
        }

        SetQuestState(QuestView.ToList(), false, $"{QuestView.Count} shown quest(s)");
    }

    private void SetQuestState(IReadOnlyList<QuestRow> rows, bool cleared, string label)
    {
        if (_slot is null)
        {
            return;
        }

        var changed = 0;
        foreach (var row in rows)
        {
            if (row.IsCleared == cleared)
            {
                continue;
            }

            _slot.SetQuestCleared(row.Index, cleared);
            row.Refresh();
            changed++;
        }

        RefreshSummary();
        QuestStatus = $"{changed} quest(s) {(cleared ? "cleared" : "un-cleared")} ({label}). " +
                      "Press Save to write.";
    }

    [RelayCommand]
    private void MarkAllQuestsCleared()
    {
        if (_slot is null)
        {
            return;
        }

        _slot.ClearedQuests.SetAll();
        _slot.SeenQuests.SetAll();
        RefreshQuestRows();
        RefreshSummary();
        Status = "All 1509 quest flags marked cleared and seen. Press Save.";
    }

    [RelayCommand]
    private void MarkAllQuestsSeen()
    {
        if (_slot is null)
        {
            return;
        }

        _slot.SeenQuests.SetAll();
        RefreshQuestRows();
        RefreshSummary();
        Status = "All 1509 quests marked seen. Press Save.";
    }

    [RelayCommand]
    private void CompleteAllQuestSets()
    {
        if (_slot is null)
        {
            return;
        }

        _slot.CompletedQuestSets.SetAll();
        RefreshSummary();
        Status = "All 100 quest sets marked complete. Press Save.";
    }

    // ---- equipment -------------------------------------------------------------------

    [RelayCommand]
    private void ApplySelectedEquip() => TryApplyEquipment(SelectedEquip);

    private bool TryApplyEquipment(EquipmentRow? row)
    {
        if (row is null || row.IsEmpty)
        {
            EquipStatus = "Select a box slot first.";
            Status = EquipStatus;
            return false;
        }

        var current = row.Piece;
        try
        {
            RequireInteger(EditEquipType, 0, 24, "Equipment type");
            RequireInteger(EditEquipId, 0, 65535, "Equipment ID");
            RequireInteger(EditEquipLevel, 1, 32, "Equipment level");
            RequireInteger(EditEquipDeco1, 0, 65535, "Decoration 1");
            RequireInteger(EditEquipDeco2, 0, 65535, "Decoration 2");
            RequireInteger(EditEquipDeco3, 0, 65535, "Decoration 3");
            RequireInteger(EditTransmogId, 0, 65535, "Transmog ID");
            RequireInteger(EditTransmogLevel, 1, 32, "Source level");
            if (IsTalisman)
            {
                RequireInteger(EditEquipSkill1, 0, 255, "Skill 1");
                RequireInteger(EditEquipSkill2, 0, 255, "Skill 2");
                RequireInteger(EditEquipSkillPoints1, -128, 127, "Skill points 1");
                RequireInteger(EditEquipSkillPoints2, -128, 127, "Skill points 2");
                RequireInteger(EditEquipSlots, 0, 3, "Slots");
                RequireInteger(EditEquipCharm, 353, 356, "Charm item");
            }
            if (IsGlaive)
            {
                RequireInteger(EditKinsectId, 0, 255, "Kinsect ID");
                RequireInteger(EditKinsectLevel, 1, 256, "Kinsect level");
            }
        }
        catch (ArgumentException ex)
        {
            EquipStatus = "Rejected: " + ex.Message;
            Status = EquipStatus;
            return false;
        }
        var piece = new EquipmentSlot
        {
            Type = (int)EditEquipType,
            Id = (int)EditEquipId,
            Level = (int)EditEquipLevel,
            TransmogId = (int)EditTransmogId,
            TransmogLevel = (int)EditTransmogLevel,
            Deco1 = (int)EditEquipDeco1,
            Deco2 = (int)EditEquipDeco2,
            Deco3 = (int)EditEquipDeco3,
            KinsectId = (int)EditKinsectId,
            KinsectLevel = (int)EditKinsectLevel,
        };

        if (piece.Type == EquipmentSlot.TalismanType)
        {
            piece.Skill1 = (int)EditEquipSkill1;
            piece.SkillPoints1 = (int)EditEquipSkillPoints1;
            piece.Skill2 = (int)EditEquipSkill2;
            piece.SkillPoints2 = (int)EditEquipSkillPoints2;
            piece.SlotCount = (int)EditEquipSlots;
            piece.CharmItem = (int)EditEquipCharm;
        }

        try
        {
            if (piece.Type > 24) throw new ArgumentOutOfRangeException(nameof(piece.Type));
            if (piece.Type != current.Type)
                throw new ArgumentException("Add a new piece to change equipment type; existing payload bytes belong to the current type.");
            if (_equipCatalog.TypeOf(piece.Type).IsWeapon && piece.Level > _equipCatalog.MaxLevel(piece.Type, piece.Id))
                throw new ArgumentException($"This equipment supports levels 1–{_equipCatalog.MaxLevel(piece.Type, piece.Id)}. Use max level to choose the table maximum.");
            if (piece.Id != current.Id && !_equipCatalog.HasEquipment(piece.Type, piece.Id))
                throw new ArgumentException("Choose an equipment ID from the named catalog.");
            if (!_equipCatalog.HasSkill(piece.Skill1) || !_equipCatalog.HasSkill(piece.Skill2))
                throw new ArgumentException("Choose named talisman skills.");
            ValidateDecorations(piece, current);
            row.SetPiece(piece);
        }
        catch (ArgumentException ex)
        {
            EquipStatus = "Rejected: " + ex.Message;
            Status = EquipStatus;
            return false;
        }

        HasEquipmentDraft = false;
        RefreshEquipFilters();
        LoadEquipmentEditor(SelectedEquip);
        EquipStatus = piece.IsEmpty
            ? $"Slot {row.SlotNumber} cleared."
            : $"Slot {row.SlotNumber} set to {_equipCatalog.NameOf(piece.Type, piece.Id)}, level {piece.Level}. Press Save.";
        Status = EquipStatus;
        return true;
    }

    [RelayCommand]
    private void ClearSelectedEquip()
    {
        if (SelectedEquip is null)
        {
            EquipStatus = "Select a box slot first.";
            return;
        }

        HasEquipmentDraft = false;
        var row = SelectedEquip;
        row.Clear();
        _ = HideEmptyEquip = true;
        RefreshEquipFilters();
        EquipStatus = $"Equipment slot {row.SlotNumber} cleared.";
    }

    [RelayCommand]
    private void MaxSelectedEquipLevel()
    {
        if (SelectedEquip is null)
        {
            EquipStatus = "Select a box slot first.";
            return;
        }

        if (!CanUseEquipmentMaximum)
        {
            EquipStatus = "This catalog has no verified maximum for this piece. Enter the level directly.";
            return;
        }
        EditEquipLevel = EditEquipMaxLevel;
        EquipStatus = $"Level set to {EditEquipMaxLevel} (the table maximum). Press Apply.";
    }

    [RelayCommand]
    private void AddEquipment()
    {
        if (_slot is null)
        {
            EquipStatus = "Load a save first.";
            return;
        }

        if (!ValidInteger(AddEquipType, 1, 24) || !ValidInteger(AddEquipId, 0, 65535) || !ValidInteger(AddEquipLevel, 1, 32))
        {
            EquipStatus = "Choose a valid equipment type, ID, and whole level.";
            return;
        }
        var type = (int)AddEquipType;
        var id = (int)AddEquipId;
        if (!_equipCatalog.HasEquipment(type, id))
        {
            EquipStatus = Status = "Choose a named equipment item. Empty and unknown IDs cannot be added.";
            return;
        }
        var max = _equipCatalog.TypeOf(type).IsWeapon ? _equipCatalog.MaxLevel(type, id) : EquipmentSlot.MaxLevel;
        var piece = new EquipmentSlot
        {
            Type = type,
            Id = id,
            Level = Math.Clamp((int)AddEquipLevel, 1, max),
            KinsectLevel = 1,
            CharmItem = type == EquipmentSlot.TalismanType ? 353 : 0,
        };

        int index;
        try
        {
            ConfigureNewEquipment(piece);
            if (!ApplyPendingEditors()) return;
            index = _slot.EquipmentBox.Add(piece);
        }
        catch (ArgumentException ex)
        {
            EquipStatus = "Rejected: " + ex.Message;
            return;
        }

        if (index < 0)
        {
            EquipStatus = "The equipment box is full.";
            return;
        }

        var row = EquipRows[index];
        row.Refresh();
        EquipFilter = string.Empty;
        EquipTypeFilter = "All";
        RefreshEquipFilters();
        SelectedEquip = row;
        EquipmentPane = 0;
        EquipStatus = $"Added {_equipCatalog.NameOf(type, id)} (type {type}, id {id}, level {piece.Level}) " +
                      $"to slot {row.SlotNumber}. Press Save.";
        Status = EquipStatus;
    }

    partial void OnAddEquipTypeChanged(decimal value)
    {
        SelectedAddEquipmentType = EquipmentTypes.FirstOrDefault(type => type.Code == value);
        EquipmentQuery = string.Empty;
        AddEquipLevel = 1;
        UpdateAddEquipName();
        RefreshEquipmentMatches();
        OnPropertyChanged(nameof(IsAddingTalisman));
        OnPropertyChanged(nameof(IsAddingGlaive));
    }

    partial void OnAddEquipIdChanged(decimal value) => UpdateAddEquipName();

    private void UpdateAddEquipName()
    {
        OnPropertyChanged(nameof(CanAddEquipment));
        OnPropertyChanged(nameof(AddLevelHint));
        AddEquipName = ValidInteger(AddEquipType, 1, 24) && ValidInteger(AddEquipId, 0, 65535)
            ? _equipCatalog.NameOf((int)AddEquipType, (int)AddEquipId) : "Choose a valid type and ID";
    }

    public void LoadItemNames(string path)
    {
        try
        {
            var catalog = ItemCatalog.LoadCsv(path);
            _catalog = catalog;
            ItemNamesStatus = $"Item names: {catalog.Count:N0} from {Path.GetFileName(path)}";
            if (_slot is not null)
            {
                foreach (var row in BoxRows.Concat(PouchRows)) row.SetCatalog(catalog);
                RefreshFilters();
                OnItemNameQueryChanged(ItemNameQuery);
                Status = ItemNamesStatus;
            }
            else
            {
                Status = ItemNamesStatus;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = "Could not read item names: " + ex.Message;
        }
    }

    // ---- helpers ---------------------------------------------------------------------

    private void RefreshQuestRows()
    {
        foreach (var row in QuestRows)
        {
            row.Refresh();
        }

        RefreshQuestFilters();
    }

    private void UpdateEmulatorWarning()
    {
        var running = _emulatorProbe();
        EmulatorWarning = running.Count == 0
            ? string.Empty
            : $"Close {string.Join(", ", running)} before saving: the emulator holds the save in memory and overwrites it on exit.";
    }

    private static bool ValidInteger(decimal value, decimal min, decimal max) =>
        value >= min && value <= max && decimal.Truncate(value) == value;

    private static void RequireInteger(decimal value, decimal min, decimal max, string name)
    {
        if (!ValidInteger(value, min, max))
            throw new ArgumentException($"{name} must be a whole number from {min:N0} to {max:N0}.");
    }

    private static string FormatPlayTime(uint seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return $"{(int)time.TotalHours}h {time.Minutes:D2}m";
    }
}
