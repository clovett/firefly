#ifndef UDP_MESSAGE_STREAM_HPP
#define UDP_MESSAGE_STREAM_HPP
#include "MessageQueue.hpp"

class UdpMessageStream
{
public:
    UdpMessageStream(MessageQueue* queue);

    // start listening for UDP broadcasts to the given port
    // and enqueue them on the message queue.
    void start_listening(int port);
    
    void send_to(Message* msg); // over udp

    // these are used privately.
    void post(Message* msg){
        queue->enqueue(msg);
    }

    // internal use only
    void monitor();
private:
    int port;  
    int udp_socket;
    MessageQueue* queue; 
};

#endif
