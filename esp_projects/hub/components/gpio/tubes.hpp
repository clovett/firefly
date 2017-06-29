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

    void color(int tube, int red, int green, int blue);
private:    
    spi_device_handle_t handle;
};

#endif