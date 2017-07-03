using BleLights.SharedControls;
using FireflyWindows.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace FireflyWindows.ViewModels
{
    public class HubManager
    {
        FireflyHubLocator locator = new FireflyHubLocator();
        ObservableCollection<HubModel> hubList = new ObservableCollection<HubModel>();
        DelayedActions delayedActions = new DelayedActions();
        bool armed = false;
        bool lightsOn = false;

        public event EventHandler<string> Message;
        public event EventHandler PlayComplete;

        public ObservableCollection<HubModel> Hubs
        {
            get { return hubList; }
        }

        public bool IsArmed { get { return armed; } }
        public bool IsLightsOn { get { return lightsOn; } }

        public void Start()
        {
            playPos = 0;
            hubList.Clear();
            locator.Reset();
            lightsOn = false;
            armed = false;

            locator.HubAdded += OnFoundHub;
            locator.HubConnected += OnHubConnected;
            locator.HubDisconnected += OnHubDisconnected;

            locator.StartFindingHubs();
        }

        public void Stop()
        {
            locator.HubAdded -= OnFoundHub;
            locator.HubConnected -= OnHubConnected;
            locator.HubDisconnected -= OnHubDisconnected;
            locator.StopFindingHubs();
        }

        private async void OnFoundHub(object sender, FireflyHub e)
        {
            OnMessage("Found hub at " + e.RemoteAddress + ":" + e.RemotePort);
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
                OnMessage("Connect failed: " + ex.Message);
            }
        }

        void OnMessage(string text)
        {
            if (Message != null)
            {
                Message(this, text);
            }
        }
        public void SetColor(byte a, byte r, byte g, byte b)
        {
            lightsOn = (r > 0 || g > 0 || b > 0);
            System.Threading.Tasks.Parallel.ForEach(this.hubList,
                (hub) =>
                {
                    hub.Hub.SetColor(a, r, g, b);
                });
        }

        public void ToggleArm()
        {
            Color c = ColorNames.ParseColor(Settings.Instance.ArmColor);
            armed = !armed;
            System.Threading.Tasks.Parallel.ForEach(this.hubList,
                (hub) =>
                {
                    hub.Hub.Arm(armed);
                    if (lightsOn)
                    {
                        if (armed)
                        {
                            hub.Hub.SetColor(c.A, c.R, c.G, c.B);
                        }
                        else
                        {
                            hub.Hub.SetColor(0, 0, 0, 0);
                        }
                    }

                });

        }

        private async void OnHubConnected(object sender, FireflyHub e)
        {
            // hearing from this hub again, let's make sure it is connected.
            if (!e.Connected)
            {
                try
                {
                    await e.ConnectAsync();
                    OnMessage("Hub reconnected: " + e.RemoteAddress);
                }
                catch (Exception ex)
                {
                    OnMessage("Connect failed: " + ex.Message);
                }
            }
        }
        private async void OnHubDisconnected(object sender, FireflyHub e)
        {
            try
            {
                await e.Reconnect();
                OnMessage("Hub reconnected: " + e.RemoteAddress);
            }
            catch (Exception ex)
            {
                OnMessage("Reconnect failed: " + ex.Message);
            }
        }

        internal void Refresh()
        {
            playPos = 0;
            if (lightsOn)
            {
                SetColor(0, 0, 0, 0);
            }
            hubList.Clear();
            locator.Reset();
            lightsOn = false;
            armed = false;
        }

        int playPos = 0;
        bool paused = false;

        internal void Pause()
        {
            paused = true;
        }

        internal void Play()
        {
            if (playPos == -1)
            {
                playPos = 0;
            }
            delayedActions.StartDelayedAction("PlayNext", () => { PlayNext(); }, TimeSpan.FromSeconds(0));
        }

        private void PlayNext()
        {
            bool done = false;
            foreach (var hub in this.hubList)
            {
                FireflyHub fh = hub.Hub;
                if (fh.Tubes == playPos)
                {
                    playPos = 0;
                    // done!
                    done = true;
                }
                else
                {
                    hub.Hub.FireTube(playPos);
                }
            }
            if (done)
            {
                if (PlayComplete != null)
                {
                    PlayComplete(this, EventArgs.Empty);
                }
            }
            else
            {
                playPos++;
                if (!paused)
                {
                    int speed = Settings.Instance.PlaySpeed;
                    delayedActions.StartDelayedAction("PlayNext", () => { PlayNext(); }, TimeSpan.FromSeconds(speed));
                }
            }
        }
    }
}
