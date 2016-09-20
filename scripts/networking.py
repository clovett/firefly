import socket
from Queue import Queue
from threading import Thread
import time

class connection(object):
    def __init__(self, tcp_conn):
        self._tcp_conn = tcp_conn
        self._incoming_queue = Queue()
        self._closed = False

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


class network(object):
    def __init__(self, is_master = False):
        self._is_master = is_master
        self._connection_list = []
        self._message_queue = Queue()

        if self._is_master:
            self.server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.server.listen(5)

    """
    return true if connected to at least one other instance of network
    """
    def connected(self):
        return len(self._connection_list) > 0

    """
    Block until connected to master, or if master, do a non-blocking check for
    nodes that are note yet in the connection list.
    """
    def connect(self):
        if self._is_master:
            self._master_connect()
        else:
            self._node_connect()

    """
    send a message with the master server information
    """
    def master_broadcast(self):
        pass


    def _master_connect(self):
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.bind(('', self.MASTER_LISTEN_PORT))
        sock.settimeout(1)

        try:
            data, (ip, port) = sock.recvfrom(1024)
        except socket.timeout:
            pass
        else:
            if data == self.NODE_CONNECTION_MSG:
                tcp_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                tcp_sock.connect((ip, self.NODE_SERVER_PORT))

                #set the connection for that ip to the tcp connection
                print "new connection", tcp_sock
                self._connection_list[ip] = tcp_sock
        finally:
            sock.close()


    def _node_connect(self):
        if not self.connected():
            udp_sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            udp_sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)

            tcp_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            node_addr = tcp_sock.bind(('', self.NODE_SERVER_PORT))
            tcp_sock.listen(5)
            tcp_sock.settimeout(1)

            done = False
            while not done:
                udp_sock.sendto(self.NODE_CONNECTION_MSG, ('255.255.255.255', self.MASTER_LISTEN_PORT))
                try:
                    sock, (ip, port) = tcp_sock.accept()
                except socket.timeout:
                    pass
                else:
                    done = True
                    self._connection_list[ip] = tcp_sock

    """
    Send the message to the master, or if is_master send to the given destination.
    If is_master and dest is none raise value error
    """
    def send(self, msg, dest=None):
        #check to see if we have any valid connections raise an error if none

        #
        pass

    """
    returns the oldest message in the queue, or none if the queue is empty.
    """
    def receive(self):
        return self._message_queue.get()

if __name__ == "__main__":
    net = network(is_master=False)
