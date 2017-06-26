#ifndef WIFI_HEADER_
#define WIFI_HEADER_

#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "esp_system.h"
#include "esp_event.h"
#include "freertos/semphr.h"
#include "lwip/ip_addr.h"
#include <string>

class Wifi
{
public:
    void initialise_wifi();

    esp_err_t handle_event(system_event_t *event);

    std::string get_local_ip();
private:
    std::string local_ip;
};

#endif
