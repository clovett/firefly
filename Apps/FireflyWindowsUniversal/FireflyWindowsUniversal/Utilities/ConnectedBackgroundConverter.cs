using FireflyWindows.Utilities;
using FireflyWindows.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace FireflyWindows
{
    class ConnectedBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Debug.WriteLine("ConnectedBackgroundConverter " + value);
            if (value is bool)
            {
                if (!(bool)value)
                {
                    string color = parameter as string;
                    if (color != null)
                    {
                        return new SolidColorBrush(ColorNames.ParseColor(color));
                    }
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

    }
}
