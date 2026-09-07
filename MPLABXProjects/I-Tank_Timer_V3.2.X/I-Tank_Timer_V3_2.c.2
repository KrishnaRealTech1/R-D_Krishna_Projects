// PIC12F683 Configuration Bit Settings
#pragma config FOSC  = INTOSCIO  // Internal oscillator, GPIO on GP4/GP5
#pragma config WDTE  = OFF       // Watchdog Timer disabled
#pragma config PWRTE = ON        // Power-up Timer enabled
#pragma config MCLRE = OFF       // MCLR pin function is digital input
#pragma config CP    = OFF       // Code protection disabled
#pragma config CPD   = OFF       // Data code protection disabled
#pragma config BOREN = ON        // Brown-out Reset enabled

#include <xc.h>
#define _XTAL_FREQ 4000000UL     // 4 MHz internal oscillator (for reference)

// -----------------------------------------------------------------------------
// DIP switch pins (inputs)
// -----------------------------------------------------------------------------
#define DIP1 GP1  // 1 Hour delay
#define DIP2 GP2  // 3 Hour delay
#define DIP3 GP4  // 8 Hour delay
#define DIP4 GP5  // 12 Hour delay

// -----------------------------------------------------------------------------
// Initialize device: GPIO, analog, comparator, Timer0
// -----------------------------------------------------------------------------
static void init_device(void)
{
    // Disable analog inputs and comparators
    ANSEL  = 0x00;       // All digital
    CMCON0 = 0x07;       // Comparators off

    // Set internal oscillator to 4 MHz (matches _XTAL_FREQ)
    // After reset it is 4 MHz by default, but this is explicit & safe.
    OSCCONbits.IRCF = 0b110;  // 4 MHz
    OSCCONbits.SCS  = 0;      // Clock source from config (INTOSCIO)

    // TRISIO: 1=input, 0=output
    // GP0 = output (relay)
    // GP1, GP2, GP4, GP5 = inputs (DIP switches)
    // GP3 is input-only; leave as 1.
    TRISIO = 0b11111110;      // GP0=0 (output), all others = 1 (inputs)

    GPIO = 0x00;              // Clear all outputs

    // Timer0 setup: Fosc/4 as source, prescaler 1:256 on TMR0
    OPTION_REGbits.T0CS = 0;  // Timer0 clock source = instruction clock (Fosc/4)
    OPTION_REGbits.PSA  = 0;  // Prescaler assigned to Timer0
    OPTION_REGbits.PS2  = 1;  // Prescaler bits = 111 => 1:256
    OPTION_REGbits.PS1  = 1;
    OPTION_REGbits.PS0  = 1;

    // Do NOT enable Timer0 interrupt; we just poll the flag.
    INTCONbits.T0IE = 0;
    INTCONbits.T0IF = 0;
}

// -----------------------------------------------------------------------------
// Blocking delay of ~1 second using Timer0
//
// Fosc  = 4 MHz -> Fcy = Fosc/4 = 1 MHz = 1 us per instruction
// Prescaler = 1:256 => TMR0 increments every 256 us
// 256 counts * 256 us ? 65.536 ms per overflow
// 16 overflows ? 1.048 s  (close enough for a tank timer)
// -----------------------------------------------------------------------------
static void delay_1s(void)
{
    unsigned char i;

    for (i = 0; i < 16; i++)
    {
        TMR0 = 0;              // Start from 0
        INTCONbits.T0IF = 0;   // Clear overflow flag

        // Wait for TMR0 to overflow
        while (INTCONbits.T0IF == 0)
        {
            // Busy wait; CPU is idle here
        }
    }
}

// -----------------------------------------------------------------------------
// Wait N seconds (blocking).  Supports very large N (unsigned long).
// -----------------------------------------------------------------------------
void Wait_Seconds(unsigned long sec)
{
    while (sec--)
    {
        delay_1s();
    }
}

// -----------------------------------------------------------------------------
// Read DIP switch settings and determine OFF delay in seconds.
// DIP1 =  1h  = 3600 s
// DIP2 =  3h  = 10800 s
// DIP3 =  8h  = 28800 s
// DIP4 = 12h  = 43200 s
//
// All can be combined: max = 1+3+8+12 = 24h = 86400 s (needs unsigned long).
// If no switch is ON, default to 10 seconds.
// -----------------------------------------------------------------------------
unsigned long Get_Off_Delay(void)
{
    unsigned long delay = 0;

    if (DIP1) delay += 3600UL;   // 1 hour
    if (DIP2) delay += 10800UL;  // 3 hours
    if (DIP3) delay += 28800UL;  // 8 hours
    if (DIP4) delay += 43200UL;  // 12 hours

    if (delay == 0UL)
    {
        delay = 10UL;            // Default to 10 seconds if no switch is ON
    }

    return delay;
}

// -----------------------------------------------------------------------------
// Main
// -----------------------------------------------------------------------------
void main(void)
{
    init_device();

    while (1)
    {
        unsigned long off_delay = Get_Off_Delay();  // Read DIP switch settings

        // ---------------------- Relay OFF phase ----------------------
        GP0 = 0;                   // Relay OFF
        Wait_Seconds(off_delay);   // Wait based on selected OFF delay

        // ---------------------- Relay ON phase -----------------------
        GP0 = 1;                   // Relay ON
        Wait_Seconds(3600UL);        // Fixed ON time of 1 hour
    }
}
