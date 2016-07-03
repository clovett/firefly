
#include "Arduino.h"
#include "Utils.h"

unsigned long killTimer = 0;

void resetKillTimer(){
	killTimer = millis();
}

void killMeMaybe(){
	unsigned long current = millis();
	if(deltaTime(current, killTimer) > 2000){
		abortShow();
	}
}

bool strComp(char* one, char* two, int strlen){
	
	int i = 0;
	for(i=0;i<strlen;i++){
		if(one[i] != two[i]){
			return false;
		}
	}
	return true;
}

int toggleVal(int val){
	if(val == 0){
		val = 1;
	}
	else if(val == 1){
		val = 0;
	}
	return val;
}

//Repeatedly calls a function at the given interval.
unsigned long periodicCall(unsigned long lastTime, unsigned long period, int (*f)()){
	unsigned long current = millis();
	if(current % period == 0 && deltaTime(current, lastTime) >= 100){
		(*f)();
		lastTime = current;
	}
	return lastTime;
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

//locks the show into an abort state.
void abortShow(){
	char reset = 0;
	char resetBuffer[5] = {0};
	char checkMessage[5] = {'r','e','s','e','t'};
	char bufferCounter = 0;
	unsigned long resetTimer = 0;

	while(!reset){
		if(millis() - resetTimer > 1500 && bufferCounter == 0){
			Serial.println("System pending reset, send \"reset\" to restart.");
			resetTimer = millis();
		}

		if(Serial.available() > 0){
			resetBuffer[bufferCounter] = Serial.read();
			Serial.print(resetBuffer[bufferCounter]); //print back so that the terminal sees what has been sent.
			bufferCounter++;
			if(bufferCounter >= 5){
				bufferCounter = 0;
			}	
		}

		if(strComp(resetBuffer, checkMessage, 5)){
			Serial.println("reset detected.");
			reset = 1;
		}
	}
	Serial.println("exited abort loop.");
}