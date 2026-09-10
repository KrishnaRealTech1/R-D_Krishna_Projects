// PIC12F683 Configuration Bit Settings
#pragma config FOSC = INTOSCIO  // Internal oscillator, GPIO on GP4/GP5
#pragma config WDTE = OFF       // Watchdog Timer disabled
#pragma config PWRTE = ON       // Power-up Timer enabled
#pragma config MCLRE = OFF      // MCLR pin function is digital input
#pragma config CP = OFF         // Code protection disabled
#pragma config CPD = OFF        // Data code protection disabled
#pragma config BOREN = ON       // Brown-out Reset enabled

#include <xc.h>
#define _XTAL_FREQ 4000000   // 4 MHz internal oscillator

// Define DIP switch pins
#define DIP1 GP1  // 5 sec delay
#define DIP2 GP2  // 10 sec delay
#define DIP3 GP4  // 15 sec delay
#define DIP4 GP5  // 20 sec delay

// Function to read DIP switch settings and determine off delay
unsigned int Get_Off_Delay() {
    unsigned int delay = 0;
    if (DIP1) delay += 5;  // 5 sec
    if (DIP2) delay += 10; // 10 sec
    if (DIP3) delay += 15; // 15 sec
    if (DIP4) delay += 20; // 20 sec

    if (delay == 0) delay = 3; // Default to 3 sec if no switch is ON
    return delay;
}

void Wait_Seconds(unsigned int sec) {
    while (sec--) {
        __delay_ms(1000); // 1 sec
        ond delay
    }
}

void main(void) {
    // GPIO Initialization
    TRISIO = 0b11110110;  // Set GP0 as output (relay), GP1, GP2, GP4, GP5 as inputs (DIP switches)
    ANSEL = 0x00;         // Disable analog inputs
    CMCON0 = 0x07;        // Disable comparators
    GPIO = 0;             // Clear all GPIO pins

    while (1) {
        unsigned int off_delay = Get_Off_Delay(); // Read DIP switch setting

        GP0 = 0;  // Relay OFF
        Wait_Seconds(off_delay); // Wait based on selected off delay

        GP0 = 1;  // Relay ON
        Wait_Seconds(3); // Fixed ON time of 3 seconds
    }
}
