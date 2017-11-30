# Hub

A FreeRTOS implementation of the firmware to drive 10 fireworks tubs on an ESP32 hub, connected to 10 relays 
for powering the electric igniters that light each tube.

When "make" is complete it will output the flash instructions, so if you are using BashOnWindows (WSL) you
can copy that command line to windows and run it there, it will look something liek this:

```
python /mnt/d/ESP32/esp-idf/components/esptool_py/esptool/esptool.py --chip esp32 --port /dev/ttyS4 --baud 115200 --before default_reset --after hard_reset write_flash -z --flash_mode dio --flash_freq 40m --flash_size detect 0x1000 /mnt/d/git/firefly/esp_projects/hub/build/bootloader/bootloader.bin 0x10000 /mnt/d/git/firefly/esp_projects/hub/build/hub.bin 0x8000 /mnt/d/git/firefly/esp_projects/hub/build/partitions_singleapp.bin
```

Obviously on windows you will need to convert those path names to windows format, and change the COM port name to 
the one you see under Device Manager.