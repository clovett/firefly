using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FireflyWindows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        FirePort port;
        Queue<FireMessage> queue = new Queue<FireMessage>();
        AutoResetEvent cmdAvailable = new AutoResetEvent(false);
        DispatcherTimer heartBeatTimer;
        ObservableCollection<Tube> allTubes = new ObservableCollection<Tube>();
        int tubeCount;
        bool closed;
        const int ReadyBeats = 8;

        public MainWindow()
        {
            InitializeComponent();
            PortName.Text = "";
            TubeList.ItemsSource = allTubes;

            heartBeatTimer = new DispatcherTimer(TimeSpan.FromSeconds(0.5), DispatcherPriority.Normal, OnHeartbeatTick, this.Dispatcher);
            heartBeatTimer.Stop();
            this.Loaded += OnMainWindowLoaded;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            closed = true;
            cmdAvailable.Set();
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
                StartHeartbeat();
                if (!taskRunning)
                {
                    taskRunning = true;
                    var nowait = Task.Run(() => { CommandQueue(); });
                }
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

        bool taskRunning;

        private void CommandQueue()
        {
            // background thread.
            while (!closed)
            {
                cmdAvailable.WaitOne(1000);

                FireMessage m = null;
                lock (queue)
                {
                    if (queue.Count > 0)
                    {
                        m = queue.Dequeue();
                    }
                }
                if (m != null)
                {
                    ProcessCommand(m);
                }
            }
            taskRunning = false;
        }

        private void StartHeartbeat()
        {
            heartBeatTimer.Start();
        }

        private void OnHeartbeatTick(object sender, EventArgs e)
        {
            heartBeatTimer.Stop();
            lock (queue)
            {
                queue.Enqueue(new FireMessage() { FireCommand = FireCommand.Heartbeat });
            }
            cmdAvailable.Set();
        }

        int commandTimeout = 5000;

        private void ProcessCommand(FireMessage m)
        {
            if (port == null)
            {
                return;
            }

            try
            {
                CancellationTokenSource src = new CancellationTokenSource();
                Task<FireMessage> sendTask = port.Send(m, src.Token);
                Task delay = Task.Delay(commandTimeout);
                if (Task.WaitAny(sendTask, delay) == 1)
                {
                    // timeout waiting.
                    src.Cancel();
                    ShowMessage(m.FireCommand.ToString() + " timeout");
                    if (m.FireCommand == FireCommand.Heartbeat)
                    {
                        if (!connecting)
                        {
                            OnLostHeartbeat();
                        }
                        // may need to reset the port
                        port.Close();
                        StartHeartbeat();
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
                    FireMessage r = sendTask.Result;                    
                    // great!
                    switch (m.FireCommand)
                    {
                        case FireCommand.Info:
                            OnInfoResponse(r);
                            break;
                        case FireCommand.Fire:
                            OnFireResponse(r);
                            break;
                        case FireCommand.Heartbeat:
                            OnHeartbeatResponse(r);
                            break;
                    }
                   
                }
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
                StartHeartbeat();
            }
        }

        private void GetTubeInfo()
        {
            // get number of tubes
            lock (queue)
            {
                queue.Enqueue(new FireMessage() { FireCommand = FireCommand.Info });
            }
            cmdAvailable.Set();
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
                    GetTubeInfo();
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
            StartHeartbeat();
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
                lock (queue)
                {
                    queue.Clear();
                }
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
            t.Color = Colors.Black;
        }

        private void OnTubeFired(int tubeId)
        {
            Tube t = (from tube in allTubes where tube.Number == tubeId select tube).FirstOrDefault();
            t.Color = Colors.Transparent;
        }

        private void UpdateTubes()
        {
            allTubes.Clear();
            for (int i = 0; i < tubeCount; i++)
            {
                int tubeId = i;
                Tube t = new Tube() { Name = tubeId.ToString(), Color = Colors.Green, Number = tubeId };
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
            tube.Fired = true;
            tube.Color = Colors.Red;
            lock (queue)
            {
                queue.Enqueue(new FireMessage() { FireCommand = FireCommand.Fire, Arg1 = (byte)tube.Number });
            }
            cmdAvailable.Set();
        }

        private void ShowError(string msg)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                ErrorMessage.Text = msg;
                ErrorShield.Visibility = (string.IsNullOrEmpty(msg)) ? Visibility.Collapsed : Visibility.Visible;
            }));
        }
    }
}
