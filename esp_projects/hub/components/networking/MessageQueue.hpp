#ifndef MSGQUEUE_HEADER_
#define MSGQUEUE_HEADER_

#include "freertos/FreeRTOS.h"
#include "lwip/inet.h"
#include <lwip/sockets.h>

class Message {
public:
    Message(struct sockaddr_in *localaddr, struct sockaddr_in *remoteaddr, const char* payload, int len);

    Message(Message* replyTo, const char* payload, int len);

    Message(int tcp_socket, const char* payload, int len);

    ~Message();

    bool is_tcp() {
        return tcp_socket != 0;
    }

    char* payload;
    int len;
    int tcp_socket; // if this is a tcp message
    struct sockaddr_in local_addr;// if this is a udp message
    struct sockaddr_in remote_addr; 
};

// This class is used to receive async messages from the networking layer.
class MessageQueue
{
private:
    xSemaphoreHandle queue_mutex;
    struct entry {
        Message* msg;
        entry* next;
    };
    
    entry* queue_head = NULL;
    entry* queue_tail = NULL;
public:
    MessageQueue();
    ~MessageQueue();

    void enqueue(Message* msg);
    Message* dequeue();
};

#endif