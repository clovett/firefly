
#include "Messages.h"
#include "Arduino.h"
#include "Utils.h"
#include "Relays.h"

void message_init(){
  	Serial.begin(9600);
}

int heartbeat_tx(){
	Serial.print("HTB");
	
	//this should be commented out when not in debug.
	resetKillTimer();
}

int message_rx(){
	char incoming[MSG_LEN] = {0};

	if(Serial.available() >= MSG_LEN){
		for(int i=0;i<MSG_LEN;i++){
			incoming[i] = Serial.read();
		}
	}

	if(strComp(incoming, "FR", MSG_LEN-1)){
		fireTube();
	}
	else if(strComp(incoming, "HBT", 3)){
		resetKillTimer();
	}
}
