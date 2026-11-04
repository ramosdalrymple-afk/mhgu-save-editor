using System;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using MhguSave.Format;

namespace MhguSave.App.ViewModels;

public partial class MainViewModel
{
    [RelayCommand] private void SortItemBox() => ReorganizeItems(false);
    [RelayCommand] private void MergeItemBox() => ReorganizeItems(true);

    private void ReorganizeItems(bool merge)
    {
        if (_slot is null || !ApplyPendingEditors()) return;
        try
        {
            if (merge) _slot.ItemBox.MergeStacks(); else _slot.ItemBox.SortById();
            RefreshItemBoxRows();
            Status = merge ? "Duplicate item stacks merged without dropping quantities. Save changes to write." : "Item box sorted by item ID. Save changes to write.";
        }
        catch (ArgumentException ex) { Status = ex.Message; }
    }

    private void RefreshItemBoxRows()
    {
        HasBoxDraft = false;
        foreach (var row in BoxRows) row.Refresh();
        RefreshFilters(); RefreshSummary();
        if (SelectedBox is not null) { EditBoxId = SelectedBox.Id; EditBoxCount = SelectedBox.Count; HasBoxDraft = false; }
    }

    public bool ExportItemBoxCsv(string path)
    {
        if (_slot is null || !ApplyPendingEditors()) return false;
        try
        {
            var name = Path.GetFileName(path);
            if (_path is not null && string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),
                Path.GetDirectoryName(Path.GetFullPath(_path)), StringComparison.OrdinalIgnoreCase)
                && (name.Equals("system", StringComparison.OrdinalIgnoreCase) || name.Equals("system_backup", StringComparison.OrdinalIgnoreCase)))
            {
                Status = "Choose a CSV destination separate from the game save files.";
                return false;
            }
            File.WriteAllText(path, InventoryCsv.Export(_slot.ItemBox, _catalog));
            Status = "Item box exported to CSV.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Status = ex.Message; return false; }
    }

    public bool ImportItemBoxCsv(string path)
    {
        if (_slot is null) return false;
        try
        {
            // Parse every row before staging any replacement of the inventory.
            var data = InventoryCsv.Parse(File.ReadAllText(path), _slot.ItemBox.Count);
            if (!ApplyPendingEditors()) return false;
            _slot.ItemBox.ReplaceAll(data);
            RefreshItemBoxRows();
            Status = "CSV staged as an item-box replacement; omitted slots are empty. Save changes to write.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { Status = ex.Message; return false; }
    }
}
