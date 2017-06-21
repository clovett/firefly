using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace FireflyWindows.Networking
{

    /// <summary>
    /// Sends a UDP message and waits for response.
    /// </summary>
    class UdpMessageStream : IDisposable
    {
        ManualResetEvent received = new ManualResetEvent(false);
        string response;
        HostName remoteHost;
        string remotePort;
        DatagramSocket socket;
        EndpointPair pair;
        string broadcastMessage;

        /// <summary>
        /// Connect a datagram socket and monitor messages that contain the given broadcastMessage 
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="broadcastMessage">The message to look for</param>
        public void ConnectAsync(EndpointPair pair, string broadcastMessage)
        {
            this.broadcastMessage = broadcastMessage;
            socket = new DatagramSocket();
            socket.MessageReceived += OnDatagramMessageReceived;
            this.pair = pair;
        }

        public void Dispose()
        {
            using (socket)
            {
                socket = null;
            }
        }
        internal static byte[] StringToAnsiiBytes(string s)
        {
            byte[] buffer = new byte[s.Length];
            int i = 0;
            foreach (char c in s)
            {
                buffer[i++] = Convert.ToByte(c);
            }
            return buffer;

        }

        public async Task SendAsync(string message)
        {
            response = null;

            using (var stream = await socket.GetOutputStreamAsync(pair))
            {
                using (var writer = new DataWriter(stream))
                {
                    byte[] msg = StringToAnsiiBytes(message);
                    writer.WriteBytes(msg);
                    await writer.StoreAsync();
                }
            }
        }

        public async Task<string> SendReceiveAsync(string message, TimeSpan timeout)
        {
            received.Reset();
            response = null;

            await SendAsync(message);

            // wait for a response
            if (timeout != TimeSpan.MinValue)
            {
                await Task.Run(new Action(() =>
                {
                    if (!received.WaitOne((int)timeout.TotalMilliseconds))
                    {
                        response = null;
                    }
                }));
            }

            return response;
        }

        /// <summary>
        /// If a message is received, this contains the remote host port number of the sender
        /// </summary>
        public string RemotePort { get { return this.remotePort; } }

        /// <summary>
        /// If a message is received, this contains the remote host name
        /// </summary>
        public HostName RemoteHost { get { return this.remoteHost; } }

        internal static string BytesToString(byte[] buffer)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in buffer)
            {
                sb.Append(Convert.ToChar(b));
            }
            return sb.ToString();

        }

        void OnDatagramMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            this.remoteHost = args.RemoteAddress;
            this.remotePort = args.RemotePort;

            var reader = args.GetDataReader();
            uint bytesRead = reader.UnconsumedBufferLength;

            byte[] data = new byte[bytesRead];
            reader.ReadBytes(data);

            string msg = BytesToString(data);
            if (msg != broadcastMessage)
            {
                Debug.WriteLine("OnDatagramMessageReceived {0}", msg);
                this.response = msg;
                received.Set();
                try
                {
                    if (MessageReceived != null)
                    {
                        MessageReceived(this, msg);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public event EventHandler<string> MessageReceived;

        public HostName LocalAddress { get { return pair.LocalHostName; } }

    }
}
