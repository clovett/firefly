from networking import Server
import message, utils
import time

class master(object):

    def __init__(self):
        self.server = Server()

    def send_hb(self, connection):
        heartbeat_msg = message.MsgHeartbeat().pack()
        self.server.send_to(heartbeat_msg, connection)

    """
    Run a simple loop for debugging
    """
    def loop(self):
        print_interval = utils.IntervalChecker(1)
        print_interval.start()

        msgs_in_last_cycle = 0

        while(1):
            if len(self.server.connections) > 0:
                for i, con in enumerate(self.server.connections):
                    self.send_hb(con)
                    incoming = con.receive(no_wait = True)
                    if incoming is not None:
                        msg_id, payload = message.unpack(incoming)
                        msgs_in_last_cycle += 1
                        #print "received", msg_id, "from connection", i

                    msg = message.MsgRequestReport().pack()
                    self.server.send_to(msg, con)

            if print_interval.check():
                print "Number of connections:", len(self.server.connections)
                print "Messages in the last period:", msgs_in_last_cycle
                msgs_in_last_cycle = 0#reset the couter

            time.sleep(0.1)

if __name__ == "__main__":
    print "running main program"
    m = master()
    try:
        m.loop()
    except KeyboardInterrupt:
        print "halting server"
        m.server.close()
