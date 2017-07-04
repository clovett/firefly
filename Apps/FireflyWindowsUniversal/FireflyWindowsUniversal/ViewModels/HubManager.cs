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
        bool armed;
        bool lightsOn;
        bool running;

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
            if (!running)
            {
                running = true;
                playPos = -1;
                hubList.Clear();
                locator.Reset();
                lightsOn = false;
                armed = false;

                locator.HubAdded += OnFoundHub;
                locator.HubConnected += OnHubConnected;
                locator.HubDisconnected += OnHubDisconnected;

                locator.StartFindingHubs();
            }
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

            try
            {
                await e.ConnectAsync();
            }
            catch (Exception ex)
            {
                OnMessage("Connect failed: " + ex.Message);
            }

            UiDispatcher.RunOnUIThread(() =>
            {
                hubList.Add(new HubModel(e));
            });
        }

        internal void AddTestHubs()
        {
            while (hubList.Count < 10)
            {
                HubModel test = new HubModel(null);
                test.AddTestTubes();
                hubList.Add(test);
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
            System.Threading.Tasks.Parallel.ForEach(this.hubList.ToArray(),
                (hub) =>
                {
                    hub.Hub.SetColor(a, r, g, b);
                });
        }

        public void ToggleArm()
        {
            Color c = ColorNames.ParseColor(Settings.Instance.ArmColor);
            armed = !armed;
            System.Threading.Tasks.Parallel.ForEach(this.hubList.ToArray(),
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

        internal void UpdateTubeSize()
        {
            foreach (var hub in this.hubList.ToArray())
            {
                hub.UpdateTubeSize();
            }
        }

        internal void Refresh()
        {
            playPos = -1;
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
        bool paused;

        internal void Pause()
        {
            paused = true;
        }

        internal void Play()
        {
            delayedActions.StartDelayedAction("PlayNext", () => { PlayNext(); }, TimeSpan.FromSeconds(0));
        }

        List<int> leftBank = null;
        List<int> rightBank = null;


        private void PlayNext()
        {
            // batch size tells us how many tubes we want to fire from each hub at a time.
            // If it is greater than 1 then we also want to balance the number across the
            // left and right sides of the hub to help split the power draw, plus it makes
            // the explosions more balanced (assuming hub configuration 5 x 2).
            int batchSize = Settings.Instance.BatchSize;

            int maxTubes = (from h in this.hubList select h.Hub.Tubes).Max();

            if (playPos == -1)
            {
                if (batchSize > 1)
                {
                    int half = (maxTubes / 2);
                    leftBank = new List<int>(Enumerable.Range(0, half));
                    rightBank = new List<int>(Enumerable.Range(half, maxTubes - half));
                }
                else
                {
                    leftBank = new List<int>(Enumerable.Range(0, maxTubes));
                }
                playPos = 0;
            }

            // now select next batchSize tubes from eacn bank
            int leftCount = (int)Math.Ceiling((double)batchSize / 2.0);
            int rightCount = batchSize - leftCount;
            if (playPos % 2 > 0 && batchSize > 1)
            {
                // switch them
                int temp = leftCount;
                leftCount = rightCount;
                rightCount = temp;
            }

            int mask = 0;
            for (int i = 0; i < leftCount; i++)
            {
                if (leftBank.Count > 0)
                {
                    int tube = leftBank[0];
                    leftBank.RemoveAt(0);
                    mask |= (1 << tube);
                }
            }
            for (int i = 0; i < rightCount; i++)
            {
                if (rightBank.Count > 0)
                {
                    int tube = rightBank[0];
                    rightBank.RemoveAt(0);
                    mask |= (1 << tube);
                }
            }
            
            foreach (var hub in this.hubList.ToArray())
            {
                FireflyHub fh = hub.Hub;
                hub.Hub.FireTubes(mask, Settings.Instance.BurnTime);
            }

            playPos += batchSize;

            if (playPos >= maxTubes)
            {
                playPos = -1;
                if (PlayComplete != null)
                {
                    PlayComplete(this, EventArgs.Empty);
                }
            }
            else
            {
                if (!paused)
                {
                    int speed = Settings.Instance.PlaySpeed;
                    delayedActions.StartDelayedAction("PlayNext", () => { PlayNext(); }, TimeSpan.FromMilliseconds(speed));
                }
            }
        }

    }
}
