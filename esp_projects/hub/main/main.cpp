/* Blink Example

This example code is in the Public Domain (or CC0 licensed, at your option.)

Unless required by applicable law or agreed to in writing, this
software is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
CONDITIONS OF ANY KIND, either express or implied.
*/
#include <stdio.h>
#include "freertos/FreeRTOS.h"
#include "esp_system.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include <lwip/init.h>
#include "../components/networking/Wifi.hpp"
#include "../components/networking/MessageQueue.hpp"
#include "../components/networking/UdpMessageStream.hpp"
#include "../components/networking/TcpMessageStream.hpp"
#include "../components/networking/Utils.hpp"
#include "../components/led/led.hpp"
#include <string.h>

static const char *TAG = "main";
static const char *FIND_HUB_BROADCAST = "FIREFLY-FIND-HUB";
static const char *FIND_HUB_RESPONSE = "FIREFLY-HUB";
const int FireflyBroadcastPort = 13777; // the magic firefly ports
const int FireflyTcpPort = 13787; // the magic firefly ports

void run(){

  Wifi wifi;
  LedController led;

  lwip_init();

  wifi.initialise_wifi();
  std::string local_ip  = wifi.get_local_ip();

  MessageQueue queue;

  UdpMessageStream udp_stream(&queue);
  udp_stream.start_listening(FireflyBroadcastPort);

  TcpMessageStream tcp_stream(&queue, local_ip);
  tcp_stream.start_listening(FireflyTcpPort);

  led.start_led_task();

  ESP_LOGI(TAG, "bootstrap complete.");

  char* buffer = new char[100];

  while (1) {
    Message* msg = queue.dequeue();
    if (msg != NULL)
    {
      if (strncmp(msg->payload, FIND_HUB_BROADCAST, msg->len) == 0)
      {
        const char* addr = addr_to_string(&msg->remote_addr);
        ESP_LOGI(TAG, "hey, (%s:%d) wants to find us!", addr, msg->remote_addr.sin_port);

        addr = local_ip.c_str();
        int len = sprintf(buffer, "%s,%s,%d", FIND_HUB_RESPONSE, addr, FireflyTcpPort);

        Message response(msg, buffer, len);
        udp_stream.send_to(&response);

      }
      delete msg;
    }
    else
    {
      vTaskDelay(200 / portTICK_PERIOD_MS);
    }
  }

  delete [] buffer;
}

extern "C"{
  void app_main()
  {
    ESP_ERROR_CHECK( nvs_flash_init() );
    run();
  }
}
