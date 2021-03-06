﻿using BleLights.SharedControls;
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
        bool armed;
        bool lightsOn;
        bool running;
        HubProgram program;

        public event EventHandler<string> Message;

        public HubManager()
        {
            program = new HubProgram();
        }

        public ObservableCollection<HubModel> Hubs
        {
            get { return hubList; }
        }

        public bool IsArmed { get { return armed; } }

        public bool IsLightsOn { get { return lightsOn; } }

        public HubProgram Program { get { return program; } }

        public void Start()
        {
            if (!running)
            {
                running = true;
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
                HubModel test = new HubModel(new FireflyHub() { Tubes = 10 });
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
            program.Refresh();
            if (lightsOn)
            {
                SetColor(0, 0, 0, 0);
            }
            hubList.Clear();
            locator.Reset();
            lightsOn = false;
            armed = false;
        }

        internal void SetProgram(List<int> program)
        {
            this.program.Numbers = program;
        }
    }
}
