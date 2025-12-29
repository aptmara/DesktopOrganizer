using System.Globalization;
using System.Windows.Data;

namespace DesktopOrganizer.UI.Controls;

public class BooleanToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBroken && isBroken)
        {
            return 0.5; // リンク切れ（Broken）の場合は半透明
        }
        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
