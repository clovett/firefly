#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_log.h"
#include "UdpMessageStream.hpp"
#include <lwip/sockets.h>
#include <string.h>
#include "Utils.hpp"

static const char *TAG = "udp";

UdpMessageStream::UdpMessageStream(MessageQueue* queue)
{
    this->queue = queue;
    udp_socket = 0;
}

void udp_monitor_task(void *pvParameter)
{
    if (pvParameter != NULL){        
        UdpMessageStream* udp = (UdpMessageStream*)pvParameter;
        udp->monitor();
    }
}

void UdpMessageStream::start_listening(int port)
{
    this->port = port;
    xTaskCreate(&udp_monitor_task, "udp_monitor_task", 8000, this, 5, NULL);
}

void UdpMessageStream::monitor(){

    ESP_LOGI(TAG, "monitor running");
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
	localAddress.sin_port = htons(this->port);    

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

        //localAddress.sin_addr.s_addr = this->local_ip.u_addr.ip4.addr;
        Message* msg = new Message(&localAddress, &serverAddress, buffer, rc);
        post(msg);

	}

END:    
	free(buffer);

	vTaskDelete(NULL);

}

void UdpMessageStream::send_to(Message* msg)
{
    const char* addr = addr_to_string(&msg->remote_addr);
    ESP_LOGI(TAG, "sending message '%s' to (%s:%d)!", msg->payload, addr, msg->remote_addr.sin_port);

    int rc = sendto(udp_socket, msg->payload, msg->len, 0, (struct sockaddr*)&msg->remote_addr, sizeof(msg->remote_addr));
    if (rc != msg->len){
        ESP_LOGI(TAG, "send_to sendto returned %d: %s", rc, strerror(errno));
    }
}
