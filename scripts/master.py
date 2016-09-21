from networking import Server
import time

class master(object):

    def __init__(self):
        self.server = Server()

    """
    Run a simple loop for debugging
    """
    def loop(self):
        start_time = time.time()
        while(1):
            if len(self.server.connections) > 0:
                if time.time() - start_time > 1:
                    start_time = time.time()
                    for con in self.server.connections:
                        msg = "hello at time:" + str(time.time())
                        self.server.send_to(msg, con)

                for con in self.server.connections:
                    incoming = con.receive()
                    if incoming is not None:
                        print incoming, "received from", con



if __name__ == "__main__":
    print "running main program"
    m = master()
    try:
        m.loop()
    except KeyboardInterrupt:
        m.server.close()
