
#include "Arduino.h"
#include "Utils.h"

unsigned long heartbeatTime = 0;
bool ready = false;


/*Call whenever a heartbeat message arrives.
Set the heartbeatTime to the time when this function is called*/
void heartbeatRX(){
	heartbeatTime = millis();
}

/*
This function manages a global ready flag that controls when the tubes may
fire. The flag starts as false and then goes true when we have recived at least one
heartbeat per second for at least three seconds.*/
void processHeartbeat(){
	unsigned long currentTime = millis();
	//if it has been longer than a second since the last beat, set ready to false.
	if(currentTime - heartbeatTime > TIMEOUT_MS){
		ready = false;
	}
	else{
		ready = true;
	}
}

//find the time delta and take wraping into account.
unsigned long deltaTime(unsigned long current, unsigned long lastTime){
	unsigned long delta;
	if(lastTime > current){
		delta = MAX_UL - lastTime + current;
	}
	else {
		delta = current - lastTime;
	}
	return delta;
}
