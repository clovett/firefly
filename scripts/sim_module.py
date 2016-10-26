from networking import Client
import struct
import time

class SimNode(object):
    def __init__(self):
        self.client = Client()
        self.num_tubes = 15
        self.tube_state = [] #0 = unloaded, 1 = loaded, 2->255 = errorcode
        for i in range(self.num_tubes): #set all of the tubes to be unloaded
            self.tube_state.append(0)

        #set the LED color to the default color
        self.led_color = (255, 255, 255)

        #create a counter for the HB time
        self.time_since_HB = pow(2, 32) - 1
        print self.time_since_HB

    #what do we have to do?
    def main(self):
        #we need to check the state of the tubes and update tube_state
        self.update_tubes()

        #we need to check for and handle new messages
        if self.client.connected():
            incoming = self.client.receive()
            self.handle(incoming)

    """
    Given an incoming message in bytes, decode the message and call the relevent handler
    """
    def handle(self, incoming):
        pass

    """
    Update the state of the tubes. For the simulation all this will do is
    check for unloaded tubes and load a random tube.
    """
    def update_tubes(self):
        pass

    def _generate_report(self):
        report = struct.pack("6sB", "REPORT", self.num_tubes)
        for i in range(self.num_tubes):
            report += struct.pack("B", self.tube_state[i])
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


if __name__ == "__main__":
    #main()
    sim = SimNode()
    report = sim._generate_report()
    print report
    print ':'.join(x.encode('hex') for x in report)
    sim.close()
