﻿//#define DEBUGUI
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FireflyWindows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        FirePort port;
        DispatcherTimer heartBeatTimer;
        ObservableCollection<Tube> allTubes = new ObservableCollection<Tube>();
        FireCommands cmds = new FireCommands();
        int tubeCount;
        const int ReadyBeats = 2;

        public MainWindow()
        {
            InitializeComponent();
            UiDispatcher.Initialize(this.Dispatcher);

            PortName.Text = "";
            TubeList.ItemsSource = allTubes;

            heartBeatTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, OnHeartbeatTick, this.Dispatcher);
            Stopheartbeat();
            this.Loaded += OnMainWindowLoaded;

            cmds.ResponseReceived += ProcessResponse;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            cmds.Close();
            base.OnClosing(e);
        }

        private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (port == null)
            {
                List<FirePort> ports = new List<FireflyWindows.FirePort>(await FirePort.FindSerialPorts());
                if (ports.Count > 1)
                {
                    // user has to select the port.
                    PortList.Visibility = Visibility.Visible;
                    PortList.ItemsSource = ports;
                }
                else if (ports.Count == 1)
                {
                    // connect automatically.
                    await Connect(ports[0]);
                }
            }
        }

        bool connecting;

        async Task Connect(FirePort port)
        {
            ShowError("Connecting...");
            connecting = true;

            try
            {
                PortList.Visibility = Visibility.Collapsed;
                await port.Connect();
                PortName.Text = port.Name;
                this.port = port;
                this.cmds.Port = port;
                StartHeartbeat();

#if DEBUGUI
                ShowError("");
                tubeCount = 40;
                UpdateTubes();
#endif
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        private void StartHeartbeat()
        {
            heartBeatTimer.Start();
        }

        private void OnHeartbeatTick(object sender, EventArgs e)
        {
            Stopheartbeat();
            cmds.SendHeartbeat();
        }

        private void ProcessResponse(object sender, FireMessage m)
        {
            if (port == null)
            {
                return;
            }

            Debug.WriteLine("Response from : " + m.SentCommand + " is " + m.FireCommand + ", arg1=" + m.Arg1 + ", arg2=" + m.Arg2);

            if (m.FireCommand == FireCommand.Error)
            {
                ShowMessage(m.Error.Message);
            }
            else if (m.FireCommand == FireCommand.Timeout)
            {
                // timeout waiting.
                ShowMessage(m.SentCommand.ToString() + " timeout");
                if (m.SentCommand == FireCommand.Heartbeat)
                {
                    if (!connecting)
                    {
                        OnLostHeartbeat();
                    }
                    // may need to reset the port
                    port.Close();
                }
            }
            else
            {
                if (connecting)
                {
                    connecting = false;
                    ShowError("");
                    ShowMessage("connected");
                }
                // great!
                switch (m.SentCommand)
                {
                    case FireCommand.Info:
                        OnInfoResponse(m);
                        break;
                    case FireCommand.Fire:
                        OnFireResponse(m);
                        break;
                    case FireCommand.Heartbeat:
                        OnHeartbeatResponse(m);
                        break;
                }
            }

            StartHeartbeat();
        }

        int goodHeartBeats;
        int badHeartBeats;

        private void OnHeartbeatResponse(FireMessage r)
        {
            if (r.FireCommand == FireCommand.Ack)
            {
                goodHeartBeats++;
                badHeartBeats = 0;
                if (goodHeartBeats == ReadyBeats)
                {
                    goodHeartBeats = 0;
                    cmds.GetTubeInfo();
                }
                ShowError("");
            }
            else
            {
                // hmmm.
                goodHeartBeats = 0;
                badHeartBeats++;
                if (badHeartBeats > 5)
                {
                    ShowMessage("Heartbeat is failing");
                }
            }
        }

        private void OnLostHeartbeat()
        {
            ShowError("Lost Heartbeat");
            Dispatcher.BeginInvoke(new Action(() =>
            {
                allTubes.Clear();
                tubeCount = 0;
                goodHeartBeats = 0;
                badHeartBeats = 0;
                cmds.Clear();
                StopPattern();
                StartHeartbeat();
            }));
        }

        private void OnInfoResponse(FireMessage r)
        {
            if (r.FireCommand == FireCommand.Ack)
            {
                if (r.Arg2 == 0)
                {
                    // not ready
                    ShowMessage("not ready");
                }
                else
                {
                    if (tubeCount != r.Arg2)
                    {
                        tubeCount = r.Arg2;
                        ShowMessage(tubeCount + " tubes loaded");
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateTubes();
                        }));
                    }
                }
            }
            else
            {
                // hmmm, try again sending good heartbeats
                goodHeartBeats = 0;
            }
        }

        private void OnFireResponse(FireMessage r)
        {
            if (r.FireCommand == FireCommand.Ack)
            {
                int tubeId = r.Arg2;
                Debug.WriteLine("Tube {0} fired", tubeId);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    OnTubeFired(tubeId);
                }));
            }
            else
            {
                int tubeId = r.Arg2;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    OnTubeFailed(tubeId);
                }));
            }
        }

        private void OnTubeFailed(int tubeId)
        {
            Tube t = (from tube in allTubes where tube.Number == tubeId select tube).FirstOrDefault();
            t.Failed = true;
        }

        private void OnTubeFired(int tubeId)
        {
            Tube t = (from tube in allTubes where tube.Number == tubeId select tube).FirstOrDefault();
            t.Fired = true;
        }

        private void UpdateTubes()
        {
            StopPattern();
            allTubes.Clear();
            for (int i = 0; i < tubeCount; i++)
            {
                int tubeId = i;
                Tube t = new Tube() { Name = tubeId.ToString(), Number = tubeId };
                allTubes.Add(t);
            }
        }

        private void Stopheartbeat()
        {
            heartBeatTimer.Stop();
        }

        private void ShowMessage(string msg)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                Messages.Text = msg;
            }));
        }

        private void OnPortSelected(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            PortList.Visibility = Visibility.Collapsed;
            FirePort port = (FirePort)button.DataContext;
            var nowait = Connect(port);
        }

        private void OnTubeSelected(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Tube tube = (Tube)button.DataContext;
            tube.Fired = false;
            tube.Failed = false;
            tube.Firing = true;
            cmds.FireTube(tube);
        }

        private void ShowError(string msg)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                ErrorMessage.Text = msg;
                ErrorShield.Visibility = (string.IsNullOrEmpty(msg)) ? Visibility.Collapsed : Visibility.Visible;
            }));
        }

        private void OnStopAll(object sender, RoutedEventArgs e)
        {
            StopPattern();
        }

        void StopPattern()
        { 
            if (pattern != null)
            {
                pattern.Stop();
            }
        }

        private void OnFireAll(object sender, RoutedEventArgs e)
        {
            if (pattern!= null && !pattern.Complete)
            {
                pattern.Resume();
            }
            else 
            {
                pattern = new SequentialPattern(cmds, TimeSpan.FromMilliseconds(50));
                //pattern = new CrescendoPattern(cmds);
                pattern.Start(this.allTubes);
            }
        }

        FiringPattern pattern;

        private void OnFireFast(object sender, RoutedEventArgs e)
        {
            if (pattern != null && !pattern.Complete)
            {
                pattern.Resume();
            }
            else
            {
                pattern = new SequentialPattern(cmds, TimeSpan.FromMilliseconds(50));
                pattern.Start(this.allTubes);
            }

        }

        private void OnFireSlow(object sender, RoutedEventArgs e)
        {
            if (pattern != null && !pattern.Complete)
            {
                pattern.Resume();
            }
            else
            {
                pattern = new SequentialPattern(cmds, TimeSpan.FromMilliseconds(250));
                pattern.Start(this.allTubes);
            }

        }

        private void OnFireCres(object sender, RoutedEventArgs e)
        {
            if (pattern != null && !pattern.Complete)
            {
                pattern.Resume();
            }
            else
            {
                pattern = new CrescendoPattern(cmds);
                pattern.Start(this.allTubes);
            }
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            StopPattern();
            pattern = null;
            foreach (var tube in allTubes)
            {
                tube.Fired = false;
                tube.Failed = false;
                tube.Firing = false;
            }
        }
    }
}
