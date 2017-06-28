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
        Error = 'E'
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
        // private TcpMessageStream socket;

        UdpMessageStream socket;
        private bool running;
        private Queue<FireMessage> queue = new Queue<FireMessage>();
        ManualResetEvent available = new ManualResetEvent(false);
        Mutex queueLock = new Mutex();

        internal async Task ConnectAsync()
        {
            //socket = new TcpMessageStream(FireMessage.MessageLength);
            //
            //await socket.ConnectAsync(
            //    new IPEndPoint(IPAddress.Parse(LocalHost.CanonicalName), 0),
            //    new IPEndPoint(IPAddress.Parse(RemoteAddress), int.Parse(RemotePort)));
            socket = new UdpMessageStream();
            socket.MessageReceived += OnUdpMessageReceived;
            await socket.ConnectAsync(new EndpointPair(LocalHost, "0", new HostName(RemoteAddress), RemotePort), "");

            running = true;
            var nowait = Task.Run(new Action(ProcessMessages));
            nowait = Task.Run(new Action(Heartbeat));
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

        public event EventHandler<FireMessage> MessageReceived;

        public void Close()
        {
            running = false;
            available.Set();
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

                        try
                        {
                            //byte[] result = socket.SendReceive(msg.ToArray());
                            Debug.WriteLine("Sending message: " + msg.FireCommand);                                 
                            if (!socket.SendAsync(msg.ToArray()).Wait(5000))
                            {
                                Debug.WriteLine("Timeout trying to send over UDP, should we retry?");
                            }

                            // tcp is synchronous request/response
                            //response = FireMessage.Parse(result);
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

                if (response != null && MessageReceived != null)
                {
                    // relay the sent command back so we know what this is in response to.
                    response.SentCommand = msg;
                    MessageReceived(this, response);
                }
            }
        }

        private async void Heartbeat()
        {
            while (running)
            {
                SendMessage(new FireMessage() { FireCommand = FireCommand.Heartbeat });
                await Task.Delay(10000);
            }
        }

        internal void GetInfo()
        {
            SendMessage(new FireMessage() { FireCommand = FireCommand.Info });
        }
    }
}
