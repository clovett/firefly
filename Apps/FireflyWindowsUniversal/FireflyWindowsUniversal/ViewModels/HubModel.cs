using BleLights.SharedControls;
using FireflyWindows.Networking;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace FireflyWindows.ViewModels
{
    class HubModel : DependencyObject
    {
        FireflyHub hub;
        ObservableCollection<TubeModel> tubes = new ObservableCollection<TubeModel>();

        public HubModel(FireflyHub e)
        {
            this.hub = e;
            e.MessageReceived += OnMessageReceived;
            e.TcpError += OnTcpError;
        }

        private void OnTcpError(object sender, Exception e)
        {
            UiDispatcher.RunOnUIThread(() =>
            {
                ErrorMessage = e.Message;
            });
        }


        public string Name
        {
            get { return (string)GetValue(NameProperty); }
            set { SetValue(NameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Name.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register("Name", typeof(string), typeof(HubModel), new PropertyMetadata(null));



        public string ErrorMessage
        {
            get { return (string)GetValue(ErrorMessageProperty); }
            set { SetValue(ErrorMessageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ErrorMessage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ErrorMessageProperty =
            DependencyProperty.Register("ErrorMessage", typeof(string), typeof(HubModel), new PropertyMetadata(null));



        public ObservableCollection<TubeModel> Tubes
        {
            get { return tubes; }
        }

        public FireflyHub Hub {  get { return this.hub; } }

        private void OnMessageReceived(object sender, FireflyMessage e)
        {
            if (this.hub == (FireflyHub)sender)
            {
                OnUpdate();
            }
        }

        public void OnUpdate()
        {
            UiDispatcher.RunOnUIThread(() =>
            {
                UpdateProperties();
            });
        }

        private void UpdateProperties()
        {
            while (tubes.Count < hub.Tubes)
            {
                tubes.Add(new ViewModels.TubeModel(this));
            }
            while (tubes.Count > hub.Tubes)
            {
                tubes.Remove(tubes.Last());
            }

            this.Name = hub.RemoteAddress;
            this.ErrorMessage = "";
        }
    }

    class TubeModel
    {
        HubModel owner;

        public TubeModel(HubModel owner)
        {
            this.owner = owner;
        }

        public HubModel Owner {  get { return this.owner; } }

    }
}
