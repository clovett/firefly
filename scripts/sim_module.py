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
    TIME_OUT_S = 1

    def __init__(self):
        self.client = Client()
        self.tubes = Tubes(15)

        #set the LED color to the default color
        self.led_color = (255, 255, 255)

        #create a counter for the HB time
        self.last_hb_time = 0

        #create an action handler for acting on incoming messages
        self.actions = utils.ActionHandler()
        self.actions.add_action("REQUEST_REPORT\0", self._request_report)
        self.actions.add_action("SET_LED\0", self._set_led)
        self.actions.add_action("FIRE_TUBE\0", self._fire_tube)
        self.actions.add_action("HEARTBEAT\0", self._heartbeat)
        self.actions.add_action("RESPONSE\0", self._response)
        self.actions.add_action("REPORT\0", self._report)

        #extra trackers for internal use only
        self._last_fire_time = 0
        self._LOAD_DELAY_S = 1
        self._TIME_BETWEEN_LOADS_S = 0.1
        self._last_load_time = time.time()

    def time_since_fire(self):
        return time.time() - self._last_fire_time

    def time_since_HB(self):
        actualTime = time.time() - self.last_hb_time
        return actualTime if actualTime <= 4294967295 else 4294967290

    #what do we have to do?
    def main(self):
        #we need to check the state of the tubes and update tube_state
        #for the simulation we are going to just assume that the tubes get
        #reloaded at some rate some time after the last fire command.
        self._update_tubes()

        #we need to check for and handle new messages
        if self.client.is_connected():
            incoming = self.client.receive(message.parser)
            if incoming is not None:
                self.actions.do_action(incoming.id, incoming)
        else:
            time.sleep(0.5)


    """
    Each of the "handler methods" below are for internal use only and are used
    to orginize the behivors that are linked to each incoming message type.
    """
    def _fire_tube(self, incoming):
        print "in fire tube"
        tube_number = incoming.tube_number

        if self.time_since_HB() < self.TIME_OUT_S:
            if tube_number <= self.tubes.get_num_tubes():
                success = self.tubes.fire_tube(tube_number)
                if success:
                    #print "firing tube number", tube_number
                    print self.tubes, "-", tube_number
                    self._last_fire_time = time.time()
                    response = message.MsgResponse(success, 1)
                else:
                    #print "Tube is not loaded", tube_number
                    response = message.MsgResponse(success, 4)
            else:
                #print "Tube number does not exist"
                response = message.MsgResponse(0, 2)
        else:
            #print "Heatbeat has expired"
            response = message.MsgResponse(0, 0)

        self.client.send(response)

    def _response(self, incoming):
        print "in response, code is", incoming.isAck, incoming.flags

    def _request_report(self, incoming):
        print "in request report"

        self.client.send(message.MsgResponse(1, 0))
        report_msg = message.MsgReport(self.tubes.get_num_tubes(), self.tubes.get_tubes(), self.led_color, self.time_since_HB())
        self.client.send(report_msg)

    def _set_led(self, incoming):
        print "in set led", incoming.red, incoming.green, incoming.blue

        self.client.send(message.MsgResponse(1,1))
        self.led_color = (incoming.red, incoming.green, incoming.blue)

    def _heartbeat(self, incoming):
        print "in heartbeat at time", time.time()

        self.last_hb_time = time.time()
        self.client.send(message.MsgResponse(1, 0))

    def _report(self, incoming):
        print "wtf is the master sending report messages?"

    """
    Update the state of the tubes. For the simulation all this will do is
    check for unloaded tubes and load a random tube.
    """
    def _update_tubes(self):
        if self.time_since_fire() > self._LOAD_DELAY_S:
            if time.time() - self._last_load_time > self._TIME_BETWEEN_LOADS_S:
                if self.tubes.get_num_empty() > 0:
                    tube_num = random.choice(self.tubes.get_empty_tubes())
                    self.tubes.load_tube(tube_num)
                    self._last_load_time = time.time()
                    print self.tubes, "+", tube_num

    def close(self):
        self.client.close()

def test():
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

if __name__ == "__main__":
    import threading

    def main_thread(event):
        sim = SimNode()
        while not event.is_set():
            sim.main()
        sim.close()

    nodes = []
    killEvent = threading.Event()
    for i in range(10):
        node = threading.Thread(target=main_thread, args=[killEvent])
        node.start()
        nodes.append(node)
        time.sleep(0.1)

    try:
        while 1:
            time.sleep(1)
    except KeyboardInterrupt:
        killEvent.set()
        for node in nodes:
            node.join()
    else:
        pass
