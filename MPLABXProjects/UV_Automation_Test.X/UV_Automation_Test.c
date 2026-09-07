#include <xc.h>

// CONFIG
#pragma config FOSC = INTOSCIO  // Internal oscillator, I/O on GP4/GP5
#pragma config WDTE = OFF       // Watchdog Timer Enable (WDT disabled)
#pragma config PWRTE = ON       // Power-up Timer Enable (PWRT enabled)
#pragma config MCLRE = OFF      // MCLR pin is digital input (GP3)
#pragma config BOREN = OFF      // Brown-out Reset disabled
#pragma config CP = OFF         // Code Protection disabled
#pragma config CPD = OFF        // Data EEPROM Code Protection disabled

#define _XTAL_FREQ 4000000      // 4MHz internal oscillator

// Pin Definitions
#define RELAY   GPIObits.GP0    // Relay control pin (pin 7)
#define MOSFET  GPIObits.GP1    // MOSFET control pin (moved to pin 6)

// Timing Constants (in seconds)
#define FORWARD_DURATION   33
#define REVERSE_DURATION   31
#define OFF_DURATION       30
#define RELAY_SWITCH_DELAY 1    // Optional delay between direction change

void delay_seconds(unsigned int sec) {
    while (sec--) {
        __delay_ms(1000);
    }
}

void motor_forward() {
    RELAY = 0;     // Original polarity
    MOSFET = 1;    // Motor ON
}

void motor_reverse() {
    MOSFET = 0;    // Motor OFF before switching direction
    delay_seconds(RELAY_SWITCH_DELAY);
    RELAY = 1;     // Reverse polarity
    MOSFET = 1;    // Motor ON again
}

void motor_off() {
    MOSFET = 0;
    RELAY = 0;
}

void setup() {
    TRISIO = 0b00001000; // GP3 input, others output (GP0, GP1, GP2)
    GPIO = 0x00;         // Clear all outputs
    ANSEL = 0x00;        // Disable analog inputs
    CMCON0 = 0x07;       // Disable comparators
}

void main() {
    setup();

    while (1) {
        motor_forward();
        delay_seconds(FORWARD_DURATION);

        motor_reverse();
        delay_seconds(REVERSE_DURATION);

        motor_off();
        delay_seconds(OFF_DURATION);
    }
}
