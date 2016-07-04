

#ifndef UTILS_H
#define UTILS_H

#define MAX_UL 4294967295

#define TIMEOUT_MS 1000
#define VALID_SECONDS 3

int toggleVal(int val);
bool strComp(char* one, char* two, int strlen);

unsigned long periodicCall(unsigned long lastTime, unsigned long period, int (*f)());
unsigned long deltaTime(unsigned long current, unsigned long lastTime);

void heartbeatRX();
void processHeartbeat();

#endif
