from networking import Client
import time

class sim_node(object):
    def __init__(self):
        self.client = Client()

def main(sim):
    data = None
    if sim.client.connected():
        data = sim.client.receive()
        if data is not None:
            print data

if __name__ == "__main__":
    print "starting sim_node"
    sim = sim_node()
    done = False
    while not done:
        try:
            main(sim)
        except KeyboardInterrupt:
            done = True
            sim.client.close()
    print "exited main loop"
