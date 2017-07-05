using BleLights.SharedControls;
using FireflyWindows.ViewModels;
using System;
using System.Diagnostics;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Input;

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

            Window.Current.CoreWindow.KeyDown += OnCoreWindowKeyDown;
        }
        
        private void OnCoreWindowKeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.F5)
            {
                hubs.Refresh();
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.T)
            {
                bool isShift = IsKeyDown(VirtualKey.LeftShift) || IsKeyDown(VirtualKey.RightShift);
                bool isCtrl = IsKeyDown(VirtualKey.LeftControl) || IsKeyDown(VirtualKey.RightControl);
                if (isShift && isCtrl)
                {
                    hubs.AddTestHubs();
                }
            }
        }

        bool IsKeyDown(VirtualKey key)
        {
            return (CoreWindow.GetForCurrentThread().GetKeyState(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            pageSize = e.NewSize;
            ResizeImage(pageSize);

            double pageWidth = pageSize.Width;

            if (pageWidth > 800)
            {
                if (!wideMode)
                {
                    wideMode = true;
                    PageCommandBar.SecondaryCommands.Remove(RefreshButton);
                    PageCommandBar.PrimaryCommands.Add(RefreshButton);
                    PageCommandBar.SecondaryCommands.Remove(HelpButton);
                    PageCommandBar.PrimaryCommands.Add(HelpButton);
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
                    PageCommandBar.PrimaryCommands.Remove(HelpButton);
                    PageCommandBar.SecondaryCommands.Add(HelpButton);
                    PageCommandBar.PrimaryCommands.Remove(FullscreenButton);
                    PageCommandBar.SecondaryCommands.Add(FullscreenButton);
                }
            }

            // todo: calcualte ideal tube size to get 100 tubes to fill the screen.
            // So N Hubs across the page results in page width, 5 tubes across per hub:
            //      5 * N = pageWidth
            //      TubeSize = pageWidth / 5 * N
            // Then whatever N becomes, tells us how high this will be:
            //      Rows = 100 / N * 10
            //      Rows = Math.Ceil(10 / N)
            //      Height = Rows * 2 * TubeSize + yMargin  // Hubs are 2 tubes tall * a margin 
            // Now optimize, if we have height remaining increase tube size...

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
            hubs.Program.PlayComplete += OnPlayComplete;
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            hubs.Message -= OnHubMessage;
            hubs.Program.PlayComplete -= OnPlayComplete;
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
            AddMessage("");
            hubs.Refresh();
        }

        private void OnPause(object sender, RoutedEventArgs e)
        {
            PlayButton.Visibility = Visibility.Visible;
            PauseButton.Visibility = Visibility.Collapsed;
            hubs.Program.Pause();
        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {
            PlayButton.Visibility = Visibility.Collapsed;
            PauseButton.Visibility = Visibility.Visible;
            hubs.Program.Play();
        }

        private void OnHelp(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(HelpPage));
        }

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }

        private void OnGraph(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(GraphPage));
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

        private void OnLoadTest(object sender, RoutedEventArgs e)
        {
            hubs.AddTestHubs();
        }
    }
}
