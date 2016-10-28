from networking import Client
import message
import utils
import struct
import random
import time

class Tubes(object):
    def __init__(self, num_tubes):
        self.num_tubes = num_tubes
        self.tubes = [0]*self.num_tubes #0 = unloaded, 1 = loaded, 2->255 = errorcode
        self.tubes_loaded = 0

    def __eq__(self, other):
        res = self.num_tubes == other.num_tubes
        if res:
            for i in range(self.num_tubes):
                res = res and self.tubes[i] == other.tubes[i]
        return res

    def __ne__(self, other):
        return not self == other

    def __str__(self):
        return str(self.tubes)

    def load_tube(self, tube_number):
        if self.tubes[tube_number] == 0:
            self.tubes[tube_number] = 1
            self.tubes_loaded += 1
            return 1
        else:
            print "sim_module.Tube: You can not load a loaded tube!"
            return 0

    def fire_tube(self, tube_number):
        if self.tubes[tube_number] == 1:
            self.tubes[tube_number] = 0
            self.tubes_loaded -= 1
            return 1
        else:
            print "sim_module.Tube: You can't fire a empty tube!"
            return 0

    def get_empty_tubes(self):
        empties = []
        for i in range(self.num_tubes):
            if self.tubes[i] == 0:
                empties.append(i)
        return empties

    def get_num_tubes(self):
        return self.num_tubes

    def get_tubes(self):
        tubes = list(self.tubes)
        return tubes

    def get_num_empty(self):
        return self.num_tubes - self.tubes_loaded

    def get_packed_tubes(self):
        pack = struct.pack("B", self.num_tubes)
        for i in range(self.num_tubes):
            pack += struct.pack("B", self.tubes[i])
        return pack


class SimNode(object):

    START_BYTE = 0xFE

    def __init__(self):
        self.client = Client()
        self.tubes = Tubes(15)

        #set the LED color to the default color
        self.led_color = (255, 255, 255)

        #create a counter for the HB time
        self.time_since_HB = pow(2, 32) - 1

        #create an action handler for acting on incoming messages
        self.actions = utils.ActionHandler()
        self.actions.add_action("REQUEST_REPORT", self._request_report)
        self.actions.add_action("SET_LED", self._set_led)
        self.actions.add_action("FIRE_TUBE", self._fire_tube)
        self.actions.add_action("HEARTBEAT", self._heartbeat)
        self.actions.add_action("RESPONSE", self._response)
        self.actions.add_action("REPORT", self._report)

        #extra trackers for internal use only
        self._time_since_Fire = pow(2, 32) - 1
        self._LOAD_DELAY_S = 10
        self._TIME_BETWEEN_LOADS_S = 0.1
        self._last_load_time = time.time()

    #what do we have to do?
    def main(self):
        #we need to check the state of the tubes and update tube_state
        #for the simulation we are going to just assume that the tubes get
        #reloaded at some rate some time after the last fire command.
        self._update_tubes()

        #we need to check for and handle new messages
        if self.client.connected():
            incoming = self.client.receive()
            self.handle(incoming)

    """
    Given an incoming message in bytes, decode the message and call the relevent handler.
    This is done using a dict of functions keyed to the relevent bits of the
    incoming message.
    """
    def handle(self, incoming):
        #handler reads the address, payload length, string and calcs the crc
        #then passes the payload to the relevent method to parse and act
        msg_id, payload = message.unpack(incoming)
        self.actions.do_action(msg_id, payload)

    """
    Each of the "handler methods" below are for internal use only and are used
    to orginize the behivors that are linked to each incoming message type.
    """
    def _fire_tube(self, incoming):
        print "in fire tube"
        utils.print_in_hex(incoming)

    def _response(self, incoming):
        print "in response"
        utils.print_in_hex(incoming)

    def _request_report(self, incoming):
        print "in request report"
        utils.print_in_hex(incoming)

    def _set_led(self, incoming):
        print "in set led"
        utils.print_in_hex(incoming)

    def _heartbeat(self, incoming):
        print "in heartbeat"
        utils.print_in_hex(incoming)

    def _report(self, incoming):
        print "in report"
        utils.print_in_hex(incoming)

    """
    Update the state of the tubes. For the simulation all this will do is
    check for unloaded tubes and load a random tube.
    """
    def _update_tubes(self):
        if self._time_since_Fire > self._LOAD_DELAY_S:
            if time.time() - self._last_load_time > self._TIME_BETWEEN_LOADS_S:
                if self.tubes.get_num_empty() > 0:
                    tube_num = random.choice(self.tubes.get_empty_tubes())
                    self.tubes.load_tube(tube_num)
                    self._last_load_time = time.time()

    def close(self):
        self.client.close()

if __name__ == "__main__":
    sim = SimNode()

    hb = message.MsgHeartbeat().pack()
    rqrep = message.MsgRequestReport().pack()
    resp = message.MsgResponse(1, 12).pack()
    led = message.MsgSetLED(123, 21, 56).pack()
    fire = message.MsgFireTubeNum(14).pack()
    report = message.MsgReport(15, [1]*15, (126,53,168), 1014).pack()

    messages = [hb, rqrep, resp, led, fire, report]
    for m in messages:
        sim.handle(m)

    sim.close()
    """
    try:
        sim.run()
    except KeyboardInterrupt:
        sim.close()
    else:
        pass
    """
