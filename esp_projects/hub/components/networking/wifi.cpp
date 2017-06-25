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
#include <lwip/sockets.h>
#include "wifi.hpp"
#include "msgqueue.hpp"
#include <string.h>

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

void udp_monitor_task(void *pvParameter)
{
    if (pvParameter != NULL){        
        Wifi* wifi = (Wifi*)pvParameter;
        wifi->monitor();
    }
}


void Wifi::initialise_wifi(MessageQueue* queue)
{
    this->queue = queue;
    udp_socket = 0;

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

void Wifi::start_udp_broadcast_monitor(int port)
{
    udp_broadcast_port = port;
    xTaskCreate(&udp_monitor_task, "udp_monitor_task", 8000, this, 5, NULL);
}

const char* Wifi::addr_to_string(struct sockaddr_in* inet){
    ip_addr_t addr;
    addr.u_addr.ip4.addr = inet->sin_addr.s_addr;
    addr.type = IPADDR_TYPE_V4;
    return ipaddr_ntoa(&addr);
}

void Wifi::monitor(){

    ESP_LOGI(TAG, "Wifi::monitor running");
    err_t rc = 0;
	struct sockaddr_in localAddress;
	struct sockaddr_in serverAddress;
    socklen_t serverAddressLength = sizeof(sockaddr_in);
    const char* addr;
    char* buffer = (char*)malloc(255);
    if (buffer == NULL){
		ESP_LOGE(TAG, "out of memory allocating udp data buffer");
		goto END;
	}

	// Create a socket that we will listen to.
    udp_socket = socket(AF_INET, SOCK_DGRAM, IPPROTO_TCP);
	if (udp_socket < 0) {
		ESP_LOGE(TAG, "socket: %d %s", udp_socket, strerror(errno));
		goto END;
	}

	// Bind our server socket to a port.
	localAddress.sin_family = AF_INET;
	localAddress.sin_addr.s_addr = htonl(INADDR_ANY);
	localAddress.sin_port = htons(udp_broadcast_port);    

	rc = bind(udp_socket, (struct sockaddr*)&localAddress, sizeof(localAddress));

	if (rc < 0) {
		ESP_LOGE(TAG, "udp bind failed: %d %s", rc, strerror(errno));
		goto END;
	}

	while (1) {

		// Listen for a new client connection.
		rc = recvfrom(udp_socket, buffer, 100, SO_BROADCAST, (struct sockaddr*)&serverAddress, &serverAddressLength);
		if (rc < 0) {
            // todo: should we ignore ECONNRESET, and EINTR ?
			ESP_LOGE(TAG, "recv failed: %d %s", udp_socket, strerror(errno));
			goto END;
		}
        
        addr = addr_to_string( &serverAddress);
        ESP_LOGI(TAG, "received msg from  %s", addr);

        localAddress.sin_addr.s_addr = this->local_ip.u_addr.ip4.addr;
        Message* msg = new Message(&localAddress, &serverAddress, buffer, rc);
        post(msg);

	}

END:    
	free(buffer);

	vTaskDelete(NULL);

}

void Wifi::send_broadcast(Message* msg)
{
    const char* addr = addr_to_string(&msg->remote_addr);
    ESP_LOGI(TAG, "sending message '%s' to (%s:%d)!", msg->payload, addr, msg->remote_addr.sin_port);

    int rc = sendto(udp_socket, msg->payload, msg->len, 0, (struct sockaddr*)&msg->remote_addr, sizeof(msg->remote_addr));
    if (rc != ERR_OK){
        ESP_LOGI(TAG, "Wifi::send_broadcast sendto failed: %d", rc);
    }
}

void tcp_server_task(void *pvParameter)
{
    if (pvParameter != NULL){        
        Wifi* wifi = (Wifi*)pvParameter;
        wifi->server();
    }
}


void Wifi::start_tcp_server(int port)
{
    tcp_server_port = port;
    xTaskCreate(&tcp_server_task, "tcp_server_task", 8000, this, 5, NULL);
}

void Wifi::server(){
    
    ESP_LOGI(TAG, "Wifi::server running");

	struct sockaddr_in clientAddress;
	struct sockaddr_in serverAddress;
    int rc;
    int sock;
    int total = 100;
    int sizeUsed = 0;
    const char* addr;
    char *data = (char*)malloc(total);

    if (data == NULL){
		ESP_LOGE(TAG, "server out of memory allocating data buffer");
		goto END;
    }

	// Create a socket that we will listen upon.
    sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
	if (sock < 0) {
		ESP_LOGE(TAG, "socket: %d %s", sock, strerror(errno));
		goto END;
	}

	// Bind our server socket to a port.
	serverAddress.sin_family = AF_INET;
	serverAddress.sin_addr.s_addr = htonl(INADDR_ANY);
	serverAddress.sin_port = htons(tcp_server_port);

	rc = bind(sock, (struct sockaddr *)&serverAddress, sizeof(serverAddress));

	if (rc < 0) {
		ESP_LOGE(TAG, "tcp bind failed: %d %s", rc, strerror(errno));
		goto END;
	}

	// Flag the socket as listening for new connections.
	rc = listen(sock, 5);
	if (rc < 0) {
		ESP_LOGE(TAG, "listen failed: %d %s", rc, strerror(errno));
		goto END;
	}

	while (1) {

		// Listen for a new client connection.
        ESP_LOGI(TAG, "Wifi::server accepting...");
		socklen_t clientAddressLength = sizeof(clientAddress);
		int client_socket = accept(sock, (struct sockaddr *)&clientAddress, &clientAddressLength);

		if (client_socket < 0) {
			ESP_LOGE(TAG, "accept failed: %d %s", client_socket, strerror(errno));
			goto END;
		}

        addr = addr_to_string( &clientAddress);
        ESP_LOGI(TAG, "Wifi::server connected to client: %s", addr);

		// Loop reading data.
		while(1) {

			ssize_t sizeRead = recv(client_socket, data + sizeUsed, total-sizeUsed, 0);

			if (sizeRead < 0) {
				ESP_LOGE(TAG, "recv: %d %s", sizeRead, strerror(errno));
				goto END;
			}

			if (sizeRead == 0) {
				break;
			}

			sizeUsed += sizeRead;
            while (sizeUsed > 4){
                int len = *(int*)&data[0];
				ESP_LOGI(TAG, "received length of: %d bytes", len);

                if (sizeUsed - 2 >= len){
                    Message* msg = new Message(client_socket, &data[2], len);
                    this->post(msg);

                    memcpy(data, &data[2 + len], sizeUsed - 2 - len);
                    sizeUsed -= (2+len);
                }
            }

		}

		free(data);

		close(client_socket);
	}

END:
    ESP_LOGI(TAG, "Wifi::server terminating");
    free(data);
	vTaskDelete(NULL);
}

void Wifi::send_response(Message* msg)
{
    int rc = send(msg->tcp_socket, msg->payload, msg->len, 0);
    if (rc != 0) {
        ESP_LOGE(TAG, "send failed: %d %s", rc, strerror(rc));
    }
}