using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.System;
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
    public sealed partial class HelpPage : Page
    {
        public HelpPage()
        {
            this.InitializeComponent();

            var run = VersionTextRun;
            var id = Package.Current.Id;
            var pv = id.Version;
            string s = string.Format("{0}.{1}.{2}.{3}", pv.Major, pv.Minor, pv.Build, pv.Revision);
            run.Text = string.Format(run.Text, s);

            this.SizeChanged += OnSizeChanged;
            
            this.AddHandler(PointerPressedEvent, new PointerEventHandler(PreviewPointerPressed), true);
            this.AddHandler(PointerMovedEvent, new PointerEventHandler(PreviewPointerMoved), true);
            this.AddHandler(PointerReleasedEvent, new PointerEventHandler(PreviewPointerReleased), true);

        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width < 600)
            {
                // single vertical column of help text
                Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                AboutTextBlock.OverflowContentTarget = null;
                AboutTextBlock.Margin = new Thickness(0);
                AboutContent.ColumnDefinitions.Clear();
                AboutTextBlock.MaxWidth = this.ActualWidth * 0.85;
            }
            else
            {
                // multi-column horizontal help text
                Scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                AboutTextBlock.OverflowContentTarget = firstOverflowContainer;
                AboutTextBlock.Margin = new Thickness(20, 0, 20, 0);
                AboutTextBlock.ClearValue(FrameworkElement.MaxWidthProperty);
                AboutContent.ColumnDefinitions.Clear();
                AboutContent.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(500) });
                AboutContent.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(500) });
                AboutContent.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(500) });
            }
        }

        Point downPos;
        bool mouseDown;
        bool dragging;

        void PreviewPointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            downPos = e.GetCurrentPoint(this).Position;
            mouseDown = true;
            dragging = false;
        }

        void PreviewPointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (mouseDown)
            {
                Point pos = e.GetCurrentPoint(this).Position;
                double dx = downPos.X - pos.X;
                double dy = downPos.Y - pos.Y;
                if (Math.Abs(dx) > 5 || Math.Abs(dy) > 5)
                {
                    dragging = true;
                }
            }
            base.OnPointerMoved(e);
        }

        void PreviewPointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Point pos = e.GetCurrentPoint(this).Position;
            base.OnPointerReleased(e);
            if (mouseDown && !dragging)
            {
                bool isHardwareButtonsAPIPresent = ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");
                if (isHardwareButtonsAPIPresent)
                {
                    // for some reason the Windows Phone doesn't pass the Click event through to the HyperlinkButton
                    foreach (var element in Windows.UI.Xaml.Media.VisualTreeHelper.FindElementsInHostCoordinates(pos, AboutTextBlock))
                    {
                        HyperlinkButton link = element as HyperlinkButton;
                        if (link != null)
                        {
                            HandleHyperlinkClick(link);
                            break;
                        }
                    }
                }
            }
            mouseDown = false;
            dragging = false;
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }

        private void OnNavigateUrl(object sender, RoutedEventArgs e)
        {
            HyperlinkButton button = (HyperlinkButton)sender;
            HandleHyperlinkClick(button);
        }


        private async void HandleHyperlinkClick(HyperlinkButton button)
        {
            Uri uri = button.NavigateUri;
            switch (button.Name)
            {
                case "WifiSettings":
                    // see http://pureinfotech.com/open-specific-settings-windows-10-mssettings-uri/
                    uri = new Uri("ms-settings:network-wifi", UriKind.RelativeOrAbsolute);
                    break;
                case "GithubLink":
                    uri = new Uri("https://github.com/clovett/firefly");
                    break;

                case "DemoVideoLink":
                    uri = new Uri("https://youtu.be/7F676ONwn2Y");
                    break;
            }
            await Launcher.LaunchUriAsync(uri);
        }


    }
}
