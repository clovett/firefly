using BleLights.SharedControls;
using FireflyWindows.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace FireflyWindows
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            this.DataContext = Settings.Instance;

            List<ColorInfo> colors = new List<FireflyWindows.ColorInfo>();
            ColorInfo selected = null;
            foreach (var name in ColorNames.GetColorMap().Keys)
            {
                var e = new FireflyWindows.ColorInfo()
                {
                    Name = name,
                    Color = ColorNames.ParseColor(name)
                };
                e.Brush = new SolidColorBrush(e.Color);
                if (Settings.Instance.ArmColor == e.Color.ToString())
                {
                    selected = e;
                }
                colors.Add(e);
            }

            ArmNamedColor.ItemsSource = colors;
            ArmNamedColor.SelectedItem = selected;
        }

        private void OnNamedColorSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                ColorInfo c = (ColorInfo)e.AddedItems[0];
                Settings.Instance.ArmColor = c.Color.ToString();
            }
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }
    }

    class ColorInfo
    {
        public string Name { get; set; }
        public Color Color { get; set; }
        public SolidColorBrush Brush { get; set; }
    }
}
