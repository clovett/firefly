
//this is the file that contains the message prototypes

#ifndef MESSAGES_H
#define MESSAGES_H

#include "Arduino.h"

#define MSG_LEN 5

#define START_BYTE 0xFE
#define CHKSUM_SEED 0x00

void message_init();
int message_rx();

void send_ready();
void send_ack();
void send_nack(unsigned char* message);

void message_tx(char cmd, char arg0, char arg1);
unsigned char calc_crc(unsigned char* message);

#endif
