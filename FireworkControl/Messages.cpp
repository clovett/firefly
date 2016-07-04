
#include "Messages.h"
#include "Arduino.h"
#include "Utils.h"
#include "Relays.h"

extern bool ready;

void message_init(){
  	Serial.begin(57600);
}

/*given a message calculate the checksum and return*/
unsigned char calc_crc(unsigned char* message){
  unsigned char sum = CHKSUM_SEED;

  for(int i=0; i<MSG_LEN-1; i++){
    sum = (sum >> 1) ^ message[i];
  }
  return sum;
}

/*given a command code and args build and send a message*/
void message_tx(char cmd, char arg0, char arg1){
  unsigned char message[MSG_LEN] = {0};
  message[0] = START_BYTE;
  message[1] = cmd;
  message[2] = arg0;
  message[3] = arg1;

  message[4] = calc_crc(message);

  Serial.write(message, MSG_LEN);
}

/*send a response for a correctly parsed command*/
void send_ack(char cmd){
  char arg = 0;
  if(cmd == 'I'){
    if(ready){
      arg = NUM_TUBES;
    }
  }
  message_tx('A', cmd, arg);
}

/*send a response for a unknown command*/
void send_nack(unsigned char* message){
  unsigned char crc = calc_crc(message);
  message_tx('N', crc, message[4]);
}

/*handle incoming messages and call actions if need be.*/
int message_rx(){
  unsigned char incoming[MSG_LEN] = {0};

	if(Serial.available() >= MSG_LEN){
		for(int i=0;i<MSG_LEN;i++){
			incoming[i] = Serial.read();
		}

    //now that we have a message, check for the correct start byte and checksum
    if( (incoming[0] == START_BYTE) && (calc_crc(incoming) == incoming[4]) ){

      //if the message is correctly formatted switch based on the comand
      switch (incoming[1])
      {
        case 'H':{ //we've gotten a heartbeat
          heartbeatRX();
          send_ack('H');
          break;
        }
        case 'F':{ //we've gotten a fire tube command
          fireTube(incoming[3]);
          send_ack('F');
          break;
        }
        case 'I':{ //we've gotten a request for info
          send_ack('I');
          break;
        }
        default:{ //otherwise we don't know what we have.
          send_nack(incoming);
          break;
        }
      }
    }
    else{ //this means that we got a malformed packet
      send_nack(incoming);
    }
	}
}
