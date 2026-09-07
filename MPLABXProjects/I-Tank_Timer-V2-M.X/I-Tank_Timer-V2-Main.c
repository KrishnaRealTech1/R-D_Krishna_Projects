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

#define RELAY GP0               // Relay control pin (pin 7)
#define MOSFET GP2              // MOSFET control pin (pin 5)

void delay_seconds(unsigned int sec) {
    while(sec--) {
        __delay_ms(1000);
    }
}

void main() {
    TRISIO = 0b00001000;        // GP3 as input, others as output
    GPIO = 0x00;                // All outputs low
    ANSEL = 0x00;               // Disable analog inputs
    CMCON0 = 0x07;   // Disable comparators (CM<2:0> = 111)


    while(1) {
        // Forward direction
        RELAY = 0;              // Relay OFF (original polarity)
        MOSFET = 1;             // Motor ON
        delay_seconds(33);      // Run for 33 seconds

        // Reverse direction
        RELAY = 1;              // Relay ON (reverse polarity)
        delay_seconds(31);      // Run for 31 seconds

        // Turn off motor
        MOSFET = 0;             // Motor OFF
        RELAY = 0;              // Reset relay

        // Wait 4 hours before next cleaning
        for (unsigned int i = 0; i < 14400; i++) {  // 4 hours = 14400 seconds
            __delay_ms(1000);
        }
    }
}
