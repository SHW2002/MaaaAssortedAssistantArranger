using CommunityToolkit.Mvvm.ComponentModel;

namespace Game_Daily_Routine_Launcher;

public partial class MainViewModel
{
    [ObservableProperty] private ThemeMode _theme;

    partial void OnThemeChanged(ThemeMode value) => ThemeService.Apply(value);
}
