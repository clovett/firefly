# firefly
Arduino based fire works controller

Messages are five byte messages arranged as follows:

1 Byte header | 1 byte command | 2 byte argument | 1 byte checksum

the header is always 0xFE

The commands are:
* H for heartbeat, which needs to be sent to the controller at least once a second
* F for fire, which needs to be paird with the tube number as the arguments as a 16 bit number
* A which is an ACK message sent from the controller to the computer for every message received
* N which is NACK sent from the controller to the computer for unknown messages, or tube number out of range
* I is an 'info' request, controller responds with the # of tubes

Checksum is a right shift once and a xor starting with zero.

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
