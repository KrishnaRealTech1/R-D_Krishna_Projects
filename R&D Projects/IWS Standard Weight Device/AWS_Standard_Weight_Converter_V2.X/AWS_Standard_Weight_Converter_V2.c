/*
 * File:   Universal_Weight_Format_Converter.c
 * Author: RealTech / Updated Universal ASCII Version
 *
 * MCU      : PIC18F25K22
 * Compiler : MPLAB X XC8
 *
 * Function:
 *   - Read weight data from scale through MAX3232 on UART1
 *   - Select scale input baud rate using 3 DIP switches on RB0/RB1/RB2
 *   - Supports common ASCII weight formats
 *   - Supports 7E1 raw data by stripping parity bit
 *   - Supports STX/ETX, CR/LF, and timeout frame ending
 *   - Supports decimal weight and optional implied decimal
 *   - Converts output to one fixed PC format
 *
 * Required PC output:
 *   wn000000.000 kg\r\n
 *
 * Hardware pin map:
 *   RC7 / RX1 / pin 18  <- MAX3232 R1OUT from scale
 *   RC6 / TX1 / pin 17  -> MAX3232 T1IN, optional
 *   RB6 / TX2 / pin 27  -> FTDI RXD through 470R
 *   RB7 / RX2 / pin 28  <- FTDI TXD through 1k, optional
 *   RB0 / pin 21        <- DIP baud bit 0
 *   RB1 / pin 22        <- DIP baud bit 1
 *   RB2 / pin 23        <- DIP baud bit 2
 *   RB4 / pin 25        -> RX LED
 *   RB5 / pin 26        -> TX LED
 *   RC0 / pin 11        -> ERROR LED
 */

#define _XTAL_FREQ 16000000UL

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

/* ------------------------------------------------------------
 * Configuration bits for PIC18F25K22
 * ------------------------------------------------------------ */

// CONFIG1H
#pragma config FOSC = INTIO67
#pragma config PLLCFG = OFF
#pragma config PRICLKEN = ON
#pragma config FCMEN = OFF
#pragma config IESO = OFF

// CONFIG2L
#pragma config PWRTEN = ON
#pragma config BOREN = SBORDIS
#pragma config BORV = 190

// CONFIG2H
#pragma config WDTEN = OFF
#pragma config WDTPS = 32768

// CONFIG3H
#pragma config CCP2MX = PORTC1
#pragma config PBADEN = OFF
#pragma config CCP3MX = PORTB5
#pragma config HFOFST = ON
#pragma config T3CMX = PORTC0
#pragma config P2BMX = PORTB5
#pragma config MCLRE = EXTMCLR

// CONFIG4L
#pragma config STVREN = ON
#pragma config LVP = OFF
#pragma config XINST = OFF
#pragma config DEBUG = OFF

// CONFIG5L
#pragma config CP0 = OFF
#pragma config CP1 = OFF
#pragma config CP2 = OFF
#pragma config CP3 = OFF

// CONFIG5H
#pragma config CPB = OFF
#pragma config CPD = OFF

// CONFIG6L
#pragma config WRT0 = OFF
#pragma config WRT1 = OFF
#pragma config WRT2 = OFF
#pragma config WRT3 = OFF

// CONFIG6H
#pragma config WRTC = OFF
#pragma config WRTB = OFF
#pragma config WRTD = OFF

// CONFIG7L
#pragma config EBTR0 = OFF
#pragma config EBTR1 = OFF
#pragma config EBTR2 = OFF
#pragma config EBTR3 = OFF

// CONFIG7H
#pragma config EBTRB = OFF

/* ------------------------------------------------------------
 * User settings
 * ------------------------------------------------------------ */

#define SCALE_LINE_MAX                 96u
#define FRAME_TIMEOUT_MS               80u

/* 0 = fixed PC output baud, 1 = PC output follows DIP baud */
#define PC_OUTPUT_FOLLOWS_DIP          0u
#define PC_OUTPUT_BAUD_FIXED           9600UL

/* 1 = supports 7E1 by removing parity bit from received byte */
#define SCALE_INPUT_STRIP_PARITY_BIT   1u

/* 1 = accepts comma decimal such as 123,456 kg */
#define COMMA_AS_DECIMAL_SEPARATOR     1u

/*
 * Optional implied decimal for integer-only formats.
 * 0: 000002345 -> 2345.000 kg
 * 3: 000002345 -> 2.345 kg
 */
#define IMPLIED_DECIMAL_DIGITS         0u

/* 1 = convert g, kg, lb, ton to kg output. 0 = treat all numbers as kg */
#define USE_DETECTED_UNIT_CONVERSION   1u

/* 1 = negative value outputs absolute value because required format has no sign */
#define OUTPUT_NEGATIVE_AS_ABSOLUTE    1u

/* 1 = output zero frame when input cannot be parsed */
#define SEND_INVALID_FRAME             1u

/* 0 = no startup text, only weight frames */
#define SEND_STARTUP_MESSAGE           0u

/* ------------------------------------------------------------
 * LED macros
 * ------------------------------------------------------------ */

#define LED_RX_LAT                     LATBbits.LATB4
#define LED_TX_LAT                     LATBbits.LATB5
#define LED_ERR_LAT                    LATCbits.LATC0

/* ------------------------------------------------------------
 * Timer0 1 ms tick
 * ------------------------------------------------------------ */

#define TIMER0_RELOAD                  65036u
#define TIMER0_RELOAD_H                ((uint8_t)(TIMER0_RELOAD >> 8))
#define TIMER0_RELOAD_L                ((uint8_t)(TIMER0_RELOAD & 0xFF))

static volatile uint32_t g_millis = 0;

typedef enum
{
    UNIT_UNKNOWN = 0,
    UNIT_KG,
    UNIT_G,
    UNIT_LB,
    UNIT_TON
} WeightUnit;

typedef struct
{
    bool valid;
    int32_t native_milli_value;
    bool has_decimal;
    uint8_t digit_count;
    uint8_t score;
} NumberCandidate;

typedef struct
{
    bool valid;
    int32_t milli_kg;
    WeightUnit detected_unit;
    char status;
} WeightData;

static void oscillator_init(void);
static void gpio_init(void);
static void timer0_init(void);
static void system_init(void);
static uint32_t millis(void);
static void delay_ms_blocking(uint16_t ms);

#ifndef __delay_ms
#define __delay_ms(x) delay_ms_blocking((uint16_t)(x))
#endif

static uint32_t read_dip_baud(void);
static uint16_t calculate_brg_value(uint32_t baud);
static void uart1_init(uint32_t baud);
static void uart2_init(uint32_t baud);
static bool uart1_try_read(uint8_t *byte);
static void uart2_write_char(char c);
static void uart2_write_string(const char *text);
static void uart2_write_uint32(uint32_t value);
static void uart2_write_uint32_padded(uint32_t value, uint8_t min_width);
static bool is_digit_ascii(char c);
static bool is_alpha_ascii(char c);
static char upper_ascii(char c);
static bool contains_ci(const char *text, const char *pattern);
static bool contains_isolated_letter_ci(const char *text, char letter);
static uint32_t pow10_u32(uint8_t power);
static NumberCandidate parse_number_at(const char *text, uint8_t start_index);
static NumberCandidate find_best_number(const char *text);
static WeightUnit detect_unit(const char *line);
static char detect_status(const char *line);
static int32_t convert_native_milli_to_milli_kg(int32_t native_milli_value, WeightUnit unit);
static bool parse_weight_line(const char *line, WeightData *weight);
static void uart2_write_wn_weight(const WeightData *weight);
static void uart2_write_error_frame(void);
static void process_scale_line(char *line);
static void handle_scale_byte(uint8_t byte, char *line_buffer, uint8_t *line_index);
static void startup_led_test(void);

void __interrupt() isr(void)
{
    if (INTCONbits.TMR0IF)
    {
        TMR0H = TIMER0_RELOAD_H;
        TMR0L = TIMER0_RELOAD_L;
        INTCONbits.TMR0IF = 0;
        g_millis++;
    }
}

void main(void)
{
    char scale_line[SCALE_LINE_MAX];
    uint8_t scale_line_index = 0u;
    uint8_t received_byte = 0u;
    uint32_t last_rx_time = 0UL;
    uint32_t scale_baud = 0UL;
    uint32_t pc_baud = 0UL;

    system_init();
    __delay_ms(30);

    scale_baud = read_dip_baud();

#if PC_OUTPUT_FOLLOWS_DIP
    pc_baud = scale_baud;
#else
    pc_baud = PC_OUTPUT_BAUD_FIXED;
#endif

    uart1_init(scale_baud);
    uart2_init(pc_baud);
    timer0_init();
    startup_led_test();

#if SEND_STARTUP_MESSAGE
    uart2_write_string("READY UNIVERSAL WEIGHT CONVERTER IN=");
    uart2_write_uint32(scale_baud);
    uart2_write_string(" OUT=");
    uart2_write_uint32(pc_baud);
    uart2_write_string("\r\n");
#endif

    while (1)
    {
        if (uart1_try_read(&received_byte))
        {
            last_rx_time = millis();
            handle_scale_byte(received_byte, scale_line, &scale_line_index);
        }

        if (scale_line_index > 0u)
        {
            uint32_t now = millis();
            if ((uint32_t)(now - last_rx_time) >= FRAME_TIMEOUT_MS)
            {
                scale_line[scale_line_index] = '\0';
                process_scale_line(scale_line);
                scale_line_index = 0u;
            }
        }
    }
}

static void oscillator_init(void)
{
    OSCCONbits.SCS = 0b10;
    OSCCONbits.IRCF = 0b111;
    OSCTUNEbits.PLLEN = 0;
    __delay_ms(5);
}

static void gpio_init(void)
{
    ANSELA = 0x00;
    ANSELB = 0x00;
    ANSELC = 0x00;
    INTCON2bits.RBPU = 1;

    TRISCbits.TRISC7 = 1;
    TRISCbits.TRISC6 = 0;
    TRISBbits.TRISB6 = 0;
    TRISBbits.TRISB7 = 1;

    TRISBbits.TRISB0 = 1;
    TRISBbits.TRISB1 = 1;
    TRISBbits.TRISB2 = 1;

    TRISBbits.TRISB4 = 0;
    TRISBbits.TRISB5 = 0;
    TRISCbits.TRISC0 = 0;

    LED_RX_LAT = 0;
    LED_TX_LAT = 0;
    LED_ERR_LAT = 0;
}

static void timer0_init(void)
{
    T0CONbits.TMR0ON = 0;
    T0CONbits.T08BIT = 0;
    T0CONbits.T0CS = 0;
    T0CONbits.T0SE = 0;
    T0CONbits.PSA = 0;
    T0CONbits.T0PS = 0b010;

    TMR0H = TIMER0_RELOAD_H;
    TMR0L = TIMER0_RELOAD_L;

    INTCONbits.TMR0IF = 0;
    INTCONbits.TMR0IE = 1;
    RCONbits.IPEN = 0;
    INTCONbits.PEIE = 1;
    INTCONbits.GIE = 1;

    T0CONbits.TMR0ON = 1;
}

static void system_init(void)
{
    oscillator_init();
    gpio_init();
}

static uint32_t millis(void)
{
    uint32_t value;
    uint8_t gie_state;

    gie_state = INTCONbits.GIE;
    INTCONbits.GIE = 0;
    value = g_millis;
    INTCONbits.GIE = gie_state;

    return value;
}

static void delay_ms_blocking(uint16_t ms)
{
    volatile uint16_t i;
    while (ms > 0u)
    {
        for (i = 0u; i < 600u; i++)
        {
            asm("nop");
        }
        ms--;
    }
}

static uint32_t read_dip_baud(void)
{
    uint8_t code;
    __delay_ms(10);
    code = 0u;

    if (PORTBbits.RB0)
    {
        code |= 0x01u;
    }
    if (PORTBbits.RB1)
    {
        code |= 0x02u;
    }
    if (PORTBbits.RB2)
    {
        code |= 0x04u;
    }

    switch (code)
    {
        case 0x00u: return 9600UL;
        case 0x01u: return 1200UL;
        case 0x02u: return 2400UL;
        case 0x03u: return 4800UL;
        case 0x04u: return 19200UL;
        case 0x05u: return 38400UL;
        case 0x06u: return 57600UL;
        case 0x07u: return 115200UL;
        default:    return 9600UL;
    }
}

static uint16_t calculate_brg_value(uint32_t baud)
{
    uint32_t brg;
    brg = ((_XTAL_FREQ / 4UL) + (baud / 2UL)) / baud;
    if (brg > 0UL)
    {
        brg--;
    }
    if (brg > 65535UL)
    {
        brg = 65535UL;
    }
    return (uint16_t)brg;
}

static void uart1_init(uint32_t baud)
{
    uint16_t brg = calculate_brg_value(baud);

    TXSTA1bits.SYNC = 0;
    TXSTA1bits.BRGH = 1;
    BAUDCON1bits.BRG16 = 1;

    SPBRGH1 = (uint8_t)(brg >> 8);
    SPBRG1 = (uint8_t)(brg & 0xFFu);

    RCSTA1bits.SPEN = 1;
    RCSTA1bits.CREN = 1;
    TXSTA1bits.TXEN = 1;
}

static void uart2_init(uint32_t baud)
{
    uint16_t brg = calculate_brg_value(baud);

    TXSTA2bits.SYNC = 0;
    TXSTA2bits.BRGH = 1;
    BAUDCON2bits.BRG16 = 1;

    SPBRGH2 = (uint8_t)(brg >> 8);
    SPBRG2 = (uint8_t)(brg & 0xFFu);

    RCSTA2bits.SPEN = 1;
    RCSTA2bits.CREN = 1;
    TXSTA2bits.TXEN = 1;
}

static bool uart1_try_read(uint8_t *byte)
{
    uint8_t dummy;

    if (RCSTA1bits.OERR)
    {
        RCSTA1bits.CREN = 0;
        RCSTA1bits.CREN = 1;
    }

    if (!PIR1bits.RC1IF)
    {
        return false;
    }

    if (RCSTA1bits.FERR)
    {
        dummy = RCREG1;
        (void)dummy;
        return false;
    }

    *byte = RCREG1;
    return true;
}

static void uart2_write_char(char c)
{
    while (!PIR3bits.TX2IF)
    {
        ;
    }
    TXREG2 = c;
}

static void uart2_write_string(const char *text)
{
    while (*text != '\0')
    {
        uart2_write_char(*text);
        text++;
    }
}

static void uart2_write_uint32(uint32_t value)
{
    char buffer[10];
    uint8_t index = 0u;

    if (value == 0UL)
    {
        uart2_write_char('0');
        return;
    }

    while ((value > 0UL) && (index < sizeof(buffer)))
    {
        buffer[index] = (char)('0' + (value % 10UL));
        value /= 10UL;
        index++;
    }

    while (index > 0u)
    {
        index--;
        uart2_write_char(buffer[index]);
    }
}

static void uart2_write_uint32_padded(uint32_t value, uint8_t min_width)
{
    uint32_t temp;
    uint8_t digits;
    uint8_t pad_count;

    temp = value;
    digits = 1u;

    while (temp >= 10UL)
    {
        temp /= 10UL;
        digits++;
    }

    if (digits < min_width)
    {
        pad_count = (uint8_t)(min_width - digits);
        while (pad_count > 0u)
        {
            uart2_write_char('0');
            pad_count--;
        }
    }

    uart2_write_uint32(value);
}

static bool is_digit_ascii(char c)
{
    return ((c >= '0') && (c <= '9'));
}

static bool is_alpha_ascii(char c)
{
    char u = upper_ascii(c);
    return ((u >= 'A') && (u <= 'Z'));
}

static char upper_ascii(char c)
{
    if ((c >= 'a') && (c <= 'z'))
    {
        return (char)(c - 32);
    }
    return c;
}

static bool contains_ci(const char *text, const char *pattern)
{
    uint8_t i;
    uint8_t j;

    if ((text == 0) || (pattern == 0))
    {
        return false;
    }

    if (pattern[0] == '\0')
    {
        return true;
    }

    for (i = 0u; text[i] != '\0'; i++)
    {
        j = 0u;
        while ((pattern[j] != '\0') &&
               (text[i + j] != '\0') &&
               (upper_ascii(text[i + j]) == upper_ascii(pattern[j])))
        {
            j++;
        }
        if (pattern[j] == '\0')
        {
            return true;
        }
    }

    return false;
}

static bool contains_isolated_letter_ci(const char *text, char letter)
{
    uint8_t i;
    char target = upper_ascii(letter);
    char current;
    char previous;
    char next;

    for (i = 0u; text[i] != '\0'; i++)
    {
        current = upper_ascii(text[i]);
        if (current == target)
        {
            if (i == 0u)
            {
                previous = '\0';
            }
            else
            {
                previous = text[i - 1u];
            }
            next = text[i + 1u];

            if (!is_alpha_ascii(previous) && !is_alpha_ascii(next))
            {
                return true;
            }
        }
    }

    return false;
}

static uint32_t pow10_u32(uint8_t power)
{
    uint32_t value = 1UL;
    while (power > 0u)
    {
        value *= 10UL;
        power--;
    }
    return value;
}

static NumberCandidate parse_number_at(const char *text, uint8_t start_index)
{
    NumberCandidate result;
    uint8_t i;
    bool negative;
    bool has_sign;
    bool has_decimal;
    uint32_t integer_part;
    uint8_t integer_digits;
    uint8_t decimal_digits;
    uint8_t d1;
    uint8_t d2;
    uint8_t d3;
    uint8_t d4;
    uint32_t native_milli_abs;
    uint32_t divisor;
    uint32_t integer_scaled;
    uint32_t rounding_add;

    result.valid = false;
    result.native_milli_value = 0;
    result.has_decimal = false;
    result.digit_count = 0u;
    result.score = 0u;

    i = start_index;
    negative = false;
    has_sign = false;
    has_decimal = false;
    integer_part = 0UL;
    integer_digits = 0u;
    decimal_digits = 0u;
    d1 = 0u;
    d2 = 0u;
    d3 = 0u;
    d4 = 0u;

    if ((text[i] == '+') || (text[i] == '-'))
    {
        has_sign = true;
        if (text[i] == '-')
        {
            negative = true;
        }
        i++;
        while (text[i] == ' ')
        {
            i++;
        }
    }

    if (!is_digit_ascii(text[i]))
    {
        return result;
    }

    while (is_digit_ascii(text[i]))
    {
        if (integer_part < 2000000UL)
        {
            integer_part = (integer_part * 10UL) + (uint32_t)(text[i] - '0');
        }
        integer_digits++;
        i++;
    }

    if ((text[i] == '.') || ((text[i] == ',') && (COMMA_AS_DECIMAL_SEPARATOR != 0u)))
    {
        if (is_digit_ascii(text[i + 1u]))
        {
            has_decimal = true;
            i++;
            while (is_digit_ascii(text[i]))
            {
                decimal_digits++;
                if (decimal_digits == 1u)
                {
                    d1 = (uint8_t)(text[i] - '0');
                }
                else if (decimal_digits == 2u)
                {
                    d2 = (uint8_t)(text[i] - '0');
                }
                else if (decimal_digits == 3u)
                {
                    d3 = (uint8_t)(text[i] - '0');
                }
                else if (decimal_digits == 4u)
                {
                    d4 = (uint8_t)(text[i] - '0');
                }
                i++;
            }
        }
    }

    if (integer_digits == 0u)
    {
        return result;
    }

    if (has_decimal)
    {
        native_milli_abs = (integer_part * 1000UL) +
                           ((uint32_t)d1 * 100UL) +
                           ((uint32_t)d2 * 10UL) +
                           (uint32_t)d3;
        if (d4 >= 5u)
        {
            native_milli_abs++;
        }
    }
    else
    {
        if (IMPLIED_DECIMAL_DIGITS == 0u)
        {
            native_milli_abs = integer_part * 1000UL;
        }
        else if (IMPLIED_DECIMAL_DIGITS <= 3u)
        {
            divisor = pow10_u32(IMPLIED_DECIMAL_DIGITS);
            integer_scaled = integer_part * 1000UL;
            rounding_add = divisor / 2UL;
            native_milli_abs = (integer_scaled + rounding_add) / divisor;
        }
        else
        {
            native_milli_abs = integer_part * 1000UL;
        }
    }

    if (native_milli_abs > 2000000000UL)
    {
        native_milli_abs = 2000000000UL;
    }

    result.valid = true;
    result.has_decimal = has_decimal;

    if (negative)
    {
        result.native_milli_value = -((int32_t)native_milli_abs);
    }
    else
    {
        result.native_milli_value = (int32_t)native_milli_abs;
    }

    result.digit_count = (uint8_t)(integer_digits + decimal_digits);
    result.score = result.digit_count;

    if (has_decimal)
    {
        result.score = (uint8_t)(result.score + 6u);
    }
    if (has_sign)
    {
        result.score = (uint8_t)(result.score + 3u);
    }
    if (integer_digits >= 4u)
    {
        result.score = (uint8_t)(result.score + 1u);
    }

    return result;
}

static NumberCandidate find_best_number(const char *text)
{
    uint8_t i;
    NumberCandidate best;
    NumberCandidate candidate;

    best.valid = false;
    best.native_milli_value = 0;
    best.has_decimal = false;
    best.digit_count = 0u;
    best.score = 0u;

    for (i = 0u; text[i] != '\0'; i++)
    {
        if (is_digit_ascii(text[i]) || (text[i] == '+') || (text[i] == '-'))
        {
            candidate = parse_number_at(text, i);
            if (candidate.valid)
            {
                if ((!best.valid) || (candidate.score > best.score))
                {
                    best = candidate;
                }
            }
        }
    }

    return best;
}

static WeightUnit detect_unit(const char *line)
{
    if (contains_ci(line, "kg") || contains_ci(line, "kgs") || contains_ci(line, "kilogram"))
    {
        return UNIT_KG;
    }
    if (contains_ci(line, "lbs") || contains_ci(line, "lb") || contains_ci(line, "pound"))
    {
        return UNIT_LB;
    }
    if (contains_ci(line, "tonne") || contains_ci(line, "tons") || contains_ci(line, "ton"))
    {
        return UNIT_TON;
    }
    if (contains_isolated_letter_ci(line, 't'))
    {
        return UNIT_TON;
    }
    if (contains_isolated_letter_ci(line, 'g'))
    {
        return UNIT_G;
    }
    return UNIT_KG;
}

static char detect_status(const char *line)
{
    if (contains_ci(line, "OVER") ||
        contains_ci(line, "OVR") ||
        contains_ci(line, "OL") ||
        contains_ci(line, "----") ||
        contains_ci(line, "ERR"))
    {
        return 'O';
    }

    if (contains_ci(line, "US") ||
        contains_ci(line, "UNST") ||
        contains_ci(line, "MOTION") ||
        contains_ci(line, "DYN"))
    {
        return 'U';
    }

    if (contains_ci(line, "ST") || contains_ci(line, "STABLE"))
    {
        return 'S';
    }

    return 'N';
}

static int32_t convert_native_milli_to_milli_kg(int32_t native_milli_value, WeightUnit unit)
{
    bool negative;
    uint32_t abs_native;
    uint32_t converted;
    uint64_t temp64;

    negative = false;
    if (native_milli_value < 0)
    {
        negative = true;
        abs_native = (uint32_t)(-native_milli_value);
    }
    else
    {
        abs_native = (uint32_t)native_milli_value;
    }

#if USE_DETECTED_UNIT_CONVERSION
    switch (unit)
    {
        case UNIT_G:
            converted = (abs_native + 500UL) / 1000UL;
            break;

        case UNIT_LB:
            temp64 = (uint64_t)abs_native * 45359237ULL;
            temp64 += 50000000ULL;
            temp64 /= 100000000ULL;
            if (temp64 > 2000000000ULL)
            {
                converted = 2000000000UL;
            }
            else
            {
                converted = (uint32_t)temp64;
            }
            break;

        case UNIT_TON:
            if (abs_native > 2000000UL)
            {
                converted = 2000000000UL;
            }
            else
            {
                converted = abs_native * 1000UL;
            }
            break;

        case UNIT_KG:
        case UNIT_UNKNOWN:
        default:
            converted = abs_native;
            break;
    }
#else
    converted = abs_native;
#endif

    if (converted > 2000000000UL)
    {
        converted = 2000000000UL;
    }

    if (negative)
    {
        return -((int32_t)converted);
    }

    return (int32_t)converted;
}

static bool parse_weight_line(const char *line, WeightData *weight)
{
    NumberCandidate number;
    WeightUnit unit;

    if ((line == 0) || (weight == 0))
    {
        return false;
    }

    number = find_best_number(line);
    if (!number.valid)
    {
        weight->valid = false;
        weight->milli_kg = 0;
        weight->detected_unit = UNIT_KG;
        weight->status = 'E';
        return false;
    }

    unit = detect_unit(line);
    weight->valid = true;
    weight->detected_unit = unit;
    weight->status = detect_status(line);
    weight->milli_kg = convert_native_milli_to_milli_kg(number.native_milli_value, unit);

    return true;
}

static void uart2_write_wn_weight(const WeightData *weight)
{
    uint32_t abs_milli_kg;
    uint32_t kg_part;
    uint32_t gram_part;

    if (weight == 0)
    {
        return;
    }

#if OUTPUT_NEGATIVE_AS_ABSOLUTE
    if (weight->milli_kg < 0)
    {
        abs_milli_kg = (uint32_t)(-weight->milli_kg);
    }
    else
    {
        abs_milli_kg = (uint32_t)(weight->milli_kg);
    }
    uart2_write_string("wn");
#else
    if (weight->milli_kg < 0)
    {
        abs_milli_kg = (uint32_t)(-weight->milli_kg);
        uart2_write_string("wn-");
    }
    else
    {
        abs_milli_kg = (uint32_t)(weight->milli_kg);
        uart2_write_string("wn");
    }
#endif

    kg_part = abs_milli_kg / 1000UL;
    gram_part = abs_milli_kg % 1000UL;

    uart2_write_uint32_padded(kg_part, 6u);
    uart2_write_char('.');
    uart2_write_uint32_padded(gram_part, 3u);
    uart2_write_string(" kg\r\n");
}

static void uart2_write_error_frame(void)
{
    uart2_write_string("wn000000.000 kg\r\n");
}

static void process_scale_line(char *line)
{
    WeightData weight;

    LED_RX_LAT = 1;

    if (parse_weight_line(line, &weight))
    {
        LED_ERR_LAT = 0;
        LED_TX_LAT = 1;
        uart2_write_wn_weight(&weight);
        LED_TX_LAT = 0;
    }
    else
    {
        LED_ERR_LAT = 1;
#if SEND_INVALID_FRAME
        uart2_write_error_frame();
#endif
    }

    LED_RX_LAT = 0;
}

static void handle_scale_byte(uint8_t byte, char *line_buffer, uint8_t *line_index)
{
#if SCALE_INPUT_STRIP_PARITY_BIT
    byte &= 0x7Fu;
#endif

    if (byte == 0x02u)
    {
        *line_index = 0u;
        return;
    }

    if ((byte == 0x03u) || (byte == '\r') || (byte == '\n'))
    {
        if (*line_index > 0u)
        {
            line_buffer[*line_index] = '\0';
            process_scale_line(line_buffer);
            *line_index = 0u;
        }
        return;
    }

    if ((byte >= 32u) && (byte <= 126u))
    {
        if (*line_index < (SCALE_LINE_MAX - 1u))
        {
            line_buffer[*line_index] = (char)byte;
            (*line_index)++;
        }
        else
        {
            *line_index = 0u;
            LED_ERR_LAT = 1;
#if SEND_INVALID_FRAME
            uart2_write_error_frame();
#endif
        }
    }
}

static void startup_led_test(void)
{
    LED_RX_LAT = 1;
    LED_TX_LAT = 1;
    LED_ERR_LAT = 1;

    __delay_ms(80);

    LED_RX_LAT = 0;
    LED_TX_LAT = 0;
    LED_ERR_LAT = 0;
}

