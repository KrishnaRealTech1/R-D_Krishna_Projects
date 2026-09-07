/*
 * PIC16F688 - Dual capacitive touch through plastic (electrodes inside handle)
 *
 * Hardware:
 *   - Electrode 0 (copper rod/tape) -> RA0
 *   - 4.7M to 10M resistor from RA0 -> GND (required)
 *   - LED (with resistor) on RC4
 *
 *   - Electrode 1 (copper rod/tape) -> RA1
 *   - 4.7M to 10M resistor from RA1 -> GND (required)
 *   - LED (with resistor) on RC5
 *
 * How it works:
 *   Charge electrode (RAx output HIGH), then switch RAx to input and time
 *   how long it takes to fall LOW through the megaohm resistor.
 *   Touch increases capacitance => longer discharge time.
 */

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

// ================= CONFIGURATION =================
#pragma config FOSC = INTOSCIO
#pragma config WDTE = OFF
#pragma config PWRTE = OFF
#pragma config MCLRE = OFF
#pragma config BOREN = OFF
#pragma config CP = OFF
#pragma config CPD = OFF
// =================================================

#define _XTAL_FREQ 4000000UL

// ------------ Tuning knobs ------------
#define SAMPLE_PERIOD_MS        5     // sampling interval
#define CHARGE_US               10    // charge time (us)
#define DISCHARGE_SETTLE_US     50    // ensure fully discharged (us)

// Threshold: touch when filtered_time > baseline + TOUCH_DELTA
#define TOUCH_DELTA             25    // timer ticks (~us) above baseline

// Filter strengths (bigger shift = slower changes)
// filtered = filtered*(2^N - 1)/2^N + sample/2^N
#define FILTER_SHIFT            2     // 2 => 1/4 new sample
#define BASELINE_SHIFT          4     // 4 => 1/16 baseline adaptation (only when not touched)

// Safety limit for timing loop (prevents lockup)
#define TIMEOUT_TICKS           60000

// ---- Touch channel masks on PORTA ----
#define TOUCH_CH0_MASK          (1u << 0)   // RA0
#define TOUCH_CH1_MASK          (1u << 1)   // RA1

// ---- LEDs ----
#define LED0_LAT                PORTCbits.RC4
#define LED1_LAT                PORTCbits.RC5

static void init_pic(void);
static uint16_t touch_measure_ticks_mask(uint8_t mask);
static uint16_t avg_samples(uint8_t n, uint8_t mask);

static void init_pic(void)
{
    // -------- Oscillator: 4 MHz --------
    OSCCON = 0b01100001;

    // -------- Disable analog & comparators --------
    ANSEL  = 0x00;
    CMCON0 = 0x07;

    // -------- I/O direction --------
    TRISAbits.TRISA0 = 1;   // RA0 input by default
    TRISAbits.TRISA1 = 1;   // RA1 input by default

    TRISCbits.TRISC4 = 0;   // RC4 output for LED0
    TRISCbits.TRISC5 = 0;   // RC5 output for LED1

    // -------- Disable weak pull-ups --------
    OPTION_REG |= 0x80;     // Disable global pull-ups
    WPUA = 0x00;

    // -------- LEDs off --------
    LED0_LAT = 0;
    LED1_LAT = 0;

    // -------- Timer1 setup (internal clock = Fosc/4) --------
    // At 4 MHz: Fosc/4 = 1 MHz => 1 tick = 1 us (with prescaler 1:1)
    T1CONbits.TMR1ON  = 0;
    T1CONbits.TMR1CS  = 0;  // internal clock
    T1CONbits.T1CKPS0 = 0;  // prescaler 1:1
    T1CONbits.T1CKPS1 = 0;
    TMR1H = 0;
    TMR1L = 0;
}

static uint16_t touch_measure_ticks_mask(uint8_t mask)
{
    // 1) Charge electrode capacitance by driving RA(bit) HIGH briefly
    //    (do not disturb other PORTA pins)
    TRISA &= (uint8_t)~mask;    // selected pin as output
    PORTA |= mask;              // drive selected pin high
    __delay_us(CHARGE_US);

    // 2) Switch to input; electrode discharges via external megaohm to GND
    TRISA |= mask;              // input (high-Z)
    PORTA &= (uint8_t)~mask;    // keep output latch low for next time

    // 3) Time until pin reads LOW
    TMR1H = 0;
    TMR1L = 0;
    T1CONbits.TMR1ON = 1;

    while (PORTA & mask)
    {
        uint16_t t = ((uint16_t)TMR1H << 8) | (uint16_t)TMR1L;
        if (t >= TIMEOUT_TICKS)
            break;
    }

    T1CONbits.TMR1ON = 0;

    // 4) Ensure it fully discharges before next sample
    __delay_us(DISCHARGE_SETTLE_US);

    return ((uint16_t)TMR1H << 8) | (uint16_t)TMR1L;
}

static uint16_t avg_samples(uint8_t n, uint8_t mask)
{
    uint32_t sum = 0;
    for (uint8_t i = 0; i < n; i++)
    {
        sum += (uint32_t)touch_measure_ticks_mask(mask);
        __delay_ms(2);
    }
    return (uint16_t)(sum / n);
}

void main(void)
{
    init_pic();

    // -------- Baseline calibration at startup (keep hands off electrodes) --------
    uint16_t baseline0 = avg_samples(32, TOUCH_CH0_MASK);
    uint16_t filtered0 = baseline0;

    uint16_t baseline1 = avg_samples(32, TOUCH_CH1_MASK);
    uint16_t filtered1 = baseline1;

    while (1)
    {
        uint16_t sample0 = touch_measure_ticks_mask(TOUCH_CH0_MASK);
        uint16_t sample1 = touch_measure_ticks_mask(TOUCH_CH1_MASK);

        // -------- Simple IIR filter on measurements --------
        filtered0 = (uint16_t)(
            ( (uint32_t)filtered0 * ((1u << FILTER_SHIFT) - 1u) + (uint32_t)sample0 )
            >> FILTER_SHIFT
        );

        filtered1 = (uint16_t)(
            ( (uint32_t)filtered1 * ((1u << FILTER_SHIFT) - 1u) + (uint32_t)sample1 )
            >> FILTER_SHIFT
        );

        // -------- Touch decisions --------
        bool touched0 = (filtered0 > (uint16_t)(baseline0 + TOUCH_DELTA));
        bool touched1 = (filtered1 > (uint16_t)(baseline1 + TOUCH_DELTA));

        // -------- Update LEDs --------
        LED0_LAT = touched0 ? 1 : 0;
        LED1_LAT = touched1 ? 1 : 0;

        // -------- Baseline tracking (only when not touched) --------
        if (!touched0)
        {
            baseline0 = (uint16_t)(
                ( (uint32_t)baseline0 * ((1u << BASELINE_SHIFT) - 1u) + (uint32_t)filtered0 )
                >> BASELINE_SHIFT
            );
        }

        if (!touched1)
        {
            baseline1 = (uint16_t)(
                ( (uint32_t)baseline1 * ((1u << BASELINE_SHIFT) - 1u) + (uint32_t)filtered1 )
                >> BASELINE_SHIFT
            );
        }

        __delay_ms(SAMPLE_PERIOD_MS);
    }
}
