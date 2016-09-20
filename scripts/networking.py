import socket, errno
from Queue import Queue
from threading import Thread
import struct
import time

class Connection(object):
    def __init__(self, con, addr):
        self.remote_addr = addr
        self._tcp_conn = con
        self._incoming_queue = Queue()
        self._closed = False

        self._listen_thread = Thread(target=self._listen_loop)
        self._listen_thread.daemon = True
        self._listen_thread.start()

    def __eq__(self, other):
        if other == None:
            return False
        elif self.remote_addr == other.remote_addr:
            return True
        else:
            return False


    def _listen_loop(self):
        while not self._closed:
            data = self._tcp_conn.recv(4096)
            if data is not None:
                self._incoming_queue.put(data)

    def send(self, data):
        return self._tcp_conn.send(data)

    def receive(self, no_wait=False):
        data = None
        if no_wait:
            data = self._incoming_queue.get_nowait()
        else:
            data = self._incoming_queue.get()

        return data

    def close(self):
        self._closed = True
        self._tcp_conn.close()


NODE_LISTEN_PORT = 8008
BROADCAST_FORMAT_STRING = '15si'

class Client(object):
    """
    Blocking init to establish connection with server
    """
    def __init__(self):
        self._closed = False
        self._connection = None

        #start the connection management loop
        self._connection_thread = Thread(target=self._connection_loop)
        self._connection_thread.daemon = True
        self._connection_thread.start()

    def close(self):
        self._closed = True
        if self._connection is not None:
            self._connection.close()

    def connected(self):
        return self._connection != None

    """
    Send the given data out over the connection
    """
    def send(self, data):
        self._connection.send(data)

    """
    Fetch and return the lastest data from the connection
    """
    def receive(self):
        return self._connection.receive()

    """
    If no connection is found listen for broadcasts from a server.
    If a broadcast is received make the connection.
    """
    def _connection_loop(self):
        udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        udp_socket.bind(("", NODE_LISTEN_PORT))
        while not self._closed:
            if self._connection == None:
                data = udp_socket.recv(4096)
                host, port = struct.unpack(BROADCAST_FORMAT_STRING, data)
                host = host.strip('\x00')
                tcp_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

                tcp_socket.connect((host, port))
                self._connection = Connection(tcp_socket, (host, port))

            else:
                time.sleep(1)



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
        self._connection_thread.daemon = True
        self._connection_thread.start()

        #start the broadcast thread
        self._broadcast_thread = Thread(target=self._broadcast_loop)
        self._broadcast_thread.daemon = True
        self._broadcast_thread.start()


    def send_to(self, data, connection):
        try:
            return connection.send(data)
        except socket.error, v:
            errorCode = v[0]
            if errorCode == errno.EPIPE:
                print "connection", connection, "died, removing from list"
                connection.close()
                self.connections.remove(connection)

    """
    Fetch and return the lastest data from the given connection
    """
    def receive(self, connection):
        return connection.receive()

    """
    Thread for accepting incomming connections.
    """
    def _connection_loop(self):
        while not self._closed:
            con, addr = self._server.accept()
            new_connection = Connection(con, addr)
            print "new connection:", new_connection
            if new_connection not in self.connections:
                self.connections.append(new_connection)

    """
    Send out the server information at a regular period
    """
    def _broadcast_loop(self):
        udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        while not self._closed:
            host, port = self._server.getsockname()
            msg = struct.pack(BROADCAST_FORMAT_STRING, host, port)
            udp_socket.sendto(msg, ("255.255.255.255", NODE_LISTEN_PORT))
            time.sleep(1)


if __name__ == "__main__":
    s = Server()
