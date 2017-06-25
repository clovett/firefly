extern "C" {
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "esp_log.h"
}
#include "led.hpp"
static const char *TAG = "led";

/* Can run 'make menuconfig' to choose the GPIO to blink,
   or you can edit the following line and set a number here.
*/
gpio_num_t BLINK_GPIO = GPIO_NUM_19;
 
void led_blink_task(void *pvParameter)
{
    if (pvParameter != NULL){
        LedController* controller = (LedController*)pvParameter;
        controller->run();
    }
}

void LedController::run()
{
    ESP_LOGI(TAG, "led task running.");
    /* Configure the IOMUX register for pad BLINK_GPIO (some pads are
       muxed to GPIO on reset already, but some default to other
       functions and need to be switched to GPIO. Consult the
       Technical Reference for a list of pads and their default
       functions.)
    */
    gpio_pad_select_gpio(BLINK_GPIO);
    /* Set the GPIO as a push/pull output */
    gpio_set_direction(BLINK_GPIO, GPIO_MODE_OUTPUT);
    while(1) {
        /* Blink off (output low) */
        gpio_set_level(BLINK_GPIO, 0);
        vTaskDelay(200 / portTICK_PERIOD_MS);
        /* Blink on (output high) */
        gpio_set_level(BLINK_GPIO, 1);
        vTaskDelay(200 / portTICK_PERIOD_MS);
    }
	vTaskDelete(NULL);
}

void LedController::start_led_task()
{
    xTaskCreate(&led_blink_task, "led_blink_task", 2048, this, 5, NULL);
}

