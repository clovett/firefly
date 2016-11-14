import socket, errno, os
from Queue import Queue, Empty
from threading import Thread
import struct
import time

class TcpStream(object):
    def __init__(self, con, addr):
        self.remote_addr = addr
        self._tcp_conn = con
        self._tcp_conn.settimeout(0.1)#we need this so that the listen thread dies
        self._incoming_queue = Queue()
        self._closed = False
        self._listen_thread = Thread(target=self._listen_loop)
        self._listen_thread.start()

        self._pending_exception = None

    def __eq__(self, other):
        if other == None:
            return False
        elif self.remote_addr == other.remote_addr:
            return True
        else:
            return False

    def _listen_loop(self):
        while not self._closed:
            try:
                data = self._tcp_conn.recv(4096)
            except Exception as e:
                if not isinstance(e, socket.timeout):
                    print "networking:TcpStream:_listen_loop;", e
                    self._pending_exception = e
                    self.close()
            else:
                if data is None or len(data) == 0:
                    #if there isn't any data do nothing.
                    print "networking:TcpStream:_listen_loop; Data is none, closing stream."
                    self.close()
                else:
                    for c in data:#put the data byte by byte into the queue
                        self._incoming_queue.put(c)

    #given bytes send them out
    def write(self, data):
        try:
            return self._tcp_conn.send(data)
        except Exception:
            self.close()
            return 0

    #return a byte or bytes from the buffer, block until num_bytes are read
    def read(self, num_bytes=1):
        data = ""
        if not self._closed:
            i = 0
            while self._pending_exception == None and i < num_bytes:
                i += 1
                try:
                    #this may be where we are getting stuck in the main loop of sim node?
                    data += self._incoming_queue.get(timeout=0.1)
                except Empty:
                    #print "networking:TcpStream:read:empty exception"
                    pass
        if self._pending_exception != None:
            raise self._pending_exception
        return data

    def is_closed(self):
        return self._closed

    def close(self):
        if not self.is_closed():
            print "networking.Connection: Closing connection", self
            self._closed = True
            try:
                self._tcp_conn.shutdown(socket.SHUT_RD)
            except socket.error:
                pass
            self._tcp_conn.close()

NODE_LISTEN_PORT = 8008
BROADCAST_FORMAT_STRING = '=15si'

class Client(object):
    """
    Blocking init to establish connection with server
    """
    def __init__(self):
        self._closed = False
        self._connection = None

        #start the connection management loop
        self._connection_thread = Thread(target=self._connection_loop)
        self._connection_thread.start()

    def close(self):
        self._closed = True
        if self._connection is not None:
            self._connection.close()
            self._connection = None

    def is_connected(self):
        return self._connection != None and not self._connection.is_closed()

    """
    Send the given data out over the connection
    """
    def send(self, message):
        if self.is_connected():
            #print "client is sending:", message.id
            self._connection.write(message.pack())
        else:
            raise ValueError("networking:Client:send Client must be connected to send.")

    """
    Given a message parser, return an assembled message from the incoming bytes
    A Parser is a method that takes a bytestream and returns an object.

    This method will block based on reads from the incoming byte queue.
    """
    def receive(self, parser):
        incoming = None
        if self.is_connected():
            incoming = parser(self._connection)
        #print "client got", incoming.id
        return incoming

    """
    If no connection is found listen for broadcasts from a server.
    If a broadcast is received make the connection.
    The connection loop also checks the state of the connection.
    """
    def _connection_loop(self):
        udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        if not os.name == "nt":
            udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEPORT, 1)
        udp_socket.settimeout(1)
        udp_socket.bind(('', NODE_LISTEN_PORT))

        while not self._closed:
            if self._connection == None or self._connection.is_closed():
                self._connection = None
                try:
                    data = udp_socket.recv(4096)
                except socket.timeout:
                    print "networking:Client:_connection_loop; udp socket timeout"
                else:
                    try:
                        host, port = struct.unpack(BROADCAST_FORMAT_STRING, data)
                    except struct.error:
                        print "error trying to unpack:", data, "length:", len(data)
                    host = host.strip('\x00')
                    tcp_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

                    try:
                        tcp_socket.connect((host, port))
                    except Exception as e:
                        print "networking:Client:_connection_loop; tcp exception"
                        print e, host, port
                        host, port = None, None
                        self._connection = None
                    else:
                        self._connection = TcpStream(tcp_socket, (host, port))
                        print "New connection!" , self._connection
            else:
                time.sleep(1)


class Server(object):
    def __init__(self):
        self._closed = False

        #setup the server
        self._server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._server.settimeout(1)
        if os.name == "posix":
            self._server.bind(("", 0))
        else:
            self._server.bind((socket.gethostname(), 0))
        self._server.listen(5)

        #start the server connection thread
        self.connections = []
        self._connection_thread = Thread(target=self._connection_loop)
        self._connection_thread.start()

        #start the broadcast thread
        self._broadcast_thread = Thread(target=self._broadcast_loop)
        self._broadcast_thread.start()


    def get_connections(self):
        return self.connections

    def send_to(self, message, connection):
        #print "server sent", message.id
        return connection.write(message.pack())

    """
    Given a message parser, fetch and return the lastest data from the given connection.
    This should return a Message and will block based on the incoming message queue.
    """
    def receive_from(self, parser, connection):
        incoming = parser(connection)
        #print "server got message:", incoming.id
        return incoming

    """
    Shutdown the server and signal to all the connections that they need to close.
    """
    def close(self):
        self._closed = True
        for con in self.connections:
            con.close()
        self._server.close()

    """
    Thread for accepting incomming connections.
    """
    def _connection_loop(self):
        while not self._closed:
            try:
                con, addr = self._server.accept()
            except:
                pass
            else:
                new_connection = TcpStream(con, addr)
                print "new connection:", new_connection
                if new_connection not in self.connections:
                    self.connections.append(new_connection)

            #check the list of connections and remove any that have closed
            for con in self.connections:
                if con.is_closed():
                    self.connections.remove(con)
        print "Networking.Server: halting connection loop"

    """
    Send out the server information at a regular period
    """
    #TODO: Switch to a broadcast group to reduce network spam
    def _broadcast_loop(self):
        udp_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        while not self._closed:
            host, port = self._server.getsockname()
            msg = struct.pack(BROADCAST_FORMAT_STRING, host, port)
            print "networking:Server:broadcast msg len:",len(msg)
            udp_socket.sendto(msg, ("<broadcast>", NODE_LISTEN_PORT))
            time.sleep(1)

        print "Network.Server: halting broadcast loop"
        udp_socket.close()

if __name__ == "__main__":
    s = Server()
