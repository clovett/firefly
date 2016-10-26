from networking import Client
import struct
import time

class sim_node(object):
    def __init__(self):
        self.client = Client()
        self.num_tubes = 15
        self.tube_state = [] #0 = unloaded, 1 = loaded, 2->255 = errorcode
        for i in range(self.num_tubes): #set all of the tubes to be unloaded
            self.tube_state.append(0)

        #set the LED color to the default color
        self.led_color = (255, 255, 255)

        #create a counter for the HB time
        self.time_since_HB = None

    def _generate_report(self):
        report = struct.pack("", "REPORT", self.num_tubes, self.tube_state, self.led_color[0],self.led_color[1],self.led_color[2], self.time_since_HB)
        return report


#networking test function
def main():
    start_time = time.time()
    print "starting sim_node"
    sim = sim_node()
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
    main()
