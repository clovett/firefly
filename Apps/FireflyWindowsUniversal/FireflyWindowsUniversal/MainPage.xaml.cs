using BleLights.SharedControls;
using FireflyWindows.Utilities;
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
using Windows.UI.ViewManagement;
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
        DelayedActions delayedActions = new DelayedActions();
        HubManager hubs;

        public MainPage()
        {
            hubs = ((App)App.Current).Hubs;
            this.InitializeComponent();
            HubGrid.ItemsSource = hubs.Hubs;
            Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged += OnNetworkStatusChange;
            CheckNetworkStatus();
            SetArmIcon();
            this.SizeChanged += MainPage_SizeChanged;
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            pageSize = e.NewSize;
            ResizeImage(pageSize);

            if (pageSize.Width > 800)
            {
                if (!wideMode)
                {
                    wideMode = true;
                    PageCommandBar.SecondaryCommands.Remove(RefreshButton);
                    PageCommandBar.PrimaryCommands.Add(RefreshButton);
                    PageCommandBar.SecondaryCommands.Remove(LightsButton);
                    PageCommandBar.PrimaryCommands.Add(LightsButton);
                    PageCommandBar.SecondaryCommands.Remove(FullscreenButton);
                    PageCommandBar.PrimaryCommands.Add(FullscreenButton);
                }
            }

            else
            {
                if (wideMode)
                {
                    wideMode = false;
                    PageCommandBar.PrimaryCommands.Remove(RefreshButton);
                    PageCommandBar.SecondaryCommands.Add(RefreshButton);
                    PageCommandBar.PrimaryCommands.Remove(LightsButton);
                    PageCommandBar.SecondaryCommands.Add(LightsButton);
                    PageCommandBar.PrimaryCommands.Remove(FullscreenButton);
                    PageCommandBar.SecondaryCommands.Add(FullscreenButton);
                }
            }
        }

        bool wideMode;
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
            hubs.Start();
            hubs.Message += OnHubMessage;
            hubs.PlayComplete += OnPlayComplete;
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            hubs.Stop();
            hubs.Message -= OnHubMessage;
            hubs.PlayComplete -= OnPlayComplete;
            base.OnNavigatedFrom(e);
        }

        private void OnPlayComplete(object sender, EventArgs e)
        {
            PlayButton.Visibility = Visibility.Visible;
            PauseButton.Visibility = Visibility.Collapsed;
        }

        private void OnHubMessage(object sender, string e)
        {
            AddMessage(e);
        }

        private void OnStop(object sender, RoutedEventArgs e)
        {
        }

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            hubs.Refresh();
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {
            PlayButton.Visibility = Visibility.Visible;
            PauseButton.Visibility = Visibility.Collapsed;
            hubs.Pause();
        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            PlayButton.Visibility = Visibility.Collapsed;
            PauseButton.Visibility = Visibility.Visible;
            hubs.Play();
        }


        private void OnHelp(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(HelpPage));
        }
        private void OnSettings(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }

        private void OnArm(object sender, RoutedEventArgs e)
        {
            hubs.ToggleArm();
            SetArmIcon();
        }

        void SetArmIcon()
        {
            SymbolIcon icon = (SymbolIcon)ArmButton.Icon;
            icon.Symbol = hubs.IsArmed ? Symbol.Favorite : Symbol.OutlineStar;
        }

        private void OnToggleLights(object sender, RoutedEventArgs e)
        {
            Color c = favoriteColors[colorPosition++];
            if (colorPosition == favoriteColors.Length)
            {
                colorPosition = 0;
            }

            hubs.SetColor(c.A, c.R, c.G, c.B);
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
        bool fullScreen = false;

        private void OnFullscreen(object sender, RoutedEventArgs e)
        {
            AppBarButton button = (AppBarButton)sender;
            var view = ApplicationView.GetForCurrentView();
            if (fullScreen)
            {
                fullScreen = false;
                view.ExitFullScreenMode();
                button.Icon = new SymbolIcon(Symbol.FullScreen);
            }
            else
            {
                if (view.TryEnterFullScreenMode())
                {
                    fullScreen = true;
                    button.Icon = new SymbolIcon(Symbol.BackToWindow);
                }
            }
        }

    }
}
