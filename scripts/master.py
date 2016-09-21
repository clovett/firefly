from networking import Server
import time

class master(object):

    def __init__(self):
        self.server = Server()

    """
    Run a simple loop for debugging
    """
    def loop(self):
        while(1):
            if len(self.server.connections) > 0:
                for con in self.server.connections:
                    msg = "hello at time:" + str(time.time())
                    self.server.send_to(msg, con)

                    incoming = con.receive()
                    if incoming is not None:
                        print incoming, "received from", con

            time.sleep(1)



if __name__ == "__main__":
    print "running main program"
    m = master()
    try:
        m.loop()
    except KeyboardInterrupt:
        m.server.close()
