using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Microsoft.Toolkit.Uwp.Notifications.DemoChatApp.Converters
{
    public class HasUnreadToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool && (bool)value)
            {
                Color accentColor = (Color)Application.Current.Resources["SystemAccentColor"];
                return new SolidColorBrush(accentColor);
            }

            SolidColorBrush brush = (SolidColorBrush)Application.Current.Resources["ApplicationForegroundThemeBrush"];
            return new SolidColorBrush(brush.Color)
            {
                Opacity = 0.6
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
