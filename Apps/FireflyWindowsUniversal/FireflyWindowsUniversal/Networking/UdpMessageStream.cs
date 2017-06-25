using System;
using System.Diagnostics;
using System.Net;
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
        
        void OnDatagramMessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            IPAddress remoteAddress = IPAddress.Parse(args.RemoteAddress.CanonicalName);
            IPEndPoint remoteEndPoint = new IPEndPoint(remoteAddress, int.Parse(args.RemotePort));

            var reader = args.GetDataReader();
            uint bytesRead = reader.UnconsumedBufferLength;

            byte[] data = new byte[bytesRead];
            reader.ReadBytes(data);

            string msg = Encoding.UTF8.GetString(data);
            if (msg != broadcastMessage)
            {
                this.response = msg;
                received.Set();
                try
                {
                    if (MessageReceived != null)
                    {
                        MessageReceived(this, new Message(remoteEndPoint, data));
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        public event EventHandler<Message> MessageReceived;

        public HostName LocalAddress { get { return pair.LocalHostName; } }

    }
}
