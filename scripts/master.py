from networking import network

class master(object):

    def __init__(self):
        self.network = network(is_master = True)
        self.network.connect()

if __name__ == "__main__":
    print "running main program"
    m = master()
    while(1):
        m.network.connect()
