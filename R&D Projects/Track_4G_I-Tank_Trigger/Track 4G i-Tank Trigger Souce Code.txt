/*
 * PIC12F683 - Delayed trigger + hold (5 seconds)
 *
 * INPUT  (GP2, pin 5) HIGH continuously for 5 seconds -> OUTPUT (GP5, pin 2) HIGH
 * OUTPUT stays HIGH while INPUT stays HIGH
 * INPUT LOW -> OUTPUT LOW immediately and timer resets
 *
 * No __delay_ms(). Uses Timer0 overflow as ~1ms tick.
 * Uses GPIO shadow register to avoid read-modify-write glitches.
 */

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

// ===================== Configuration Bits =====================
#pragma config FOSC  = INTOSCIO
#pragma config WDTE  = OFF
#pragma config PWRTE = ON
#pragma config MCLRE = ON
#pragma config CP    = OFF
#pragma config CPD   = OFF
#pragma config BOREN = ON
#pragma config IESO  = OFF
#pragma config FCMEN = OFF

// ===================== Pin mapping =====================
// Input  = GP2 (pin 5)
// Output = GP5 (pin 2)
#define INPUT_RAW()           (GPIObits.GP2)
#define OUTPUT_MASK           (1u << 5)

// ===================== Timing =====================
#define DEBOUNCE_MS           10u     // set 0 if input is clean logic
#define TRIGGER_DELAY_MS    5000u

// ===================== GPIO shadow latch =====================
static volatile uint8_t gpio_shadow = 0;

// Write shadow to port (single point)
static inline void gpio_apply(void)
{
    GPIO = gpio_shadow;
}

// Set/Clear output using shadow only (no reading GPIO)
static inline void output_set(bool on)
{
    if (on)
        gpio_shadow |= (uint8_t)OUTPUT_MASK;
    else
        gpio_shadow &= (uint8_t)~OUTPUT_MASK;

    gpio_apply();
}

// ===================== Timer0 ~1ms tick =====================
// Fosc=4MHz => instruction clock=1MHz (1us)
// Prescaler=1:32 => TMR0 increments every 32us
// 31 counts => ~992us (~1ms)
static void tick_1ms(void)
{
    // ---- four+ lines before ----
    // Timer0 overflow tick ~1ms
    // internal clock + prescaler 1:32
    // preload so overflow occurs in ~1ms
    // blocking wait (fine for simple control)

    TMR0 = (uint8_t)(256u - 31u);
    INTCONbits.T0IF = 0;
    while (!INTCONbits.T0IF) { ; }
    INTCONbits.T0IF = 0;

    // ---- four+ lines after ----
}

static void mcu_init(void)
{
    // Internal oscillator = 4 MHz
    OSCCONbits.IRCF = 0b110;
    OSCCONbits.SCS  = 1;

    // Disable analog
    ANSEL  = 0x00;
    ADCON0 = 0x00;
    CMCON0 = 0x07;

    // Make outputs well-defined to reduce floating nodes:
    // GP2 input, GP3 input-only; GP0/GP1/GP4/GP5 outputs
    TRISIO = 0b00001100;

    // Timer0: internal clock, prescaler 1:32, pull-ups off
    OPTION_REGbits.T0CS  = 0;        // internal clock
    OPTION_REGbits.PSA   = 0;        // prescaler to TMR0
    OPTION_REGbits.PS    = 0b100;    // 1:32
    OPTION_REGbits.nGPPU = 1;        // disable weak pull-ups

    // Start with all outputs LOW
    gpio_shadow = 0x00;
    gpio_apply();
    output_set(false);
}

void main(void)
{
    mcu_init();

    bool stable_in = false;
    uint16_t debounce_cnt = 0;
    uint16_t high_ms = 0;

    bool raw = false;

    while (1)
    {
        tick_1ms();

        // ---- four+ lines before ----
        // Debounce (non-blocking)
        // If raw != stable for DEBOUNCE_MS consecutive ms, accept new state
        // If DEBOUNCE_MS == 0, accept immediately
        // Keeps accurate 5s timing

        raw = (INPUT_RAW() ? true : false);

        if (raw == stable_in)
        {
            debounce_cnt = 0;
        }
        else
        {
            if (DEBOUNCE_MS == 0u)
            {
                stable_in = raw;
                debounce_cnt = 0;
            }
            else
            {
                if (debounce_cnt < DEBOUNCE_MS)
                    debounce_cnt++;

                if (debounce_cnt >= DEBOUNCE_MS)
                {
                    stable_in = raw;
                    debounce_cnt = 0;
                }
            }
        }
        // ---- four+ lines after ----

        // Delay + hold behavior
        if (stable_in)
        {
            if (high_ms < TRIGGER_DELAY_MS)
            {
                high_ms++;
                output_set(false);     // keep output OFF during delay
            }
            else
            {
                output_set(true);      // after delay, output ON
            }
        }
        else
        {
            high_ms = 0;
            output_set(false);
        }
    }
}
