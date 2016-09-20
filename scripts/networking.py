from socket import *
from Queue import Queue
from threading import Thread
import time

class network(object):

    MASTER_LISTEN_PORT = 8008

    def __init__(self, is_master = False):
        self._is_master = is_master
        self._connection_list = []
        self._message_queue = Queue()

    """
    return true if connected to at least one other instance of network
    """
    def connected(self):
        return len(self._connection_list) > 0

    """
    Block until connected to master, or if master, until at least one node connects
    """
    def connect(self):
        if self._is_master:
            self._master_connect()
        else:
            self._node_connect()

    def _master_connect(self):
        sock = socket(AF_INET, SOCK_DGRAM)
        sock.bind(('', self.MASTER_LISTEN_PORT))
        data, addr = sock.recvfrom(1024)

        print "got data:", data, "from ", addr

    def _node_connect(self):
        if not self.connected():
            sock = socket(AF_INET, SOCK_DGRAM)
            sock.setsockopt(SOL_SOCKET, SO_BROADCAST, 1)

            done = False
            while not done:
                time.sleep(1)
                sock.sendto("hello", ('255.255.255.255', self.MASTER_LISTEN_PORT))


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
