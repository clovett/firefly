from networking import Server
import message, utils
import time, struct

class master(object):
    def __init__(self):
        self.server = Server()
        self.connections = self.server.get_connections()
        self.reports = {}#Report keyed on connection

        self.messages_sent = 0

    def send_hb(self, connection):
        try:
            heartbeat_msg = message.MsgHeartbeat()
            self.server.send_to(heartbeat_msg, connection)
            incoming = self.server.receive_from(message.parser, connection)

            self.messages_sent += 1
        except:
            print "aborted hb due to error"
            connection.close()
    """
    Given a connection and a tube number attempt to fire the tube
    """
    def fire_tube(self, tube_number, connection):
        try:
            msg = message.MsgFireTube(tube_number)
            self.server.send_to(msg, connection)
            incoming = self.server.receive_from(message.parser, connection)

            self.messages_sent += 1
        except:
            print "aborted fire due to error"
            connection.close()

    """
    Given a connection, request and save the report from the node
    that contains all of the key node information
    """
    def get_report(self, connection):
        try:
            msg = message.MsgRequestReport()
            self.server.send_to(msg, connection)
            response = self.server.receive_from(message.parser, connection)
            report = None
            if response.isAck == 1:
                incoming = self.server.receive_from(message.parser, connection)
                if "REPORT" in incoming.id:
                    report = incoming
                    self.server.send_to(message.MsgResponse(1, 0), connection)
                    self.reports[connection] = report
                    self.messages_sent += 2
        except:
            print "aborted get report due to error"
            connection.close()

    """
    Run a simple loop for debugging
    """
    def loop(self):
        heartbeat_interval = utils.IntervalChecker(0.5)
        heartbeat_interval.start()

        report_interval = utils.IntervalChecker(1)
        report_interval.start()

        fire_interval = utils.IntervalChecker(10)
        fire_interval.start()

        while(1):
            if len(self.connections) > 0:
                #send heartbeats to the connections
                if heartbeat_interval.check():
                    for i, con in enumerate(self.connections):
                        self.send_hb(con)

                if report_interval.check():
                    print "Connections:", len(self.connections), "Messages:", self.messages_sent
                    self.messages_sent = 0

                    for con in self.connections:
                        self.get_report(con)
                        if len(self.reports) > 0:
                            report = self.reports[con]
                            #print report.num_tubes, report.time_since_HB


                if fire_interval.check():
                    print "### Fire the tubes! ###"
                    for con in self.connections:
                        report = self.reports[con]
                        ready = True
                        for t in report.tube_state:
                            ready = ready and t == 1

                        if ready:
                            #fire everything!
                            for i in range(report.num_tubes):
                                self.fire_tube(i, con)

            time.sleep(0.1)

if __name__ == "__main__":
    print "running main program"
    m = master()

    try:
        pass
        m.loop()
    except KeyboardInterrupt:
        print "halting server"
        m.server.close()
    else:
        m.server.close()
