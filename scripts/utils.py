import struct


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
    pass
    #check for the correct start byte

    #read the length


    #check that the crc is correct


    #based on the message id do the correct action
