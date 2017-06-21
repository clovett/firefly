using FireflyWindows.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;

namespace FireflyWindows
{
    class HubLocator
    {
        CancellationTokenSource wifiSearchTokenSource;
        ManualResetEvent cancelled = new ManualResetEvent(false);
        bool findRunning;
        const int m_portNumber = 13777; // the magic firefly port
        internal const string UdpBroadcastMessage = "FIREFLY-FIND-HUBS";
        Dictionary<string, UdpMessageStream> sockets = new Dictionary<string, UdpMessageStream>();
        Dictionary<string, FireflyHub> hubs = new Dictionary<string, FireflyHub>();

        public event EventHandler<FireflyHub> HubAdded;

        public void StartFindingHubs()
        {
            if (wifiSearchTokenSource == null || wifiSearchTokenSource.IsCancellationRequested)
            {
                wifiSearchTokenSource = new CancellationTokenSource();
                var result = Task.Run(new Action(FindHubs));
            }
        }


        public void StopFindingHubs()
        {
            if (wifiSearchTokenSource != null && !wifiSearchTokenSource.IsCancellationRequested)
            {
                bool running = findRunning;
                wifiSearchTokenSource.Cancel();
                if (running)
                {
                    cancelled.WaitOne(5000);
                }
            }

            lock (sockets)
            {
                foreach (var socket in sockets.Values)
                {
                    // dispose the socket.
                    if (socket != null)
                    {
                        socket.Dispose();
                    }
                }
                sockets.Clear();
            }
        }


        private void FindHubs()
        {
            findRunning = true;
            try
            {
                hubs.Clear();
                var cancellationToken = wifiSearchTokenSource.Token;
                while (!cancellationToken.IsCancellationRequested)
                {
                    // send out the UDP ping every few seconds.
                    Guid adapter = Guid.Empty;
                    foreach (var hostName in Windows.Networking.Connectivity.NetworkInformation.GetHostNames())
                    {
                        if (hostName.IPInformation != null)
                        {
                            // blast it out on all local hosts, since user may be connected to the network in multiple ways
                            // and some of these hostnames might be virtual ethernets that go no where, like 169.254.80.80.
                            try
                            {
                                if (hostName.IPInformation.NetworkAdapter != null && !hostName.CanonicalName.StartsWith("169.")
                                    && hostName.Type == HostNameType.Ipv4)
                                {
                                    SendUdpPing(hostName).Wait(cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Ping failed: " + ex.Message);
                            }
                        }
                    }
                    try
                    {
                        Task.Delay(3000).Wait(cancellationToken);
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                cancelled.Set();
                findRunning = false;
            }
        }

        private async Task SendUdpPing(HostName hostName)
        {
            Debug.WriteLine(DateTime.Now.TimeOfDay.ToString() + ": SendUdpPing");
            string ipAddress = hostName.CanonicalName;
            UdpMessageStream socket;
            bool setup = false;
            lock (sockets)
            {
                if (!sockets.TryGetValue(ipAddress, out socket))
                {
                    // setup the socket for this network adapter.
                    socket = new UdpMessageStream();
                    sockets[ipAddress] = socket;
                    setup = true;
                }
            }
            if (setup)
            {
                socket.MessageReceived += OnUdpMessageReceived;
                string broadcastAddr = "255.255.255.255";
                string[] parts = ipAddress.Split('.');
                if (parts.Length == 4)
                {
                    parts[3] = "255";
                    broadcastAddr = string.Join(".", parts);
                }
                socket.ConnectAsync(new EndpointPair(hostName, m_portNumber.ToString(), new HostName(broadcastAddr), m_portNumber.ToString()), UdpBroadcastMessage);
            }
            await socket.SendAsync(UdpBroadcastMessage);
        }

        private void OnUdpMessageReceived(object sender, string msg)
        {
            UdpMessageStream stream = (UdpMessageStream)sender;
            Debug.WriteLine(DateTime.Now.TimeOfDay.ToString() + ": " + msg);

            string[] parts = msg.Split(',');
            if (parts.Length == 3)
            {
                string ipAddr = parts[0];
                string macAddr = parts[1];
                string model = parts[2];

                FireflyHub hub = null;
                if (!hubs.TryGetValue(ipAddr, out hub))
                {
                    hub = new FireflyHub()
                    {
                        LocalHost = stream.LocalAddress,
                        IPAddress = ipAddr,
                        MacAddress = macAddr,
                        ModelName = model
                    };
                    hubs[ipAddr] = hub;

                    if (HubAdded != null)
                    {
                        HubAdded(this, hub);
                    }
                }
            }
        }

    }
}
