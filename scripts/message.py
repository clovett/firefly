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
Take a incoming message and check to see if it has a valid crc and start byte.
If is does, return the message id and payload, otherwise raise a ValueError
"""
def unpack(message):
    #check for the correct start byte
    first_byte = struct.unpack_from("=B", message)[0]

    if first_byte == START_BYTE:
        #read the length
        payload_length = struct.unpack_from("=BH", message)[1]

        #get the crc and check that it is correct
        #add three to account for message length for start byte and payload size
        crc = struct.unpack("H", message[payload_length + 3:])[0]
        body = message[:payload_length + 3]

        if crc == calc_crc16(body):
            payload = message[3:payload_length+3]
            message_id = get_string(payload)
            #then the message checks out and we should get the id and data.

            return message_id, payload
        else:
            raise ValueError("util:unpack: CRC does not match.")
    else:
        raise ValueError("util:unpack: Startbyte (0xFE) not found.")


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

class MsgHeartbeat(Message):
    def __init__(self):
        self.id = "HEARTBEAT\0"

    def _pack_payload(self):
        print len(self.id)
        print type(self.id)
        return struct.pack("=10s", self.id)

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

class MsgFireTubeNum(Message):
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

if __name__ == "__main__":
    hb_test = MsgFireTubeNum(5)
    packed_hb = hb_test.pack()

    print unpack(packed_hb)

    test_msg = Message()
