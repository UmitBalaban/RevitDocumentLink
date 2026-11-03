using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ParamBridge.Infrastructure;
using ParamBridge.UI;

namespace ParamBridge.Commands;

[Transaction(TransactionMode.Manual)]
public class OpenMainWindowCommand : IExternalCommand
{
    private static MainWindow? _window;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (commandData.Application.ActiveUIDocument?.Document is null)
            {
                TaskDialog.Show("ParamBridge", "Open a project first.");
                return Result.Cancelled;
            }

            if (_window is { IsLoaded: true })
            {
                _window.Activate();
                return Result.Succeeded;
            }

            _window = new MainWindow();
            new WindowInteropHelper(_window) { Owner = commandData.Application.MainWindowHandle };
            _window.Closed += (_, _) => _window = null;
            _window.Show();
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Log.Error("Open window failed", ex);
            message = ex.Message;
            return Result.Failed;
        }
    }
}
