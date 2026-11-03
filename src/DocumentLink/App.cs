using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using DocumentLink.Commands;
using DocumentLink.Infrastructure;

namespace DocumentLink;

/// <summary>Revit entry point: builds the ribbon and hosts the ExternalEvent used by RevitTask.</summary>
public class App : IExternalApplication
{
    public const string PanelName = "DocumentLink";
    private static PushButton? _button;

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            RevitTask.Initialize();
            CreateRibbon(application);
#if !REVIT2023
            application.ThemeChanged += OnThemeChanged;
#endif
            Log.Info("DocumentLink started");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed", ex);
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
#if !REVIT2023
        application.ThemeChanged -= OnThemeChanged;
#endif
        return Result.Succeeded;
    }

    private static void CreateRibbon(UIControlledApplication app)
    {
        // No tab name → the panel goes to Revit's built-in "Add-Ins" tab.
        var panel = app.CreateRibbonPanel(PanelName);

        var data = new PushButtonData(
            "DL_ExcelSync",
            "Excel\nSync",
            Assembly.GetExecutingAssembly().Location,
            typeof(OpenMainWindowCommand).FullName)
        {
            ToolTip = "Export model data to Excel, edit it, and update the model from the edited file.",
        };
        _button = (PushButton)panel.AddItem(data);
        ApplyIcons();
    }

#if !REVIT2023
    private static void OnThemeChanged(object? sender, ThemeChangedEventArgs e) => ApplyIcons();
#endif

    /// <summary>Light/dark icon pairs (theme switching from Revit 2024; 2023 always light) (icon_light_32.png / icon_dark_32.png …), following the Revit UI theme.</summary>
    private static void ApplyIcons()
    {
        if (_button is null) return;
        var theme = Compat.IsDarkTheme() ? "dark" : "light";
        _button.LargeImage = LoadIcon($"icon_{theme}_32.png");
        _button.Image = LoadIcon($"icon_{theme}_16.png");
    }

    private static BitmapImage? LoadIcon(string file)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/DocumentLink;component/Resources/Icons/{file}", UriKind.Absolute);
            return new BitmapImage(uri);
        }
        catch (Exception ex)
        {
            Log.Error($"Icon {file} not loaded", ex);
            return null;
        }
    }
}
