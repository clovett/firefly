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
        public FireflyCommand FireCommand;
        public byte Arg1;
        public byte Arg2;
        public FireflyMessage SentCommand;
        public Exception Error;
        const byte HeaderByte = 0xfe;

        public FireflyMessage()
        {

        }

        public static FireflyMessage Parse(byte[] result)
        {
            FireflyMessage msg = null;
            if (result != null && result.Length == FireflyMessage.MessageLength &&
                result[0] == HeaderByte)
            {
                if (Crc(result, 0, 4) == result[4])
                {
                    msg = new FireflyMessage()
                    {
                        FireCommand = (FireflyCommand)result[1],
                        Arg1 = result[2],
                        Arg2 = result[3]
                    };
                }
                else
                {
                    Debug.WriteLine("CRC failed");
                }
            }
            return msg;
        }

        public static int MessageLength
        {
            get { return 5; }
        }

        public byte[] ToArray()
        {
            byte[] buffer = new byte[MessageLength];
            buffer[0] = HeaderByte;
            buffer[1] = (byte)FireCommand;
            buffer[2] = Arg1;
            buffer[3] = Arg2;
            buffer[4] = Crc(buffer, 0, 4);
            return buffer;
        }
        private static byte Crc(byte[] buffer, int offset, int len)
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
