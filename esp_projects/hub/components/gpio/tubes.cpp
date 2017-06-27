#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "driver/gpio.h"
#include "esp_log.h"
#include "driver/spi_master.h"
#include "tubes.hpp"
#include <string.h>

static const char *TAG = "tubes";

#define GPIO_OUTPUT_PIN_SEL 0x3040F6021
#define GPIO_NUM_0     34 //Input only gpio as the sense line
#define GPIO_NUM_1     35
#define GPIO_INPUT_PIN_SEL  0xC0A609014//(((uint64_t)1<<GPIO_NUM_0) | ((uint64_t)1<<GPIO_NUM_1))
#define ESP_INTR_FLAG_DEFAULT 0

#define GPIO_MOSI 1//3 // RX
#define GPIO_SCLK 3//1 // TX

#define NUM_LEDS 12
#define SPI_BUFLEN 4*(NUM_LEDS+1) //+1 for the leading empty header

static xQueueHandle gpio_evt_queue = NULL;

#define EN_RELAY GPIO_NUM_16
#define NUM_GPIO 10

#define FUSE_BURN_TIME 500  // milliseconds

int tube_list[NUM_GPIO] = {
  17,0,5,18,19,13,14,26,33,32
};

int sense_list[NUM_GPIO] = {
  4,2,15,23,22,12,27,25,34,35
};

/*
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
            printf("GPIO[%u] intr, val: %d\n", io_num, gpio_get_level((gpio_num_t)io_num));
        }
    }
}
*/

Tubes::Tubes(){
}

int Tubes::init()
{
    esp_err_t ret;
    gpio_config_t io_conf;    

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


    ret=spi_bus_initialize(HSPI_HOST, &buscfg, 1);
    if (ret != ESP_OK){
        ESP_LOGI(TAG, "spi_bus_initialize failed, ret=%d", ret);
        return ret;
    }
    ret=spi_bus_add_device(HSPI_HOST, &devcfg, &handle);
    if (ret != ESP_OK){
        ESP_LOGI(TAG, "spi_bus_add_device failed, ret=%d", ret);
        return ret;
    }

    //disable interrupt
    io_conf.intr_type = GPIO_INTR_DISABLE;
    //set as output mode
    io_conf.mode = GPIO_MODE_OUTPUT;
    //bit mask of the pins that you want to set,e.g.GPIO18/19
    io_conf.pin_bit_mask = GPIO_OUTPUT_PIN_SEL;
    //disable pull-down mode
    io_conf.pull_down_en = GPIO_PULLDOWN_DISABLE;
    //disable pull-up mode
    io_conf.pull_up_en = GPIO_PULLUP_DISABLE;
    //configure GPIO with the given settings
    gpio_config(&io_conf);

    //interrupt of rising edge
    io_conf.intr_type = GPIO_INTR_POSEDGE;
    //bit mask of the pins, use GPIO4/5 here
    io_conf.pin_bit_mask = GPIO_INPUT_PIN_SEL;
    //set as input mode
    io_conf.mode = GPIO_MODE_INPUT;
    //enable pull-up mode
    io_conf.pull_up_en = GPIO_PULLUP_ENABLE;
    gpio_config(&io_conf);

/*
    //change gpio intrrupt type for one pin
    gpio_set_intr_type(GPIO_NUM_0, GPIO_INTR_ANYEDGE);

    //create a queue to handle gpio event from isr
    gpio_evt_queue = xQueueCreate(10, sizeof(uint32_t));
    //start gpio task
    xTaskCreate(gpio_task_example, "gpio_task_example", 2048, NULL, 10, NULL);

    //install gpio isr service
    gpio_install_isr_service(ESP_INTR_FLAG_DEFAULT);
    //hook isr handler for specific gpio pin
    gpio_isr_handler_add(GPIO_NUM_0, gpio_isr_handler, (void*) GPIO_NUM_0);
    //hook isr handler for specific gpio pin
    gpio_isr_handler_add(GPIO_NUM_1, gpio_isr_handler, (void*) GPIO_NUM_1);

    //remove isr handler for gpio number.
    gpio_isr_handler_remove(GPIO_NUM_0);
    //hook isr handler for specific gpio pin again
    gpio_isr_handler_add(GPIO_NUM_0, gpio_isr_handler, (void*) GPIO_NUM_0);
*/

    return 0;
}


void Tubes::fire(int tube)
{
    gpio_set_level(EN_RELAY, 1);

    if (tube < NUM_GPIO) {
      int io = tube_list[tube];
      color(tube, 255, 0, 0);
      gpio_set_level((gpio_num_t)io, 1);   
      vTaskDelay(FUSE_BURN_TIME / portTICK_PERIOD_MS);
      gpio_set_level((gpio_num_t)io, 0);
      color(tube, 0, 0, 0);
    }

    gpio_set_level(EN_RELAY, 0);
}

int Tubes::sense(int tube)
{
    // todo
    return 0;
}

void Tubes::color(int tube, int red, int green, int blue)
{
    char sendbuf[SPI_BUFLEN] = {0};
    spi_transaction_t t;
    memset(&t, 0, sizeof(t));
    esp_err_t ret;

    sendbuf[(tube+1)*4] = 0xFF;
    sendbuf[(tube+1)*4+1] = red;
    sendbuf[(tube+1)*4+2] = green;
    sendbuf[(tube+1)*4+3] = blue;

    t.length=SPI_BUFLEN*8; //BUFLEN bytes
    t.tx_buffer=sendbuf;
    ret=spi_device_transmit(handle, &t);
    if (ret != 0){        
        ESP_LOGI(TAG, "spi_device_transmit failed, ret=%d", ret);
    }
}
