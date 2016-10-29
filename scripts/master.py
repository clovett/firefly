from networking import Server
import message, utils
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
                    heartbeat_msg = message.MsgHeartbeat().pack()
                    self.server.send_to(heartbeat_msg, con)
                    incoming = con.receive(no_wait = True)
                    if incoming is not None:
                        print incoming, "received from", con

            time.sleep(0.1)

if __name__ == "__main__":
    print "running main program"
    m = master()
    try:
        m.loop()
    except KeyboardInterrupt:
        print "halting server"
        m.server.close()
