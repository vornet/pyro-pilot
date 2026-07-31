using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PyroPilot.App.Services;
using PyroPilot.Core.Model;
using PyroPilot.Core.Persistence;

namespace PyroPilot.App.ViewModels;

/// <summary>CRUD screen for the operator's global firework library (see PyroPilot.Core.Persistence.FireworkLibraryStore).</summary>
public partial class LibraryViewModel : ViewModelBase
{
    public ObservableCollection<FireworkDefinition> Fireworks { get; } = [];
    public string[] CommonCategories { get; } = ["Cake", "Shell", "Fountain", "Roman Candle", "Mine", "Uncategorized"];

    [ObservableProperty]
    private FireworkDefinition? _selected;

    [ObservableProperty]
    private string _editName = "New Firework";

    [ObservableProperty]
    private string _editCategory = "Uncategorized";

    [ObservableProperty]
    private int _editDurationMs = 3000;

    [ObservableProperty]
    private string _editColorHex = "#FF7A00";

    [ObservableProperty]
    private string? _editDescription;

    public LibraryViewModel()
    {
        foreach (FireworkDefinition fw in FireworkLibraryStore.Load(AppPaths.LibraryFilePath))
            Fireworks.Add(fw);
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        EditName = "New Firework";
        EditCategory = "Uncategorized";
        EditDurationMs = 3000;
        EditColorHex = "#FF7A00";
        EditDescription = null;
    }

    [RelayCommand]
    private void Save()
    {
        if (Selected is null)
        {
            var created = new FireworkDefinition
            {
                Name = EditName,
                Category = EditCategory,
                DurationMs = EditDurationMs,
                ColorHex = EditColorHex,
                Description = EditDescription,
            };
            Fireworks.Add(created);
            Selected = created;
        }
        else
        {
            Selected.Name = EditName;
            Selected.Category = EditCategory;
            Selected.DurationMs = EditDurationMs;
            Selected.ColorHex = EditColorHex;
            Selected.Description = EditDescription;

            // FireworkDefinition isn't a notifying type -- re-assigning through
            // the indexer forces the bound list's display to refresh for this item.
            int index = Fireworks.IndexOf(Selected);
            if (index >= 0) Fireworks[index] = Selected;
        }

        Persist();
    }

    [RelayCommand]
    private void Delete(FireworkDefinition item)
    {
        Fireworks.Remove(item);
        if (ReferenceEquals(Selected, item)) New();
        Persist();
    }

    partial void OnSelectedChanged(FireworkDefinition? value)
    {
        if (value is null) return;
        EditName = value.Name;
        EditCategory = value.Category;
        EditDurationMs = value.DurationMs;
        EditColorHex = value.ColorHex;
        EditDescription = value.Description;
    }

    private void Persist() => FireworkLibraryStore.Save(AppPaths.LibraryFilePath, Fireworks);
}
