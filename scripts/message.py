import struct
from utils import calc_crc16

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
class Message(object):

    START_BYTE = 0xFE

    def __init__(self):
        self.id = "MESSAGE"

    def get_message_id(self):
        return self.id

    def pack(self):
        payload = self._pack_payload()
        message = struct.pack("BH", self.START_BYTE, len(payload))

        #add the payload to the outgoing message
        message += payload

        #calc the crc for the outgoing message
        crc = calc_crc16(message)
        message += struct.pack("H", crc)

        return message

    def _pack_payload(self):
        raise NotImplementedError

class MsgHeartbeat(Message):
    def __init__(self):
        self.id = "HEARTBEAT"

    def _pack_payload(self):
        return struct.pack("9s", self.id)

class MsgRequestReport(Message):
    def __init__(self):
        self.id = "REQUEST_REPORT"

    def _pack_payload(self):
        return struct.pack("14s", self.id)

class MsgResponse(Message):
    def __init__(self, isAck, flags):
        self.id = "RESPONSE"
        self.isAck = isAck
        self.flags = flags

    def _pack_payload(self):
        return struct.pack("8sBI", self.id, self.isAck, self.flags)

class MsgSetLED(Message):
    def __init__(self, red, green, blue):
        self.id = "SET_LED"
        self.red = red
        self.green = green
        self.blue = blue

    def _pack_payload(self):
        return struct.pack("7s3B", self.id, self.red, self.green, self.blue)

class MsgFireTubeNum(Message):
    def __init__(self, tube_number):
        self.id = "FIRE_TUBE"
        self.tube_number = tube_number

    def _pack_payload(self):
        return struct.pack("9sB", self.id, self.tube_number)

class MsgReport(Message):
    def __init__(self, num_tubes, tube_state, led_color, time_since_HB):
        self.id = "REPORT"
        self.num_tubes = num_tubes
        self.tube_state = tube_state #array length num_tubes
        self.led_color = led_color #(R, G, B)
        self.time_since_HB = time_since_HB

    def _pack_payload(self):
        msg = struct.pack('6sB', self.id, self.num_tubes)
        for s in self.tube_state:
            msg += struct.pack('B', s)

        for c in self.led_color:
            msg += struct.pack('B', c)

        msg += struct.pack('I', self.time_since_HB)
        return msg

def print_in_hex(message):
    string = str(message)
    print ':'.join(x.encode('hex') for x in string)

if __name__ == "__main__":
    hb_test = MsgHeartbeat()
    print_in_hex(hb_test.pack())


    test_msg = Message()
