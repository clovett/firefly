#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/queue.h"
#include "driver/gpio.h"
#include "esp_log.h"
#include "tubes.hpp"
#include <string.h>

static const char *TAG = "tubes";

#define GPIO_OUTPUT_PIN_SEL 0x3040F6021
#define GPIO_NUM_0     34 //Input only gpio as the sense line
#define GPIO_NUM_1     35
#define GPIO_INPUT_PIN_SEL  0xC0A609014//(((uint64_t)1<<GPIO_NUM_0) | ((uint64_t)1<<GPIO_NUM_1))
#define ESP_INTR_FLAG_DEFAULT 0

//static xQueueHandle gpio_evt_queue = NULL;

#define EN_RELAY GPIO_NUM_16

int tube_list[NUM_TUBES] = {
  17,0,5,18,19,13,14,26,33,32
};

int sense_list[NUM_TUBES] = {
    4,2,15,23,22,12,27,25,35,34
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

void sense_task(void *pvParameter)
{
    if (pvParameter != NULL){
        Tubes* controller = (Tubes*)pvParameter;
        controller->run_sensing();
    }
}

int Tubes::init()
{
    memset(&tube_state[0], 0, NUM_TUBES);

    gpio_config_t io_conf;    

    ESP_LOGI(TAG, "init");
    ESP_LOGI(TAG, "configure gpio output");

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

    ESP_LOGI(TAG, "configure gpio input");

    //interrupt of rising edge
    io_conf.intr_type = GPIO_INTR_DISABLE; // GPIO_INTR_POSEDGE;
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

    xTaskCreate(&sense_task, "sense_task", 2048, this, 5, NULL);

    ESP_LOGI(TAG, "init complete");
    return 0;
}

void Tubes::arm(bool on)
{
    gpio_set_level(EN_RELAY, on ? 1 : 0);
}

void Tubes::fire(uint bits, int burnTime)
{
    // bit mask packs the tubes we want to fire.
    ESP_LOGI(TAG, "firing tubes %x for %d ms", bits, burnTime);
    int shift = bits;
    int tube = 0;
    for(int i = 0; i < NUM_TUBES; i++) {
        int bit = (shift & 0x1);
        if (bit) {
            int io = tube_list[tube];
            gpio_set_level((gpio_num_t)io, 1);   
        }
        shift >>= 1;
        tube++;
    }
    vTaskDelay(burnTime / portTICK_PERIOD_MS);
    shift = bits;
    tube = 0;
    for(int i = 0; i < NUM_TUBES; i++) {
        int bit = (shift & 0x1);
        if (bit) {
            int io = tube_list[tube];
            gpio_set_level((gpio_num_t)io, 0);
        }
        shift >>= 1;
        tube++;
    }
}

int Tubes::get_tube_state(int tube){
    if (tube >= 0 && tube < NUM_TUBES){
        return tube_state[tube];
    }
    return 0;
}

void Tubes::run_sensing() {
    while (1) {
        for (int i = 0; i < NUM_TUBES; i++){
            tube_state[i] = gpio_get_level((gpio_num_t)sense_list[i]);
        }
        vTaskDelay(500 / portTICK_PERIOD_MS);
    }
}