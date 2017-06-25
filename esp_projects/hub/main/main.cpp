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
#include "../components/networking/wifi.hpp"
#include "../components/networking/msgqueue.hpp"
#include "../components/led/led.hpp"
#include <string.h>

static const char *TAG = "main";
static Wifi wifi;
static LedController led;
static MessageQueue queue;
static const char *FIND_HUB_BROADCAST = "FIREFLY-FIND-HUB";
static const char *FIND_HUB_RESPONSE = "FIREFLY-HUB";
const int FireflyBroadcastPort = 13777; // the magic firefly ports
const int FireflyTcpPort = 13787; // the magic firefly ports

void run(){

    wifi.initialise_wifi(&queue);
    wifi.start_udp_broadcast_monitor(FireflyBroadcastPort);
    wifi.start_tcp_server(FireflyTcpPort);

    led.start_led_task();
    
    ESP_LOGI(TAG, "bootstrap complete.");

    char* buffer = new char[100];

    while (1) {
        Message* msg = queue.dequeue();
        if (msg != NULL)
        {
            if (strncmp(msg->payload, FIND_HUB_BROADCAST, msg->len) == 0)
            {
                const char* addr = Wifi::addr_to_string(&msg->remote_addr);
                ESP_LOGI(TAG, "hey, (%s:%d) wants to find us!", addr, msg->remote_addr.sin_port);

                addr = Wifi::addr_to_string(&msg->local_addr);
                int len = sprintf(buffer, "%s,%s,%d", FIND_HUB_RESPONSE, addr, FireflyTcpPort);

                Message response(msg, buffer, len);
                wifi.send_broadcast(&response);
                
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