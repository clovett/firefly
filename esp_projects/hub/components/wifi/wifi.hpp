#ifndef WIFI_HEADER_
#define WIFI_HEADER_

#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "esp_system.h"
#include "esp_event.h"
#include "freertos/semphr.h"
#include "lwip/ip_addr.h"

struct udp_message {
    udp_message* next;
    char* buffer;
    int len;
    ip_addr_t from;
    int port;
};

class Wifi 
{
public:
    void initialise_wifi(void);

    // these are used privately.
    void monitor();
    esp_err_t handle_event(system_event_t *event);

    xSemaphoreHandle udp_queue_mutex;
    udp_message* queue_head = NULL;
    udp_message* queue_tail = NULL;
    udp_message* queue_message(struct pbuf *pb, const ip_addr_t *addr, uint16_t port);
    udp_message* dequeue_message();
    void free_message(udp_message* msg);

};

#endif