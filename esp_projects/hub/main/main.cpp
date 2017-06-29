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
#include "../components/gpio/tubes.hpp"
#include <string.h>

static const char *TAG = "main";
static const char *FIND_HUB_BROADCAST = "FIREFLY-FIND-HUB";
static const char *FIND_HUB_RESPONSE = "FIREFLY-HUB";
const int FireflyBroadcastPort = 13777; // the magic firefly ports
const int FireflyTcpPort = 13787; // the magic firefly ports
const uint8_t HeaderByte = 0xfe;
const int FireflyCommandLength = 5;
const int MaxTubes = 10;

Tubes tubes;

static uint8_t Crc(char* buffer, int offset, int len)
{
    uint8_t crc = 0;
    for (int i = offset; i < len; i++)
    {
        uint8_t c = buffer[i];
        crc = (uint8_t)((crc >> 1) ^ c);
    }
    return crc;
}

enum FireflyCommand
{
    None = 0,
    Info = 'I',
    Fire = 'F',
    Heartbeat = 'H',
    // responses
    Ready = 'R',
    Ack = 'A',
    Nack = 'N',
    Timeout = 'T',
    Error = 'E'
};

class  FireMessage
{
public:
    uint8_t header_byte = 0;
    FireflyCommand command= None;
    uint8_t arg1= 0;
    uint8_t arg2= 0;
    uint8_t crc= 0;
    bool crc_valid = false;

    FireMessage(Message* msg){      
      if (msg->len == FireflyCommandLength) {
          header_byte = (uint8_t)msg->payload[0];
          command = (FireflyCommand)msg->payload[1];
          arg1 = (uint8_t)msg->payload[2];
          arg2 = (uint8_t)msg->payload[3];
          crc = (uint8_t)msg->payload[4];
          crc_valid = (this->crc == Crc(msg->payload, 0, 4));
      }
    }

    char* pack() {
      buffer[0] = HeaderByte;
      buffer[1] = (char)command;
      buffer[2] = (char)arg1;
      buffer[3] = (char)arg2;
      buffer[4] = Crc(buffer,0,4);
      return buffer;
    }

    int tube() {
      return (arg1 + (arg2 << 8) );
    }
    private:
    char buffer[5];
};

void handle_command(FireMessage& msg)
{
  int tube = 0;
  if (msg.header_byte == HeaderByte && msg.crc_valid)
  {
    switch (msg.command)
    {
    case Info:
      // return number of tubes.
      ESP_LOGI(TAG, "Info request");
      msg.command = Ack;
      msg.arg1 = MaxTubes;
      msg.arg2 = 0;
      // todo: we could also sense loaded tubes right?
      // then perhaps return a bitmap of loaded vs empty tubes.
      break;
    case Heartbeat:
        // heartbeat, echo back simple response.
        ESP_LOGI(TAG, "heartbeat ping");
        msg.command = Ack;
        msg.arg1 = 0;
        msg.arg2 = 0;
        break;
    case Fire:
        // fire !
        tube = msg.tube();
        if (tube < MaxTubes) {
          ESP_LOGI(TAG, "firing tube %d", tube);
          msg.command = Ack;
          tubes.fire(tube);
        }
        else {
          ESP_LOGI(TAG, "tube index out of range %d", tube);
          msg.command = Nack;
        }
        break;
        
      // not expecting anything else
    case None:
    case Ready:
    case Ack:
    case Nack:
    case Timeout:
    case Error:
    default:
      msg.command = Error;
      break;  
      
    }
  }

}

void run(){

  Wifi wifi;
  LedController led;

  tubes.init();

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
      if (msg->is_tcp()){
          ESP_LOGI(TAG, "received tcp message %d, type=%d",  msg->len, (int)msg->payload[0]);
          FireMessage fm(msg);
          handle_command(fm);
          Message response(msg, fm.pack(), FireflyCommandLength);
          tcp_stream.send_reply(&response);
      }
      else if (strncmp(msg->payload, FIND_HUB_BROADCAST, msg->len) == 0)
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
