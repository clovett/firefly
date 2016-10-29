import struct, time

"""
A simple wrapper class for a dict of methods.
The idea is to be able to switch between a number of behivors based
on a common input in a more readable way than a naked dict.
"""
class ActionHandler(object):
    def __init__(self):
        self.actions = {}

    def add_action(self, key, action):
        self.actions[key] = action

    def do_action(self, key, args):
        self.actions[key](args)

"""
the interval checker class checks to see if a set period of time
has elapsed. Note that it is subject to jitter based on the reset
counter only being reset when it is checked, so if it is checked slower
than the set period it may be off by up to 0.99 periods.
"""
class IntervalChecker(object):
    def __init__(self, period):
        self.period = period
        self.last_time = None

    def start(self):
        self.last_time = time.time()

    def check(self, reset=True):
        check_time = time.time()
        isDone = check_time - self.last_time >= self.period
        if isDone and reset:#TODO: Add correction for checking at less that period rate.
            self.last_time = time.time()
        return isDone

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
