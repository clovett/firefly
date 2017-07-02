using BleLights.SharedControls;
using FireflyWindows.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace FireflyWindows
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        FireflyHubLocator locator = new FireflyHubLocator();
        DelayedActions delayedActions = new DelayedActions();
        ObservableCollection<HubModel> hubList = new ObservableCollection<HubModel>();

        public MainPage()
        {
            locator.HubAdded += OnFoundHub;
            locator.HubConnected += OnHubConnected;
            locator.HubDisconnected += OnHubDisconnected;
            this.InitializeComponent();
            HubGrid.ItemsSource = hubList;
            Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged += OnNetworkStatusChange;
            CheckNetworkStatus();
            SetArmIcon();
            this.SizeChanged += MainPage_SizeChanged;
        }
        
        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            pageSize = e.NewSize;
            ResizeImage(pageSize);
        }

        Size pageSize;

        void ResizeImage(Size pageSize)
        {
            BitmapSource src = BackgroundImage.Source as BitmapSource;
            if (src != null && src.PixelWidth != 0 && pageSize.Width > 0)
            {
                BackgroundImage.Width = src.PixelWidth;
                BackgroundImage.Height = src.PixelHeight;

                double wScale = pageSize.Width / BackgroundImage.Width;
                double hScale = pageSize.Height / BackgroundImage.Height;
                double max = Math.Max(wScale, hScale);

                // keep it centered.
                double offsetX = (((BackgroundImage.Width * wScale) - BackgroundImage.Width) / 2) * max;
                double offsetY = (((BackgroundImage.Height * hScale) - BackgroundImage.Height) / 2) * max;
                MatrixTransform mat = new MatrixTransform() { Matrix = new Windows.UI.Xaml.Media.Matrix(max, 0, 0, max, offsetX, offsetY) };
                BackgroundImage.RenderTransform = mat;
            }
            else
            {
                // wait for image to be loaded
                delayedActions.StartDelayedAction("ResizeImage", () =>
                {
                    ResizeImage(this.pageSize);
                }, TimeSpan.FromMilliseconds(100));
            }
        }


        private void OnNetworkStatusChange(object sender)
        {
            CheckNetworkStatus();
        }

        void CheckNetworkStatus()
        {

        }

        private async void OnFoundHub(object sender, FireflyHub e)
        {
            AddMessage("Found hub at " + e.RemoteAddress + ":" + e.RemotePort);
            UiDispatcher.RunOnUIThread(() =>
            {
                hubList.Add(new HubModel(e));
            });

            try
            {
                await e.ConnectAsync();
            }
            catch (Exception ex)
            {
                AddMessage("Connect failed: " + ex.Message);
            }
        }

        private async void OnHubConnected(object sender, FireflyHub e)
        {
            // hearing from this hub again, let's make sure it is connected.
            if (!e.Connected)
            {
                try
                {
                    await e.ConnectAsync();
                    AddMessage("Hub reconnected: " + e.RemoteAddress);
                }
                catch (Exception ex)
                {
                    AddMessage("Connect failed: " + ex.Message);
                }
            }
        }

        private async void OnHubDisconnected(object sender, FireflyHub e)
        {
            try
            {
                await e.Reconnect();
                AddMessage("Hub reconnected: " + e.RemoteAddress);
            }
            catch (Exception ex)
            {
                AddMessage("Reconnect failed: " + ex.Message);
            }
        }


        void AddMessage(string msg)
        {
            Debug.WriteLine(msg);
            UiDispatcher.RunOnUIThread(() =>
            {
                Messages.Text = msg;
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            locator.StartFindingHubs();
            base.OnNavigatedTo(e);
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
        }

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            if (lightsOn)
            {
                SetColor(0, 0, 0, 0);
            }
            hubList.Clear();
            locator.Reset();
            lightsOn = false;
            armed = false;
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {

        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {

        }

        private void OnHelp(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(HelpPage));
        }

        bool armed = false;

        private void OnArm(object sender, RoutedEventArgs e)
        {
            armed = !armed;
            foreach (var hub in this.hubList)
            {
                hub.Hub.Arm(armed);
            }
            SetArmIcon();
        }

        void SetArmIcon()
        {
            SymbolIcon icon = (SymbolIcon)ArmButton.Icon;
            icon.Symbol = armed ? Symbol.Favorite : Symbol.OutlineStar;
        }

        private void OnToggleLights(object sender, RoutedEventArgs e)
        {
            Color c = favoriteColors[colorPosition++];
            if (colorPosition == favoriteColors.Length)
            {
                colorPosition = 0;
            }

            SetColor(c.A, c.R, c.G, c.B);
        }

        private void SetColor(byte a, byte r, byte g, byte b)
        {
            lightsOn = (r > 0 || g > 0 || b > 0);
            foreach (var hub in this.hubList)
            {
                hub.Hub.SetColor(a, r, g, b);
            }
        }

        Color[] favoriteColors = new Color[]
        {
            Colors.Red,
            Colors.Green,
            Colors.Blue,
            Color.FromArgb(0xff, 0x50,0,0x50),
            Color.FromArgb(0xff, 0x50,0x50,0),
            Color.FromArgb(0xff, 0x0,0x50,0x50),
            Color.FromArgb(0,0,0,0), // off
        };

        int colorPosition = 0;
        bool lightsOn = false;

    }
}
