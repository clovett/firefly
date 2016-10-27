import struct

"""
Messages must be able to:
-> pack into a transmitable string
-> unpack from a string
-> provide getters for the parsed data

The message class provides the basics for messages, and each
message type subclasses the Message class.

An addional module method needs to be made that makes use of all message classes
in order to read an incoming message and return a correctly parsed message of
the correct type, or an error if no message type matched the incoming bytes.
"""

"""
The message class should not be used directly, and simply provides methods for
checking the start bytes and CRC's of incoming messages, and packing outgoing
messages.
"""
class _Message(object):

    START_BYTE = 0xFE

    def __init__(self, **kwargs):
        self.parse()

    def parse(self):
        raise NotImplementedError

    def pack(self):
        payload = self._pack_payload()
        message = struct.pack("BH", self.START_BYTE, len(payload))

        #add the payload to the outgoing message
        message += payload

        #calc the crc for the outgoing message
        crc = self._calc_crc16(message)
        message += struct.pack("H", crc)

        return message

    def _pack_payload(self):
        raise NotImplementedError

    def _calc_crc16(self, message):
        CRC16_POLYNOM = 0x2F15
        CRC_SEED = 0x0000
        crc = CRC_SEED

        for b in message:
            c = int(b.encode('hex'), base=16)
            crc ^= (c << 8)
            for i in range(8):
                if (crc & 0x8000) != 0:
                     crc = (crc << 1) ^ CRC16_POLYNOM
                else:
                    crc = crc << 1
                crc = crc & 0xFFFF
        return crc

class MsgHeartbeat(_Message):
    pass

def print_in_hex(message):
    string = str(message)
    print ':'.join(x.encode('hex') for x in string)

if __name__ == "__main__":
    test_msg = _Message()
    print_in_hex(test_msg.pack())
