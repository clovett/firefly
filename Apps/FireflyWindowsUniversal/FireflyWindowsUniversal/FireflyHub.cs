using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;

namespace FireflyWindows
{
    class FireflyHub
    {
        public string IPAddress { get; internal set; }
        public HostName LocalHost { get; internal set; }
        public string MacAddress { get; internal set; }
        public string ModelName { get; internal set; }
    }
}
