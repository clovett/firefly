#ifndef WIFI_HEADER_
#define WIFI_HEADER_

#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "esp_system.h"
#include "esp_event.h"
#include "freertos/semphr.h"
#include "lwip/ip_addr.h"

class Wifi 
{
public:
    void initialise_wifi();
    
    esp_err_t handle_event(system_event_t *event);

    const char* get_local_ip();
private:
    ip_addr_t local_ip;
};

#endif