#ifndef FIREFLY_MESSAGE_HPP
#define FIREFLY_MESSAGE_HPP
#include <stdio.h>
#include <stdlib.h>
#include <string>

const int MagicHeader = 0x152C81;

enum FireflyCommand
{
    None = 0,
    Info = 'I',
    Fire = 'F',
    Heartbeat = 'H',
    Arm = 'X',
    Color = 'C',
    Ramp = 'R',
    Blink = 'B',
    // responses
    Ack = 'A',
    Nack = 'N',
    Timeout = 'T',
    Error = 'E',    
};

class  FireMessage
{
public:
    int header = 0;
    FireflyCommand command= None;
    int arg1 = 0;
    int arg2 = 0;
    int arg3 = 0;
    int arg4 = 0;
    int crc = 0;
    uint computed = 0;
    bool crc_valid = false;
    const char FieldSeparator = ',';

    FireMessage(Message* msg) {      
        int start = 0;
        int field = 0;
        int crcPos = 0;
        for (int i = 0, n = msg->len; i<n; i++){
            char ch = msg->payload[i];
            if (ch == FieldSeparator) {
                std::string arg(&msg->payload[start], i - start);
                add_field(field, arg);
                field++;
                if (field == 6) {
                    crcPos = i;
                }
                start = i + 1;
            }
        }
        if (start < msg->len) {
            std::string arg(&msg->payload[start], msg->len - start);
            add_field(field, arg);
        }
        if (header == MagicHeader){
            computed = Crc(msg->payload, 0, crcPos);
            crc_valid = (this->crc == computed);
        }
    }

    std::string format() {
        std::string msg;
        char buf[2];

        msg.append(to_string(MagicHeader));
        msg.append(",");
        buf[0] = command;
        buf[1] = 0;
        msg.append(buf);
        msg.append(",");
        msg.append(to_string(arg1));
        msg.append(",");
        msg.append(to_string(arg2));
        msg.append(",");
        msg.append(to_string(arg3));
        msg.append(",");
        msg.append(to_string(arg4));

        uint crc = Crc(msg.c_str(), 0, msg.size());
        
        msg.append(",");
        msg.append(to_string(crc));
        return msg;
    }

    std::string to_string(int i){
        char buf[20];
        itoa(i, buf, 10);
        return std::string(buf);
    }

    int tube() {
      return arg1;
    }
    
private:
    void add_field(int field, const std::string& arg){
        switch (field) {
            case 0:
                header = atoi(arg.c_str());
                break;
            case 1:
                command = arg.size() > 0 ? (FireflyCommand)arg[0] : None;
                break;
            case 2:
                arg1 = atoi(arg.c_str());
                break;
            case 3:
                arg2 = atoi(arg.c_str());
                break;
            case 4:
                arg3 = atoi(arg.c_str());
                break;
            case 5:
                arg4 = atoi(arg.c_str());
                break;
            case 6:
                crc = atoi(arg.c_str());
                break;
        }
    }
        
    static uint Crc(const char* buffer, int offset, int len)
    {
        uint crc = 0;
        for (int i = offset; i < len; i++)
        {
            uint8_t c = buffer[i];
            uint sum = crc + c;
            crc = (uint)((sum << 1) ^ sum);
        }
        return crc;
    }

};

#endif