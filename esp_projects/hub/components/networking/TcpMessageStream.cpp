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
  this->connected = false;
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

void TcpMessageStream::server()
{
  ESP_LOGI(TAG, "server running");

  struct sockaddr_in clientAddress;
  struct sockaddr_in serverAddress;
  int rc;
  int sock;
  int total = 1000;
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
    this->connected = true;
    addr = addr_to_string( &clientAddress);
    ESP_LOGI(TAG, "server connected to client: %s", addr);

    remote_ip = addr;

    // Loop reading data.
    while(1) {

      ssize_t sizeRead = recv(client_socket, data + sizeUsed, total-sizeUsed, 0);

      if (sizeRead < 0) {
        ESP_LOGE(TAG, "recv: %d %s", sizeRead, strerror(errno));
        remote_ip = "";
        break;
      }
      else if (sizeRead == 0)
      {
        ESP_LOGE(TAG, "connection closed");
        remote_ip = "";
        break;
      }
      else
      {
        int len = -1;
        for(int i = 0; i < sizeRead; i++)
        {
          if (data[sizeUsed + i] == 0){
            // this is the end of the message.
            len = sizeUsed + i + 1;
          }
        }
        sizeUsed += sizeRead;
        if (len >= 0)
        {
          ESP_LOGI(TAG, "received length of %d bytes: %s", len, data);

          Message* msg = new Message(client_socket, data, len);
          this->post(msg);
          if (sizeUsed > len){
            memcpy(data, &data[len], sizeUsed - len);
          }
          sizeUsed -= len;
        }
        if (sizeUsed == total){
          // Hmmm, we filled up the entire buffer but found no message, so this must be garbage          
          ESP_LOGI(TAG, "purging %d bytes of gibberish", total);
          sizeUsed = 0;
        }  
      }
      
    }

    this->connected = false;
    close(client_socket);
  }

  END:
  remote_ip = "";
  ESP_LOGI(TAG, "server terminating");
  free(data);
  vTaskDelete(NULL);
}

void TcpMessageStream::send_reply(Message* msg)
{   
  if (this->connected){
    char terminator[1];
    terminator[0] = 0; 

    int rc = send(msg->tcp_socket, msg->payload, msg->len, 0);
    if (rc != msg->len) {
      ESP_LOGE(TAG, "send failed: %d %s", rc, strerror(errno));
    }
    // send NULL terminator;
    rc = send(msg->tcp_socket, terminator, 1, 0);
    if (rc != 1) {
      ESP_LOGE(TAG, "send NULL terminator failed: %d %s", rc, strerror(errno));
    }
  } else {    
    ESP_LOGE(TAG, "send failed: server not connected");
  }
}
