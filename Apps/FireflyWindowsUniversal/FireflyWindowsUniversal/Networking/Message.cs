using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FireflyWindows.Networking
{
    class Message
    {
        public Message(IPEndPoint address, byte[] msg)
        {
            this.Address = address;
            this.Payload = msg;
        }

        public IPEndPoint Address { get; set; }

        public byte[] Payload { get; set; }

    }
}
