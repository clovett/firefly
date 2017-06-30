#ifndef TUBES_HEADER_
#define TUBES_HEADER_

#include "freertos/FreeRTOS.h"
#include "driver/spi_master.h"

class Tubes
{
public:
    Tubes();

    int init();

    void arm(bool on);

    void fire(int tube);

    int sense(int tube);

};

#endif