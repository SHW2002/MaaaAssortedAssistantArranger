using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Game_Daily_Routine_Launcher;

public class TaskStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TaskState state)
        {
            return state switch
            {
                TaskState.Idle => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                TaskState.Launching => new SolidColorBrush(Color.FromRgb(255, 183, 77)),
                TaskState.Running => new SolidColorBrush(Color.FromRgb(66, 165, 245)),
                TaskState.MonitoringWaitStart => new SolidColorBrush(Color.FromRgb(255, 167, 38)),
                TaskState.MonitoringWaitStop => new SolidColorBrush(Color.FromRgb(255, 167, 38)),
                TaskState.Completed => new SolidColorBrush(Color.FromRgb(102, 187, 106)),
                TaskState.TimedOut or TaskState.Error or TaskState.Terminated => new SolidColorBrush(Color.FromRgb(239, 83, 80)),
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
        }
        return new SolidColorBrush(Color.FromRgb(158, 158, 158));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
