#include "freertos/FreeRTOS.h"
#include <lwip/sockets.h>
#include "Utils.hpp"


const char* addr_to_string(struct sockaddr_in* inet){
    ip_addr_t addr;
    addr.u_addr.ip4.addr = inet->sin_addr.s_addr;
    addr.type = IPADDR_TYPE_V4;
    return ipaddr_ntoa(&addr);
}