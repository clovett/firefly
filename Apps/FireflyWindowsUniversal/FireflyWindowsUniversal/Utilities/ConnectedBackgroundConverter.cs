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
                        return GetSolidColorBrush(color);
                    }
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public SolidColorBrush GetSolidColorBrush(string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte a = 0xff;
            byte r = 0, g = 0, b = 0;
            if (hex.Length == 8)
            {
                a = (byte)(System.Convert.ToUInt32(hex.Substring(0, 2), 16));
                hex = hex.Substring(2);
            }
            if (hex.Length == 6)
            {
                r = (byte)(System.Convert.ToUInt32(hex.Substring(0, 2), 16));
                hex = hex.Substring(2);
            }
            if (hex.Length == 4)
            {
                g = (byte)(System.Convert.ToUInt32(hex.Substring(0, 2), 16));
                hex = hex.Substring(2);
            }
            if (hex.Length == 2)
            {
                b = (byte)(System.Convert.ToUInt32(hex.Substring(0, 2), 16));
                hex = hex.Substring(2);
            }
            var color = Windows.UI.Color.FromArgb(a, r, g, b);
            Debug.WriteLine("ConnectedBackgroundConverter GetSolidColorBrush" + color.ToString());
            return new SolidColorBrush(color);
        }
    }
}
