using BleLights.SharedControls;
using FireflyWindows.Networking;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace FireflyWindows.ViewModels
{
    class HubModel : INotifyPropertyChanged
    {
        FireflyHub hub;
        bool connected;
        string name;
        int count;
        string error;
        ObservableCollection<TubeModel> tubes = new ObservableCollection<TubeModel>();

        public HubModel(FireflyHub e)
        {
            this.hub = e;
            this.Connected = this.hub.Connected;
            e.MessageReceived += OnMessageReceived;
            e.TcpError += OnTcpError;
            e.ConnectionChanged += OnConnectionChanged;
        }

        private void OnConnectionChanged(object sender, EventArgs e)
        {
            UiDispatcher.RunOnUIThread(() =>
            {
                this.Connected = this.hub.Connected;
            });
        }

        private void OnTcpError(object sender, Exception e)
        {
            UiDispatcher.RunOnUIThread(() =>
            {
                this.ErrorMessage = e.Message;
            });
        }

        public bool Connected
        {
            get { return this.connected; }
            set {
                if (this.connected != value)
                {
                    this.connected = value;
                    OnPropertyChanged("Connected");
                }
            }
        }


        public string Name
        {
            get { return this.name; }
            set
            {
                if (this.name != value)
                {
                    this.name = value;
                    OnPropertyChanged("Name");
                }
            }
        }


        public string ErrorMessage
        {
            get { return this.error; }
            set
            {
                if (this.error != value)
                {
                    this.error = value;
                    OnPropertyChanged("ErrorMessage");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

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
