
#ifndef RELAYS_H
#define RELAYS_H

#define STOPPED 1 	//not firing, current is stopped.
#define FLOWING 0	//means current is flowing.

#define NUM_TUBES 40
#define FIRETIME 200

void assignPins();
void assignPinsInRaceOrder();
void initRelays();

void fireTube(unsigned char tube);

//a function that is called to manage the timing for
//actually firing the shots.
void processTubes();

#endif
