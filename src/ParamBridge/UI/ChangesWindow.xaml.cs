using System.Windows;
using ParamBridge.Core.Models;
using ParamBridge.UI.ViewModels;

namespace ParamBridge.UI;

public partial class ChangesWindow : Window
{
    public ChangesWindow() => InitializeComponent();

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangesViewModel vm && !vm.Changes.Any(c => c.IsSelected && c.Status != ChangeStatus.Error))
        {
            MessageBox.Show(this, "No applicable changes are selected.", "ParamBridge");
            return;
        }
        DialogResult = true;
    }
}
