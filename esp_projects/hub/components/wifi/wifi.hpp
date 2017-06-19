#ifndef WIFI_HEADER_
#define WIFI_HEADER_

#include "freertos/FreeRTOS.h"
#include "esp_system.h"
#include "esp_event.h"
#include "mdns.h"

class Wifi 
{
public:
     void initialise_wifi(void);

     // these are used privately.
     void monitor();
     esp_err_t handle_event(system_event_t *event);
     void query_mdns_service(mdns_server_t * mdns, const char * service, const char * proto);
};

#endif