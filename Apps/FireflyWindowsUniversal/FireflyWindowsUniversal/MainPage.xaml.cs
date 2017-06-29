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
        DelayedActions delayedActions = new DelayedActions();
        ObservableCollection<HubModel> hubList = new ObservableCollection<HubModel>();

        public MainPage()
        {
            locator.HubAdded += OnFoundHub;
            this.InitializeComponent();
            HubGrid.ItemsSource = hubList;
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

        }

        private void OnPause(object sender, RoutedEventArgs e)
        {

        }

        private void OnPlay(object sender, RoutedEventArgs e)
        {

        }

        private void OnSettings(object sender, RoutedEventArgs e)
        {

        }
    }
}
