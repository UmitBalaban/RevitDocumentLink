using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParamBridge.Core.Models;
using ParamBridge.UI.ViewModels;

namespace ParamBridge.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.LoadCategoriesAsync();
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource != Tabs) return;
        _vm.Tab = (SourceTab)Tabs.SelectedIndex;
    }

    private void Scope_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _vm.Scope = sender == ScopeView ? ElementScope.ActiveView
                  : sender == ScopeSel ? ElementScope.Selection
                  : ElementScope.WholeModel;
    }

    private void Available_DoubleClick(object sender, MouseButtonEventArgs e) => _vm.AddCommand.Execute(null);
    private void Selected_DoubleClick(object sender, MouseButtonEventArgs e) => _vm.RemoveCommand.Execute(null);
}
