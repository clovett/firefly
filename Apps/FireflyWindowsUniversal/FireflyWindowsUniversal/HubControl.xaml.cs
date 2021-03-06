﻿using BleLights.SharedControls;
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

            double tubeSize = Settings.Instance.TubeSize;
            this.Width = (tubeSize * 5) + 20 + (2* tubeSize / 3);
            this.Height = (tubeSize * 2) + 24 + (2 * tubeSize / 10);
            TubeGrid.Margin = new Thickness(tubeSize / 3, tubeSize / 10, tubeSize / 3, tubeSize / 10);
        }


        private void OnTubeSelected(object sender, RoutedEventArgs e)
        {
            Button tubeButton = (Button)sender;

            HubModel hub = (HubModel)this.DataContext;
            TubeModel tube = (TubeModel)tubeButton.DataContext;
            if (hub != null && tube != null && hub.Hub != null)
            {
                int i = hub.Tubes.IndexOf(tube);
                if (i >= 0)
                {
                    hub.Hub.FireTube(i, Settings.Instance.BurnTime);
                }
            }
        }
    }
}
