#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/event_groups.h"
#include "esp_system.h"
#include "esp_wifi.h"
#include "esp_event_loop.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "mdns.h"
#include "wifi.hpp"
#include <string.h>

/* FreeRTOS event group to signal when we are connected & ready to make a request */
static EventGroupHandle_t wifi_event_group;

/* The event group allows multiple bits for each event,
   but we only care about one event - are we connected
   to the AP with an IP? */
const int CONNECTED_BIT = BIT0;

static const char *TAG = "firefly-wifi";

static esp_err_t wifi_event_handler(void *ctx, system_event_t *event)
{
    if (ctx != nullptr){
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

void mdns_monitor_task(void *pvParameter)
{
    if (pvParameter != NULL){
        Wifi* wifi = (Wifi*)pvParameter;
        wifi->monitor();
    }
}


void Wifi::initialise_wifi(void)
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
    
    ESP_LOGI(TAG, "wifi initialization complete.");

    xTaskCreate(&mdns_monitor_task, "mdns_monitor_task", 2048, this, 5, NULL);
}

void Wifi::monitor(){

    ESP_LOGI(TAG, "monitoring firefly host.");
    
    mdns_server_t * mdns = NULL;
    while(1) {
        /* Wait for the callback to set the CONNECTED_BIT in the event group.
        */

        // bugbug: this is an infinite wait... if ssid is not there, so we need to figure
        // out if ssid is out there, if not try and take over 'master' role and become the ssid...
        xEventGroupWaitBits(wifi_event_group, CONNECTED_BIT,
                            false, true, portMAX_DELAY);
        ESP_LOGI(TAG, "Connected to AP");

        if (!mdns) {
            esp_err_t err = mdns_init(TCPIP_ADAPTER_IF_STA, &mdns);
            if (err) {
                ESP_LOGE(TAG, "Failed starting MDNS: %u", err);
                continue;
            }

            ESP_ERROR_CHECK( mdns_set_hostname(mdns, CONFIG_MDNS_HOSTNAME) );

            // todo: figure out a way to name this instance dynamically so we don't have to compile 
            // unique instance name into each firmware (MDNS_INSTANCENAME1, MDNS_INSTANCENAME2, MDNS_INSTANCENAME3....)
            ESP_ERROR_CHECK( mdns_set_instance(mdns, CONFIG_MDNS_INSTANCENAME) );

            const char * arduTxtData[4] = {
                "board=esp32",
                "tcp_check=no",
                "ssh_upload=no",
                "auth_upload=no"
            };

            ESP_ERROR_CHECK( mdns_service_add(mdns, "_arduino", "_tcp", 3232) );
            ESP_ERROR_CHECK( mdns_service_txt_set(mdns, "_arduino", "_tcp", 4, arduTxtData) );
            ESP_ERROR_CHECK( mdns_service_add(mdns, "_http", "_tcp", 80) );
            ESP_ERROR_CHECK( mdns_service_instance_set(mdns, "_http", "_tcp", "ESP32 WebServer") );
            ESP_ERROR_CHECK( mdns_service_add(mdns, "_smb", "_tcp", 445) );
        }
        else {

            // test code...
            query_mdns_service(mdns, "esp32", NULL);
            query_mdns_service(mdns, "_arduino", "_tcp");
            query_mdns_service(mdns, "_http", "_tcp");
            query_mdns_service(mdns, "_printer", "_tcp");
            query_mdns_service(mdns, "_ipp", "_tcp");
            query_mdns_service(mdns, "_afpovertcp", "_tcp");
            query_mdns_service(mdns, "_smb", "_tcp");
            query_mdns_service(mdns, "_ftp", "_tcp");
            query_mdns_service(mdns, "_nfs", "_tcp");
        }

        vTaskDelay(200 / portTICK_PERIOD_MS);
    }
}


void Wifi::query_mdns_service(mdns_server_t * mdns, const char * service, const char * proto)
{
    // test code...
    if(!mdns) {
        return;
    }
    uint32_t res;
    if (!proto) {
        ESP_LOGI(TAG, "Host Lookup: %s", service);
        res = mdns_query(mdns, service, 0, 1000);
        if (res) {
            size_t i;
            for(i=0; i<res; i++) {
                const mdns_result_t * r = mdns_result_get(mdns, i);
                if (r) {
                    ESP_LOGI(TAG, "  %u: " IPSTR " " IPV6STR, i+1, 
                        IP2STR(&r->addr), IPV62STR(r->addrv6));
                }
            }
            mdns_result_free(mdns);
        } else {
            ESP_LOGI(TAG, "  Not Found");
        }
    } else {
        ESP_LOGI(TAG, "Service Lookup: %s.%s ", service, proto);
        res = mdns_query(mdns, service, proto, 1000);
        if (res) {
            size_t i;
            for(i=0; i<res; i++) {
                const mdns_result_t * r = mdns_result_get(mdns, i);
                if (r) {
                    ESP_LOGI(TAG, "  %u: %s \"%s\" " IPSTR " " IPV6STR " %u %s", i+1, 
                        (r->host)?r->host:"", (r->instance)?r->instance:"", 
                        IP2STR(&r->addr), IPV62STR(r->addrv6),
                        r->port, (r->txt)?r->txt:"");
                }
            }
            mdns_result_free(mdns);
        }
    }
}
