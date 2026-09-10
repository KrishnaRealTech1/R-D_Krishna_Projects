/*
 * Project     : KousicandCo
 * Controller  : PIC12F683
 * Compiler    : MPLAB X + XC8 v2.x
 *
 * Requirement:
 *  - When Pin 2 is HIGH, send "Detects" through UART TX on Pin 5.
 *  - At the same time, trigger/blink Pin 3 three times for LED indication.
 *
 * Physical pin mapping for PIC12F683:
 *  - Pin 2 = GP5 = Input trigger
 *  - Pin 3 = GP4 = LED output
 *  - Pin 5 = GP2 = Software UART TX
 *
 * UART format:
 *  - 9600 baud
 *  - 8 data bits
 *  - No parity
 *  - 1 stop bit
 */

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

#pragma config FOSC  = INTOSCIO
#pragma config WDTE  = OFF
#pragma config PWRTE = ON
#pragma config MCLRE = OFF
#pragma config CP    = OFF
#pragma config CPD   = OFF
#pragma config BOREN = ON
#pragma config IESO  = OFF
#pragma config FCMEN = OFF

#define PIN2_INPUT_MASK     0x20u   /* GP5, physical pin 2 */
#define LED_PIN_MASK        0x10u   /* GP4, physical pin 3 */
#define UART_TX_PIN_MASK    0x04u   /* GP2, physical pin 5 */

#define CMD_TEXT            "Detects"

#define LED_ON_TIME_MS      120u
#define LED_OFF_TIME_MS     120u
#define DEBOUNCE_TIME_MS    20u

/*
 * Oscillator = 8 MHz
 * Instruction clock = FOSC / 4 = 2 MHz
 *
 * Timer2 UART:
 * Timer2 tick = 0.5 us with prescaler 1:1
 * 9600 baud bit time = 104.166 us
 * PR2 = 207 gives 208 ticks x 0.5 us = 104 us
 */
#define UART_TMR2_PR2_VALUE 207u

/*
 * Timer1 delay:
 * FOSC = 8 MHz
 * Instruction clock = 2 MHz
 * Timer1 prescaler = 1:8
 * Timer1 tick = 4 us
 *
 * For 1 ms:
 * 1000 us / 4 us = 250 ticks
 * Timer1 preload = 65536 - 250 = 65286 = 0xFF06
 */
#define TIMER1_1MS_PRELOAD_H 0xFFu
#define TIMER1_1MS_PRELOAD_L 0x06u

static volatile uint8_t gpio_shadow = UART_TX_PIN_MASK;

static volatile const char *uart_tx_ptr = 0;
static volatile uint16_t uart_tx_frame = 0;
static volatile uint8_t uart_tx_bits_remaining = 0;
static volatile bool uart_tx_busy = false;

static void delay_ms_custom(uint16_t ms)
{
    while (ms > 0u)
    {
        T1CON &= 0xFEu;

        TMR1H = TIMER1_1MS_PRELOAD_H;
        TMR1L = TIMER1_1MS_PRELOAD_L;

        PIR1bits.TMR1IF = 0;

        T1CON |= 0x01u;

        while (PIR1bits.TMR1IF == 0u)
        {
            ;
        }

        T1CON &= 0xFEu;
        PIR1bits.TMR1IF = 0;

        ms--;
    }
}

static void gpio_write_atomic(uint8_t mask, bool level)
{
    uint8_t gie_state;

    gie_state = INTCONbits.GIE;
    INTCONbits.GIE = 0;

    if (level)
    {
        gpio_shadow |= mask;
    }
    else
    {
        gpio_shadow &= (uint8_t)(~mask);
    }

    GPIO = gpio_shadow;

    INTCONbits.GIE = gie_state;
}

static bool pin2_is_high(void)
{
    if ((GPIO & PIN2_INPUT_MASK) != 0u)
    {
        return true;
    }

    return false;
}

static void led_on(void)
{
    gpio_write_atomic(LED_PIN_MASK, true);
}

static void led_off(void)
{
    gpio_write_atomic(LED_PIN_MASK, false);
}

static void uart_timer_start(void)
{
    TMR2 = 0;
    PIR1bits.TMR2IF = 0;
    T2CONbits.TMR2ON = 1;
}

static void uart_timer_stop(void)
{
    T2CONbits.TMR2ON = 0;
    PIR1bits.TMR2IF = 0;
}

static bool uart_is_busy(void)
{
    return uart_tx_busy;
}

static void uart_load_character(char data)
{
    /*
     * UART frame:
     * bit 0      = start bit, 0
     * bits 1..8  = data bits, LSB first
     * bit 9      = stop bit, 1
     */
    uart_tx_frame = ((uint16_t)1u << 9) | ((uint16_t)((uint8_t)data) << 1);
    uart_tx_bits_remaining = 10u;
}

static void uart_output_next_bit_main_context(void)
{
    if ((uart_tx_frame & 0x0001u) != 0u)
    {
        gpio_shadow |= UART_TX_PIN_MASK;
    }
    else
    {
        gpio_shadow &= (uint8_t)(~UART_TX_PIN_MASK);
    }

    GPIO = gpio_shadow;

    uart_tx_frame >>= 1;
    uart_tx_bits_remaining--;
}

static void uart_send_string_async(const char *text)
{
    while (uart_tx_busy)
    {
        ;
    }

    if (text == 0)
    {
        return;
    }

    if (text[0] == '\0')
    {
        return;
    }

    INTCONbits.GIE = 0;

    uart_tx_ptr = text;
    uart_load_character(*uart_tx_ptr);
    uart_tx_ptr++;

    uart_tx_busy = true;

    /*
     * Output start bit immediately.
     * Timer2 ISR continues the remaining bits.
     */
    uart_output_next_bit_main_context();

    uart_timer_start();

    INTCONbits.GIE = 1;
}

static void blink_led_three_times(void)
{
    uint8_t i;

    for (i = 0u; i < 3u; i++)
    {
        led_on();
        delay_ms_custom(LED_ON_TIME_MS);

        led_off();
        delay_ms_custom(LED_OFF_TIME_MS);
    }
}

static void trigger_detect_action(void)
{
    uart_send_string_async(CMD_TEXT);

    blink_led_three_times();

    while (uart_is_busy())
    {
        ;
    }
}

static void system_init(void)
{
    /*
     * Internal oscillator = 8 MHz.
     */
    OSCCON = 0b01110000;

    /*
     * Disable analog input and comparator.
     */
    ANSEL = 0x00;
    ADCON0 = 0x00;
    CMCON0 = 0x07;

    /*
     * Initial output state:
     * UART TX idle HIGH, LED OFF.
     */
    gpio_shadow = UART_TX_PIN_MASK;
    GPIO = gpio_shadow;

    /*
     * GP5 / Pin 2 = input
     * GP4 / Pin 3 = output
     * GP3 / Pin 4 = input-only
     * GP2 / Pin 5 = UART TX output
     * GP1 / Pin 6 = output unused
     * GP0 / Pin 7 = output unused
     */
    TRISIO = 0b00101000;

    /*
     * Disable weak pull-ups.
     * Use external 10k pull-down on Pin 2.
     */
    OPTION_REG = OPTION_REG | 0x80u;
    WPU = 0x00;

    /*
     * Timer1 for millisecond delay.
     * Prescaler 1:8, internal clock, Timer1 OFF initially.
     */
    T1CON = 0b00110000;
    TMR1H = 0x00;
    TMR1L = 0x00;
    PIR1bits.TMR1IF = 0;
    PIE1bits.TMR1IE = 0;

    /*
     * Timer2 for UART bit timing.
     * Prescaler 1:1, postscaler 1:1, Timer2 OFF initially.
     */
    PR2 = UART_TMR2_PR2_VALUE;
    TMR2 = 0;
    T2CON = 0x00;

    PIR1bits.TMR2IF = 0;
    PIE1bits.TMR2IE = 1;

    INTCONbits.PEIE = 1;
    INTCONbits.GIE = 1;
}

void __interrupt() isr(void)
{
    if (PIR1bits.TMR2IF)
    {
        PIR1bits.TMR2IF = 0;

        if (uart_tx_busy)
        {
            if (uart_tx_bits_remaining > 0u)
            {
                /*
                 * Fast UART bit output.
                 * No function call here, to keep 9600 baud timing stable.
                 */
                if ((uart_tx_frame & 0x0001u) != 0u)
                {
                    gpio_shadow |= UART_TX_PIN_MASK;
                }
                else
                {
                    gpio_shadow &= (uint8_t)(~UART_TX_PIN_MASK);
                }

                GPIO = gpio_shadow;

                uart_tx_frame >>= 1;
                uart_tx_bits_remaining--;
            }
            else
            {
                /*
                 * Previous character complete.
                 * Load next character or finish UART transmission.
                 */
                if (*uart_tx_ptr != '\0')
                {
                    uart_load_character(*uart_tx_ptr);
                    uart_tx_ptr++;

                    if ((uart_tx_frame & 0x0001u) != 0u)
                    {
                        gpio_shadow |= UART_TX_PIN_MASK;
                    }
                    else
                    {
                        gpio_shadow &= (uint8_t)(~UART_TX_PIN_MASK);
                    }

                    GPIO = gpio_shadow;

                    uart_tx_frame >>= 1;
                    uart_tx_bits_remaining--;
                }
                else
                {
                    uart_timer_stop();

                    gpio_shadow |= UART_TX_PIN_MASK;
                    GPIO = gpio_shadow;

                    uart_tx_busy = false;
                }
            }
        }
        else
        {
            uart_timer_stop();

            gpio_shadow |= UART_TX_PIN_MASK;
            GPIO = gpio_shadow;
        }
    }
}

void main(void)
{
    bool pin2_was_high;

    system_init();

    pin2_was_high = false;

    while (1)
    {
        if (pin2_is_high())
        {
            delay_ms_custom(DEBOUNCE_TIME_MS);

            if (pin2_is_high() && !pin2_was_high)
            {
                trigger_detect_action();
                pin2_was_high = true;
            }
        }
        else
        {
            pin2_was_high = false;
        }
    }
}