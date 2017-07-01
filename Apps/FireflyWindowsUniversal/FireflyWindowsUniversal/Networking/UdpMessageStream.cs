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
        public async Task ConnectAsync(EndpointPair pair, string broadcastMessage)
        {
            this.broadcastMessage = broadcastMessage;
            socket = new DatagramSocket();
            socket.MessageReceived += OnDatagramMessageReceived;
            this.pair = pair;
            if (string.IsNullOrEmpty(broadcastMessage))
            {
                // then this is a directed UDP socket that should talk to only one end point.
                await socket.ConnectAsync(pair);
            }
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
            await SendAsync(StringToAnsiiBytes(message));
        }

        public async Task SendAsync(byte[] message)
        {
            response = null;

            using (var stream = await socket.GetOutputStreamAsync(pair))
            {
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(message);
                    await writer.StoreAsync();
                }
            }
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
