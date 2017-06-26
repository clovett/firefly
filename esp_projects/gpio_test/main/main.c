/* GPIO Example
   This example code is in the Public Domain (or CC0 licensed, at your option.)
   Unless required by applicable law or agreed to in writing, this
   software is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
   CONDITIONS OF ANY KIND, either express or implied.
*/
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "driver/gpio.h"
#include "driver/spi_master.h"

/**
 * Brief:
 * This test code is testing to see if the ESP32 THING from sparkfun will work
 * well as the main controller for the Firefly fireworks system
 */

#define GPIO_OUTPUT_PIN_SEL 0x3040F6021
#define GPIO_INPUT_IO_0     34 //Input only gpio as the sense line
#define GPIO_INPUT_IO_1     35
#define GPIO_INPUT_PIN_SEL  0xC0A609014//(((uint64_t)1<<GPIO_INPUT_IO_0) | ((uint64_t)1<<GPIO_INPUT_IO_1))
#define ESP_INTR_FLAG_DEFAULT 0

#define GPIO_MOSI 1//3 // RX
#define GPIO_SCLK 3//1 // TX

#define NUM_LEDS 12
#define SPI_BUFLEN 4*(NUM_LEDS+1) //+1 for the leading empty header

static xQueueHandle gpio_evt_queue = NULL;


#define EN_RELAY 16
#define NUM_GPIO 10

int tube_list[NUM_GPIO] = {
  17,0,5,18,19,13,14,26,33,32
};

int sense_list[NUM_GPIO] = {
  4,2,15,23,22,12,27,25,34,35
};


static void IRAM_ATTR gpio_isr_handler(void* arg)
{
    uint32_t gpio_num = (uint32_t) arg;
    xQueueSendFromISR(gpio_evt_queue, &gpio_num, NULL);
}

static void gpio_task_example(void* arg)
{
    uint32_t io_num;
    for(;;) {
        if(xQueueReceive(gpio_evt_queue, &io_num, portMAX_DELAY)) {
            printf("GPIO[%d] intr, val: %d\n", io_num, gpio_get_level(io_num));
        }
    }
}

void app_main()
{
    esp_err_t ret;

    spi_device_handle_t handle;
    //spi configuration and setup
    spi_bus_config_t buscfg={
        .mosi_io_num=GPIO_MOSI,
        .miso_io_num=-1,
        .sclk_io_num=GPIO_SCLK,
        .quadwp_io_num=-1,
        .quadhd_io_num=-1
    };

    int n=0;
    char sendbuf[SPI_BUFLEN] = {0};
    spi_transaction_t t;
    memset(&t, 0, sizeof(t));

    //Configuration for the SPI device on the other side of the bus
    spi_device_interface_config_t devcfg={
        .command_bits=0,
        .address_bits=0,
        .dummy_bits=0,
        .clock_speed_hz=5000000,
        .duty_cycle_pos=128,        //50% duty cycle
        .mode=0,
        .cs_ena_posttrans=3,        //Keep the CS low 3 cycles after transaction, to stop slave from missing the last bit when CS has less propagation delay than CLK
        .queue_size=3
    };

    ret=spi_bus_initialize(HSPI_HOST, &buscfg, 1);
    assert(ret==ESP_OK);
    ret=spi_bus_add_device(HSPI_HOST, &devcfg, &handle);
    assert(ret==ESP_OK);

    gpio_config_t io_conf;
    //disable interrupt
    io_conf.intr_type = GPIO_PIN_INTR_DISABLE;
    //set as output mode
    io_conf.mode = GPIO_MODE_OUTPUT;
    //bit mask of the pins that you want to set,e.g.GPIO18/19
    io_conf.pin_bit_mask = GPIO_OUTPUT_PIN_SEL;
    //disable pull-down mode
    io_conf.pull_down_en = 0;
    //disable pull-up mode
    io_conf.pull_up_en = 0;
    //configure GPIO with the given settings
    gpio_config(&io_conf);

    //interrupt of rising edge
    io_conf.intr_type = GPIO_PIN_INTR_POSEDGE;
    //bit mask of the pins, use GPIO4/5 here
    io_conf.pin_bit_mask = GPIO_INPUT_PIN_SEL;
    //set as input mode
    io_conf.mode = GPIO_MODE_INPUT;
    //enable pull-up mode
    io_conf.pull_up_en = 1;
    gpio_config(&io_conf);

    //change gpio intrrupt type for one pin
    gpio_set_intr_type(GPIO_INPUT_IO_0, GPIO_INTR_ANYEDGE);

    //create a queue to handle gpio event from isr
    gpio_evt_queue = xQueueCreate(10, sizeof(uint32_t));
    //start gpio task
    xTaskCreate(gpio_task_example, "gpio_task_example", 2048, NULL, 10, NULL);

    //install gpio isr service
    gpio_install_isr_service(ESP_INTR_FLAG_DEFAULT);
    //hook isr handler for specific gpio pin
    gpio_isr_handler_add(GPIO_INPUT_IO_0, gpio_isr_handler, (void*) GPIO_INPUT_IO_0);
    //hook isr handler for specific gpio pin
    gpio_isr_handler_add(GPIO_INPUT_IO_1, gpio_isr_handler, (void*) GPIO_INPUT_IO_1);

    //remove isr handler for gpio number.
    gpio_isr_handler_remove(GPIO_INPUT_IO_0);
    //hook isr handler for specific gpio pin again
    gpio_isr_handler_add(GPIO_INPUT_IO_0, gpio_isr_handler, (void*) GPIO_INPUT_IO_0);

    int cnt = 0;
    int en = 0;
    int red = 0;
    int green = 0;
    int blue = 0;
    while(1) {
        vTaskDelay(100 / portTICK_RATE_MS);

        en = !en;
        gpio_set_level(EN_RELAY, en);

        red = cnt;
        blue = 255 - cnt;
        green = 0;

        cnt++;
        if (cnt > 255){
          cnt = 0;
        }

        for(int i=0;i<NUM_LEDS; i++){
          sendbuf[(i+1)*4] = 0xFF;
          sendbuf[(i+1)*4+1] = red;
          sendbuf[(i+1)*4+2] = green;
          sendbuf[(i+1)*4+3] = blue;
        }

        t.length=SPI_BUFLEN*8; //BUFLEN bytes
        t.tx_buffer=sendbuf;
        ret=spi_device_transmit(handle, &t);
        n++;

        //for(int i=0;i<NUM_GPIO;i++){
        //  vTaskDelay(10 / portTICK_RATE_MS);
        //  int io = tube_list[i];
        //  gpio_set_level(io, cnt % 2);
        //}
    }
}
