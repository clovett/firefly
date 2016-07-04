using System;
using System.Collections.Generic;
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
        List<Tube> allTubes = new List<Tube>();
        int tubeCount;
        bool closed;
        const int ReadyBeats = 8;

        public MainWindow()
        {
            InitializeComponent();
            PortName.Text = "";

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

        async Task Connect(FirePort port)
        {
            ShowMessage("Connecting...");

            try
            {
                PortList.Visibility = Visibility.Collapsed;
                await port.Connect();
                PortName.Text = port.Name;
                this.port = port;
                ShowMessage("Connected");
                StartHeartbeat();
                var nowait = Task.Run(() => { CommandQueue(); });
            }
            catch (Exception ex)
            {
                ShowMessage(ex.Message);
            }
        }

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
                        OnLostHeartbeat();
                    }
                }
                else
                {
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
                    GetTubeInfo();
                }
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
            StartHeartbeat();
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
                    tubeCount = r.Arg2;
                    ShowMessage(tubeCount + " tubes loaded");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateTubes();
                    }));
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
            t.Background = Brushes.Black;
        }

        private void OnTubeFired(int tubeId)
        {
            Tube t = (from tube in allTubes where tube.Number == tubeId select tube).FirstOrDefault();
            t.Background = Brushes.Transparent;
        }

        private void UpdateTubes()
        {
            List<Tube> tubes = new List<FireflyWindows.Tube>();
            for (int i = 0; i < tubeCount; i++)
            {
                int tubeId = i;
                Tube t = new Tube() { Name = tubeId.ToString(), Background = Brushes.Green, Number = tubeId };
                tubes.Add(t);
            }
            allTubes = tubes;
            TubeList.ItemsSource = tubes;
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
            if (!tube.Fired)
            {
                tube.Fired = true;
                tube.Background = Brushes.Red;

                lock (queue)
                {
                    queue.Enqueue(new FireMessage() { FireCommand = FireCommand.Fire, Arg1 = (byte)tube.Number });
                }
                cmdAvailable.Set();
            }
        }

        private void ShowError(string msg)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                ErrorMessage.Text = msg;
                ErrorMessage.Visibility = (string.IsNullOrEmpty(msg)) ? Visibility.Collapsed : Visibility.Visible;
            }));
        }
    }
}
