#ifndef TCP_MESSAGE_STREAM_HPP
#define TCP_MESSAGE_STREAM_HPP
#include "MessageQueue.hpp"
#include <string>

class TcpMessageStream
{
public:
    TcpMessageStream(MessageQueue* queue, std::string& local_ip);

    // start listening for TCP connections on the given port
    // then read from that connection and post messages to the queue
    void start_listening(int port);
    
    // When message is received use this method to send a reply back
    // to the sender. 
    void send_reply(Message* msg); 

    // these are used privately.
    void post(Message* msg){
        queue->enqueue(msg);
    }

    // internal use only
    void server();
private: 
    std::string local_ip;
    int port;  
    int tcp_socket;
    MessageQueue* queue; 
};

#endif