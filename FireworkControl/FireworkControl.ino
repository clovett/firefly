

// FireworkControl.ino

#include "Messages.h"
#include "Relays.h"
#include "Utils.h"

void setup() {
  	message_init();
  	initRelays();
}

void loop() {
  //manage heartbeats
  processHeartbeat();

	//check for incoming messages
	message_rx();

	//fire and stop tubes as nessary.
	processTubes();
}
