#ifndef TUBES_HEADER_
#define TUBES_HEADER_

#include "freertos/FreeRTOS.h"
#include "driver/spi_master.h"

#define NUM_TUBES 10

class Tubes
{
public:
    Tubes();

    int init();

    void arm(bool on);

    void fire(uint tube, int burn_time);

    int sense(int tube);

    void run_sensing();

    int get_tube_state(int tube);

private:
    int tube_state[NUM_TUBES];
};

#endif