// main.c - PIC18F25K22 Vacuum Cleaner Controller with OLED Runtime Display
// Compiler: XC8 v2.x (C99)
// MCU: PIC18F25K22
// OLED: SSD1306-compatible I2C 128x64 (bit-banged I2C)

// ========================= INCLUDES & CLOCK ==========================

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

#define _XTAL_FREQ 16000000UL   // 16 MHz internal oscillator

// ========================= CONFIGURATION BITS =========================
// CONFIG1H
#pragma config FOSC = INTIO67   // Internal oscillator block
#pragma config PLLCFG = OFF     // 4X PLL disabled
#pragma config PRICLKEN = OFF   // Primary clock can be disabled
#pragma config FCMEN = OFF      // Fail-Safe Clock Monitor disabled
#pragma config IESO = OFF       // Oscillator Switchover disabled

// CONFIG2L
#pragma config PWRTEN = OFF     // Power-up Timer disabled
#pragma config BOREN  = SBORDIS // Brown-out Reset enabled in hardware only
#pragma config BORV   = 190     // BOR voltage 1.90V (example)

// CONFIG2H
#pragma config WDTEN = OFF      // Watchdog Timer disabled
#pragma config WDTPS = 32768    // WDT postscale (don?t care if WDTEN=OFF)

// CONFIG3H
#pragma config CCP2MX = PORTC1  // CCP2 on RC1
#pragma config PBADEN = OFF     // PORTB<5:0> as digital on reset
#pragma config CCP3MX = PORTB5  // CCP3 on RB5 (don?t care if unused)
#pragma config HFOFST = ON      // HFINTOSC starts immediately
#pragma config P2BMX  = PORTC0  // P2B on RC0
#pragma config MCLRE  = EXTMCLR // MCLR pin enabled

// CONFIG4L
#pragma config STVREN = ON      // Stack overflow reset enabled
#pragma config LVP    = OFF     // Low-voltage programming disabled
#pragma config XINST  = OFF     // Extended instruction set disabled
#pragma config DEBUG  = OFF     // Background debugger disabled

// CONFIG5L/5H,6L/6H,7L/7H: code protection off
#pragma config CP0 = OFF, CP1 = OFF, CP2 = OFF, CP3 = OFF
#pragma config CPB = OFF, CPD = OFF
#pragma config WRT0 = OFF, WRT1 = OFF, WRT2 = OFF, WRT3 = OFF
#pragma config WRTC = OFF, WRTB = OFF, WRTD = OFF
#pragma config EBTR0 = OFF, EBTR1 = OFF, EBTR2 = OFF, EBTR3 = OFF
#pragma config EBTRB = OFF

// ========================= PIN DEFINITIONS ============================

// Switch inputs
#define H1_TRIS     TRISAbits.TRISA0
#define H2_TRIS     TRISAbits.TRISA1
#define LEVER_TRIS  TRISAbits.TRISA2

#define H1_PORT     PORTAbits.RA0
#define H2_PORT     PORTAbits.RA1
#define LEVER_PORT  PORTAbits.RA2

// Relay outputs
#define BRUSH_TRIS  TRISCbits.TRISC0
#define VAC_TRIS    TRISCbits.TRISC1

#define BRUSH_LAT   LATCbits.LATC0
#define VAC_LAT     LATCbits.LATC1

// I2C (bit-banged) on RC3 (SCL), RC4 (SDA)
#define SDA_TRIS    TRISCbits.TRISC4
#define SDA_LAT     LATCbits.LATC4
#define SDA_PORT    PORTCbits.RC4

#define SCL_TRIS    TRISCbits.TRISC3
#define SCL_LAT     LATCbits.LATC3
#define SCL_PORT    PORTCbits.RC3

// LED for vacuum delay indication on RB0
#define VAC_DELAY_LED_TRIS  TRISBbits.TRISB0
#define VAC_DELAY_LED_LAT   LATBbits.LATB0

// I2C address for SSD1306 (0x3C << 1 for write)
#define OLED_I2C_ADDR  0x78   // 0b01111000

// ========================= EEPROM MAP ================================
// Only store overall machine running time (hour meter)
#define EEADDR_OVERALL   0x0000u   // 4 bytes: overall seconds
#define EEADDR_MAGIC     0x0004u   // 1 byte : validity flag
#define EE_MAGIC_VALUE   0x5Au

// ========================= HOUR METER LIMIT ==========================
// After 99999 hours, automatically reset to 0
#define HOURMETER_MAX_HOURS         99999UL
#define HOURMETER_RESET_SECONDS     (HOURMETER_MAX_HOURS * 3600UL)  // 359,996,400

// ========================= HANDLE TIMEOUT (CONFIGURABLE) ==============
// Default behavior remains EXACTLY the same as before: 5 minutes.
// Change HANDLE_TIMEOUT_MINUTES to adjust.
#define HANDLE_TIMEOUT_MINUTES      5u
#define HANDLE_TIMEOUT_SECONDS      (HANDLE_TIMEOUT_MINUTES * 60u)

// ========================= GLOBAL VARIABLES ===========================

// Timekeeping (seconds)
volatile uint32_t g_overall_seconds = 0;
volatile uint32_t g_brush_seconds   = 0;   // RAM only
volatile uint32_t g_vacuum_seconds  = 0;   // RAM only

// Millisecond and second timing
volatile uint16_t g_ms_counter  = 0;
volatile uint8_t  g_10ms_flag   = 0;
volatile uint8_t  g_1s_flag     = 0;

// Extra millisecond counter for precise boot delay (3s)
volatile uint16_t g_boot_ms     = 0;

// Switch debounced states
volatile bool g_h1_state    = false;
volatile bool g_h2_state    = false;
volatile bool g_lever_state = false;

// Internal debounce shift registers
static uint8_t h1_hist    = 0xFF;
static uint8_t h2_hist    = 0xFF;
static uint8_t lever_hist = 0xFF;

// Motor states
volatile bool g_brush_on  = false;
volatile bool g_vacuum_on = false;

// 5-minute timeout for handle switches (in seconds)
volatile uint16_t g_handle_low_seconds   = 0; // counts when (!H1 || !H2)
volatile bool     g_brush_timeout_active = false;

// 10-second delayed OFF for vacuum (in seconds)
volatile uint8_t  g_vacuum_off_delay    = 0;     // counts when Lever=LOW and vacuum_on=1
volatile bool     g_vacuum_delay_active = false; // true while 10s delay is running

// Logo blink state (for brush & vacuum icons)
volatile bool g_logo_blink_state = false;

// ========================= FUNCTION PROTOTYPES ========================

// Simple software delays (no __delay_ms/us)
void delay_us_soft(uint16_t us);
void delay_ms_soft(uint16_t ms);

// Init
void system_init(void);
void osc_init(void);
void io_init(void);
void timer0_init(void);

// ISR
void __interrupt() isr(void);

// Logic
void debounce_inputs_10ms(void);
void update_logic_10ms(void);
void update_logic_1s(void);

// OLED / I2C: bit-banged
void i2c_init(void);
void i2c_start(void);
void i2c_stop(void);
bool i2c_write_byte(uint8_t data);

void oled_init(void);
void oled_send_command(uint8_t cmd);
void oled_send_data_start(void);
void oled_send_data_stop(void);
void oled_send_data_byte(uint8_t data);
void oled_clear(void);
void oled_set_cursor(uint8_t col, uint8_t page);
void oled_draw_char(char c);
void oled_draw_text(uint8_t col, uint8_t page, const char *text);
void oled_update_screen(void);
void oled_show_boot_screen(void);

// Centered text helper
void oled_draw_text_centered(uint8_t page, const char *text);

// Time formatting
void format_hourmeter_string(char *buf, uint32_t seconds); // "HHHHH:MM"
void format_runtime_string(char *buf, uint32_t seconds);   // "HH:MM:SS"

// HUGE (4x) ?graphics? text for boot
void scale4x_vertical(uint8_t in, uint8_t out[4]);
void oled_draw_char_huge(uint8_t col, uint8_t page, char c);
void oled_draw_text_huge(uint8_t col, uint8_t page, const char *text);

// Boot underline animation
void oled_draw_underline_animated(uint8_t col, uint8_t page, uint8_t width,
                                  uint8_t pattern, uint8_t chunk, uint16_t chunk_delay_ms);

// EEPROM helpers (only overall time)
void eeprom_write_byte(uint16_t addr, uint8_t value);
uint8_t eeprom_read_byte(uint16_t addr);
void hourmeter_load_from_eeprom(void);
void hourmeter_save_to_eeprom(void);

// Font helpers
const uint8_t* font_get_pattern(char c);

// ========================= SOFTWARE DELAYS ===========================

void delay_us_soft(uint16_t us)
{
    while (us--)
    {
        volatile uint8_t i;
        for (i = 0; i < 4; i++)
        {
            // waste time
        }
    }
}

void delay_ms_soft(uint16_t ms)
{
    while (ms--)
    {
        delay_us_soft(1000);
    }
}

// ========================= SYSTEM INIT ================================

void system_init(void)
{
    osc_init();
    io_init();
    i2c_init();
    oled_init();     // OLED left OFF (no garbage flash)
    timer0_init();   // start 1ms tick
}

void osc_init(void)
{
    // Use 16 MHz HFINTOSC
    OSCCON = 0b01110010;  // HFINTOSC 16 MHz, internal clock
}

void io_init(void)
{
    // Disable all analog on used ports
    ANSELA = 0x00;
    ANSELB = 0x00;
    ANSELC = 0x00;

    // Inputs
    H1_TRIS    = 1;
    H2_TRIS    = 1;
    LEVER_TRIS = 1;

    // Outputs
    BRUSH_TRIS         = 0;
    VAC_TRIS           = 0;
    VAC_DELAY_LED_TRIS = 0;

    BRUSH_LAT          = 0;
    VAC_LAT            = 0;
    VAC_DELAY_LED_LAT  = 0;

    // I2C lines start as "high" (released)
    SCL_TRIS = 1;
    SDA_TRIS = 1;
    SCL_LAT  = 0;
    SDA_LAT  = 0;
}

void timer0_init(void)
{
    // Timer0: 16-bit, internal clock, no prescaler, 1ms interrupt
    // Fosc=16MHz => Fcy=4MHz => 0.25us/tick, 1ms=4000 ticks
    // Preload = 65536 - 4000 = 61536 = 0xF060

    T0CON = 0b00001000; // TMR0 OFF, 16-bit, internal clock, no prescaler
    TMR0H = 0xF0;
    TMR0L = 0x60;

    INTCONbits.TMR0IF = 0;
    INTCONbits.TMR0IE = 1;
    INTCONbits.GIE    = 1;

    T0CONbits.TMR0ON = 1;
}

// ========================= INTERRUPT SERVICE ROUTINE ==================

void __interrupt() isr(void)
{
    if (INTCONbits.TMR0IF)
    {
        // Reload 1ms
        TMR0H = 0xF0;
        TMR0L = 0x60;
        INTCONbits.TMR0IF = 0;

        g_ms_counter++;
        g_boot_ms++;

        // Every 10ms
        if (g_ms_counter >= 10)
        {
            g_ms_counter -= 10;
            g_10ms_flag = 1;
        }

        // Every 1000ms -> 1s
        static uint16_t ms_for_1s = 0;
        ms_for_1s++;
        if (ms_for_1s >= 1000)
        {
            ms_for_1s = 0;
            g_1s_flag = 1;
        }
    }
}

// ========================= INPUT DEBOUNCE ============================

void debounce_inputs_10ms(void)
{
    // Simple 8-sample shift-register debounce ~80ms

    h1_hist <<= 1;
    h1_hist |= (H1_PORT ? 1 : 0);
    if (h1_hist == 0x00)      g_h1_state = false;
    else if (h1_hist == 0xFF) g_h1_state = true;

    h2_hist <<= 1;
    h2_hist |= (H2_PORT ? 1 : 0);
    if (h2_hist == 0x00)      g_h2_state = false;
    else if (h2_hist == 0xFF) g_h2_state = true;

    lever_hist <<= 1;
    lever_hist |= (LEVER_PORT ? 1 : 0);
    if (lever_hist == 0x00)      g_lever_state = false;
    else if (lever_hist == 0xFF) g_lever_state = true;
}

// ========================= LOGIC (10ms + 1s) =========================

void update_logic_10ms(void)
{
    // Brush control (fast reaction)
    if (g_h1_state && g_h2_state && !g_brush_timeout_active)
    {
        g_brush_on = true;
    }
    else
    {
        if (!g_h1_state && !g_h2_state)
        {
            g_brush_on = false;
        }
        else
        {
            if (g_brush_timeout_active)
            {
                g_brush_on = false;
            }
        }
    }

    // Vacuum control (fast ON)
    if (g_lever_state)
    {
        g_vacuum_on = true;
    }

    // Apply to hardware outputs
    BRUSH_LAT = g_brush_on ? 1 : 0;
    VAC_LAT   = g_vacuum_on ? 1 : 0;

    // Delay LED blink: ON 300ms / OFF 300ms (toggle every 300ms)
    static uint8_t blink_10ms = 0;
    static bool last_delay_active = false;

    if (g_vacuum_delay_active)
    {
        if (!last_delay_active)
        {
            last_delay_active = true;
            blink_10ms = 0;
            VAC_DELAY_LED_LAT = 1; // start ON for consistent look
        }

        blink_10ms++;
        if (blink_10ms >= 30) // 30 * 10ms = 300ms
        {
            blink_10ms = 0;
            VAC_DELAY_LED_LAT ^= 1;
        }
    }
    else
    {
        last_delay_active = false;
        blink_10ms = 0;
        VAC_DELAY_LED_LAT = 0;
    }
}

void update_logic_1s(void)
{
    // Icon blink phase (1Hz)
    static bool logo_phase = false;
    logo_phase = !logo_phase;
    g_logo_blink_state = logo_phase;

    // Overall time counting
    g_overall_seconds++;

    // Auto-reset hour meter after 99999 hours and save immediately
    if (g_overall_seconds >= HOURMETER_RESET_SECONDS)
    {
        g_overall_seconds = 0;
        hourmeter_save_to_eeprom();
    }

    if (g_brush_on)  g_brush_seconds++;
    if (g_vacuum_on) g_vacuum_seconds++;

    // 5-minute timeout for handles (if either is LOW)
    if (!g_h1_state || !g_h2_state)
    {
        if (g_handle_low_seconds < HANDLE_TIMEOUT_SECONDS)
            g_handle_low_seconds++;

        if (g_handle_low_seconds >= HANDLE_TIMEOUT_SECONDS)
        {
            g_brush_timeout_active = true;
            g_brush_on = false;
        }
    }
    else
    {
        g_handle_low_seconds   = 0;
        g_brush_timeout_active = false;
    }

    // Vacuum 10s delayed OFF when lever is LOW
    if (!g_lever_state && g_vacuum_on)
    {
        if (g_vacuum_off_delay == 0)
        {
            g_vacuum_delay_active = true;
        }

        if (g_vacuum_off_delay < 10)
            g_vacuum_off_delay++;

        if (g_vacuum_off_delay >= 10)
        {
            g_vacuum_on           = false;
            g_vacuum_delay_active = false;
        }
    }
    else
    {
        g_vacuum_off_delay    = 0;
        g_vacuum_delay_active = false;
    }

    BRUSH_LAT = g_brush_on ? 1 : 0;
    VAC_LAT   = g_vacuum_on ? 1 : 0;

    // Update OLED once per second
    oled_update_screen();

    // EEPROM auto-save every 10 seconds
    static uint8_t eeprom_save_counter = 0;
    eeprom_save_counter++;
    if (eeprom_save_counter >= 10)
    {
        eeprom_save_counter = 0;
        hourmeter_save_to_eeprom();
    }
}

// ========================= I2C BIT-BANG IMPLEMENTATION ===============

static void i2c_delay(void)
{
    // Faster bus => faster clear/draw => less blank time
    delay_us_soft(2);
}


void i2c_init(void)
{
    SCL_TRIS = 1;
    SDA_TRIS = 1;
    SCL_LAT  = 0;
    SDA_LAT  = 0;
}

void i2c_start(void)
{
    SDA_TRIS = 1;
    SCL_TRIS = 1;
    i2c_delay();

    SDA_TRIS = 0; // SDA low while SCL high
    i2c_delay();
    SCL_TRIS = 0; // SCL low
    i2c_delay();
}

void i2c_stop(void)
{
    SCL_TRIS = 0;
    i2c_delay();

    SDA_TRIS = 0;
    i2c_delay();
    SCL_TRIS = 1;
    i2c_delay();
    SDA_TRIS = 1;
    i2c_delay();
}

bool i2c_write_byte(uint8_t data)
{
    for (uint8_t i = 0; i < 8; i++)
    {
        if (data & 0x80) SDA_TRIS = 1;
        else            SDA_TRIS = 0;

        i2c_delay();
        SCL_TRIS = 1;
        i2c_delay();
        SCL_TRIS = 0;
        i2c_delay();

        data <<= 1;
    }

    // ACK (ignored)
    SDA_TRIS = 1;
    i2c_delay();
    SCL_TRIS = 1;
    i2c_delay();
    (void)SDA_PORT;
    SCL_TRIS = 0;
    i2c_delay();

    return true;
}

// ========================= OLED (SSD1306-like) =======================

void oled_send_command(uint8_t cmd)
{
    i2c_start();
    i2c_write_byte(OLED_I2C_ADDR);
    i2c_write_byte(0x00);
    i2c_write_byte(cmd);
    i2c_stop();
}

void oled_send_data_start(void)
{
    i2c_start();
    i2c_write_byte(OLED_I2C_ADDR);
    i2c_write_byte(0x40);
}

void oled_send_data_stop(void)
{
    i2c_stop();
}

void oled_send_data_byte(uint8_t data)
{
    i2c_write_byte(data);
}

void oled_init(void)
{
    // Reduce this to reduce blank time at power-up
    delay_ms_soft(30);

    oled_send_command(0xAE); // Display OFF
    oled_send_command(0x20);
    oled_send_command(0x00);
    oled_send_command(0xB0);

    oled_send_command(0x20); // Memory addressing mode
    oled_send_command(0x00); // Horizontal addressing mode
    oled_send_command(0xB0); // Page start address
    oled_send_command(0xC8); // COM output scan direction remapped
    oled_send_command(0x00); // low column start address
    oled_send_command(0x10); // high column start address
    oled_send_command(0x40); // start line address
    oled_send_command(0x81); // contrast control
    oled_send_command(0x7F); // contrast value
    oled_send_command(0xA1); // segment re-map
    oled_send_command(0xA6); // normal display
    oled_send_command(0xA8); // multiplex ratio
    oled_send_command(0x3F); // 1/64 duty
    oled_send_command(0xA4); // display follows RAM
    oled_send_command(0xD3); // display offset
    oled_send_command(0x00); // no offset
    oled_send_command(0xD5); // display clock divide ratio
    oled_send_command(0x80);
    oled_send_command(0xD9); // pre-charge period
    oled_send_command(0xF1);
    oled_send_command(0xDA); // COM pins hardware configuration
    oled_send_command(0x12);
    oled_send_command(0xDB); // VCOMH deselect level
    oled_send_command(0x40);
    oled_send_command(0x8D); // charge pump
    oled_send_command(0x14);

    // IMPORTANT: keep OLED OFF here (no garbage flash).
    // Boot screen and main UI will turn it ON when ready.
}

void oled_set_cursor(uint8_t col, uint8_t page)
{
    oled_send_command(0xB0 | (page & 0x07));
    oled_send_command(0x00 | (col & 0x0F));
    oled_send_command(0x10 | ((col >> 4) & 0x0F));
}

void oled_clear(void)
{
    // Clear 132 columns to avoid edge garbage on SH1106-like modules
    for (uint8_t page = 0; page < 8; page++)
    {
        oled_set_cursor(0, page);
        oled_send_data_start();
        for (uint8_t col = 0; col < 132; col++)
        {
            oled_send_data_byte(0x00);
        }
        oled_send_data_stop();
    }
}

// ----------------- Tiny Font (5x7 + 1 column space) ------------------

const uint8_t* font_get_pattern(char c)
{
    static const uint8_t digits[10][5] = {
        {0x3E,0x51,0x49,0x45,0x3E}, // '0'
        {0x00,0x42,0x7F,0x40,0x00}, // '1'
        {0x42,0x61,0x51,0x49,0x46}, // '2'
        {0x21,0x41,0x45,0x4B,0x31}, // '3'
        {0x18,0x14,0x12,0x7F,0x10}, // '4'
        {0x27,0x45,0x45,0x45,0x39}, // '5'
        {0x3C,0x4A,0x49,0x49,0x30}, // '6'
        {0x01,0x71,0x09,0x05,0x03}, // '7'
        {0x36,0x49,0x49,0x49,0x36}, // '8'
        {0x06,0x49,0x49,0x29,0x1E}  // '9'
    };

    static const uint8_t letter_T[5]      = {0x01,0x01,0x7F,0x01,0x01};
    static const uint8_t letter_O[5]      = {0x3E,0x41,0x41,0x41,0x3E};
    static const uint8_t letter_V[5]      = {0x07,0x18,0x60,0x18,0x07};
    static const uint8_t letter_A[5]      = {0x7E,0x11,0x11,0x11,0x7E};
    static const uint8_t letter_C[5]      = {0x3E,0x41,0x41,0x41,0x22};
    static const uint8_t letter_B[5]      = {0x7F,0x49,0x49,0x49,0x36};
    static const uint8_t letter_R[5]      = {0x7F,0x09,0x19,0x29,0x46};
    static const uint8_t letter_U[5]      = {0x3F,0x40,0x40,0x40,0x3F};
    static const uint8_t letter_D[5]      = {0x7F,0x41,0x41,0x22,0x1C};
    static const uint8_t letter_I[5]      = {0x00,0x41,0x7F,0x41,0x00};
    static const uint8_t letter_M[5]      = {0x7F,0x02,0x0C,0x02,0x7F};
    static const uint8_t letter_H[5]      = {0x7F,0x08,0x08,0x08,0x7F};
    static const uint8_t letter_E[5]      = {0x7F,0x49,0x49,0x49,0x41};
    static const uint8_t letter_space[5]  = {0x00,0x00,0x00,0x00,0x00};
    static const uint8_t colon_char[5]    = {0x00,0x36,0x36,0x00,0x00};

    if (c >= '0' && c <= '9')
        return digits[c - '0'];

    switch (c)
    {
        case 'T': return letter_T;
        case 'O': return letter_O;
        case 'V': return letter_V;
        case 'A': return letter_A;
        case 'C': return letter_C;
        case 'B': return letter_B;
        case 'R': return letter_R;
        case 'U': return letter_U;
        case 'D': return letter_D;
        case 'I': return letter_I;
        case 'M': return letter_M;
        case 'H': return letter_H;
        case 'E': return letter_E;
        case ':': return colon_char;
        case ' ': default: return letter_space;
    }
}

void oled_draw_char(char c)
{
    const uint8_t* pattern = font_get_pattern(c);
    oled_send_data_byte(pattern[0]);
    oled_send_data_byte(pattern[1]);
    oled_send_data_byte(pattern[2]);
    oled_send_data_byte(pattern[3]);
    oled_send_data_byte(pattern[4]);
    oled_send_data_byte(0x00);
}

void oled_draw_text(uint8_t col, uint8_t page, const char *text)
{
    oled_set_cursor(col, page);
    oled_send_data_start();
    while (*text)
    {
        oled_draw_char(*text++);
    }
    oled_send_data_stop();
}

// Centered (small font, 6px per char)
void oled_draw_text_centered(uint8_t page, const char *text)
{
    uint8_t len = 0;
    const char *p = text;
    while (*p && len < 21)
    {
        len++;
        p++;
    }

    uint8_t width = (uint8_t)(len * 6u);
    uint8_t start_col = (uint8_t)((128u - width) / 2u);
    oled_draw_text(start_col, page, text);
}

// ========================= TIME FORMATTING ===========================

// "HHHHH:MM"
void format_hourmeter_string(char *buf, uint32_t seconds)
{
    uint32_t hh = seconds / 3600u;
    uint32_t mm = (seconds % 3600u) / 60u;

    if (hh > 99999u) hh = 99999u;

    buf[0] = '0' + (char)((hh / 10000u) % 10u);
    buf[1] = '0' + (char)((hh / 1000u)  % 10u);
    buf[2] = '0' + (char)((hh / 100u)   % 10u);
    buf[3] = '0' + (char)((hh / 10u)    % 10u);
    buf[4] = '0' + (char)( hh           % 10u);
    buf[5] = ':';
    buf[6] = '0' + (char)((mm / 10u) % 10u);
    buf[7] = '0' + (char)( mm        % 10u);
    buf[8] = '\0';
}

// "HH:MM:SS" (HH saturates at 99)
void format_runtime_string(char *buf, uint32_t seconds)
{
    uint32_t hh = seconds / 3600u;
    uint32_t mm = (seconds % 3600u) / 60u;
    uint32_t ss = seconds % 60u;

    if (hh > 99u) hh = 99u;

    buf[0] = '0' + (char)((hh / 10u) % 10u);
    buf[1] = '0' + (char)( hh        % 10u);
    buf[2] = ':';
    buf[3] = '0' + (char)((mm / 10u) % 10u);
    buf[4] = '0' + (char)( mm        % 10u);
    buf[5] = ':';
    buf[6] = '0' + (char)((ss / 10u) % 10u);
    buf[7] = '0' + (char)( ss        % 10u);
    buf[8] = '\0';
}

// ========================= HUGE BOOT GRAPHICS (4x) ====================

void scale4x_vertical(uint8_t in, uint8_t out[4])
{
    out[0] = 0; out[1] = 0; out[2] = 0; out[3] = 0;

    // 7 original rows -> 28 rows after 4x
    for (uint8_t r = 0; r < 7; r++)
    {
        if (in & (1u << r))
        {
            uint8_t base = (uint8_t)(4u * r); // 0..24
            for (uint8_t k = 0; k < 4; k++)
            {
                uint8_t pos  = (uint8_t)(base + k); // 0..27
                uint8_t byte = (uint8_t)(pos >> 3); // 0..3
                uint8_t bit  = (uint8_t)(pos & 7u);
                out[byte] |= (1u << bit);
            }
        }
    }
}

// One HUGE char: width = 5*4 = 20 columns, height = 4 pages
void oled_draw_char_huge(uint8_t col, uint8_t page, char c)
{
    const uint8_t *pattern = font_get_pattern(c);
    uint8_t scaled[5][4];

    for (uint8_t i = 0; i < 5; i++)
        scale4x_vertical(pattern[i], scaled[i]);

    for (uint8_t p = 0; p < 4; p++)
    {
        oled_set_cursor(col, (uint8_t)(page + p));
        oled_send_data_start();

        for (uint8_t i = 0; i < 5; i++)
        {
            oled_send_data_byte(scaled[i][p]);
            oled_send_data_byte(scaled[i][p]);
            oled_send_data_byte(scaled[i][p]);
            oled_send_data_byte(scaled[i][p]);
        }

        oled_send_data_stop();
    }
}

// Draw HUGE text: spacing ONLY between chars (4 columns), no trailing spacing.
void oled_draw_text_huge(uint8_t col, uint8_t page, const char *text)
{
    uint8_t x = col;

    while (*text)
    {
        oled_draw_char_huge(x, page, *text++);

        if (*text)
        {
            // Clear spacing columns across 4 pages
            for (uint8_t p = 0; p < 4; p++)
            {
                oled_set_cursor((uint8_t)(x + 20u), (uint8_t)(page + p));
                oled_send_data_start();
                oled_send_data_byte(0x00);
                oled_send_data_byte(0x00);
                oled_send_data_byte(0x00);
                oled_send_data_byte(0x00);
                oled_send_data_stop();
            }
            x = (uint8_t)(x + 24u); // 20 + 4
        }
        else
        {
            x = (uint8_t)(x + 20u);
        }
    }
}

// Underline animation after text shown
void oled_draw_underline_animated(uint8_t col, uint8_t page, uint8_t width,
                                  uint8_t pattern, uint8_t chunk, uint16_t chunk_delay_ms)
{
    uint8_t drawn = 0;

    while (drawn < width)
    {
        uint8_t n = (uint8_t)(width - drawn);
        if (n > chunk) n = chunk;

        oled_set_cursor((uint8_t)(col + drawn), page);
        oled_send_data_start();
        for (uint8_t i = 0; i < n; i++)
        {
            oled_send_data_byte(pattern);
        }
        oled_send_data_stop();

        drawn = (uint8_t)(drawn + n);
        delay_ms_soft(chunk_delay_ms);
    }
}

// ========================= ICONS (8x8) ================================

static const uint8_t icon_brush_on[8]  = { 0x18,0x3C,0x7E,0xFF,0x7E,0x24,0x66,0x42 };
static const uint8_t icon_brush_off[8] = { 0x18,0x24,0x42,0x81,0x42,0x24,0x18,0x00 };

static const uint8_t icon_vac_on[8]    = { 0x3C,0x42,0x81,0xA5,0x81,0x99,0x42,0x3C };
static const uint8_t icon_vac_off[8]   = { 0x3C,0x42,0x81,0x81,0x81,0x81,0x42,0x3C };

// ========================= MAIN RUNTIME SCREEN =======================

void oled_update_screen(void)
{
    char timebuf[9];     // "HHHHH:MM" or "HH:MM:SS" + '\0'
    char linebuf[24];

    // Header
    oled_draw_text_centered(0, "DIMA");

    // Hour meter: "HOUR METER  HHHHH:MM"
    format_hourmeter_string(timebuf, g_overall_seconds);
    linebuf[0]='H'; linebuf[1]='O'; linebuf[2]='U'; linebuf[3]='R';
    linebuf[4]=' '; linebuf[5]='M'; linebuf[6]='E'; linebuf[7]='T';
    linebuf[8]='E'; linebuf[9]='R'; linebuf[10]=' '; linebuf[11]=' ';
    for (uint8_t i=0;i<9;i++) linebuf[12+i]=timebuf[i];
    oled_draw_text_centered(2, linebuf);

    // Brush: icon + "BRU  HH:MM:SS"
    format_runtime_string(timebuf, g_brush_seconds);
    linebuf[0]='B'; linebuf[1]='R'; linebuf[2]='U';
    linebuf[3]=' '; linebuf[4]=' ';
    for (uint8_t i=0;i<9;i++) linebuf[5+i]=timebuf[i];

    uint8_t len = 0;
    const char *p = linebuf;
    while (*p && len < 23) { len++; p++; }
    uint8_t text_width = (uint8_t)(len * 6u);
    const uint8_t icon_width = 10u;
    uint8_t total_width = (uint8_t)(icon_width + text_width);
    uint8_t start_col = (uint8_t)((128u - total_width) / 2u);
    uint8_t icon_col = start_col;
    uint8_t text_col = (uint8_t)(start_col + icon_width);

    const uint8_t *brush_icon = (g_brush_on && g_logo_blink_state) ? icon_brush_on : icon_brush_off;
    oled_set_cursor(icon_col, 4);
    oled_send_data_start();
    for (uint8_t i = 0; i < 8; i++) oled_send_data_byte(brush_icon[i]);
    oled_send_data_stop();
    oled_draw_text(text_col, 4, linebuf);

    // Vacuum: icon + "VAC  HH:MM:SS"
    format_runtime_string(timebuf, g_vacuum_seconds);
    linebuf[0]='V'; linebuf[1]='A'; linebuf[2]='C';
    linebuf[3]=' '; linebuf[4]=' ';
    for (uint8_t i2=0;i2<9;i2++) linebuf[5+i2]=timebuf[i2];

    len = 0;
    p = linebuf;
    while (*p && len < 23) { len++; p++; }
    text_width = (uint8_t)(len * 6u);
    total_width = (uint8_t)(icon_width + text_width);
    start_col = (uint8_t)((128u - total_width) / 2u);
    icon_col = start_col;
    text_col = (uint8_t)(start_col + icon_width);

    const uint8_t *vac_icon = (g_vacuum_on && g_logo_blink_state) ? icon_vac_on : icon_vac_off;
    oled_set_cursor(icon_col, 6);
    oled_send_data_start();
    for (uint8_t j = 0; j < 8; j++) oled_send_data_byte(vac_icon[j]);
    oled_send_data_stop();
    oled_draw_text(text_col, 6, linebuf);
}

// ========================= BOOT SPLASH (HUGE + ANIM UNDERLINE) ========

void oled_show_boot_screen(void)
{
    // HUGE DIMA geometry:
    // each huge char is 20px wide, spacing between chars is 4px
    // total width = 4*20 + 3*4 = 92
    const uint8_t num_chars       = 4u;
    const uint8_t char_advance    = 24u;      // 20 + 4 spacing
    const uint8_t text_width      = (uint8_t)(char_advance * num_chars - 4u); // 92
    const uint8_t start_col       = (uint8_t)((128u - text_width) / 2u);      // centered
    const uint8_t top_page        = 2;        // huge text uses pages 2..5
    const uint8_t underline_page  = 6;        // underline below huge text

    // 1) DISPLAY OFF -> clear RAM invisibly
    oled_send_command(0xAE);
    oled_clear();

    // 2) Draw HUGE "DIMA" centered
    oled_draw_text_huge(start_col, top_page, "DIMA");

    // 3) Ensure underline area blank first (so animation is visible)
    oled_set_cursor(start_col, underline_page);
    oled_send_data_start();
    for (uint8_t i = 0; i < text_width; i++)
        oled_send_data_byte(0x00);
    oled_send_data_stop();

    // 4) DISPLAY ON -> show ONLY text first
    oled_send_command(0xAF);

    // Total boot time = 3 seconds (includes underline draw time)
    g_boot_ms = 0;

    // 5) Animate underline AFTER text is shown (graphic line draw)
    // 0x01 = thin line, use 0x03 for thicker (2px) if you want.
    oled_draw_underline_animated(start_col, underline_page, text_width,
                                 0x01,   // underline pattern
                                 24,      // bytes per step
                                 5);    // ms per step

    // 6) Hold until total 3 seconds
    while (g_boot_ms < 1000)
    {
        // Timer0 ISR increments g_boot_ms
    }

    // 7) DISPLAY OFF -> clear RAM invisibly for next screen
    oled_send_command(0xAE);
    oled_clear();
    // leave OFF; main() will draw UI then turn ON
}

// ========================= EEPROM HELPERS (OVERALL ONLY) =============

void eeprom_write_byte(uint16_t addr, uint8_t value)
{
    while (EECON1bits.WR);

    EEADR  = (uint8_t)(addr & 0xFFu);
    EEDATA = value;

    EECON1bits.EEPGD = 0;
    EECON1bits.CFGS  = 0;
    EECON1bits.WREN  = 1;

    INTCONbits.GIE = 0;
    EECON2 = 0x55;
    EECON2 = 0xAA;
    EECON1bits.WR = 1;
    INTCONbits.GIE = 1;

    while (EECON1bits.WR);
    EECON1bits.WREN = 0;
}

uint8_t eeprom_read_byte(uint16_t addr)
{
    while (EECON1bits.WR);

    EEADR  = (uint8_t)(addr & 0xFFu);

    EECON1bits.EEPGD = 0;
    EECON1bits.CFGS  = 0;
    EECON1bits.RD    = 1;

    return EEDATA;
}

void hourmeter_save_to_eeprom(void)
{
    uint32_t val = g_overall_seconds;

    eeprom_write_byte(EEADDR_OVERALL + 0, (uint8_t)(val & 0xFFu));
    eeprom_write_byte(EEADDR_OVERALL + 1, (uint8_t)((val >> 8) & 0xFFu));
    eeprom_write_byte(EEADDR_OVERALL + 2, (uint8_t)((val >> 16) & 0xFFu));
    eeprom_write_byte(EEADDR_OVERALL + 3, (uint8_t)((val >> 24) & 0xFFu));

    eeprom_write_byte(EEADDR_MAGIC, EE_MAGIC_VALUE);
}

void hourmeter_load_from_eeprom(void)
{
    uint8_t magic = eeprom_read_byte(EEADDR_MAGIC);

    if (magic != EE_MAGIC_VALUE)
    {
        g_overall_seconds = 0;
        g_brush_seconds   = 0;
        g_vacuum_seconds  = 0;
        return;
    }

    uint32_t val;
    val  = (uint32_t)eeprom_read_byte(EEADDR_OVERALL + 0);
    val |= (uint32_t)eeprom_read_byte(EEADDR_OVERALL + 1) << 8;
    val |= (uint32_t)eeprom_read_byte(EEADDR_OVERALL + 2) << 16;
    val |= (uint32_t)eeprom_read_byte(EEADDR_OVERALL + 3) << 24;
    g_overall_seconds = val;

    // brush & vacuum start from 0 on each power-up
    g_brush_seconds  = 0;
    g_vacuum_seconds = 0;
}

// ========================= MAIN ======================================

int main(void)
{
    system_init();

    // Load saved hour-meter values from EEPROM (overall only)
    hourmeter_load_from_eeprom();

    // Boot screen: HUGE graphics "DIMA" centered + animated underline
    oled_show_boot_screen();

    // Draw first UI frame while display is OFF, then turn ON
    oled_update_screen();
    oled_send_command(0xAF);

    while (1)
    {
        if (g_10ms_flag)
        {
            g_10ms_flag = 0;
            debounce_inputs_10ms();
            update_logic_10ms();
        }

        if (g_1s_flag)
        {
            g_1s_flag = 0;
            update_logic_1s();
        }
    }

    return 0;
}

