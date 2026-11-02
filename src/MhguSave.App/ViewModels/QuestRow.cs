using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

/// <summary>
/// One row of the quest list. The checkbox writes straight through to the save image's
/// cleared bitmap; nothing reaches the file until the Save button runs.
/// </summary>
public sealed class QuestRow : ObservableObject
{
    private readonly CharacterSlot _slot;

    public QuestRow(CharacterSlot slot, QuestInfo quest)
    {
        _slot = slot;
        Quest = quest;
    }

    /// <summary>Raised after <see cref="IsCleared"/> changed, so the summary can refresh.</summary>
    public event EventHandler? ClearedChanged;

    public QuestInfo Quest { get; }

    public int Index => Quest.Index;

    public string Category => Quest.Category;

    public string Name => Quest.Name;

    public string Rank => Quest.Rank;

    public bool IsCleared
    {
        get => _slot.IsQuestCleared(Index);
        set
        {
            if (value == IsCleared)
            {
                return;
            }

            _slot.SetQuestCleared(Index, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSeen));
            ClearedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsSeen => _slot.IsQuestSeen(Index);

    public string Display => $"{Index,4}  {Category}, ★{Rank}  {Name}";

    /// <summary>Re-reads the model after a bulk edit.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(IsCleared));
        OnPropertyChanged(nameof(IsSeen));
    }
}
