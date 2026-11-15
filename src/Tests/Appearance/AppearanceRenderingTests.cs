using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Game_Daily_Routine_Launcher.Tests;

public class AppearanceRenderingTests
{
    [Fact]
    public void RealViewsRenderOffscreenAndUpdateAllControlsWhenThemeChanges()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { VerifyViews(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), "Offscreen render timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void VerifyViews()
    {
        var app = new App();
        app.InitializeComponent();
        var main = AppearanceRender.LoadMainWindowWithoutServices();
        var root = (FrameworkElement)main.Content;
        Assert.Equal(ApplicationInfo.WindowTitle, main.Title);
        root.Measure(new Size(840, 1400));
        root.Arrange(new Rect(0, 0, 840, 1400));
        root.UpdateLayout();
        try
        {
            foreach (var mode in new[] { ThemeMode.Dark, ThemeMode.Light, ThemeMode.Dark })
            {
                ThemeService.Apply(mode);
                var selector = Assert.Single(AppearanceRender.Children<ComboBox>(root), combo => ReferenceEquals(combo.ItemsSource, ThemeOption.All));
                selector.SetCurrentValue(Selector.SelectedValueProperty, mode);
                AppearanceRender.Save(root, $"main-{mode.ToString().ToLowerInvariant()}", 840, 1400);
                var background = (Brush)app.FindResource("InputBackground");
                var foreground = (Brush)app.FindResource("TextPrimary");
                Assert.NotEmpty(AppearanceRender.Children<TextBox>(root));
                foreach (var input in AppearanceRender.Children<TextBox>(root))
                {
                    Assert.Equal(AppearanceRender.ColorOf(background), AppearanceRender.ColorOf(input.Background));
                    Assert.Equal(AppearanceRender.ColorOf(foreground), AppearanceRender.ColorOf(input.Foreground));
                }
                Assert.Equal(AppearanceRender.ColorOf(background), AppearanceRender.ColorOf(selector.Background));
                Assert.Equal(3, selector.Items.Count);
                var primaryButton = Assert.Single(AppearanceRender.Children<Button>(root), button => ReferenceEquals(button.Style, app.FindResource("ActionButton")));
                var buttonText = Assert.Single(AppearanceRender.Children<TextBlock>(primaryButton));
                Assert.Equal(AppearanceRender.ColorOf(primaryButton.Foreground), AppearanceRender.ColorOf(buttonText.Foreground));
                selector.ApplyTemplate();
                var popup = Assert.IsType<Popup>(selector.Template.FindName("PART_Popup", selector));
                var border = Assert.IsType<Border>(popup.Child);
                Assert.Equal(AppearanceRender.ColorOf((Brush)app.FindResource("SurfaceBackground")), AppearanceRender.ColorOf(border.Background));
                var help = new WebhookRelayHelpWindow();
                try
                {
                    AppearanceRender.Save((FrameworkElement)help.Content, $"help-{mode.ToString().ToLowerInvariant()}", 680, 285);
                    foreach (var input in AppearanceRender.Children<TextBox>(help))
                        Assert.Equal(AppearanceRender.ColorOf((Brush)app.FindResource("InsetBackground")), AppearanceRender.ColorOf(input.Background));
                }
                finally { help.Close(); }
            }
        }
        finally
        {
            main.Close();
            ThemeService.Dispose();
            app.Shutdown();
        }
    }
}
