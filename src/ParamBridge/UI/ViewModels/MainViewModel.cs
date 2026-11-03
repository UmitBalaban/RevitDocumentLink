using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ParamBridge.Core;
using ParamBridge.Core.Excel;
using ParamBridge.Core.Models;
using ParamBridge.Core.Revit;
using ParamBridge.Infrastructure;
using ParamBridge.UI;

namespace ParamBridge.UI.ViewModels;

public partial class CategoryRow : ObservableObject
{
    public required CategoryItem Item { get; init; }
    [ObservableProperty] private bool _isChecked;
    public string Label => $"{Item.Name}";
    public int Count => Item.Count;
}

public partial class ParamRow : ObservableObject
{
    public required ParamInfo Info { get; init; }
    public string Name => Info.Name;
    public string Badge => Info.IsReadOnly ? "RO" : Info.Scope == ParamScope.Type ? "TYPE" : "INST";
    public string DataType => Info.DataType;
}

/// <summary>Main window state. Implements the Model / Annotation tabs end-to-end (M1–M3 baseline).</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private Dictionary<string, ParamInfo> _infoByKey = new();

    public ObservableCollection<CategoryRow> Categories { get; } = new();
    public ObservableCollection<ParamRow> Available { get; } = new();
    public ObservableCollection<ParamRow> Selected { get; } = new();
    public ObservableCollection<Profile> Profiles { get; } = new();

    public ICollectionView CategoriesView { get; }
    public ICollectionView AvailableView { get; }

    [ObservableProperty] private SourceTab _tab = SourceTab.Model;
    [ObservableProperty] private ElementScope _scope = ElementScope.WholeModel;
    [ObservableProperty] private string _categorySearch = "";
    [ObservableProperty] private string _paramSearch = "";
    [ObservableProperty] private bool _showInstance = true;
    [ObservableProperty] private bool _showType = true;
    [ObservableProperty] private bool _showReadOnly = true;
    [ObservableProperty] private bool _sortByType;
    [ObservableProperty] private bool _singleSheet;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private ParamRow? _availableSelection;
    [ObservableProperty] private ParamRow? _selectedSelection;
    [ObservableProperty] private Profile? _currentProfile;

    public MainViewModel()
    {
        CategoriesView = CollectionViewSource.GetDefaultView(Categories);
        CategoriesView.Filter = o => o is CategoryRow r &&
            (string.IsNullOrWhiteSpace(CategorySearch) || r.Item.Name.Contains(CategorySearch, StringComparison.CurrentCultureIgnoreCase));
        AvailableView = CollectionViewSource.GetDefaultView(Available);
        AvailableView.Filter = o => o is ParamRow r && FilterParam(r);
        foreach (var p in ProfileStore.LoadAll()) Profiles.Add(p);
    }

    partial void OnCategorySearchChanged(string value) => CategoriesView.Refresh();
    partial void OnParamSearchChanged(string value) => AvailableView.Refresh();
    partial void OnShowInstanceChanged(bool value) => AvailableView.Refresh();
    partial void OnShowTypeChanged(bool value) => AvailableView.Refresh();
    partial void OnShowReadOnlyChanged(bool value) => AvailableView.Refresh();
    partial void OnTabChanged(SourceTab value) => _ = LoadCategoriesAsync();
    partial void OnScopeChanged(ElementScope value) => _ = LoadCategoriesAsync();

    private bool FilterParam(ParamRow r)
    {
        if (Selected.Contains(r)) return false;
        if (!string.IsNullOrWhiteSpace(ParamSearch) && !r.Name.Contains(ParamSearch, StringComparison.CurrentCultureIgnoreCase)) return false;
        if (r.Info.IsReadOnly) return ShowReadOnly;
        return r.Info.Scope == ParamScope.Type ? ShowType : ShowInstance;
    }

    [RelayCommand]
    public async Task LoadCategoriesAsync()
    {
        if (Tab is not (SourceTab.Model or SourceTab.Annotation)) { Categories.Clear(); UpdateStatus(); return; } // TODO(M5/M6)
        await Busy(async () =>
        {
            var type = Tab == SourceTab.Model ? Autodesk.Revit.DB.CategoryType.Model : Autodesk.Revit.DB.CategoryType.Annotation;
            var scope = Scope;
            var items = await RevitTask.RunAsync(app => ElementSource.ListCategories(app, type, scope));
            Categories.Clear();
            foreach (var i in items)
            {
                var row = new CategoryRow { Item = i };
                row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CategoryRow.IsChecked)) _ = ScanParametersAsync(); };
                Categories.Add(row);
            }
        });
    }

    private async Task ScanParametersAsync()
    {
        var req = BuildRequest();
        await Busy(async () =>
        {
            var infos = await RevitTask.RunAsync(app =>
                ParameterScanner.Scan(app.ActiveUIDocument.Document, ElementSource.Collect(app, req)));
            _infoByKey = infos.ToDictionary(i => i.Key.ToString());
            var keep = Selected.Select(s => s.Info.Key.ToString()).ToHashSet();
            Available.Clear();
            var selectedNew = new List<ParamRow>();
            foreach (var i in infos)
            {
                var row = new ParamRow { Info = i };
                Available.Add(row);
                if (keep.Contains(i.Key.ToString())) selectedNew.Add(row);
            }
            Selected.Clear();
            foreach (var s in selectedNew) Selected.Add(s);
            AvailableView.Refresh();
        });
    }

    [RelayCommand] private void Add() { if (AvailableSelection is { } r) { Selected.Add(r); AvailableView.Refresh(); UpdateStatus(); } }
    [RelayCommand] private void Remove() { if (SelectedSelection is { } r) { Selected.Remove(r); AvailableView.Refresh(); UpdateStatus(); } }
    [RelayCommand] private void AddAll() { foreach (var r in AvailableView.Cast<ParamRow>().ToList()) Selected.Add(r); AvailableView.Refresh(); UpdateStatus(); }
    [RelayCommand] private void RemoveAll() { Selected.Clear(); AvailableView.Refresh(); UpdateStatus(); }
    [RelayCommand] private void MoveUp() => Move(-1);
    [RelayCommand] private void MoveDown() => Move(1);

    private void Move(int delta)
    {
        if (SelectedSelection is null) return;
        var i = Selected.IndexOf(SelectedSelection);
        var j = i + delta;
        if (j < 0 || j >= Selected.Count) return;
        Selected.Move(i, j);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var req = BuildRequest();
        if (req.CategoryIds.Count == 0) { MessageBox.Show("Please select at least one category.", "ParamBridge"); return; }

        var dlg = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = "ParamBridge export.xlsx", OverwritePrompt = false };
        if (dlg.ShowDialog() != true) return;

        await Busy(async () =>
        {
            var info = _infoByKey;
            var (sheets, opts) = await RevitTask.RunAsync(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var els = ElementSource.Collect(app, req);
                var s = ValueReader.BuildSheets(doc, els, req, info);
                var o = new WorkbookOptions
                {
                    DocumentTitle = doc.Title,
                    DocumentGuid = doc.ProjectInformation?.UniqueId ?? "",
                    RevitVersion = app.Application.VersionNumber,
                };
                return (s, o);
            });
            await Task.Run(() => WorkbookWriter.Write(dlg.FileName, sheets, opts));
            Status = $"Exported {sheets.Sum(s => s.Rows.Count)} elements to {Path.GetFileName(dlg.FileName)}";
            if (MessageBox.Show("Export finished. Open the file?", "ParamBridge", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        });
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var dlg = new OpenFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx" };
        if (dlg.ShowDialog() != true) return;

        await Busy(async () =>
        {
            var read = await Task.Run(() => WorkbookReader.Read(dlg.FileName));
            if (read.Problems.Count > 0)
                MessageBox.Show(string.Join(Environment.NewLine + Environment.NewLine, read.Problems), "ParamBridge – import", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (read.Sheets.Count == 0) return;

            var changes = await RevitTask.RunAsync(app => DiffEngine.Diff(app.ActiveUIDocument.Document, read.Sheets, _culture));
            if (changes.Count == 0) { MessageBox.Show("No changes found – the model is up to date.", "ParamBridge"); return; }

            var vm = new ChangesViewModel(changes);
            var win = new ChangesWindow { DataContext = vm, Owner = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault() };
            if (win.ShowDialog() != true) return;

            var result = await RevitTask.RunAsync(app => ChangeApplier.Apply(app.ActiveUIDocument.Document, changes, _culture));
            var msg = $"Updated: {result.Applied}   Failed: {result.Failed}";
            if (result.Messages.Count > 0) msg += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(30));
            MessageBox.Show(msg, "ParamBridge – import result");
        });
    }

    [RelayCommand]
    private void SaveProfile()
    {
        var name = CurrentProfile?.Name ?? "Default";
        var p = new Profile
        {
            Name = name, Tab = Tab, Scope = Scope, SortByType = SortByType, SingleSheet = SingleSheet,
            Sources = Categories.Where(c => c.IsChecked).Select(c => c.Item.BuiltInName).ToList(),
            ParameterKeys = Selected.Select(s => s.Info.Key.ToString()).ToList(),
        };
        ProfileStore.Save(p);
        var existing = Profiles.FirstOrDefault(x => x.Name == name);
        if (existing is not null) Profiles.Remove(existing);
        Profiles.Add(p);
        CurrentProfile = p;
        Status = $"Profile '{name}' saved.";
    }
    // TODO(M4): Save as new / rename / delete / import / export profile, apply profile on selection.

    private ExportRequest BuildRequest() => new()
    {
        Tab = Tab,
        Scope = Scope,
        CategoryIds = Categories.Where(c => c.IsChecked).Select(c => c.Item.Id).ToList(),
        Parameters = Selected.Select(s => s.Info.Key).ToList(),
        SortByType = SortByType,
        SingleSheet = SingleSheet,
    };

    private void UpdateStatus()
    {
        var cats = Categories.Where(c => c.IsChecked).ToList();
        Status = $"{cats.Count} categories · {cats.Sum(c => c.Count)} elements · {Available.Count} parameters found · {Selected.Count} selected";
    }

    private async Task Busy(Func<Task> work)
    {
        IsBusy = true;
        try { await work(); }
        catch (OperationCanceledException) { Status = "Cancelled."; }
        catch (Exception ex)
        {
            Log.Error("Operation failed", ex);
            MessageBox.Show(ex.Message, "ParamBridge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; UpdateStatus(); }
    }
}
