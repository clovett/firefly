using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireflyWindows.Networking
{
    enum FireflyCommand
    {
        None,
        Info = 'I',
        Fire = 'F',
        Heartbeat = 'H',
        // responses
        Ready = 'R',
        Ack = 'A',
        Nack = 'N',
        Timeout = 'T',
        Error = 'E',
        Arm = 'X'
    }

    class FireflyMessage
    {
        public int Header;
        public FireflyCommand FireCommand;
        public int Arg1;
        public int Arg2;
        public int Arg3;
        public int Arg4;
        public uint Crc;
        public uint Computed;
        public FireflyMessage SentCommand;
        public Exception Error;
        const int MagicHeader= 0x152C81;

        public FireflyMessage()
        {

        }

        public static FireflyMessage Parse(byte[] result)
        {
            FireflyMessage msg = null;
            if (result != null)
            {
                string text = Encoding.UTF8.GetString(result);
                string[] parts = text.Split(',');
                if (parts.Length == 7)
                {
                    msg = new FireflyMessage();
                    msg.Header = int.Parse(parts[0]);
                    string cmd = parts[1];
                    msg.FireCommand = (cmd.Length > 0) ? (FireflyCommand)cmd[0] : FireflyCommand.None;
                    msg.Arg1 = int.Parse(parts[2]);
                    msg.Arg2 = int.Parse(parts[3]);
                    msg.Arg3 = int.Parse(parts[4]);
                    msg.Arg4 = int.Parse(parts[5]);
                    msg.Crc = uint.Parse(parts[6]);
                    int pos = text.LastIndexOf(',');
                    msg.Computed = ComputeCrc(result, 0, pos);

                    if (msg.Header != MagicHeader || msg.Crc != msg.Computed)
                    {
                        Debug.WriteLine("Ignoring bad message: " + text + ", computed" + msg.Computed);
                        msg = null;
                    }
                }
            }
            return msg;
        }
        
        public byte[] Format()
        {
            StringBuilder msg = new StringBuilder();

            msg.Append(MagicHeader.ToString());
            msg.Append(",");
            msg.Append((char)FireCommand);
            msg.Append(",");
            msg.Append(Arg1.ToString());
            msg.Append(",");
            msg.Append(Arg2.ToString());
            msg.Append(",");
            msg.Append(Arg3.ToString());
            msg.Append(",");
            msg.Append(Arg4.ToString());

            string text = msg.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            uint crc = ComputeCrc(bytes, 0, bytes.Length);

            msg.Append(",");
            msg.Append(crc.ToString());
            msg.Append((char)0); // include a null terminator in the message
            return Encoding.UTF8.GetBytes(msg.ToString());
        }

         private static uint ComputeCrc(byte[] buffer, int offset, int len)
        {
            uint crc = 0;
            for (int i = offset; i<len; i++)
            {
                byte c = buffer[i];
                uint sum = crc + c;
                crc = (uint)((sum << 1) ^ sum);
            }
            return crc;
        }

    }

}
