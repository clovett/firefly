using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace FireflyWindows.Networking
{
    class TcpMessageStream : IDisposable
    {
        StreamSocket socket;
        BinaryReader reader;
        BinaryWriter writer;
        int messageMaxLength;

        public event EventHandler<Exception> Error;


        public TcpMessageStream(int messageMaxLength)
        {
            this.messageMaxLength = messageMaxLength;
        }

        public async Task ConnectAsync(IPEndPoint localAddress, IPEndPoint remoteAddress)
        {
            socket = new StreamSocket();
            await socket.ConnectAsync(new Windows.Networking.EndpointPair(
                new Windows.Networking.HostName(localAddress.Address.ToString()),
                localAddress.Port.ToString(),
                new Windows.Networking.HostName(remoteAddress.Address.ToString()),
                remoteAddress.Port.ToString()
                ));

            this.reader = new BinaryReader(socket.InputStream.AsStreamForRead(), Encoding.UTF8, true);
            this.writer = new BinaryWriter(socket.OutputStream.AsStreamForWrite(), Encoding.UTF8, true);
        }

        public byte[] SendReceive(byte[] message)
        {
            try
            {
                StreamSocket s = this.socket;
                if (s != null)
                {
                    int len = message.Length;
                    this.writer.Write(len);
                    this.writer.Write(message, 0, len);
                    this.writer.Flush();

                    len = this.reader.ReadInt32();
                    if (len < messageMaxLength)
                    {
                        byte[] result = this.reader.ReadBytes(len);
                        return result;
                    }
                }

            }
            catch (Exception e)
            {
                OnError(e);
            }
            return null;
        }

        public void OnError(Exception e)
        {
            if (Error!= null)
            {
                Error(this, e);
            }
        }



        public void Dispose()
        {
            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }
        }
    }
}
