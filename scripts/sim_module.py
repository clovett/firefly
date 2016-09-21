from networking import Client
import time

class sim_node(object):
    def __init__(self):
        self.client = Client()

def main(sim, start_time):
    data = None
    if sim.client.connected():
        data = sim.client.receive()
        if data is not None:
            print data

        if time.time() - start_time > 0.1:
            start_time = time.time()
            msg = "boop"
            sim.client.send(msg)

if __name__ == "__main__":
    start_time = time.time()
    print "starting sim_node"
    sim = sim_node()
    done = False
    while not done:
        try:
            main(sim, start_time)
        except KeyboardInterrupt:
            done = True
            sim.client.close()
    print "exited main loop"
