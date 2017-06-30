#ifndef LED_HEADER_
#define LED_HEADER_

class LedController
{
public:
    void init();
    
    void off();
    void color(uint8_t red, uint8_t green, uint8_t blue);
    void ramp(uint8_t red, uint8_t green, uint8_t blue, int milliseconds);
    void blink(uint8_t red, uint8_t green, uint8_t blue, int millisecondsDelay);

    // todo: add functions for controlling LED strip...
    void run();
private:
    void allOff();
    void color();
    void ramp();
    void blink();

    enum LedCommand {
        None,
        AllOff,
        SetColor,
        RampColor,
        Blink
    };

    LedCommand cmd = None;
    uint8_t red;
    uint8_t green;
    uint8_t blue;
    uint8_t start_red;
    uint8_t start_green;
    uint8_t start_blue;
    uint8_t target_red;
    uint8_t target_green;
    uint8_t target_blue;
    uint8_t milliseconds;
    uint8_t count;

};

#endif