# firefly
Arduino based fire works controller

Messages are five byte messages aranged as follows:

1 Byte headder | 1 byte command | 2 byte argument | 1 byte checksum

the headder is always 0xFE

The commands are:
* H for heartbeat, which needs to be sent to the controller at least once a second
* F for fire, which needs to be paird with the tube number as the arguments as a 16 bit number
* A which is an ACK message sent from the controller to the computer for every message recived
* N which is NACK sent from the controller to the computer for unknown messages
* R which is a ready command sent from the controller to the computer once the required number of seconds worth of heartbeats are received.
* 

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
