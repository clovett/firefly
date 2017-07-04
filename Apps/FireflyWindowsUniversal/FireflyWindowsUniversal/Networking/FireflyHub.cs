using BleLights.SharedControls;
using FireflyWindows.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;

namespace FireflyWindows
{
    public class FireflyHub
    {
        const int MaximumMessageLength = 1000;
        public HostName LocalHost { get; internal set; }
        public string RemotePort { get; internal set; }
        public string RemoteAddress { get; internal set; }

        // Tcp commented out until we figure out how to fix it.
        private TcpMessageStream socket;
        private bool running;
        bool closing;
        bool armed;
        bool connected;
        bool connecting;
        DateTime connectTime;
        private Queue<FireflyMessage> queue = new Queue<FireflyMessage>();
        ManualResetEvent available = new ManualResetEvent(false);
        ManualResetEvent queueEmpty = new ManualResetEvent(false);
        Mutex queueLock = new Mutex();
        const int HeartbeatDelay = 2000; // 2 seconds

        public event EventHandler<string> Error;
        public event EventHandler ConnectionChanged;
        public event EventHandler StateChanged;

        internal async Task ConnectAsync()
        {
            if (!connecting)
            {
                connecting = true;
                this.connectTime = DateTime.Now;
                try
                {
                    socket = new TcpMessageStream(MaximumMessageLength);
                    socket.Error += OnSocketError;

                    await socket.ConnectAsync(
                        new IPEndPoint(IPAddress.Parse(LocalHost.CanonicalName), 0),
                        new IPEndPoint(IPAddress.Parse(RemoteAddress), int.Parse(RemotePort)));
                    running = true;
                    var nowait = Task.Run(new Action(ProcessMessages));
                    nowait = Task.Run(new Action(Heartbeat));

                    Debug.WriteLine("Connected !!");
                    this.connected = true;
                    this.connectTime = DateTime.Now;
                    OnConnectionChanged();

                    // get tube count
                    GetInfo();
                }
                finally
                {
                    connecting = false;
                }
            }
        }

        private void OnSocketError(object sender, Exception e)
        {
            if ((uint)e.HResult == 0x80072746 || (e.InnerException != null && (uint)e.InnerException.HResult == 0x80072746))
            {
                // An existing connection was forcibly closed by the remote host
                this.connected = false;
                this.Close();
                OnConnectionChanged();
            }
            OnError(e.Message);
        }

        private void OnError(string msg)
        {
            if (Error != null)
            {
                Error(this, msg);
            }
        }

        private void OnConnectionChanged()
        {
            if (ConnectionChanged != null)
            {
                ConnectionChanged(this, EventArgs.Empty);
            }
        }

        internal async Task Reconnect()
        {
            TimeSpan timeSinceConnection = DateTime.Now - this.connectTime;
            if (timeSinceConnection > TimeSpan.FromSeconds(5))
            {
                // the hub may have rebooted, so we need to reconnect...
                Close();
                await ConnectAsync();
            }
        }

        private void OnUdpMessageReceived(object sender, Message message)
        {
            FireflyMessage response = FireflyMessage.Parse(message.Payload);
            if (response != null && MessageReceived != null)
            {
                // relay the sent command back so we know what this is in response to.
                // response.SentCommand = msg; no easy way to correlate the responses over UDP...
                MessageReceived(this, response);
            }
        }

        private int tubes;
        private int[] tubeState;

        /// <summary>
        /// Get or set the number of tubes
        /// </summary>
        public int Tubes
        {
            get { return tubes; }
            set
            {
                if (tubes != value)
                {
                    tubes = value;
                    tubeState = new int[tubes];
                }
            }
        }

        public int GetTubeState(int tube)
        {
            if (tubeState != null && tube >= 0 && tube < tubeState.Length)
            {
                return tubeState[tube];
            }
            return 0;
        }

        public DateTime LastHeartBeat { get; private set; }

        public event EventHandler<FireflyMessage> MessageReceived;

        public void Close()
        {
            closing = true;
            running = false;
            Arm(false);
            available.Set();
            if (socket != null)
            {
                socket.Dispose();
            }
            this.connected = false;
        }

        // stop sending and flush the queue.
        internal void Stop()
        {
            closing = false;
            if (!queueEmpty.WaitOne(5000))
            {
                Debug.WriteLine("{0}: Timeout 5 seconds waiting for queue to drain", this.RemoteAddress);
            }
        }

        public void SendMessage(FireflyMessage f)
        {
            using (queueLock)
            {
                queueEmpty.Reset();
                int count = queue.Count;
                queue.Enqueue(f);
                if (count == 0)
                {
                    available.Set();
                }
            }
        }

        private void ProcessMessages()
        {
            bool postedEmpty = false;
            while (running)
            {
                FireflyMessage msg = null;
                FireflyMessage response = null;
                int count = queue.Count;
                using (queueLock)
                {
                    count = queue.Count; 
                }
                if (count == 0)
                {
                    available.WaitOne(1000);
                }
                using (queueLock)
                {
                    if (queue.Count > 0)
                    {
                        postedEmpty = false;
                        msg = queue.Dequeue();
                    }
                    else if (!postedEmpty)
                    {
                        postedEmpty = true;
                        queueEmpty.Set();
                    }
                }
                if (msg != null)
                {
                    try
                    {
                        string formatted = msg.Format();
                        Debug.WriteLine("{0}: Sending message: {1}: {2}", this.RemoteAddress, msg.FireCommand, formatted);
                        Stopwatch watch = new Stopwatch();
                        watch.Start();
                        byte[] result = socket.SendReceive(Encoding.UTF8.GetBytes(formatted));
                        // our tcp protocol is synchronous request/response
                        response = FireflyMessage.Parse(result);
                        watch.Stop();
                        if (response != null)
                        {
                            Debug.WriteLine("{0}: Received response in {1}ms: {2}", this.RemoteAddress, watch.ElapsedMilliseconds, response.Format());
                        }
                    }
                    catch (Exception e)
                    {
                        response = new FireflyMessage()
                        {
                            Error = e
                        };
                    }
                }

                if (response != null)
                {
                    response.SentCommand = msg;
                    HandleResponse(response);
                }
            }
            Debug.WriteLine("{0}: Message processing thread terminating", this.RemoteAddress);
        }

        private void HandleResponse(FireflyMessage response)
        {
            switch (response.SentCommand.FireCommand)
            {
                case FireflyCommand.None:
                    break;
                case FireflyCommand.Info:
                    HandleInfoResponse(response);
                    break;
                case FireflyCommand.Fire:
                    HandleFireResponse(response);
                    break;
                case FireflyCommand.Heartbeat:
                    HandleHeartbeatResponse(response);
                    break;
                default:
                    break;
            }

            // relay the sent command back so we know what this is in response to.
            if (MessageReceived != null)
            {
                MessageReceived(this, response);
            }
        }

        private void HandleHeartbeatResponse(FireflyMessage response)
        {
            LastHeartBeat = DateTime.Now;
            Debug.WriteLine("Heartbeat returned Arg1={0:x}, Arg2={1:x}", response.Arg1, response.Arg2);
            if (!this.connected)
            {
                this.connected = true;
                OnConnectionChanged();
            }
            if (tubeState != null)
            {
                // get sense info
                int value = response.Arg1;
                for (int i = 0; i < 5; i++)
                {
                    int on = (value & 0x1);
                    tubeState[i] = on;
                    value >>= 1;
                }
                value = response.Arg2;

                for (int i = 0; i < 5; i++)
                {
                    int on = (value & 0x1);
                    tubeState[5 + i] = on;
                    value >>= 1;
                }

                if (StateChanged != null)
                {
                    StateChanged(this, EventArgs.Empty);
                }
            }
        }

        private void HandleFireResponse(FireflyMessage response)
        {
            switch (response.FireCommand)
            {
                case FireflyCommand.Ack:
                    // great!
                    break;
                default:
                    // hmm, failed to fire?
                    OnError("Error (" + response.FireCommand + ") firing tube " + response.Arg1);
                    break;
            }
        }

        private void HandleInfoResponse(FireflyMessage response)
        {
            switch (response.FireCommand)
            {
                case FireflyCommand.Ack:
                    Tubes = response.Arg1;

                    break;
                default:
                    OnError("Error (" + response.FireCommand + ") getting info");
                    break;
            }
        }


        private async void Heartbeat()
        {
            while (running && !closing)
            {
                SendMessage(new FireflyMessage() { FireCommand = FireflyCommand.Heartbeat });
                await Task.Delay(HeartbeatDelay);
                if (queue.Count > 10)
                {
                    this.connected = false;
                    OnConnectionChanged();
                    OnError("Hub not responding...");
                    lock(queue)
                    {
                        queue.Clear();
                    }
                } 
            }
            Debug.WriteLine("{0}: Heartbeat thread terminating", this.RemoteAddress);
        }

        internal void GetInfo()
        {
            SendMessage(new FireflyMessage() { FireCommand = FireflyCommand.Info });
        }

        public bool Armed {  get { return this.armed; } }

        public bool Connected {
            get { return this.connected; }
        }

        internal void Arm(bool arm)
        {
            armed = arm;
            SendMessage(new FireflyMessage() { FireCommand = FireflyCommand.Arm, Arg1 = arm ? (byte)1 : (byte)0 });
        }

        internal void FireTubes(int bits, int burnTimeMs)
        {
            SendMessage(new FireflyMessage() { FireCommand = FireflyCommand.Fire, Arg1 = bits, Arg2 = burnTimeMs });
        }

        internal void SetColor(byte a, byte r, byte g, byte b)
        {
            a = (byte)(((((int)r + (int)g + (int)b) / 3) * 31) / 255);
            SendMessage(new FireflyMessage() { FireCommand = FireflyCommand.Color, Arg1 = a, Arg2 = r, Arg3 = g, Arg4 = b });
        }

    }
}
