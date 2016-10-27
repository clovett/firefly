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

def unpack(message):
    START_BYTE = 0xFE
    #check for the correct start byte
    first_byte = struck.unpack_from("B", message)
    print first_byte == START_BYTE

    #read the length


    #check that the crc is correct


    #based on the message id do the correct action
