using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
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

        public MainPage()
        {
            locator.HubAdded += OnFoundHub;
            this.InitializeComponent();
        }

        private async void OnFoundHub(object sender, FireflyHub e)
        {
            AddMessage("Found hub at " + e.RemoteAddress + ":" + e.RemotePort);

            try
            {
                await e.ConnectAsync();
            }
            catch (Exception ex)
            {
                AddMessage("Connect failed: " + ex.Message);
            }
        }

        void AddMessage(string msg)
        {
            var nowait = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                if (DebugOutput.Blocks.Count == 0)
                {
                    DebugOutput.Blocks.Add(new Paragraph());
                }
                Paragraph p = (Paragraph)DebugOutput.Blocks[0];
                p.Inlines.Add(new Run() { Text = msg });
                p.Inlines.Add(new LineBreak());
            }));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            locator.StartFindingHubs();
            base.OnNavigatedTo(e);
        }
    }
}
