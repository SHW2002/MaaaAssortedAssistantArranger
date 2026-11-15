using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Game_Daily_Routine_Launcher.Tests;

internal static class AppearanceRender
{
    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Game-Daily-Routine-Launcher.csproj")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate project sources.");
        }
    }

    public static Window LoadMainWindowWithoutServices()
    {
        // Parse the real view without its production view model or event handlers.
        // No window is shown, no HWND is created, and no scheduler or system hook runs.
        var text = File.ReadAllText(Path.Combine(RepositoryRoot, "src/MainWindow.xaml"));
        text = Regex.Replace(text, "x:Class=\"[^\"]+\"", "");
        text = Regex.Replace(text, @"<Window.DataContext>.*?</Window.DataContext>", "", RegexOptions.Singleline);
        text = Regex.Replace(text, "\\s(?:Click|MouseDoubleClick)=\"[^\"]+\"", "");
        text = text.Replace("clr-namespace:Game_Daily_Routine_Launcher\"", "clr-namespace:Game_Daily_Routine_Launcher;assembly=MaaaAssortedAssistantArranger\"")
            .Replace("clr-namespace:Game_Daily_Routine_Launcher.Views\"", "clr-namespace:Game_Daily_Routine_Launcher.Views;assembly=MaaaAssortedAssistantArranger\"");
        var window = (Window)XamlReader.Parse(text);
        window.DataContext = new AppearancePreviewData();
        return window;
    }

    public static void Save(FrameworkElement element, string name, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        var image = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        var composition = new DrawingVisual();
        using (var drawing = composition.RenderOpen())
        {
            drawing.DrawRectangle((Brush)Application.Current.FindResource("WindowBackground"), null, new Rect(0, 0, width, height));
        }
        image.Render(composition);
        image.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        var output = Path.Combine(RepositoryRoot, "artifacts/appearance-verification");
        Directory.CreateDirectory(output);
        using var file = File.Create(Path.Combine(output, name + ".png"));
        encoder.Save(file);
    }

    public static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }

    public static Color ColorOf(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;
}
