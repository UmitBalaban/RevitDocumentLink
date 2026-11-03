using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using ParamBridge.Core.Models;

namespace ParamBridge.UI.ViewModels;

public partial class ChangesViewModel : ObservableObject
{
    public ObservableCollection<CellChange> Changes { get; }
    public ICollectionView View { get; }
    public string[] Filters { get; } = { "All", "Will apply", "Warnings", "Errors" };

    [ObservableProperty] private string _filter = "All";

    public ChangesViewModel(IEnumerable<CellChange> changes)
    {
        Changes = new ObservableCollection<CellChange>(changes);
        View = CollectionViewSource.GetDefaultView(Changes);
        View.Filter = o => o is CellChange c && Filter switch
        {
            "Will apply" => c.Status == ChangeStatus.WillApply,
            "Warnings" => c.Status == ChangeStatus.Warning,
            "Errors" => c.Status == ChangeStatus.Error,
            _ => true,
        };
    }

    partial void OnFilterChanged(string value) => View.Refresh();

    public string Counters =>
        $"✓ {Changes.Count(c => c.Status == ChangeStatus.WillApply)}   " +
        $"⚠ {Changes.Count(c => c.Status == ChangeStatus.Warning)}   " +
        $"✗ {Changes.Count(c => c.Status == ChangeStatus.Error)}";
}
