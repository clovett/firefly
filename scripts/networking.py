from socket import *
from Queue import Queue
from threading import Thread

class network(object):

    #list of ports to either send on if node or listen on if master incoming
    #connection information
    connection_ports = []

    def __init__(self, is_master = False):
        self._is_master = is_master
        self._connection_list = []
        self._message_queue = Queue()

    """
    return true if connected to at least one other instance of network
    """
    def connected(self):
        pass

    """
    Block until connected to master, or if master, until at least one node connects
    """
    def connect(self):
        pass

    """
    Send the message to the master, or if is_master send to the given destination.
    If is_master and dest is none raise value error
    """
    def send(self, msg, dest=None):
        pass

    """
    returns the oldest message in the queue, or none if the queue is empty.
    """
    def receive(self):
        pass

if __name__ == "__main__":
    net = network(is_master=False)
