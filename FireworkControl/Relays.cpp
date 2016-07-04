#include "Relays.h"
#include "Arduino.h"

extern bool ready;

//the array of pins numbers for each tube.
//this array serves as the loopup table for pin order assignments.
char pins[NUM_TUBES];

Tube tubes[NUM_TUBES];

//all pins have been fired when current pin == NUM_TUBES
int firedUpToPin = -1;

//the pin queue is added to by the fireTube method and
//decremented by the processTubes method.
int pinQueue = -1;

class Tube {
	int pin;
	bool active;
	unsigned long start_time;

	public:
		Tube(int, bool);
		void fire();
		bool isFiring();
		void processTube();
};

Tube::processTube(){
	if(start_time - millis() > FIRETIME){
		active = false;
		digitalWrite(pin, STOPPED);
	}
}

Tube::fire(){
	active = True;
	start_time = millis();
	digitalWrite(pin, FLOWING);
}

Tube::isFiring(){
	return active;
}


void initRelays(){
	//use for actuall shows so that tubes are spaced out.
	//assignPins();

	//use for debug so that relays fire in order.
	assignPinsInRaceOrder();

	pinMode(LED_BUILTIN, OUTPUT);

	for(int i = 0; i<NUM_TUBES; i++){
		pinMode(pins[i], OUTPUT);
		digitalWrite(pins[i], STOPPED);
	}
}


void fireTube(unsigned char tube){
	if(ready){
		//turn the tube on.

	}
}


//this function is called every loop in the main exicution code.
//this function will have a wrapping problem for 200ms every 7(ish) weeks.
//this function is called in the main loop
void processTubes(){

}


//this is here to act as a pin maping table.
//the index array maps to the fire order, the value
//is the pin on the Mega.
void assignPins(){
	//Port A
	pins[0]  = A0; //1
	pins[10] = A1; //2
	pins[20] = A2; //3
	pins[30] = A3; //4
	pins[5]  = A4; //5
	pins[15] = A5; //6
	pins[25] = A6; //7
	pins[35] = A7; //8

	//Port B
	pins[6] = 22;  	//1
	pins[16] = 23;  //2
	pins[26] = 24; 	//3
	pins[36] = 25; 	//4
	pins[1] = 26; 	//5
	pins[11] = 27; 	//6
	pins[21] = 28; 	//7
	pins[31] = 29; 	//8

	//Port C
	pins[2] = 30; 	//1
	pins[12] = 31; 	//2
	pins[22] = 32; 	//3
	pins[32] = 33; 	//4
	pins[7] = 34; 	//5
	pins[17] = 35; 	//6
	pins[27] = 36; 	//7
	pins[37] = 37; 	//8

	//Port D
	pins[8] = 38; 	//1
	pins[18] = 39; 	//2
	pins[28] = 40; 	//3
	pins[38] = 41; 	//4
	pins[3] = 42; 	//5
	pins[13] = 43; 	//6
	pins[23] = 44; 	//7
	pins[33] = 45; 	//8

	//Port E
	pins[4] = 46; 	//1
	pins[14] = 47; 	//2
	pins[24] = 48; 	//3
	pins[34] = 49; 	//4
	pins[9] = 50; 	//5
	pins[19] = 51; 	//6
	pins[29] = 52; 	//7
	pins[39] = 53; 	//8
}

//this function is for testing the wiring,
//and will assign the pins in linear order.
void assignPinsInRaceOrder(){
	pins[0] = A0;   	//1
	pins[1] = A1;   	//2
	pins[2] = A2;   	//3
	pins[3] = A3;   	//4
	pins[4] = A4;   	//5
	pins[5] = A5;   	//6
	pins[6] = A6;   	//7
	pins[7] = A7;   	//8

	pins[8] = 22;  	//1
	pins[9] = 23;  	//2
	pins[10] = 24; 	//3
	pins[11] = 25; 	//4
	pins[12] = 26; 	//5
	pins[13] = 27; 	//6
	pins[14] = 28; 	//7
	pins[15] = 29; 	//8

	pins[16] = 30; 	//1
	pins[17] = 31; 	//2
	pins[18] = 32; 	//3
	pins[19] = 33; 	//4
	pins[20] = 34; 	//5
	pins[21] = 35; 	//6
	pins[22] = 36; 	//7
	pins[23] = 37; 	//8

	pins[24] = 38; 	//1
	pins[25] = 39; 	//2
	pins[26] = 40; 	//3
	pins[27] = 41; 	//4
	pins[28] = 42; 	//5
	pins[29] = 43; 	//6
	pins[30] = 44; 	//7
	pins[31] = 45; 	//8

	pins[32] = 46; 	//1
	pins[33] = 47; 	//2
	pins[34] = 48; 	//3
	pins[35] = 49; 	//4
	pins[36] = 50; 	//5
	pins[37] = 51; 	//6
	pins[38] = 52; 	//7
	pins[39] = 53; 	//8
}
