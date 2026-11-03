using System.Windows;
using DocumentLink.Core.Models;
using DocumentLink.UI.ViewModels;

namespace DocumentLink.UI;

public partial class ChangesWindow : Window
{
    public ChangesWindow() => InitializeComponent();

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangesViewModel vm && !vm.Changes.Any(c => c.IsSelected && c.Status != ChangeStatus.Error))
        {
            MessageBox.Show(this, "No applicable changes are selected.", "DocumentLink");
            return;
        }
        DialogResult = true;
    }
}
