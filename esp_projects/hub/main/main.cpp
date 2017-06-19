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
#include "../components/wifi/wifi.hpp"
#include "../components/led/led.hpp"

static const char *TAG = "main";

void run(){

    Wifi wifi;
    wifi.initialise_wifi();

    LedController led;
    led.start_led_task();
    
    ESP_LOGI(TAG, "bootstrap complete.");
}

extern "C"{
void app_main()
{
    ESP_ERROR_CHECK( nvs_flash_init() );
    run();
}
}