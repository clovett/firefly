using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        Nack = 'N'
    }

    class FireMessage
    {
        public FireCommand FireCommand;
        public byte Arg1;
        public byte Arg2;
    }

    class FirePort
    {
        SerialPort port;
        string name;
        string deviceId;
        const byte HeaderByte = 0xfe;

        public string Name { get { return name; } }

        public FirePort(string name, string deviceId)
        {
            this.name = name;
            this.deviceId = deviceId;
        }

        public static async Task<IEnumerable<FirePort>> FindSerialPorts()
        {
            List<FirePort> result = new List<FirePort>();
            await Task.Run(() =>
            {
                try
                {
                    RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
                    if (key != null)
                    {
                        foreach (string sub in key.GetValueNames())
                        {
                            string name = key.GetValue(sub).ToString();
                            result.Add(new FireflyWindows.FirePort(name, sub));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            });
            return result;
        }

        public async Task Connect()
        {
            await Task.Run(() =>
            {
                port = new SerialPort(this.name, 57600);
                port.Open();
            });

            return;
        }

        public async Task<FireMessage> Send(FireMessage m, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                byte[] buffer = new byte[5];
                buffer[0] = HeaderByte;
                buffer[1] = (byte)m.FireCommand;
                buffer[2] = m.Arg1;
                buffer[3] = m.Arg2;
                buffer[4] = Crc(buffer, 0, 4);
                port.Write(buffer, 0, buffer.Length);

                // get response
                buffer = new byte[5];
                int len = 0;
                while (len < 5)
                {
                    len += port.Read(buffer, len, 5 - len);
                }
                if (len == 5)
                {
                    if (buffer[0] == HeaderByte && buffer[4] == Crc(buffer, 0, 4))
                    {
                        FireMessage r = new FireMessage() { FireCommand = (FireCommand)buffer[1], Arg1 = buffer[2], Arg2 = buffer[3] };
                        return r;
                    }
                }
                // message failed.
                return new FireMessage() { FireCommand = FireCommand.Nack };
            }, cancellationToken);
        }

        private byte Crc(byte[] buffer, int offset, int len)
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
}