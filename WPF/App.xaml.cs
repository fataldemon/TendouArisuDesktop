using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace AliceBotSettings;

public partial class App : Application
{
    private const int HWND_BROADCAST = 0xFFFF;
    private const int WM_COPYDATA = 0x004A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutW(IntPtr hWnd, uint msg, IntPtr wParam, ref COPYDATASTRUCT lParam, uint flags, uint timeout, out IntPtr result);

    [StructLayout(LayoutKind.Sequential)]
    private struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    private static readonly Mutex _mutex = new(true, "AliceBotSettings_Mutex");

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!_mutex.WaitOne(0))
        {
            var hwnd = FindWindowW(null, "AliceBot 设置");
            if (hwnd != IntPtr.Zero)
            {
                int tabIndex = 0;
                if (e.Args.Length > 0 && int.TryParse(e.Args[0], out int ti))
                    tabIndex = ti;
                var bytes = Encoding.Unicode.GetBytes(tabIndex.ToString());
                var data = new COPYDATASTRUCT
                {
                    dwData = new IntPtr(1),
                    cbData = bytes.Length,
                    lpData = Marshal.StringToHGlobalUni(tabIndex.ToString())
                };
                SendMessageTimeoutW(hwnd, WM_COPYDATA, IntPtr.Zero, ref data, SMTO_ABORTIFHUNG, 1000, out _);
                Marshal.FreeHGlobal(data.lpData);
            }
            Shutdown();
            return;
        }

        base.OnStartup(e);
        int tab = 0;
        if (e.Args.Length > 0 && int.TryParse(e.Args[0], out int t))
            tab = t;
        var mainWindow = new MainWindow();
        mainWindow.NavigateToTab(tab);
        mainWindow.Show();
    }
}
