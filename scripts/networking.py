import socket
from Queue import Queue
from threading import Thread
import time

class Connection(object):
    def __init__(self, con, addr):
        self.remote_addr = addr
        self._tcp_conn = tcp_conn
        self._incoming_queue = Queue()
        self._closed = False

    def __eq__(self, other):
        if self.remote_addr == other.remote_addr:
            return True
        else:
            return False

    def _listen_thread(self):
        while not self._closed:
            data = self._tcp_conn.recv(4096)
            self._incoming_queue.put(data)

    def send(self, data):
        return self._tcp_conn.send(data)

    def receive(self):
        data = None
        try:
            data = self._incoming_queue.get_nowait()
        except socket.empty:
            data = None
        return data

    def close(self):
        self._closed = True
        self._tcp_conn.close()


NODE_LISTEN_PORT = 8008

class Client(object):
    """
    Blocking init to establish connection with server
    """
    def __init__(self):
        pass

    """
    Send the given data out over the connection
    """
    def send(self, data):
        pass

    """
    Fetch and return the lastest data from the connection
    """
    def receive(self):
        pass


class Server(object):
    def __init__(self):
        self._closed = False

        #setup the server
        self._server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._server.bind((socket.gethostname(), 0))
        self._server.listen(5)

        #start the server connection thread
        self.connections = []
        self._connection_thread = Thread(target=self._connection_loop)
        self._connection_thread.start()

        #start the broadcast thread
        self._broadcast_thread = Thread(target=self._broadcast_loop)
        self._broadcast_thread.start()


    def send_to(self, data, connection):
        pass

    """
    Fetch and return the lastest data from the given connection
    """
    def receive(self, connection):
        pass

    """
    Thread for accepting incomming connections.
    """
    def _connection_loop(self):
        while not self._closed:
            con, addr = self._server.accept()
            new_connection = Connection(con, addr)
            if new_connection not in self.connections:
                self.connections.append(new_connection)

    """
    Send out the server information at a regular period
    """
    def _broadcast_loop(self):
        udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        while not self._closed:
            udp_socket.send_to(self._server.getsockname(), ("255.255.255.255", NODE_LISTEN_PORT))
            time.sleep(0.25)


if __name__ == "__main__":
    s = Server()
