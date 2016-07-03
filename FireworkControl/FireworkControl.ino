

// FireworkControl.ino

#include "Messages.h"
#include "Relays.h"
#include "Utils.h"


void setup() {
  	message_init();
  	initRelays();
}

unsigned long lastBeatTime = 0;

void loop() {

	//check the kill timer
	killMeMaybe();

	//send the heartbeat if the time is right.
	lastBeatTime = periodicCall(lastBeatTime, BEAT_TIME, heartbeat_tx);

	//check for incoming messages
	message_rx();

	//fire and stop tubes as nessary.
	processTubes();
}
