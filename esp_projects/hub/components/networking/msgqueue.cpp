#include "msgqueue.hpp"
#include "esp_log.h"
#include <string.h>

static const char *TAG = "queue";
#define MAX_MUTEX_WAIT_MS       1000
#define MAX_MUTEX_WAIT_TICKS    (MAX_MUTEX_WAIT_MS / portTICK_PERIOD_MS)

Message::Message(struct sockaddr_in *localaddr, struct sockaddr_in *remoteaddr, char* payload, int len)
{    
    this->tcp_socket = 0;
    memcpy(&this->local_addr, localaddr, sizeof(struct sockaddr_in));
    memcpy(&this->remote_addr, remoteaddr, sizeof(struct sockaddr_in));

    this->len = len;
    this->payload = (char*)malloc(len + 1);
    if (this->payload == NULL) {
        ESP_LOGI(TAG, "out of memory allocating Message payload of len=%d", len);
    } else {
        memcpy(this->payload, payload, len);
        this->payload[len] = 0;
    }
}

Message::Message(Message* replyTo, char* payload, int len)
{
    this->tcp_socket = replyTo->tcp_socket;
    memcpy(&this->local_addr, &replyTo->local_addr, sizeof(struct sockaddr_in));
    memcpy(&this->remote_addr, &replyTo->remote_addr, sizeof(struct sockaddr_in));

    this->len = len;
    this->payload = (char*)malloc(len + 1);
    if (this->payload == NULL) {
        ESP_LOGI(TAG, "out of memory allocating Message payload of len=%d", len);
    } else {
        memcpy(this->payload, payload, len);
        this->payload[len] = 0;
    }
}


Message::Message(int tcp_socket, char* payload, int len)
{
    this->tcp_socket = tcp_socket;
    this->payload = (char*)malloc(len + 1);
    if (this->payload == NULL) {
        ESP_LOGI(TAG, "out of memory allocating Message payload of len=%d", len);
    } else {
        memcpy(this->payload, payload, len);
        this->payload[len] = 0;
        this->len = len;
    }
}

Message::~Message(){
    if (payload != NULL){
        delete[] payload;
        payload = NULL;
    }
}

MessageQueue::MessageQueue() {
    
    queue_head = NULL;
    queue_tail = NULL;
    
    queue_mutex = xSemaphoreCreateMutex();
    if (!queue_mutex){        
        ESP_LOGI(TAG, "failed to create queue_mutex.");
    }

}

MessageQueue::~MessageQueue()
{
    while (queue_head != NULL){
        entry* e = queue_head;
        queue_head = queue_head->next;
        delete e->msg;
        delete e;
    }
    queue_tail = NULL;
}
   
void MessageQueue::enqueue(Message* msg)
{
    if (queue_mutex == NULL){
        ESP_LOGI(TAG, "queue_message called without queue_mutex");
        return;
    }
    entry* e = (entry*)malloc(sizeof(entry));
    if (e == NULL) {
        ESP_LOGI(TAG, "out of memory allocating mesg queue entry");
        return;
    }

    e->next = NULL;
    e->msg = msg;
    
    xSemaphoreTake(queue_mutex, MAX_MUTEX_WAIT_TICKS);
    if (queue_tail == NULL){
        queue_head = queue_tail = e;
    }
    else {
        queue_tail->next = e;
        queue_tail = e;
    }
    xSemaphoreGive(queue_mutex);
}

Message* MessageQueue::dequeue()
{    
    if (queue_mutex == NULL){
        ESP_LOGI(TAG, "dequeue_message called without queue_mutex");
        return NULL;
    }
    entry* e = NULL;
    xSemaphoreTake(queue_mutex, MAX_MUTEX_WAIT_TICKS);
    if (queue_head != NULL){
        e = queue_head;
        queue_head = queue_head->next;
        if (queue_head == NULL){
            queue_tail = NULL;
        }
    }
    xSemaphoreGive(queue_mutex);

    // unwqrap the entry
    Message* result = NULL;
    if (e != NULL){
        result = e->msg;
        delete e;
    }
        
    return result; 
}
