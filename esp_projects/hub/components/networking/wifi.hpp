#ifndef WIFI_HEADER_
#define WIFI_HEADER_

#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "esp_system.h"
#include "esp_event.h"
#include "freertos/semphr.h"
#include "lwip/ip_addr.h"
#include "msgqueue.hpp"

class Wifi 
{
public:
    void initialise_wifi(MessageQueue* queue);

    void start_udp_broadcast_monitor(int port);
    
    void start_tcp_server(int port);

    void send_broadcast(Message* msg); // over udp

    void send_response(Message* msg); // over tco

    // these are used privately.
    void post(Message* msg){
        queue->enqueue(msg);
    }
    void monitor();
    void server();
    
    esp_err_t handle_event(system_event_t *event);

    static const char* addr_to_string(struct sockaddr_in* inet);

    ip_addr_t* get_local_ip(){
        return &(local_ip);
    }
private:    
    MessageQueue* queue;    

    ip_addr_t local_ip;
    int udp_broadcast_port;
    int tcp_server_port;
    int udp_socket;
};

#endif