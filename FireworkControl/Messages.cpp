
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
          message_tx('A', 'H', 0);
          break;
        }
        case 'F':{ //we've gotten a fire tube command
          if(incoming[3] >= NUM_TUBES){
            message_tx('N', 'F', incoming[2]);
          }
          else{
            fireTube(incoming[3]);
            message_tx('A', 'F', incoming[2]);
          }
          break;
        }
        case 'I':{ //we've gotten a request for info
          if(ready){
            message_tx('A', 'I', NUM_TUBES);
          }
          else{
            message_tx('A', 'I', 0);
          }
          break;
        }
        default:{ //otherwise we don't know what we have.
          message_tx('N', incoming[1], 0);
          break;
        }
      }
    }
    else{ //this means that we got a malformed packet
      message_tx('N', incoming[1], 0);
    }
	}
}
