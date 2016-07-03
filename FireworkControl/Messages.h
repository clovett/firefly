
//this is the file that contains the message prototypes

#ifndef MESSAGES_H
#define MESSAGES_H

#include "Arduino.h"

#define BEAT_TIME 1000
#define HEARTBEAT_TIMEOUT 1000
#define MSG_LEN 3

void message_init();
int message_rx();
int heartbeat_tx();

#endif