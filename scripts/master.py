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
        except Exception as e:
            print "aborted fire due to error", e
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
        except Exception as e:
            print "aborted get report due to error", e
            connection.close()

    """
    Run a simple loop for debugging
    """
    def loop(self):
        heartbeat_interval = utils.IntervalChecker(0.5)
        heartbeat_interval.start()

        report_interval = utils.IntervalChecker(1)
        report_interval.start()

        stats_interval = utils.IntervalChecker(1)
        stats_interval.start()

        fire_interval = utils.IntervalChecker(10)
        fire_interval.start()

        startOfLoop = time.time()
        loopTime = 0
        heartbeat_time = 0
        reportTime = 0
        fireTime = 0
        while(1):
            loopTime = time.time() - startOfLoop
            startOfLoop = time.time()

            if stats_interval.check():
                print "Connections:", len(self.connections), "Messages:", self.messages_sent, "Loop time:", loopTime
                print "Heartbeat time:", heartbeat_time, "reportTime:", reportTime, "Fire time:", fireTime
                self.messages_sent = 0

            if len(self.connections) > 0:
                #send heartbeats to the connection
                if heartbeat_interval.check():
                    hbStartTime = time.time()
                    for i, con in enumerate(self.connections):
                        self.send_hb(con)
                    heartbeat_time = time.time() - hbStartTime

                if report_interval.check():
                    reportStartTime = time.time()
                    for con in self.connections:
                        self.get_report(con)
                        if len(self.reports) > 0:
                            report = self.reports[con]
                            #print report.num_tubes, report.time_since_HB
                    reportTime = time.time() - reportStartTime

                if fire_interval.check():
                    fireStartTime = time.time()
                    for con in self.connections:
                        try:
                            report = self.reports[con]
                        except KeyError:
                            print "report for connection", con, "is missing, fetching now."
                            self.get_report(con)
                            report = self.reports[con]
                        ready = True
                        for t in report.tube_state:
                            ready = ready and t == 1

                        if ready:
                            #fire everything!
                            for i in range(report.num_tubes):
                                self.fire_tube(i, con)
                    fireTime = time.time() - fireStartTime

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
