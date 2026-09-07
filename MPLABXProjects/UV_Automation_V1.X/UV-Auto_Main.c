#include <xc.h>

// CONFIGURATION BITS
#pragma config FOSC = INTOSCIO  // Internal oscillator, I/O on GP4/GP5
#pragma config WDTE = OFF       // Watchdog Timer disabled
#pragma config PWRTE = ON       // Power-up Timer enabled
#pragma config MCLRE = OFF      // MCLR pin is digital input
#pragma config BOREN = OFF      // Brown-out Reset disabled
#pragma config CP = OFF         // Code Protection disabled
#pragma config CPD = OFF        // Data EEPROM Code Protection disabled

#define _XTAL_FREQ 4000000      // 4 MHz internal clock

// Pin Definitions
#define RELAY  GPIObits.GP0     // Relay control (pin 7)
#define MOSFET GPIObits.GP1     // MOSFET control (pin 6)

// Timing Constants
#define FORWARD_DURATION   33   // Forward motor run time (seconds)
#define REVERSE_DURATION   31   // Reverse motor run time (seconds)
#define OFF_DELAY          2    // Delay after reverse before OFF (safety)
#define CYCLE_INTERVAL     14400 // 4 hours in seconds (cleaning cycle)

// Delay helper
void delay_seconds(unsigned int seconds) {
    while (seconds--) {
        __delay_ms(1000);
    }
}

void setup() {
    TRISIO = 0b00001000; // GP3 = input, others = output
    ANSEL = 0x00;        // Disable analog functions
    CMCON0 = 0x07;       // Disable comparators
    GPIO = 0x00;         // Initialize all outputs low
}

void main() {
    setup();

    while (1) {
        // --- Forward Direction ---
        RELAY = 0;        // Set relay to original polarity
        __delay_ms(100);  // Relay settle time
        MOSFET = 1;       // Turn ON motor
        delay_seconds(FORWARD_DURATION);

        // --- Reverse Direction ---
        MOSFET = 0;       // Turn off motor before switching
        __delay_ms(300);  // Safety delay before switching
        RELAY = 1;        // Change relay to reverse polarity
        __delay_ms(100);  // Relay settle time
        MOSFET = 1;       // Turn ON motor
        delay_seconds(REVERSE_DURATION);

        // --- Turn Off ---
        MOSFET = 0;
        RELAY = 0;
        delay_seconds(OFF_DELAY);

        // --- Wait for next cycle (4 hours) ---
        for (unsigned int i = 0; i < CYCLE_INTERVAL; i++) {
            __delay_ms(1000);
        }
    }
}
