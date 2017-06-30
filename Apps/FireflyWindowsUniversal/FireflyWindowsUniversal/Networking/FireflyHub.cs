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
    enum FireCommand
    {
        None,
        Info = 'I',
        Fire = 'F',
        Heartbeat = 'H',
        // responses
        Ready = 'R',
        Ack = 'A',
        Nack = 'N',
        Timeout = 'T',
        Error = 'E',
        Arm = 'X'
    }

    class FireMessage
    {
        public FireCommand FireCommand;
        public byte Arg1;
        public byte Arg2;
        public FireMessage SentCommand;
        public Exception Error;
        const byte HeaderByte = 0xfe;

        public FireMessage()
        {

        }
        
        public static FireMessage Parse(byte[] result)
        {
            FireMessage msg = null;
            if (result != null && result.Length == FireMessage.MessageLength && 
                result[0] == HeaderByte)
            {   
                if (Crc(result,0,4) == result[4])
                {
                    msg = new FireMessage()
                    {
                        FireCommand = (FireCommand)result[1],
                        Arg1 = result[2],
                        Arg2 = result[3]
                    };
                }
                else
                {
                    Debug.WriteLine("CRC failed");
                }                
            }
            return msg;
        }

        public static int MessageLength
        {
            get { return 5; }
        }

        public byte[] ToArray()
        {
            byte[] buffer = new byte[MessageLength];
            buffer[0] = HeaderByte;
            buffer[1] = (byte)FireCommand;
            buffer[2] = Arg1;
            buffer[3] = Arg2;
            buffer[4] = Crc(buffer, 0, 4);
            return buffer;
        }
        private static byte Crc(byte[] buffer, int offset, int len)
        {
            byte crc = 0;
            for (int i = offset; i < len; i++)
            {
                byte c = buffer[i];
                crc = (byte)((crc >> 1) ^ c);
            }
            return crc;
        }

    }

    class FireflyHub
    {
        public HostName LocalHost { get; internal set; }
        public string RemotePort { get; internal set; }
        public string RemoteAddress { get; internal set; }

        // Tcp commented out until we figure out how to fix it.
        private TcpMessageStream socket;
        private bool running;
        bool armed;
        private Queue<FireMessage> queue = new Queue<FireMessage>();
        ManualResetEvent available = new ManualResetEvent(false);
        Mutex queueLock = new Mutex();

        public event EventHandler<Exception> TcpError;

        internal async Task ConnectAsync()
        {
            socket = new TcpMessageStream(FireMessage.MessageLength);
            socket.Error += OnSocketError;

            await socket.ConnectAsync(
                new IPEndPoint(IPAddress.Parse(LocalHost.CanonicalName), 0),
                new IPEndPoint(IPAddress.Parse(RemoteAddress), int.Parse(RemotePort)));
            running = true;
            var nowait = Task.Run(new Action(ProcessMessages));
            nowait = Task.Run(new Action(Heartbeat));

            // get tube count
            GetInfo();
        }

        private void OnSocketError(object sender, Exception e)
        {
            if (TcpError != null)
            {
                TcpError(this, e);
            }
        }

        private void OnUdpMessageReceived(object sender, Message message)
        {
            FireMessage response = FireMessage.Parse(message.Payload);
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

        public event EventHandler<FireMessage> MessageReceived;

        public void Close()
        {
            running = false;
            available.Set();
            if (socket != null)
            {
                socket.Dispose();
            }
        }

        public void SendMessage(FireMessage f)
        {
            using (queueLock)
            {
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
            while (running)
            {
                FireMessage msg = null;
                FireMessage response = null;
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
                        msg = queue.Dequeue();
                        if (msg != null)
                        {
                            try
                            {
                                Debug.WriteLine("Sending message: " + msg.FireCommand);
                                byte[] result = socket.SendReceive(msg.ToArray());
                                // tcp is synchronous request/response
                                response = FireMessage.Parse(result);
                            }
                            catch (Exception e)
                            {
                                response = new FireflyWindows.FireMessage()
                                {
                                    Error = e
                                };
                            }
                        }
                    }
                }

                if (response != null)
                {
                    response.SentCommand = msg;
                    HandleResponse(response);
                }
            }
        }

        private void HandleResponse(FireMessage response)
        {
            switch (response.SentCommand.FireCommand)
            {
                case FireCommand.None:
                    break;
                case FireCommand.Info:
                    HandleInfoResponse(response);
                    break;
                case FireCommand.Fire:
                    HandleFireResponse(response);
                    break;
                case FireCommand.Heartbeat:
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

        private void HandleHeartbeatResponse(FireMessage response)
        {
            LastHeartBeat = DateTime.Now;
        }

        private void HandleFireResponse(FireMessage response)
        {
            // todo
        }

        private void HandleInfoResponse(FireMessage response)
        {
            switch (response.FireCommand)
            {
                case FireCommand.Ack:
                    Tubes = (response.Arg1 + (256 * response.Arg2));
                    break;
                case FireCommand.Nack:
                    break;
                case FireCommand.Timeout:
                    break;
                case FireCommand.Error:
                    break;
                default:
                    break;
            }
        }


        private async void Heartbeat()
        {
            while (running)
            {
                SendMessage(new FireMessage() { FireCommand = FireCommand.Heartbeat });
                await Task.Delay(3000);
            }
        }

        internal void GetInfo()
        {
            SendMessage(new FireMessage() { FireCommand = FireCommand.Info });
        }

        public bool Armed {  get { return this.armed; } }

        internal void Arm(bool arm)
        {
            armed = arm;
            SendMessage(new FireMessage() { FireCommand = FireCommand.Arm, Arg1 = arm ? (byte)1 : (byte)0 });
        }

        internal void FireTube(int i)
        {
            byte arg1 = (byte)i;
            byte arg2 = (byte)(i >> 8);
            SendMessage(new FireMessage() { FireCommand = FireCommand.Fire, Arg1 = arg1, Arg2 = arg2 });
        }
    }
}
