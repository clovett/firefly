from networking import network

class sim_node(object):
    def __init__(self):
        self.network = network()
        self.network.connect()


if __name__ == "__main__":
    print "starting sim_node"
    sim = sim_node()
