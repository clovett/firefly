import struct
from utils import *

START_BYTE = 0xFE

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

    def __init__(self):
        self.id = "MESSAGE"

    def get_message_id(self):
        return self.id

    def pack(self):
        payload = self._pack_payload()
        message = struct.pack("=BH", START_BYTE, len(payload))

        #add the payload to the outgoing message
        message += payload

        #calc the crc for the outgoing message
        crc = calc_crc16(message)
        message += struct.pack("H", crc)

        return message

    def _pack_payload(self):
        raise NotImplementedError

    def unpack(self, message_bytes):
        raise NotImplementedError

class MsgHeartbeat(Message):
    def __init__(self):
        self.id = "HEARTBEAT\0"

    def _pack_payload(self):
        return struct.pack("=10s", self.id)

    def unpack(self, payload_bytes):
        message_id = struct.unpack("=10s", payload_bytes)[0]
        if message_id == self.id:
            pass
        else:
            raise ValueError("id mis-match on message unpack")

class MsgRequestReport(Message):
    def __init__(self):
        self.id = "REQUEST_REPORT\0"

    def _pack_payload(self):
        return struct.pack("=15s", self.id)

class MsgResponse(Message):
    def __init__(self, isAck, flags):
        self.id = "RESPONSE\0"
        self.isAck = isAck
        self.flags = flags

    def _pack_payload(self):
        return struct.pack("=9sBI", self.id, self.isAck, self.flags)

class MsgSetLED(Message):
    def __init__(self, red, green, blue):
        self.id = "SET_LED\0"
        self.red = red
        self.green = green
        self.blue = blue

    def _pack_payload(self):
        return struct.pack("=8s3B", self.id, self.red, self.green, self.blue)

class MsgFireTube(Message):
    def __init__(self, tube_number):
        self.id = "FIRE_TUBE\0"
        self.tube_number = tube_number

    def _pack_payload(self):
        return struct.pack("=10sB", self.id, self.tube_number)

class MsgReport(Message):
    def __init__(self, num_tubes, tube_state, led_color, time_since_HB):
        self.id = "REPORT\0"
        self.num_tubes = num_tubes
        self.tube_state = tube_state #array length num_tubes
        self.led_color = led_color #(R, G, B)
        self.time_since_HB = time_since_HB

    def _pack_payload(self):
        msg = struct.pack('=7sB', self.id, self.num_tubes)
        for s in self.tube_state:
            msg += struct.pack('=B', s)

        for c in self.led_color:
            msg += struct.pack('=B', c)

        msg += struct.pack('=I', self.time_since_HB)
        return msg


MESSAGE_SELECT = {
    "HEARTBEAT":MsgHeartbeat,
    "REQUEST_REPORT":MsgRequestReport,
    "RESPONSE":MsgResponse,
    "SET_LED":MsgSetLED,
    "FIRE_TUBE":MsgFireTube,
    "REPORT":MsgReport
}

def parser(stream):
    #interate until a startbyte is found
    newByte = "0"
    while struct.unpack('=B', newByte)[0] != START_BYTE:
        newByte = stream.read()

    #read the next two bytes as a length
    lengthBytes = stream.read(2)
    length = struct.unpack("=H", lengthBytes)[0]

    #read the payload into a seperate buffer
    payloadBytes = stream.read(length)

    #get and calculate the checksum
    crcBytes = stream.read(2)
    crc = struct.unpack("=H", crcBytes)[0]

    calc_crc = calc_crc16(newByte + lengthBytes + payloadBytes)

    if calc_crc == crc:
        #get the id string from the message
        message_id = get_string(payloadBytes)

        #retun a constructed message from the payload
        message = MESSAGE_SELECT[message_id]()
        message.unpack(payloadBytes)
        return message
    else:
        return None

if __name__ == "__main__":
    hb_test = MsgFireTubeNum(5)
    report_test = MsgReport(15, [1]*15, (126,53,168), 1014)

    packed_hb = hb_test.pack()
    packed_report = report_test.pack()

    print unpack(packed_hb)
    print unpack(packed_report)

    test_msg = Message()
