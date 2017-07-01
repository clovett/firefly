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
    class FireflyHub
    {
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
        private Queue<FireflyMessage> queue = new Queue<FireflyMessage>();
        ManualResetEvent available = new ManualResetEvent(false);
        ManualResetEvent queueEmpty = new ManualResetEvent(false);
        Mutex queueLock = new Mutex();

        public event EventHandler<Exception> TcpError;
        public event EventHandler ConnectionChanged;

        internal async Task ConnectAsync()
        {
            if (!connecting)
            {
                connecting = true;
                try
                {
                    socket = new TcpMessageStream(FireflyMessage.MessageLength);
                    socket.Error += OnSocketError;

                    await socket.ConnectAsync(
                        new IPEndPoint(IPAddress.Parse(LocalHost.CanonicalName), 0),
                        new IPEndPoint(IPAddress.Parse(RemoteAddress), int.Parse(RemotePort)));
                    running = true;
                    var nowait = Task.Run(new Action(ProcessMessages));
                    nowait = Task.Run(new Action(Heartbeat));

                    Debug.WriteLine("Connected !!");
                    this.connected = true;
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

            if (TcpError != null)
            {
                TcpError(this, e);
            }
        }

        private void OnConnectionChanged()
        {
            if (ConnectionChanged != null)
            {
                ConnectionChanged(this, EventArgs.Empty);
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

        /// <summary>
        /// Get or set the number of tubes
        /// </summary>
        public int Tubes { get; set; }

        public DateTime LastHeartBeat { get; private set; }

        public event EventHandler<FireflyMessage> MessageReceived;

        public void Close()
        {
            closing = true;
            Arm(false);
            if (!queueEmpty.WaitOne(5000))
            {
                Debug.WriteLine("{0}: Timeout 5 seconds waiting for queue to drain", this.RemoteAddress);
            }
            running = false;
            available.Set();
            if (socket != null)
            {
                socket.Dispose();
            }
            this.connected = false;
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
                        if (msg != null)
                        {
                            try
                            {
                                Debug.WriteLine("{0}: Sending message: {1}", this.RemoteAddress, msg.FireCommand);
                                byte[] result = socket.SendReceive(msg.ToArray());
                                // tcp is synchronous request/response
                                response = FireflyMessage.Parse(result);
                            }
                            catch (Exception e)
                            {
                                response = new FireflyMessage()
                                {
                                    Error = e
                                };
                            }
                        }
                    }
                    else if (!postedEmpty)
                    {
                        postedEmpty = true;
                        queueEmpty.Set();
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
        }

        private void HandleFireResponse(FireflyMessage response)
        {
            // todo
        }

        private void HandleInfoResponse(FireflyMessage response)
        {
            switch (response.FireCommand)
            {
                case FireflyCommand.Ack:
                    Tubes = (response.Arg1 + (256 * response.Arg2));
                    break;
                case FireflyCommand.Nack:
                    break;
                case FireflyCommand.Timeout:
                    break;
                case FireflyCommand.Error:
                    break;
                default:
                    break;
            }
        }


        private async void Heartbeat()
        {
            while (running && !closing)
            {
                SendMessage(new FireflyMessage() { FireCommand = FireflyCommand.Heartbeat });
                await Task.Delay(3000);
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

        internal void FireTube(int i)
        {
            byte arg1 = (byte)i;
            byte arg2 = (byte)(i >> 8);
            SendMessage(new FireflyMessage() { FireCommand = FireflyCommand.Fire, Arg1 = arg1, Arg2 = arg2 });
        }
    }
}
