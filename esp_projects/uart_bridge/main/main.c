/* MDNS-SD Query and advertise Example + UART + TCP

 This example code is in the Public Domain (or CC0 licensed, at your option.)

 Unless required by applicable law or agreed to in writing, this
 software is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 CONDITIONS OF ANY KIND, either express or implied.

 This code has been modified to read in a uart stream and repeate it to a connected tcp client.
 Zach Lovett 1/30/17

 */
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/event_groups.h"
#include "esp_system.h"
#include "esp_wifi.h"
#include "esp_event_loop.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "mdns.h"

#include "driver/uart.h"
#include "soc/uart_struct.h"

#include "lwip/err.h"
#include "lwip/sockets.h"
#include "lwip/sys.h"


#define WIFI_SSID CONFIG_WIFI_SSID
#define WIFI_PASS CONFIG_WIFI_PASSWORD

#define MDNS_HOSTNAME CONFIG_HOSTNAME
#define MDNS_INSTANCE "esp_uart"


/* FreeRTOS event group to signal when we are connected & ready to make a request */
static EventGroupHandle_t wifi_event_group;
static EventGroupHandle_t event_group;

static SemaphoreHandle_t bufferSwapSemaphore;
static uint8_t* bufferA;
static uint8_t* bufferB;
volatile bool bufferToggle;
volatile size_t bufferLen;


/* The event group allows multiple bits for each event,
 but we only care about one event - are we connected
 to the AP with an IP? */
const int CONNECTED_BIT = BIT0;

const int UART_DATA_READY_BIT = BIT0;
const int NEW_UART_DATA_BIT = BIT1;

const int TCP_CONNECTED = BIT2;
const int TCP_DISCONECTED = BIT3;

static const char *TAG = "mdns+uart-test";

static esp_err_t event_handler(void *ctx, system_event_t *event)
{
    switch(event->event_id) {
        case SYSTEM_EVENT_STA_START:
            esp_wifi_connect();
            break;
        case SYSTEM_EVENT_STA_CONNECTED:
            /* enable ipv6 */
            ESP_LOGI(TAG, "Wifi Connected");
            tcpip_adapter_create_ip6_linklocal(TCPIP_ADAPTER_IF_STA);
            break;
        case SYSTEM_EVENT_STA_GOT_IP:
            xEventGroupSetBits(wifi_event_group, CONNECTED_BIT);
            ESP_LOGI(TAG, "Got IP");
            break;
        case SYSTEM_EVENT_STA_DISCONNECTED:
            /* This is a workaround as ESP32 WiFi libs don't currently
             auto-reassociate. */
            ESP_LOGI(TAG, "Wifi disconnected");
            esp_wifi_connect();
            xEventGroupClearBits(wifi_event_group, CONNECTED_BIT);
            break;
        default:
            break;
    }
    return ESP_OK;
}

static void initialise_wifi(void)
{
    tcpip_adapter_init();
    wifi_event_group = xEventGroupCreate();
    ESP_ERROR_CHECK( esp_event_loop_init(event_handler, NULL) );
    wifi_init_config_t cfg = WIFI_INIT_CONFIG_DEFAULT();
    ESP_ERROR_CHECK( esp_wifi_init(&cfg) );
    ESP_ERROR_CHECK( esp_wifi_set_storage(WIFI_STORAGE_RAM) );
    wifi_config_t wifi_config = {
        .sta = {
            .ssid = WIFI_SSID,
            .password = WIFI_PASS,
        },
    };
    ESP_LOGI(TAG, "Setting WiFi configuration SSID %s...", wifi_config.sta.ssid);
    ESP_ERROR_CHECK( esp_wifi_set_mode(WIFI_MODE_STA) );
    ESP_ERROR_CHECK( esp_wifi_set_config(ESP_IF_WIFI_STA, &wifi_config) );
    ESP_ERROR_CHECK( esp_wifi_start() );
}

static void mdns_task(void *pvParameters)
{
    mdns_server_t * mdns = NULL;
    while(1) {
        /* Wait for the callback to set the CONNECTED_BIT in the
         event group.
         */
        xEventGroupWaitBits(wifi_event_group, CONNECTED_BIT,
                            false, true, portMAX_DELAY);

        if (!mdns) {
            ESP_LOGI(TAG, "Starting mdns server");
            esp_err_t err = mdns_init(TCPIP_ADAPTER_IF_STA, &mdns);
            if (err) {
                ESP_LOGE(TAG, "Failed starting MDNS: %u", err);
                continue;
            }

            ESP_ERROR_CHECK( mdns_set_hostname(mdns, MDNS_HOSTNAME) );
            ESP_ERROR_CHECK( mdns_set_instance(mdns, MDNS_INSTANCE) );


            const char * uartTxtData[4] = {
                "Board=ESP32Thing",
                "Use=Testing",
                "Sensor=UART",
                "Param=Hello!"
            };

            ESP_ERROR_CHECK( mdns_service_add(mdns, "_uart", "_tcp", 5001) );
            ESP_ERROR_CHECK( mdns_service_instance_set(mdns, "_uart", "_tcp", "ESP32_UART_Bridge") );
            ESP_ERROR_CHECK( mdns_service_txt_set(mdns, "_uart", "_tcp", 4, uartTxtData) );
        }
        vTaskDelay(10000 / portTICK_PERIOD_MS);
    }
}


/**
 * This example shows how to configure uart settings and install uart driver.
 *
 * uart_echo_test() is an example that read and write data on UART1, and handler some of the special events.
 * - port: UART1
 * - rx WIFI_SSIDfer: on
 * - tx buffer: off
 * - flow control: on
 * - event queue: off
 * - pin assignment: txd(io4), rxd(io5), rts(18), cts(19)
 *
 */

#define BUF_SIZE (1024)
#define ECHO_TEST_TXD  (4)
#define ECHO_TEST_RXD  (5)
#define ECHO_TEST_RTS  (18)
#define ECHO_TEST_CTS  (19)

QueueHandle_t uart1_queue;
void uart_task(void *pvParameters)
{
    int uart_num = (int) pvParameters;
    uart_event_t event;
    uint8_t* dtmp = (uint8_t*) malloc(BUF_SIZE);
    for(;;) {
        //Waiting for UART event.
        if(xQueueReceive(uart1_queue, (void * )&event, (portTickType)portMAX_DELAY)) {
            //ESP_LOGI(TAG, "uart[%d] event:", uart_num);
            switch(event.type) {
                    //Event of UART receving data
                    /*We'd better handler data event fast, there would be much more data events than
                     other types of events. If we take too much time on data event, the queue might
                     be full.
                     in this example, we don't process data in event, but read data outside.*/
                case UART_DATA:
                    xEventGroupSetBits(event_group, UART_DATA_READY_BIT);
                    break;
                    //Event of HW FIFO overflow detected
                case UART_FIFO_OVF:
                    ESP_LOGI(TAG, "hw fifo overflow\n");
                    //If fifo overflow happened, you should consider adding flow control for your application.
                    //We can read data out out the buffer, or directly flush the rx buffer.
                    uart_flush(uart_num);
                    break;
                    //Event of UART ring buffer full
                case UART_BUFFER_FULL:
                    ESP_LOGI(TAG, "ring buffer full\n");
                    //If buffer full happened, you should consider increasing your buffer size
                    //We can read data out out the buffer, or directly flush the rx buffer.
                    uart_flush(uart_num);
                    break;
                    //Event of UART RX break detected
                case UART_BREAK:
                    ESP_LOGI(TAG, "uart rx break\n");
                    break;
                    //Event of UART parity check error
                case UART_PARITY_ERR:
                    ESP_LOGI(TAG, "uart parity error\n");
                    break;
                    //Event of UART frame error
                case UART_FRAME_ERR:
                    ESP_LOGI(TAG, "uart frame error\n");
                    break;
                    //UART_PATTERN_DET
                case UART_PATTERN_DET:
                    ESP_LOGI(TAG, "uart pattern detected\n");
                    break;
                    //Others
                default:
                    ESP_LOGI(TAG, "uart event type: %d\n", event.type);
                    break;
            }
        }
    }
    free(dtmp);
    dtmp = NULL;
    vTaskDelete(NULL);
}

//in this case "processing" simply means writing to the console
void uart_data_processing(void* pvParameters){
    //use malloc to get a buffer to use for passing around serial data.
    uint8_t* buffer = bufferA;

    while (1) {
        //wait for incoming data and clear the bit as soon as it comes.
        xEventGroupWaitBits(event_group, UART_DATA_READY_BIT, true, false, portMAX_DELAY);

        //read the data into the buffer, up to length BUF_SIZE, with a 0.1s timeout.
        int len = uart_read_bytes(UART_NUM_1, buffer, BUF_SIZE, 100 / portTICK_PERIOD_MS);
        //if (len <= 0){
            //ESP_LOGI(TAG, "UART read timed out.\n");
        //}
        //else{
            //ESP_LOGI(TAG, "Got %d UART bytes.\n", len);
        //}

        //at this point we have the data so we can swap the buffers, release the semaphore, and set the event.
        //now we must wait for the buffers to be free
        xSemaphoreTake(bufferSwapSemaphore, portMAX_DELAY);
        bufferLen = len;
        if(bufferToggle){
            buffer = bufferA;
        }
        else{
            buffer = bufferB;
        }
        bufferToggle = !bufferToggle;
        xSemaphoreGive(bufferSwapSemaphore);

        //there is new data in the buffer, so it is time to run!
        xEventGroupSetBits(event_group, NEW_UART_DATA_BIT);
    }
    //not that this will ever run...
    free(buffer);
}


//an example of echo test with hardware flow control on UART1
void uart_setup()
{
    //configure the uart port
    int uart_num = UART_NUM_1;
    uart_config_t uart_config;
    uart_config.baud_rate = 9600;
    uart_config.data_bits = UART_DATA_8_BITS;
    uart_config.parity = UART_PARITY_DISABLE;
    uart_config.stop_bits = UART_STOP_BITS_1;
    uart_config.flow_ctrl = UART_HW_FLOWCTRL_DISABLE;
    uart_config.rx_flow_ctrl_thresh = 0;

    //Configure UART2 parameters
    uart_param_config(uart_num, &uart_config);
    //Set UART1 pins(TX: IO17, RX: I016, RTS: IO7, CTS: IO8)
    uart_set_pin(uart_num, ECHO_TEST_TXD, ECHO_TEST_RXD, ECHO_TEST_RTS, ECHO_TEST_CTS);

    //Install the usart driver and get the queue
    uart_driver_install(uart_num, BUF_SIZE * 2, BUF_SIZE * 2, 10, &uart1_queue, 0);

    //create the uart event handling task, note that it is priority 12, which is higher than 5.
    xTaskCreate(uart_task, "uart_event_task", 2048, (void*)uart_num, 4, NULL);

    //create the uart processing task, which will print the incoming data to the console.
    xTaskCreate(uart_data_processing, "uart_processing_task", 2048, NULL, 3, NULL);
}

void tcp_rx_thread(void* pvParameters){
    //ew.
    int* clientSock_p = (int*)pvParameters;
    int clientSock = *clientSock_p;

    uint8_t byte;
    bool done = false;

    while(!done){
        int retLen = recv(clientSock, &byte, 1, 0);
        if(retLen <= 0){
            ESP_LOGI(TAG, "recv returned 0, closing connection.\n");
            xEventGroupSetBits(event_group, TCP_DISCONECTED);
            done = true;
        }
        else{
            //do something with the incoming bytes?
            ESP_LOGI(TAG, "recv returned %c\n", byte);
        }
    }
    //Kill the thread.
    ESP_LOGI(TAG, "tcp_rx_thread is exiting.");
    vTaskDelete(NULL);
}

void tcp_server_task(void* pvParameters){
    int portNumber = 5001;

    //setup the tcp server
    struct sockaddr_in serverAddress;
    serverAddress.sin_family = AF_INET;
    serverAddress.sin_addr.s_addr = htons(INADDR_ANY);
    serverAddress.sin_port = htons(portNumber);

    struct sockaddr_in clientAddress;
    socklen_t clientAddessLength = sizeof(clientAddress);
    uint8_t* buffer = (uint8_t*)malloc(sizeof(uint8_t)*BUF_SIZE);

    bool connected = false;
    int clientSock;
    int sock = -1;
    int len;

    ESP_LOGI(TAG, "Wifi connected, starting server\n");
    sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    bind(sock, (struct sockaddr*)&serverAddress, sizeof(serverAddress));
    ESP_LOGI(TAG, "Server bound, Waiting for connections\n");

    //accept a connection
    listen(sock, 1);

    while(1){
        //wait for the wifi system to be connected
        xEventGroupWaitBits(wifi_event_group, CONNECTED_BIT, false, true, portMAX_DELAY);

        //wait for a new connection
        clientSock = accept(sock, (struct sockaddr *)&clientAddress, &clientAddessLength);
        ESP_LOGI(TAG, "New connection accepted.\n");

        //we have a new connection, so we want to clear the uart event bits so that we don't print stale data to the client.
        xEventGroupClearBits(event_group, NEW_UART_DATA_BIT);

        //we also want to clear the TCP_DISCONECTED flag if it is set since we know that we have a new connection
        xEventGroupClearBits(event_group, TCP_DISCONECTED);

        //create a task to handle reading from the remote thread.
        xTaskCreate(tcp_rx_thread, "tcp_rx_thread", 2048, &clientSock, 2, NULL);
        connected = true;
        ESP_LOGI(TAG, "Connection opened in server task.\n");

        while(connected){
            //wait for the uart thread to signal that we have data.
            EventBits_t bits = xEventGroupWaitBits(event_group, (NEW_UART_DATA_BIT | TCP_DISCONECTED), true, false, portMAX_DELAY);
            if(bits & TCP_DISCONECTED){
                close(clientSock);
                connected = false;
            }
            else if(bits & NEW_UART_DATA_BIT){
                //we know that there is new data, we just need to get it!
                //Thus we take the semaphore so we can copy the contents of the outBuffer.
                xSemaphoreTake(bufferSwapSemaphore, portMAX_DELAY);
                len = bufferLen;
                if(bufferToggle){
                    memcpy(buffer, bufferA, len);
                }
                else{
                    memcpy(buffer, bufferB, len);
                }
                xSemaphoreGive(bufferSwapSemaphore);

                int sentLen = send(clientSock, buffer, len, 0);
                if(sentLen < 0){//disconnect because we just sent to a closed or otherwise unavaliable socket
                    ESP_LOGI(TAG, "Failed to send data to client, closing connection.\n");
                    connected = false;
                    close(clientSock);
                }
            }
        }
    }
}

void app_main()
{
    //do system init stuff
    nvs_flash_init();
    initialise_wifi();

    event_group = xEventGroupCreate();

    //init the trackers and guards for the misc tasks and buffers
    bufferSwapSemaphore = xSemaphoreCreateMutex();
    bufferA = (uint8_t*)malloc(sizeof(uint8_t)*BUF_SIZE);
    bufferB = (uint8_t*)malloc(sizeof(uint8_t)*BUF_SIZE);
    bufferLen = 0;
    bufferToggle = false;
    xSemaphoreGive(bufferSwapSemaphore);//give the semaphore so that it starts in a state where it can be taken.

    uart_setup();

    xTaskCreate(mdns_task, "mdns_task", 2048, NULL, 6, NULL);
    xTaskCreate(tcp_server_task, "tcp_server_task", 2048, NULL, 5, NULL);
}
