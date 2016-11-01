from networking import Server
import message, utils
import time, struct


class master(object):
    def __init__(self):
        self.server = Server()
        self.connections = self.server.get_connections()
        self.reports = {}#Report keyed on connection

    def send_hb(self, connection):
        heartbeat_msg = message.MsgHeartbeat().pack()
        connection.send(heartbeat_msg)
        response, flags = self.wait_for_response(connection)
        return response

    """
    Given a connection and a tube number attempt to fire the tube
    """
    def fire_tube(self, tube_number, connection):
        msg = message.MsgFireTubeNum(tube_number).pack()
        connection.send(msg)
        response, flags = self.wait_for_response(connection)
        return response

    """
    Given a connection, request and save the report from the node
    that contains all of the key node information
    """
    def get_report(self, connection):
        msg = message.MsgRequestReport().pack()
        connection.send(msg)
        response, flags = self.wait_for_response(connection)
        report = None
        if response == 1:
            incoming = connection.receive()
            msg_id, payload = message.unpack(incoming)
            if "REPORT" in msg_id:
                report = self._report(payload)
        self.reports[connection] = report
        return report

    def wait_for_response(self, connection):
        incoming = connection.receive()
        msg_id, payload = message.unpack(incoming)
        msg_id, response, flags = self._response(payload)
        return response, flags

    def _response(self, payload):
        msg_id, response, flags = struct.unpack("=9sBI", payload)
        msg_id = utils.get_string(msg_id)
        return msg_id, response, flags

    def _report(self, payload):
        msg_id, num_tubes = struct.unpack_from("=7sB", payload)
        msg_id = utils.get_string(msg_id)
        tubes = []
        for i in range(num_tubes):
            tubes.append(struct.unpack_from('=B', payload, 8+i)[0])
        led_color = struct.unpack_from('=3B', payload, 8+num_tubes)
        time_since_HB = struct.unpack_from("=I", payload, 8+num_tubes+3)[0]
        return msg_id, num_tubes, tubes, led_color, time_since_HB

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
                            print report[2]

                if fire_interval.check():
                    for con in self.connections:
                        num_tubes = self.reports[con][1]

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
