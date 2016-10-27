from networking import Client
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
    def __init__(self):
        self.client = Client()
        self.tubes = Tubes(15)

        #set the LED color to the default color
        self.led_color = (255, 255, 255)

        #create a counter for the HB time
        self.time_since_HB = pow(2, 32) - 1

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

    def main_loop(self):
        done = False
        while not done:
            try:
                self.main()
            except KeyboardInterrupt:
                done = True
                self.close()
            else:
                pass

    """
    Given an incoming message in bytes, decode the message and call the relevent handler.
    This is done using a dict of functions keyed to the relevent bits of the
    incoming message.
    """
    def handle(self, incoming):
        messages_dir = {
                    "REQUEST_REPORT":,
                    "SET_LED":,
                    "FIRE_TUBE":,
                    "HEARTBEAT":self._heartbeat,
                    "RESPONSE":self._response
        }
        pass

    def _fire_tube(self, incoming):
        pass

    def _response(self, incoming):
        pass

    def _request_report(self, incoming):
        pass

    def _set_led(self, incoming):
        pass

    def _heartbeat(self, incoming):
        pass

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

    def _generate_report(self):
        report = struct.pack("6s", "REPORT")
        report += self.tubes.get_packed_tubes()
        report += struct.pack("3BI", self.led_color[0], self.led_color[1], self.led_color[2], self.time_since_HB)
        return report

    def close(self):
        self.client.close()

#networking test function
def main():
    start_time = time.time()
    print "starting sim_node"
    sim = SimNode()
    done = False
    while not done:
        try:
            data = None
            if sim.client.connected():
                data = sim.client.receive()
                if data is not None:
                    print data

                if time.time() - start_time > 0.1:
                    start_time = time.time()
                    msg = "boop"
                    sim.client.send(msg)
        except KeyboardInterrupt:
            done = True
            sim.client.close()
    print "exited main loop"


def test_report(sim):
    report = sim._generate_report()
    print report
    print ':'.join(x.encode('hex') for x in report)

def test_update_tubes(sim):
    done = False
    while not done:
        sim.update_tubes()
        print sim.tubes
        time.sleep(0.1)
        done = sim.tubes.get_num_empty() == 0

if __name__ == "__main__":
    try:
        #main()
        sim = SimNode()
        test_report(sim)
        test_update_tubes(sim)
        sim.close()
    except KeyboardInterrupt:
        sim.close()
    else:
        pass
