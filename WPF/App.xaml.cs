using System;
using System.Windows;

namespace AliceBotSettings;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        int tabIndex = 0;
        if (e.Args.Length > 0 && int.TryParse(e.Args[0], out int ti))
            tabIndex = ti;
        var mainWindow = new MainWindow();
        mainWindow.NavigateToTab(tabIndex);
        mainWindow.Show();
    }
}
