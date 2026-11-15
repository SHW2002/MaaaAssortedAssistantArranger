using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Game_Daily_Routine_Launcher;

public static class ThemeService
{
    private static bool _initialized;
    private static ThemeMode _mode;
    public static bool IsDark { get; private set; }

    public static bool ResolveDark(ThemeMode mode, bool systemDark) => mode switch
    {
        ThemeMode.Light => false,
        ThemeMode.Dark => true,
        _ => systemDark
    };

    public static void Initialize(ThemeMode mode)
    {
        if (!_initialized)
        {
            SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
            _initialized = true;
        }
        Apply(mode);
    }

    public static void Apply(ThemeMode mode)
    {
        _mode = Enum.IsDefined(mode) ? mode : ThemeMode.System;
        var application = Application.Current;
        if (application is null) return;
        IsDark = ResolveDark(_mode, ReadSystemDark());
        application.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"/MaaaAssortedAssistantArranger;component/src/Themes/Palettes/{(IsDark ? "Dark" : "Light")}.xaml", UriKind.Relative)
        };
        foreach (Window window in application.Windows) UpdateTitleBar(window);
    }

    public static void ObserveWindow(Window window) =>
        window.SourceInitialized += (_, _) => UpdateTitleBar(window);

    public static void Dispose()
    {
        if (_initialized) SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
        _initialized = false;
    }

    private static bool ReadSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }

    private static void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        var application = Application.Current;
        if (application is not null && !application.Dispatcher.HasShutdownStarted)
            _ = application.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_mode == ThemeMode.System) Apply(_mode);
            }));
    }

    private static void UpdateTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        var dark = IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
