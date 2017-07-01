using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    // This class implements a simple message based TCP protocol where the
    // end of each message is denoted by a NULL terminating character.
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
                MemoryStream buffer = new MemoryStream();

                StreamSocket s = this.socket;
                if (s != null && this.writer != null)
                {
                    // send message
                    this.writer.Write(message, 0, message.Length);
                    // send null terminator
                    byte[] terminator = new byte[1];
                    terminator[0] = 0;
                    this.writer.Write(terminator, 0, 1);
                    this.writer.Flush();

                    // read response                    
                    byte ch = this.reader.ReadByte();
                    while (ch != 0) { 
                        buffer.WriteByte(ch);
                        ch = this.reader.ReadByte();
                        if (buffer.Length > this.messageMaxLength)
                        {
                            Debug.WriteLine("Purging message that exceeded maximum length");
                            buffer = new MemoryStream();
                        }
                    }
                    return buffer.ToArray();
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
                this.writer = null;
                this.reader = null;
            }
        }
    }
}
