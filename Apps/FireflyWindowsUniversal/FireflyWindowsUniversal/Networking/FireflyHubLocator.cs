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
    class FireflyHubLocator
    {
        CancellationTokenSource wifiSearchTokenSource;
        ManualResetEvent cancelled = new ManualResetEvent(false);
        bool findRunning;
        const int m_portNumber = 13777; // the magic firefly port
        internal const string UdpBroadcastMessage = "FIREFLY-FIND-HUB";
        internal const string UdpResponseMessage = "FIREFLY-HUB";
        Dictionary<string, UdpMessageStream> sockets = new Dictionary<string, UdpMessageStream>();
        Dictionary<string, FireflyHub> hubs = new Dictionary<string, FireflyHub>();
        const int BroadcastDelay = 5000; // 5 seconds

        public event EventHandler<FireflyHub> HubAdded;
        public event EventHandler<FireflyHub> HubConnected;
        public event EventHandler<FireflyHub> HubDisconnected;

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
                        Task.Delay(BroadcastDelay).Wait(cancellationToken);
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

        internal void Reset()
        {
            lock (hubs)
            {
                foreach (var item in hubs.Values)
                {
                    item.Stop();
                    item.Close();
                }
                hubs.Clear();
            }
        }

        private async Task SendUdpPing(HostName hostName)
        {
            //Debug.WriteLine(DateTime.Now.ToString("T") + ": SendUdpPing");
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
                await socket.ConnectAsync(new EndpointPair(hostName, m_portNumber.ToString(), new HostName(broadcastAddr), m_portNumber.ToString()), UdpBroadcastMessage);
            }
            await socket.SendAsync(UdpBroadcastMessage);
        }

        long count = 0;

        private void OnUdpMessageReceived(object sender, Message m)
        {
            UdpMessageStream stream = (UdpMessageStream)sender;

            string msg = Encoding.UTF8.GetString(m.Payload);
            string debugString = string.Format("{0}: {1} from {2} ({3})", DateTime.Now.ToString("T"), msg, m.Address.ToString(), count);
            Debug.WriteLine(debugString);

            count++;
            string[] parts = msg.Split(',');
            if (parts.Length == 4)
            {
                string prefix = parts[0];
                string ipAddr = parts[1];
                string port = parts[2];
                string connected = parts[3];
                if (prefix == UdpResponseMessage)
                {
                    FireflyHub hub = null;
                    if (!hubs.TryGetValue(ipAddr, out hub))
                    {
                        hub = new FireflyHub()
                        {
                            LocalHost = stream.LocalAddress,
                            RemoteAddress = ipAddr,
                            RemotePort = port
                        };
                        hubs[ipAddr] = hub;

                        if (HubAdded != null)
                        {
                            HubAdded(this, hub);
                        }
                    } else {
                        if (connected == hub.LocalHost.CanonicalName)
                        {
                            if (HubConnected != null)
                            {
                                HubConnected(this, hub);
                            }
                        }
                        else
                        {
                            // the hub no longer thinks they are connected to us,
                            // it may have done a reboot, so we need to reconnect.
                            if (HubDisconnected != null)
                            {
                                HubDisconnected(this, hub);
                            }
                        }
                    }
                }
            }
        }

    }
}
