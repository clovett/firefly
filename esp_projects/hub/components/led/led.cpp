extern "C" {
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/spi_master.h"
#include "esp_log.h"
}
#include "led.hpp"
#include <string.h>

static const char *TAG = "led";
#define GPIO_MOSI 1//3 // RX
#define GPIO_SCLK 3//1 // TX

#define NUM_LEDS 12
#define SPI_BUFLEN 4*(NUM_LEDS+2) //+1 for the leading empty header

uint8_t min(uint8_t a, uint8_t b){
    if (a < b) return a;
    return b;
}

spi_device_handle_t handle;

// This code is for controlling a strip of LED's using APA102C controller
// from www.shiji-led.com.  See datasheet:
// https://cdn-shop.adafruit.com/product-files/2343/APA102C.pdf

void led_task(void *pvParameter)
{
    if (pvParameter != NULL){
        LedController* controller = (LedController*)pvParameter;
        controller->run();
    }
}

void LedController::init()
{
    this->brightness = 0;
    this->red = 0;
    this->green = 0;
    this->blue = 0;

    ESP_LOGI(TAG, "LedController::init");
    //spi configuration and setup
    spi_bus_config_t buscfg={
        .mosi_io_num=GPIO_MOSI,
        .miso_io_num=-1,
        .sclk_io_num=GPIO_SCLK,
        .quadwp_io_num=-1,
        .quadhd_io_num=-1,
        .max_transfer_sz = 0
    };

    //Configuration for the SPI device on the other side of the bus
    spi_device_interface_config_t devcfg;
    devcfg.command_bits=0;
    devcfg.address_bits=0;
    devcfg.dummy_bits=0;
    devcfg.mode=0;
    devcfg.duty_cycle_pos=128;        //50% duty cycle
    devcfg.cs_ena_pretrans=0;
    devcfg.cs_ena_posttrans=3;        //Keep the CS low 3 cycles after transaction, to stop slave from missing the last bit when CS has less propagation delay than CLK
    devcfg.clock_speed_hz=5000000;
    devcfg.spics_io_num=-1;
    devcfg.flags = 0;
    devcfg.pre_cb = NULL;
    devcfg.post_cb = NULL;
    devcfg.queue_size=3;
    
    ESP_LOGI(TAG, "spi_bus_initialize");
    esp_err_t ret=spi_bus_initialize(HSPI_HOST, &buscfg, 1);
    if (ret != ESP_OK){
        ESP_LOGI(TAG, "spi_bus_initialize failed, ret=%d", ret);
        return;
    }

    ESP_LOGI(TAG, "spi_bus_add_device");
    ret=spi_bus_add_device(HSPI_HOST, &devcfg, &handle);
    if (ret != ESP_OK){
        ESP_LOGI(TAG, "spi_bus_add_device failed, ret=%d", ret);
        return;
    }

    xTaskCreate(&led_task, "led_task", 2048, this, 5, NULL);

}

void LedController::off() 
{
    this->cmd = AllOff;
}

void LedController::color(uint8_t brightness, uint8_t red, uint8_t green,uint8_t blue)
{
    this->brightness = min(31,brightness);
    this->red = red;
    this->green = green;
    this->blue = blue;
    this->count = 0;
    this->milliseconds = 0;
    this->cmd = SetColor;
}

void LedController::ramp(uint8_t brightness, uint8_t red, uint8_t green,uint8_t blue, int milliseconds)
{
    this->start_brightness = min(31,brightness);
    this->start_red = this->red;
    this->start_green = this->green;
    this->start_blue= this->blue;
    
    this->target_brightness = brightness;
    this->target_red = red;
    this->target_green = green;
    this->target_blue = blue;
    this->count = 0;
    this->milliseconds = milliseconds;
    this->cmd = RampColor;
}
void LedController::blink(uint8_t brightness, uint8_t red, uint8_t green,uint8_t blue, int milliseconds)
{
    this->brightness = min(31,brightness);
    this->red = red;
    this->green = green;
    this->blue = blue;
    this->count = 0;
    this->milliseconds = milliseconds;
    this->cmd = Blink;
}

void LedController::run()
{
    ESP_LOGI(TAG, "led task running.");
    while(1) {
        switch (cmd){
            case None:
                vTaskDelay(20 / portTICK_PERIOD_MS);
                break;
            case AllOff:
                allOff();
                vTaskDelay(20 / portTICK_PERIOD_MS);
                break;
            case SetColor:
                color();
                vTaskDelay(20 / portTICK_PERIOD_MS);
                break;
            case RampColor:
                ramp();
                break;
            case Blink:
                blink();
            break;
        }
    }
	vTaskDelete(NULL);
}

void LedController::allOff()
{
    this->brightness = 0;
    this->red = 0;
    this->green = 0;
    this->blue = 0;
    color();
}

uint8_t interpolate_color(uint8_t start, uint8_t end, float percent)
{
    return (uint8_t)((float)start + (((float)end - (float)start) * percent));
}

void LedController::ramp()
{
    float percent =  (float)count / (float)milliseconds;
    if (milliseconds == 0) {
        percent = 1;
    }
    this->brightness = interpolate_color(this->start_brightness, this->target_brightness, percent);
    this->red = interpolate_color(this->start_red, this->target_red, percent);
    this->green = interpolate_color(this->start_green, this->start_green, percent);
    this->blue = interpolate_color(this->start_blue, this->start_blue, percent);
    color();
    count++;
    if (count == milliseconds){
        // done!
        cmd = None;
    }

    vTaskDelay(1);
}

void LedController::blink()
{
    uint8_t a = this->brightness;
    uint8_t r = this->red;
    uint8_t g = this->green;
    uint8_t b = this->blue;

    this->brightness = 0;
    this->red = 0;
    this->green = 0;
    this->blue = 0;

    allOff();
    vTaskDelay(milliseconds / portTICK_PERIOD_MS);
    
    this->brightness = a;
    this->red = r;
    this->green = g;
    this->blue = b;

    color();
    vTaskDelay(milliseconds / portTICK_PERIOD_MS);
}

void LedController::color()
{
    uint8_t sendbuf[SPI_BUFLEN] = {0};
    spi_transaction_t t;
    memset(&t, 0, sizeof(t)); 
    memset(&sendbuf[SPI_BUFLEN-5], 0xff, 4); // end frame
    esp_err_t ret;
    const uint8_t start_bits = 0xE0;
    
    for(int i = 0; i < NUM_LEDS; i++)
    {
        int index = (i+1)*4;
        sendbuf[index] = start_bits + min(31,brightness);
        sendbuf[index + 1] = blue;
        sendbuf[index + 2] = green;
        sendbuf[index + 3] = red;
    }
    t.length = SPI_BUFLEN * 8; //BUFLEN bytes
    t.tx_buffer = sendbuf;
    ret = spi_device_transmit(handle, &t);
    if (ret != 0){        
        ESP_LOGI(TAG, "spi_device_transmit failed, ret=%d", ret);
    }
}
