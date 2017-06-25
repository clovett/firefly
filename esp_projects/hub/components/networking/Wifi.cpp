#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/event_groups.h"
#include "freertos/semphr.h"
#include "esp_system.h"
#include "esp_wifi.h"
#include "esp_event_loop.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "lwip/ip_addr.h"
#include "Wifi.hpp"
#include <string.h>
#include "Utils.hpp"

/* FreeRTOS event group to signal when we are connected & ready to make a request */
static EventGroupHandle_t wifi_event_group;

/* The event group allows multiple bits for each event,
   but we only care about one event - are we connected
   to the AP with an IP? */
const int CONNECTED_BIT = BIT0;

static const char *TAG = "wifi";

static esp_err_t wifi_event_handler(void *ctx, system_event_t *event)
{
    if (ctx != NULL){
        Wifi* ptr = (Wifi*)ctx;
        return ptr->handle_event(event);
    }
    return ESP_OK;
}

esp_err_t Wifi::handle_event(system_event_t *event)
{
    switch(event->event_id) {
    case SYSTEM_EVENT_STA_START:
        esp_wifi_connect();
        break;
    case SYSTEM_EVENT_STA_CONNECTED:
        /* enable ipv6 */
        tcpip_adapter_create_ip6_linklocal(TCPIP_ADAPTER_IF_STA);
        break;
    case SYSTEM_EVENT_STA_GOT_IP:
        xEventGroupSetBits(wifi_event_group, CONNECTED_BIT);
        break;
    case SYSTEM_EVENT_STA_DISCONNECTED:
        /* This is a workaround as ESP32 WiFi libs don't currently
           auto-reassociate. */
        esp_wifi_connect();
        xEventGroupClearBits(wifi_event_group, CONNECTED_BIT);
        break;
    default:
        break;
    } 
    return ESP_OK;
}

void Wifi::initialise_wifi()
{
    tcpip_adapter_init();
    
    wifi_event_group = xEventGroupCreate();
    ESP_ERROR_CHECK( esp_event_loop_init(wifi_event_handler, this) );
    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
      
    wifi_config_t wifi_config;
    strncpy((char*)wifi_config.sta.ssid, CONFIG_WIFI_SSID, sizeof(wifi_config.sta.ssid));
    strncpy((char*)wifi_config.sta.password, CONFIG_WIFI_PASSWORD, sizeof(wifi_config.sta.password));
    wifi_config.sta.bssid_set = 0;
    wifi_config.sta.bssid[0] = 0;
    wifi_config.sta.channel = 0;

    ESP_ERROR_CHECK( esp_wifi_init(&cfg) );
    ESP_ERROR_CHECK( esp_wifi_set_storage(WIFI_STORAGE_RAM) );

    ESP_LOGI(TAG, "Setting WiFi configuration SSID %s...", wifi_config.sta.ssid);
    ESP_ERROR_CHECK( esp_wifi_set_mode(WIFI_MODE_STA) );
    ESP_ERROR_CHECK( esp_wifi_set_config(ESP_IF_WIFI_STA, &wifi_config) );
    ESP_ERROR_CHECK( esp_wifi_start() );
    
    esp_err_t err = tcpip_adapter_set_hostname(TCPIP_ADAPTER_IF_STA, CONFIG_MDNS_HOSTNAME);
    if (err){        
        ESP_LOGI(TAG, "tcpip_adapter_set_hostname failed, rc=%d", err);
    }
    
    ESP_LOGI(TAG, "wifi initialization complete.");

    // wait for wifi to be connected.
    xEventGroupWaitBits(wifi_event_group, CONNECTED_BIT, false, true, portMAX_DELAY);
    ESP_LOGI(TAG, "wifi Connected to AP");

    tcpip_adapter_ip_info_t if_ip_info;
    err = tcpip_adapter_get_ip_info(TCPIP_ADAPTER_IF_STA, &if_ip_info);
    if (err) {
        ESP_LOGI(TAG, "tcpip_adapter_get_ip_info failed with error=%d", (int)err);
        return;
    }

    memcpy(&(this->local_ip), &if_ip_info.ip, sizeof(ip4_addr_t));
    this->local_ip.type = IPADDR_TYPE_V4;
    const char* addr = ipaddr_ntoa(&this->local_ip);
    ESP_LOGI(TAG, "local ip is %s", addr);

}

const char* Wifi::get_local_ip(){
    return ipaddr_ntoa(&local_ip);
}
