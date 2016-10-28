import struct

class ActionHandler(object):
    def __init__(self):
        self.actions = {}

    def add_action(self, action, key):
        self.actions[key] = action

    def do_action(self, key, args):
        self.actions[key](args)

def calc_crc16(message):
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

def print_in_hex(message):
    string = str(message)
    print ':'.join(x.encode('hex') for x in string)

def get_string(buf):
    string = ""
    for c in buf:
        if c == '\0':
            break
        else:
            string += c
    return string

if __name__=="__main__":
    print "utils.py"
