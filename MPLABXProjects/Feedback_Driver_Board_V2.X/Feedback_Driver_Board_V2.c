// -----------------------------------------------------------------------------
// PIC12F683 Configuration Bit Settings
// -----------------------------------------------------------------------------
#pragma config FOSC  = INTOSCIO  // Internal oscillator, GP4/GP5 used as GPIO
#pragma config WDTE  = OFF       // Watchdog Timer disabled
#pragma config PWRTE = ON        // Power-up Timer enabled
#pragma config MCLRE = OFF       // GP3/MCLR used as digital input
#pragma config CP    = OFF       // Program memory protection disabled
#pragma config CPD   = OFF       // Data EEPROM protection disabled
#pragma config BOREN = ON        // Brown-out Reset enabled

#include <xc.h>

// -----------------------------------------------------------------------------
// Pin assignments
//
// PIC12F683 physical pin 5 = GP2 = active-HIGH trigger input
// PIC12F683 physical pin 7 = GP0 = active-HIGH output
//
// Initial trigger sequence:
//
//     GP2 initially LOW
//     GP2 changes LOW to HIGH
//     Wait 5 seconds
//     GP0 becomes HIGH for 3 seconds
//     GP0 returns LOW
//
// Retrigger sequence:
//
//     After GP0 returns LOW, monitor GP2.
//     If GP2 remains continuously HIGH for 10 seconds:
//         GP0 becomes HIGH for 3 seconds
//         GP0 returns LOW
//
//     Repeat the 10-second monitoring and 3-second output pulse
//     while GP2 remains HIGH.
//
//     When GP2 becomes stably LOW, retriggering stops and the
//     system waits for the next LOW-to-HIGH trigger.
// -----------------------------------------------------------------------------
#define PIN5_INPUT_MASK  0x04U

// -----------------------------------------------------------------------------
// Input states
//
// GPIO bit 2:
//
//     1 = physical pin 5 is HIGH
//     0 = physical pin 5 is LOW
// -----------------------------------------------------------------------------
#define PIN5_IS_HIGH()   ((GPIO & PIN5_INPUT_MASK) != 0U)
#define PIN5_IS_LOW()    ((GPIO & PIN5_INPUT_MASK) == 0U)

// -----------------------------------------------------------------------------
// Output control
//
// Physical pin 7 / GP0:
//
//     LOW  = output inactive
//     HIGH = output active
// -----------------------------------------------------------------------------
#define PIN7_OFF()       do { GPIObits.GP0 = 0; } while (0)
#define PIN7_ON()        do { GPIObits.GP0 = 1; } while (0)

// -----------------------------------------------------------------------------
// Timing configuration
// -----------------------------------------------------------------------------
#define INITIAL_TRIGGER_DELAY_SECONDS  5U
#define RETRIGGER_DELAY_SECONDS       10U
#define PIN7_ON_TIME_SECONDS           3U

#define DEBOUNCE_COUNT                 2U
#define TEN_MS_TICKS_PER_SECOND      100U

// -----------------------------------------------------------------------------
// Function declarations
// -----------------------------------------------------------------------------
static void Initialize_Device(void);

static void Delay_10ms(void);
static void Delay_1Second(void);
static void Delay_Seconds(unsigned char seconds);

static unsigned char Pin5_Is_Stable_Low(void);
static unsigned char Pin5_Is_Stable_High(void);

static void Wait_For_Pin5_Low(void);
static void Wait_For_Pin5_High(void);

static unsigned char Pin5_Remains_High_For_Seconds(
    unsigned char seconds
);

static void Pulse_Pin7(void);
static void Run_Initial_Pin7_Sequence(void);

// -----------------------------------------------------------------------------
// Initialize PIC12F683
// -----------------------------------------------------------------------------
static void Initialize_Device(void)
{
    // -------------------------------------------------------------------------
    // Internal oscillator configuration
    //
    // IRCF = 110:
    //     Internal oscillator frequency = 4 MHz
    //
    // SCS = 1:
    //     Internal oscillator is used as the system clock.
    // -------------------------------------------------------------------------
    OSCCONbits.IRCF = 0b110;
    OSCCONbits.SCS  = 1;

    // -------------------------------------------------------------------------
    // Disable analog input functions.
    //
    // GP2 is shared with AN2. ANSEL must be cleared before GP2 can operate
    // correctly as a digital input.
    // -------------------------------------------------------------------------
    ANSEL = 0x00;

    // Disable the comparator module.
    CMCON0 = 0x07;

    // -------------------------------------------------------------------------
    // Clear the GPIO output latch.
    //
    // This ensures physical pin 7 / GP0 starts LOW.
    // -------------------------------------------------------------------------
    GPIO = 0x00;

    // -------------------------------------------------------------------------
    // GPIO direction configuration
    //
    // TRISIO:
    //
    //     1 = input
    //     0 = output
    //
    //            GP5 GP4 GP3 GP2 GP1 GP0
    // Direction:  1   1   1   1   1   0
    //
    // GP2 / physical pin 5 = input
    // GP0 / physical pin 7 = output
    // -------------------------------------------------------------------------
    TRISIO = 0b00111110;

    // -------------------------------------------------------------------------
    // Disable internal weak pull-ups.
    //
    // External input circuitry controls physical pin 5.
    // -------------------------------------------------------------------------
    WPU = 0x00;

    // nGPPU = 1 disables GPIO weak pull-ups globally.
    OPTION_REGbits.nGPPU = 1;

    // -------------------------------------------------------------------------
    // Timer0 configuration
    //
    // Fosc = 4 MHz
    // Instruction frequency = Fosc / 4 = 1 MHz
    // Instruction period = 1 microsecond
    //
    // Timer0 prescaler = 1:256
    // Timer0 increment period = 256 microseconds
    // -------------------------------------------------------------------------
    OPTION_REGbits.T0CS = 0;
    OPTION_REGbits.PSA  = 0;
    OPTION_REGbits.PS2  = 1;
    OPTION_REGbits.PS1  = 1;
    OPTION_REGbits.PS0  = 1;

    // Disable interrupt-on-change.
    IOC = 0x00;

    // Disable all interrupts.
    INTCONbits.GIE  = 0;
    INTCONbits.PEIE = 0;
    INTCONbits.T0IE = 0;
    INTCONbits.INTE = 0;
    INTCONbits.GPIE = 0;

    // Clear interrupt flags.
    INTCONbits.T0IF = 0;
    INTCONbits.INTF = 0;
    INTCONbits.GPIF = 0;

    // Reset Timer0.
    TMR0 = 0;

    // Physical pin 7 starts LOW.
    PIN7_OFF();
}

// -----------------------------------------------------------------------------
// Approximately 10 millisecond delay
//
// Timer0 increment period:
//
//     256 microseconds
//
// Required Timer0 counts:
//
//     10,000 us / 256 us = approximately 39 counts
//
// Timer0 preload:
//
//     256 - 39 = 217
//
// Actual delay:
//
//     39 × 256 us = 9.984 milliseconds
// -----------------------------------------------------------------------------
static void Delay_10ms(void)
{
    TMR0 = 217;
    INTCONbits.T0IF = 0;

    while (INTCONbits.T0IF == 0)
    {
        // Wait for Timer0 overflow.
    }

    INTCONbits.T0IF = 0;
}

// -----------------------------------------------------------------------------
// Approximately one-second delay
//
// Timer0 preload:
//
//     12
//
// Counts before overflow:
//
//     256 - 12 = 244
//
// Time for each overflow:
//
//     244 × 256 us = 62.464 milliseconds
//
// Sixteen overflows:
//
//     62.464 ms × 16 = 999.424 milliseconds
// -----------------------------------------------------------------------------
static void Delay_1Second(void)
{
    unsigned char overflowCount;

    for (overflowCount = 0U;
         overflowCount < 16U;
         overflowCount++)
    {
        TMR0 = 12;
        INTCONbits.T0IF = 0;

        while (INTCONbits.T0IF == 0)
        {
            // Wait for Timer0 overflow.
        }

        INTCONbits.T0IF = 0;
    }
}

// -----------------------------------------------------------------------------
// Delay for the requested number of seconds
// -----------------------------------------------------------------------------
static void Delay_Seconds(unsigned char seconds)
{
    while (seconds > 0U)
    {
        Delay_1Second();
        seconds--;
    }
}

// -----------------------------------------------------------------------------
// Check whether physical pin 5 remains LOW.
//
// Approximately 20 ms of debounce and noise filtering is applied.
//
// Returns:
//
//     1 = pin 5 is stably LOW
//     0 = pin 5 is HIGH or unstable
// -----------------------------------------------------------------------------
static unsigned char Pin5_Is_Stable_Low(void)
{
    unsigned char sampleCount;

    for (sampleCount = 0U;
         sampleCount < DEBOUNCE_COUNT;
         sampleCount++)
    {
        if (!PIN5_IS_LOW())
        {
            return 0U;
        }

        Delay_10ms();
    }

    if (PIN5_IS_LOW())
    {
        return 1U;
    }

    return 0U;
}

// -----------------------------------------------------------------------------
// Check whether physical pin 5 remains HIGH.
//
// Approximately 20 ms of debounce and noise filtering is applied.
//
// Returns:
//
//     1 = pin 5 is stably HIGH
//     0 = pin 5 is LOW or unstable
// -----------------------------------------------------------------------------
static unsigned char Pin5_Is_Stable_High(void)
{
    unsigned char sampleCount;

    for (sampleCount = 0U;
         sampleCount < DEBOUNCE_COUNT;
         sampleCount++)
    {
        if (!PIN5_IS_HIGH())
        {
            return 0U;
        }

        Delay_10ms();
    }

    if (PIN5_IS_HIGH())
    {
        return 1U;
    }

    return 0U;
}

// -----------------------------------------------------------------------------
// Wait until physical pin 5 becomes stably LOW.
//
// Pin 5 LOW arms the system for the next HIGH trigger.
// -----------------------------------------------------------------------------
static void Wait_For_Pin5_Low(void)
{
    while (1)
    {
        while (PIN5_IS_HIGH())
        {
            Delay_10ms();
        }

        if (Pin5_Is_Stable_Low())
        {
            return;
        }

        // The detected LOW was unstable.
        // Continue waiting for a stable LOW.
    }
}

// -----------------------------------------------------------------------------
// Wait until physical pin 5 becomes stably HIGH.
//
// A stable HIGH is the active trigger.
// -----------------------------------------------------------------------------
static void Wait_For_Pin5_High(void)
{
    while (1)
    {
        while (PIN5_IS_LOW())
        {
            Delay_10ms();
        }

        if (Pin5_Is_Stable_High())
        {
            return;
        }

        // The detected HIGH was unstable.
        // Continue waiting for a stable HIGH.
    }
}

// -----------------------------------------------------------------------------
// Monitor whether physical pin 5 remains HIGH for the requested duration.
//
// The input is checked approximately every 10 milliseconds.
//
// Behavior:
//
//     Pin 5 remains HIGH:
//         The HIGH-duration counter continues.
//
//     Pin 5 momentarily becomes LOW:
//         The HIGH-duration counter is reset.
//
//     Pin 5 becomes stably LOW:
//         Return immediately and stop retriggering.
//
// Returns:
//
//     1 = pin 5 remained HIGH for the complete duration
//     0 = pin 5 became stably LOW before the duration completed
// -----------------------------------------------------------------------------
static unsigned char Pin5_Remains_High_For_Seconds(
    unsigned char seconds
)
{
    unsigned int highTickCount;
    unsigned int requiredHighTicks;

    highTickCount = 0U;

    requiredHighTicks =
        (unsigned int)seconds *
        (unsigned int)TEN_MS_TICKS_PER_SECOND;

    // Handle a zero-second request safely.
    if (requiredHighTicks == 0U)
    {
        if (PIN5_IS_HIGH())
        {
            return 1U;
        }

        return 0U;
    }

    while (1)
    {
        // Check the input before starting the next timing interval.
        if (PIN5_IS_LOW())
        {
            // Any detected LOW breaks the continuous-HIGH timing.
            highTickCount = 0U;

            // Exit retrigger mode only when LOW is stable.
            if (Pin5_Is_Stable_Low())
            {
                return 0U;
            }

            // It was only an unstable or brief LOW.
            // Restart the 10-second HIGH timing from zero.
            continue;
        }

        // Pin 5 is currently HIGH.
        Delay_10ms();

        // Confirm that pin 5 remained HIGH during this timing interval.
        if (PIN5_IS_HIGH())
        {
            highTickCount++;

            if (highTickCount >= requiredHighTicks)
            {
                return 1U;
            }
        }
        else
        {
            // Pin 5 changed LOW during the 10 ms interval.
            // Reset the continuous-HIGH timer.
            highTickCount = 0U;
        }
    }
}

// -----------------------------------------------------------------------------
// Generate one physical pin 7 output pulse.
//
//     GP0 becomes HIGH
//     GP0 remains HIGH for 3 seconds
//     GP0 returns LOW
// -----------------------------------------------------------------------------
static void Pulse_Pin7(void)
{
    PIN7_ON();

    Delay_Seconds(PIN7_ON_TIME_SECONDS);

    PIN7_OFF();
}

// -----------------------------------------------------------------------------
// Initial physical pin 7 output sequence.
//
// After physical pin 5 changes from LOW to HIGH:
//
//     1. Keep physical pin 7 LOW.
//     2. Wait 5 seconds.
//     3. Generate one 3-second pin 7 output pulse.
// -----------------------------------------------------------------------------
static void Run_Initial_Pin7_Sequence(void)
{
    // Ensure physical pin 7 starts LOW.
    PIN7_OFF();

    // Wait five seconds after detecting pin 5 HIGH.
    Delay_Seconds(INITIAL_TRIGGER_DELAY_SECONDS);

    // Generate the first three-second output pulse.
    Pulse_Pin7();
}

// -----------------------------------------------------------------------------
// Main program
// -----------------------------------------------------------------------------
void main(void)
{
    Initialize_Device();

    // -------------------------------------------------------------------------
    // Startup behavior
    //
    // Pin 5 must first become stably LOW.
    //
    // This prevents the controller from triggering immediately at startup
    // when pin 5 is already HIGH.
    // -------------------------------------------------------------------------
    PIN7_OFF();
    Wait_For_Pin5_Low();

    while (1)
    {
        // Physical pin 7 normally remains LOW.
        PIN7_OFF();

        // Wait for physical pin 5 to change from LOW to stable HIGH.
        Wait_For_Pin5_High();

        // ---------------------------------------------------------------------
        // Initial trigger:
        //
        //     Wait 5 seconds
        //     Pin 7 HIGH for 3 seconds
        //     Pin 7 LOW
        // ---------------------------------------------------------------------
        Run_Initial_Pin7_Sequence();

        // ---------------------------------------------------------------------
        // Repeated trigger behavior:
        //
        //     If pin 5 remains HIGH continuously for another 10 seconds:
        //         Pin 7 HIGH for 3 seconds
        //         Pin 7 LOW
        //
        //     Repeat while pin 5 remains HIGH.
        //
        // The 10-second timer starts after each pin 7 pulse finishes.
        // ---------------------------------------------------------------------
        while (Pin5_Remains_High_For_Seconds(
                   RETRIGGER_DELAY_SECONDS))
        {
            Pulse_Pin7();
        }

        // Pin 5 became stably LOW.
        //
        // Confirm the LOW state and rearm the controller for the next
        // LOW-to-HIGH transition.
        PIN7_OFF();
        Wait_For_Pin5_Low();
    }
}