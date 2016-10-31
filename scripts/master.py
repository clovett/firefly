from networking import Server
import message, utils
import time, struct

class master(object):

    def __init__(self):
        self.server = Server()

    def send_hb(self, connection):
        heartbeat_msg = message.MsgHeartbeat().pack()
        self.server.send_to(heartbeat_msg, connection)

    def test(self):
        #wait for a connection to the slave node
        while len(self.server.connections) == 0:
            time.sleep(1)
            print "Waiting for connection"

        connection = self.server.connections[0]
        self.tests(connection)

    def tests(self, connection):
        #now that we are connected test getting a report
        print "Testing pre-hb report request..."
        msg = message.MsgRequestReport().pack()
        self.server.send_to(msg, connection)

        #should get a positive ack, and then the report
        incoming = connection.receive()
        msg_id, payload = message.unpack(incoming)
        try:
            msg_id, response, flags = self._response(payload)
        except Exception as e:
            print "test failed with exception", e
        else:
            if msg_id == "RESPONSE" and response == 1:
                print "...positive ack received..."
            else:
                print "Test failed, ID:", msg_id, "Code:", response

        incoming = connection.receive()
        msg_id, payload = message.unpack(incoming)
        try:
            msg_id, num_tubes, tubes, led_color, time_since_HB = self._report(payload)
        except Exception as e:
            print "Test failed with exception", e
        else:
            if msg_id == "REPORT":
                print "Test passed.", time_since_HB

        #test firing a tube while no valid heartbeats have been sent
        print "Testing pre-hb fire_tube..."
        msg = message.MsgFireTubeNum(0).pack()
        self.server.send_to(msg, connection)
        response, flags = self.wait_for_response(connection)
        #print msg_id, response, flags
        if response == 0:
            print "Test passed."

        #test sending an invalid message
        print "Testing sending an invalid message..."
        msg = "bleep bloop"
        self.server.send_to(msg, connection)
        response, flags = self.wait_for_response(connection)
        #print msg_id, response, flags
        if response == 0:
            print "Test passed."


        #send a heartbeat and then test firing a tube
        print "Testing firing tube after sending HB..."
        self.send_hb(connection)
        response, flags = self.wait_for_response(connection)

        msg = message.MsgFireTubeNum(1).pack()
        self.server.send_to(msg, connection)
        response, flags = self.wait_for_response(connection)
        #print msg_id, response, flags
        if response == 1 and flags == 1:
            print "Test passed."
        else:
            print "Test failed", response, flags

        #send a heartbeat and test firing a tube that does not exist
        print "Testing firing tube that does not exist after sending HB..."
        self.send_hb(connection)
        response, flags = self.wait_for_response(connection)

        msg = message.MsgFireTubeNum(100).pack()
        self.server.send_to(msg, connection)
        response, flags = self.wait_for_response(connection)
        #print msg_id, response, flags
        if response == 0 and flags == 2:
            print "Test passed."
        else:
            print "Test failed", response, flags

        #fire an unloaded tube
        print "Testing fire command for unloaded tube..."
        time.sleep(1)#wait a bit to recover from the last tests

        #send a HB
        self.send_hb(connection)
        response, flags = self.wait_for_response(connection)

        #clear all the tubes
        for i in range(num_tubes):
            msg = message.MsgFireTubeNum(i).pack()
            self.server.send_to(msg, connection)
            response, flags = self.wait_for_response(connection)

        #send a HB
        self.send_hb(connection)
        response, flags = self.wait_for_response(connection)

        msg = message.MsgFireTubeNum(1).pack()
        self.server.send_to(msg, connection)
        response, flags = self.wait_for_response(connection)
        if response == 0 and flags == 4:
            print "Test Passed."
        else:
            print "Test failed with response", response, flags

        time.sleep(1)
        print "Tests Finished"

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
            tubes.append(struct.unpack_from('=B', payload, 8+i))
        led_color = struct.unpack_from('=3B', payload, 8+num_tubes)
        time_since_HB = struct.unpack_from("=I", payload, 8+num_tubes+3)[0]
        return msg_id, num_tubes, tubes, led_color, time_since_HB

    """
    Run a simple loop for debugging
    """
    def loop(self):
        heartbeat_interval = utils.IntervalChecker(0.1)
        heartbeat_interval.start()
        print_interval = utils.IntervalChecker(1)
        print_interval.start()

        msgs_in_last_cycle = 0

        while(1):
            if len(self.server.connections) > 0:
                #send heartbeats to the connections
                if heartbeat_interval.check():
                    for i, con in enumerate(self.server.connections):
                        self.send_hb(con)

                #check to see if any messages have been received
                for i, con in enumerate(self.server.connections):
                    incoming = con.receive(no_wait=True)
                    if incoming is not None:
                        msgs_in_last_cycle += 1


            if print_interval.check():
                print "Number of connections:", len(self.server.connections)
                print "Messages in the last period:", msgs_in_last_cycle
                msgs_in_last_cycle = 0#reset the couter

            time.sleep(0.1)

if __name__ == "__main__":
    print "running main program"
    m = master()

    m.test()

    try:
        pass
        #m.loop()
    except KeyboardInterrupt:
        print "halting server"
        m.server.close()
    else:
        m.server.close()
