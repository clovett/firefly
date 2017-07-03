using BleLights.SharedControls;
using FireflyWindows.ViewModels;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace FireflyWindows
{
    public sealed partial class HubControl : UserControl
    {
        public HubControl()
        {
            this.InitializeComponent();
        }

        private void OnTubeSelected(object sender, RoutedEventArgs e)
        {
            Button tubeButton = (Button)sender;

            HubModel hub = (HubModel)this.DataContext;
            TubeModel tube = (TubeModel)tubeButton.DataContext;
            if (hub != null && tube != null)
            {
                int i = hub.Tubes.IndexOf(tube);
                if (i >= 0)
                {
                    hub.Hub.FireTube(i);
                }
            }
        }
    }
}
