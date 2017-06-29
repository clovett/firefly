using BleLights.SharedControls;
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
        }

        public ObservableCollection<TubeModel> Tubes
        {
            get { return tubes; }
        }

        public FireflyHub Hub {  get { return this.hub; } }

        private void OnMessageReceived(object sender, FireMessage e)
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
