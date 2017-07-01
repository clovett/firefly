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
#include "FireflyMessage.hpp"
#include <string.h>

static const char *TAG = "main";
static const char *FIND_HUB_BROADCAST = "FIREFLY-FIND-HUB";
static const char *FIND_HUB_RESPONSE = "FIREFLY-HUB";
const int FireflyBroadcastPort = 13777; // the magic firefly ports
const int FireflyTcpPort = 13787; // the magic firefly ports
const int MaxTubes = 10;
static bool LedInitialized = false;

Wifi wifi;
LedController led;
Tubes tubes;

void initialize_leds(){
  if (!LedInitialized){
    LedInitialized = true;
    led.init();
    led.off();
  }
}

void handle_command(FireMessage& msg)
{
  int tube = 0;
  if (msg.header == MagicHeader && msg.crc_valid)
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
    case Arm:// arm the hub !
        ESP_LOGI(TAG, "arming tubes %d", msg.arg1);
        msg.command = Ack;        
        tubes.arm(msg.arg1 == 1 ? true : false);
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
    case Color:
        initialize_leds();
        led.color(msg.arg1, msg.arg2, msg.arg3, msg.arg4);  
        msg.command = Ack;      
        break;
    case Ramp:
        initialize_leds();
        led.ramp(msg.arg1, msg.arg2, msg.arg3, msg.arg4,1000);
        msg.command = Ack;
        break;
    case Blink:
        initialize_leds();
        led.blink(msg.arg1, msg.arg2, msg.arg3, msg.arg4,1000);
        msg.command = Ack;
        break;
        
      // not expecting anything else
    case None:
    case Ack:
    case Nack:
    case Timeout:
    case Error:
    default:
      msg.command = Error;
      break;  
      
    }
  } else {
    ESP_LOGI(TAG, "ignoring invalid mesg, header=%d, checksum %d, computed=%d",  msg.header, msg.crc, msg.computed);  
  }

}

void run(){

  ESP_LOGI(TAG, "portTICK_PERIOD_MS=%d", (int)portTICK_PERIOD_MS);

  tubes.init();

  lwip_init();

  wifi.initialise_wifi();
  std::string local_ip  = wifi.get_local_ip();

  MessageQueue queue;

  UdpMessageStream udp_stream(&queue);
  udp_stream.start_listening(FireflyBroadcastPort);

  TcpMessageStream tcp_stream(&queue, local_ip);
  tcp_stream.start_listening(FireflyTcpPort);

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
          std::string packed = fm.format();
          Message response(msg, packed.c_str(), packed.size());
          tcp_stream.send_reply(&response);
      }
      else if (strncmp(msg->payload, FIND_HUB_BROADCAST, msg->len) == 0)
      {
        const char* addr = addr_to_string(&msg->remote_addr);
        ESP_LOGI(TAG, "hey, (%s:%d) wants to find us!", addr, msg->remote_addr.sin_port);

        addr = local_ip.c_str();        
        int len = sprintf(buffer, "%s,%s,%d,%s", FIND_HUB_RESPONSE, addr, FireflyTcpPort,  tcp_stream.get_remote_host().c_str());

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
