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
#include "lwip/pbuf.h"
#include "lwip/igmp.h"
#include "lwip/udp.h"
#include "wifi.hpp"
#include <string.h>

/* FreeRTOS event group to signal when we are connected & ready to make a request */
static EventGroupHandle_t wifi_event_group;

/* The event group allows multiple bits for each event,
   but we only care about one event - are we connected
   to the AP with an IP? */
const int CONNECTED_BIT = BIT0;

static const char *TAG = "firefly-wifi";
static const char *FIND_HUB_BROADCAST = "FIREFLY-FIND-HUB";
static const char *FIND_HUB_RESPONSE = "FIREFLY-HUB";
const int FireflyBroadcastPort = 13777; // the magic firefly ports
const int FireflyTcpPort = 13787; // the magic firefly ports

#define MAX_MUTEX_WAIT_MS       1000
#define MAX_MUTEX_WAIT_TICKS    (MAX_MUTEX_WAIT_MS / portTICK_PERIOD_MS)

udp_message* Wifi::queue_message(struct pbuf *pb, const ip_addr_t *addr, uint16_t port)
{
    if (udp_queue_mutex == NULL){
        ESP_LOGI(TAG, "queue_message called without udp_queue_mutex");
        return NULL;
    }
    udp_message* result = (udp_message*)malloc(sizeof(udp_message));
    if (!result) {
        ESP_LOGI(TAG, "out of memory allocating udp_message");
        return 0;
    }
    result->next = NULL;
    result->buffer = (char*)malloc(pb->len);
    if (!result) {
        free(result);        
        ESP_LOGI(TAG, "out of memory allocating udp_message buffer of len=%d", pb->len);
        return 0;
    }
    memcpy(result->buffer, pb->payload, pb->len);
    result->len = pb->len;
    memcpy(&result->from, addr, sizeof(ip_addr_t));
    result->port = port;

    xSemaphoreTake(udp_queue_mutex, MAX_MUTEX_WAIT_TICKS);
    if (queue_tail == NULL){
        queue_head = queue_tail = result;
    }
    else {
        queue_tail->next = result;
        queue_tail = result;
    }
    xSemaphoreGive(udp_queue_mutex);
    return result;
}

udp_message* Wifi::dequeue_message()
{    
    if (udp_queue_mutex == NULL){
        ESP_LOGI(TAG, "dequeue_message called without udp_queue_mutex");
        return NULL;
    }
    udp_message* result = NULL;    
    xSemaphoreTake(udp_queue_mutex, MAX_MUTEX_WAIT_TICKS);
    if (queue_head != NULL){
        result = queue_head;
        queue_head = queue_head->next;
        if (queue_head == NULL){
            queue_tail = NULL;
        }
    }
    xSemaphoreGive(udp_queue_mutex);
    return result; 
}

void Wifi::free_message(udp_message* msg)
{
    if (msg->buffer != NULL){
        free(msg->buffer);
    }
    free(msg);
}


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

void udp_monitor_task(void *pvParameter)
{
    if (pvParameter != NULL){        
        Wifi* wifi = (Wifi*)pvParameter;
        wifi->monitor();
    }
}


void Wifi::initialise_wifi(void)
{
    queue_head = NULL;
    queue_tail = NULL;

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

    udp_queue_mutex = xSemaphoreCreateMutex();
    if (!udp_queue_mutex){        
        ESP_LOGI(TAG, "failed to create udp_queue_mutex.");
    }

    xTaskCreate(&udp_monitor_task, "udp_monitor_task", 8000, this, 5, NULL);

}

static void udp_receiver(void *arg, struct udp_pcb *upcb, struct pbuf *pb, const ip_addr_t *addr, uint16_t port)
{
    Wifi* wifi = (Wifi*)arg;
    while(pb != NULL) {
        struct pbuf * this_pb = pb;
        pb = pb->next;
        this_pb->next = NULL;
        wifi->queue_message(this_pb, addr, port);
        pbuf_free(this_pb);
    }
}

void Wifi::monitor(){

    esp_err_t err = ESP_OK;
    
    // wait for wifi to be connected.
    xEventGroupWaitBits(wifi_event_group, CONNECTED_BIT, false, true, portMAX_DELAY);
    ESP_LOGI(TAG, "Connected to AP");

    tcpip_adapter_ip_info_t if_ip_info;
    err = tcpip_adapter_get_ip_info(TCPIP_ADAPTER_IF_STA, &if_ip_info);
    if (err) {
        ESP_LOGI(TAG, "tcpip_adapter_get_ip_info failed with error=%d", (int)err);
        return;
    }

    struct udp_pcb * pcb = udp_new_ip_type(IPADDR_TYPE_ANY);
    if (!pcb) {
        return;
    }
    err_t rc = udp_bind(pcb, IP_ANY_TYPE, FireflyBroadcastPort);
    if (rc != ERR_OK) {
        ESP_LOGI(TAG, "Wifi::monitor bind failed: %d", rc);
        udp_remove(pcb);
        return;
    }

    ESP_LOGI(TAG, "start listening...");
    udp_recv(pcb, &udp_receiver, this);    

    char* buffer = (char*)malloc(100);
    
    while (1) {
        udp_message* msg = dequeue_message();
        if (msg != NULL)
        {
            if (strncmp(msg->buffer, FIND_HUB_BROADCAST, msg->len) == 0)
            {
                const char* addr = ipaddr_ntoa(&msg->from);
                ESP_LOGI(TAG, "hey, (%s:%d) wants to find us!", addr, msg->port);
                
                int len = sprintf(buffer, "%s,%s,%d", FIND_HUB_RESPONSE,addr, FireflyTcpPort);
                struct pbuf* pbt = pbuf_alloc(PBUF_TRANSPORT, len + 1, PBUF_RAM);
                memcpy(pbt->payload, buffer, len);

                rc = udp_sendto(pcb, pbt, &(msg->from), msg->port);
                if (rc != ERR_OK){
                    ESP_LOGI(TAG, "Wifi::monitor udp_sendto: %d", rc);
                }
                pbuf_free(pbt);
            }
            free_message(msg);
        }
        else
        {
            vTaskDelay(200 / portTICK_PERIOD_MS);
        }
    }

    free(buffer);
    udp_remove(pcb);
}
