#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "esp_log.h"
#include "TcpMessageStream.hpp"
#include <lwip/sockets.h>
#include <string.h>
#include "Utils.hpp"

static const char *TAG = "tcp";

TcpMessageStream::TcpMessageStream(MessageQueue* queue, std::string& local_ip)
{
  this->queue = queue;
  this->tcp_socket = 0;
  this->local_ip = local_ip;
}


void tcp_server_task(void *pvParameter)
{
  if (pvParameter != NULL){
    TcpMessageStream* tcp = (TcpMessageStream*)pvParameter;
    tcp->server();
  }
}


void TcpMessageStream::start_listening(int port)
{
  this->port = port;
  xTaskCreate(&tcp_server_task, "tcp_server_task", 8000, this, 5, NULL);
}

void TcpMessageStream::server(){

  ESP_LOGI(TAG, "server running");

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
  sock = ::socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
  if (sock < 0) {
    ESP_LOGE(TAG, "socket: %d %s", sock, strerror(errno));
    goto END;
  }


  // Bind our server socket to a port.
  serverAddress.sin_family = AF_INET;
  serverAddress.sin_addr.s_addr = htonl(INADDR_ANY);
  serverAddress.sin_port = htons(this->port);

  ESP_LOGI(TAG, "server port=%d, sinport=%d", this->port, (int)serverAddress.sin_port);

  rc = ::bind(sock, (struct sockaddr *)&serverAddress, sizeof(struct sockaddr_in));

  if (rc < 0) {
    ESP_LOGE(TAG, "tcp bind failed: %d %s", rc, strerror(errno));
    goto END;
  }

  // Flag the socket as listening for new connections.
  rc = ::listen(sock, 5);
  if (rc < 0) {
    ESP_LOGE(TAG, "listen failed: %d %s", rc, strerror(errno));
    goto END;
  }

  while (1) {

    // Listen for a new client connection.
    ESP_LOGI(TAG, "server accepting...");
    socklen_t clientAddressLength = sizeof(clientAddress);
    int client_socket = ::accept(sock, (struct sockaddr *)&clientAddress, &clientAddressLength);

    if (client_socket < 0) {
      ESP_LOGE(TAG, "accept failed: %d %s", client_socket, strerror(errno));
      goto END;
    }

    addr = addr_to_string( &clientAddress);
    ESP_LOGI(TAG, "server connected to client: %s", addr);

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
      while (sizeUsed > 2){
        int len = (uint8_t)data[0] + ((uint8_t)data[1] << 8);
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
  ESP_LOGI(TAG, "server terminating");
  free(data);
  vTaskDelete(NULL);
}

void TcpMessageStream::send_reply(Message* msg)
{   
  uint8_t len[2];
  len[0] = (uint8_t)(msg->len);
  len[1] = (uint8_t)(msg->len >> 8);
  int rc = send(msg->tcp_socket, (char*)&len[0], 2, 0);
  rc = send(msg->tcp_socket, msg->payload, msg->len, 0);
  if (rc != msg->len) {
    ESP_LOGE(TAG, "send failed: %d %s", rc, strerror(errno));
  }
}
