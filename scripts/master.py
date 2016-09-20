from networking import network
import threading

class master(object):

    def __init__(self):
        self.network = network(is_master = True)
        self.network.connect()

    """
    Run a simple loop for debugging
    """
    def loop(self):
        while(1):
            #check for new connections
            self.network.connect()



if __name__ == "__main__":
    print "running main program"
    m = master()
    m.loop()
