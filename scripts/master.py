from networking import Server
import message, utils
import time, struct

class master(object):
    def __init__(self):
        self.server = Server()
        self.connections = self.server.get_connections()
        self.reports = {}#Report keyed on connection

    def send_hb(self, connection):
        heartbeat_msg = message.MsgHeartbeat()
        self.server.send_to(heartbeat_msg, connection)
        incoming = self.server.receive_from(message.parser, connection)

    """
    Given a connection and a tube number attempt to fire the tube
    """
    def fire_tube(self, tube_number, connection):
        msg = message.MsgFireTube(tube_number)
        self.server.send_to(msg, connection)
        incoming = self.server.receive_from(message.parser, connection)

    """
    Given a connection, request and save the report from the node
    that contains all of the key node information
    """
    def get_report(self, connection):
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
        return report

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
                    for con in self.connections:
                        if len(self.reports) > 0:
                            old_report = self.reports[con]
                        else:
                            old_report = None
                        report = self.get_report(con)
                        if report != old_report:
                            print report.num_tubes, report.time_since_HB

                if fire_interval.check():
                    for con in self.connections:
                        num_tubes = self.reports[con].num_tubes

                        #fire everything!
                        for i in range(num_tubes):
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
