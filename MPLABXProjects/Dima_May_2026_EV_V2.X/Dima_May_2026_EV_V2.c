// main.c - PIC18F25K22 Vacuum Cleaner Controller with OLED Runtime Display
// Compiler: XC8 v2.x (C99)
// MCU: PIC18F25K22
// OLED: SSD1306-compatible I2C 128x64 (bit-banged I2C)

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

#define _XTAL_FREQ 16000000UL

// ========================= CONFIGURATION BITS =========================
#pragma config FOSC = INTIO67
#pragma config PLLCFG = OFF
#pragma config PRICLKEN = OFF
#pragma config FCMEN = OFF
#pragma config IESO = OFF

#pragma config PWRTEN = OFF
#pragma config BOREN  = SBORDIS
#pragma config BORV   = 190

#pragma config WDTEN = OFF
#pragma config WDTPS = 32768

#pragma config CCP2MX = PORTC1
#pragma config PBADEN = OFF
#pragma config CCP3MX = PORTB5
#pragma config HFOFST = ON
#pragma config P2BMX  = PORTC0
#pragma config MCLRE  = EXTMCLR

#pragma config STVREN = ON
#pragma config LVP    = OFF
#pragma config XINST  = OFF
#pragma config DEBUG  = OFF

#pragma config CP0 = OFF, CP1 = OFF, CP2 = OFF, CP3 = OFF
#pragma config CPB = OFF, CPD = OFF
#pragma config WRT0 = OFF, WRT1 = OFF, WRT2 = OFF, WRT3 = OFF
#pragma config WRTC = OFF, WRTB = OFF, WRTD = OFF
#pragma config EBTR0 = OFF, EBTR1 = OFF, EBTR2 = OFF, EBTR3 = OFF
#pragma config EBTRB = OFF

// ========================= PIN DEFINITIONS ============================

// Inputs
#define BRUSH_SW_TRIS     TRISAbits.TRISA0
#define VAC_SW_TRIS       TRISAbits.TRISA1

#define BRUSH_SW_PORT     PORTAbits.RA0
#define VAC_SW_PORT       PORTAbits.RA1

// Outputs
#define BRUSH_RELAY_TRIS  TRISCbits.TRISC0
#define VAC_RELAY_TRIS    TRISCbits.TRISC1
#define BRUSH_REL_TRIS    TRISAbits.TRISA2

#define BRUSH_RELAY_LAT   LATCbits.LATC0
#define VAC_RELAY_LAT     LATCbits.LATC1
#define BRUSH_REL_LAT     LATAbits.LATA2

#define VAC_DELAY_LED_TRIS  TRISBbits.TRISB0
#define VAC_DELAY_LED_LAT   LATBbits.LATB0

// I2C bit-banged pins
#define SDA_TRIS    TRISCbits.TRISC4
#define SDA_LAT     LATCbits.LATC4
#define SDA_PORT    PORTCbits.RC4

#define SCL_TRIS    TRISCbits.TRISC3
#define SCL_LAT     LATCbits.LATC3
#define SCL_PORT    PORTCbits.RC3

#define OLED_I2C_ADDR  0x78

// ========================= EEPROM MAP ================================
#define EEADDR_OVERALL   0x0000u
#define EEADDR_MAGIC     0x0004u
#define EE_MAGIC_VALUE   0x5Au

// ========================= HOUR METER LIMIT ==========================
#define HOURMETER_MAX_HOURS         99999UL
#define HOURMETER_RESET_SECONDS     (HOURMETER_MAX_HOURS * 3600UL)

// ========================= TOUCH / PRESS SETTINGS ====================
#define TOUCH_SAMPLE_PERIOD_MS          10u
#define TOUCH_DEBOUNCE_STABLE_TICKS     2u
#define LONG_PRESS_TICKS                100u
#define BRUSH_RELEASE_ON_TICKS          50u
#define VAC_OFF_DELAY_SECONDS           10u

// Fast OLED arrow blink while brush release graphic is active
#define GRAPHIC_ARROW_BLINK_TICKS       15u

// If touch input is inverted, change to 0u
#define BRUSH_SW_ACTIVE_LEVEL           1u
#define VAC_SW_ACTIVE_LEVEL             1u

// OLED geometry
#define OLED_WIDTH                      128u
#define OLED_PAGES                      8u

// Graphic layout
#define GRAPHIC_RECT_X                  24u
#define GRAPHIC_RECT_W                  80u
#define GRAPHIC_RECT_PAGE               2u
#define GRAPHIC_RECT_HEIGHT_PAGES       3u
#define GRAPHIC_BRISTLE_PAGE            (GRAPHIC_RECT_PAGE + GRAPHIC_RECT_HEIGHT_PAGES)
#define GRAPHIC_ARROW_PAGE              6u

// ========================= GLOBAL VARIABLES ===========================
volatile uint32_t g_overall_seconds = 0;
volatile uint32_t g_brush_seconds   = 0;
volatile uint32_t g_vacuum_seconds  = 0;

volatile uint16_t g_ms_counter = 0;
volatile uint8_t  g_10ms_flag  = 0;
volatile uint8_t  g_1s_flag    = 0;
volatile uint16_t g_boot_ms    = 0;

volatile bool g_brush_sw_state = false;
volatile bool g_vac_sw_state   = false;

volatile bool g_brush_sw_prev_state = false;
volatile bool g_vac_sw_prev_state   = false;

static uint8_t brush_sw_integrator = 0u;
static uint8_t vac_sw_integrator   = 0u;

volatile bool g_brush_on  = false;
volatile bool g_vacuum_on = false;

volatile uint8_t g_vacuum_off_delay = 0;
volatile bool    g_vacuum_delay_active = false;

volatile bool     g_brush_tracking_press  = false;
volatile uint16_t g_brush_hold_ticks      = 0;
volatile bool     g_brush_long_press_done = false;

volatile bool     g_brush_release_on         = false;
volatile uint16_t g_brush_release_ticks_left = 0;

volatile bool g_logo_blink_state = false;
volatile bool g_display_refresh_request = true;

// fast graphic blink state
volatile bool g_graphic_arrow_blink_state = true;

// ========================= FUNCTION PROTOTYPES ========================
void delay_us_soft(uint16_t us);
void delay_ms_soft(uint16_t ms);

void system_init(void);
void osc_init(void);
void io_init(void);
void timer0_init(void);

void __interrupt() isr(void);

bool read_brush_sw_raw(void);
bool read_vac_sw_raw(void);

void debounce_inputs_10ms(void);
void handle_brush_switch_10ms(void);
void handle_vac_switch_10ms(void);
void update_outputs(void);
void update_logic_10ms(void);
void update_logic_1s(void);

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
void oled_draw_text_centered(uint8_t page, const char *text);
void oled_update_screen(void);
void oled_show_boot_screen(void);

void oled_draw_old_runtime_screen(void);
void oled_draw_graphic_screen(void);
void oled_draw_graphic_base(void);
void oled_clear_5x8(uint8_t x, uint8_t page);
void oled_draw_down_arrow_5x8(uint8_t x, uint8_t page);
void oled_draw_rectangle(uint8_t x, uint8_t y_page, uint8_t width, uint8_t height_page);
void oled_draw_brush_bristles(uint8_t rect_x, uint8_t rect_width, uint8_t bristle_page);

void scale4x_vertical(uint8_t in, uint8_t out[4]);
void oled_draw_char_huge(uint8_t col, uint8_t page, char c);
void oled_draw_text_huge(uint8_t col, uint8_t page, const char *text);
void oled_draw_underline_animated(uint8_t col, uint8_t page, uint8_t width,
                                  uint8_t pattern, uint8_t chunk, uint16_t chunk_delay_ms);

void format_hourmeter_string(char *buf, uint32_t seconds);
void format_runtime_string(char *buf, uint32_t seconds);

void eeprom_write_byte(uint16_t addr, uint8_t value);
uint8_t eeprom_read_byte(uint16_t addr);
void hourmeter_load_from_eeprom(void);
void hourmeter_save_to_eeprom(void);

const uint8_t* font_get_pattern(char c);

// ========================= SMALL GRAPHICS =============================
static const uint8_t down_arrow[5] = {
    0x10,
    0x30,
    0xFF,
    0x30,
    0x10
};

// ========================= SOFTWARE DELAYS ===========================
void delay_us_soft(uint16_t us)
{
    while (us--)
    {
        volatile uint8_t i;
        for (i = 0; i < 4; i++)
        {
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
    oled_init();
    timer0_init();
}

void osc_init(void)
{
    OSCCON = 0b01110010;
}

void io_init(void)
{
    ANSELA = 0x00;
    ANSELB = 0x00;
    ANSELC = 0x00;

    BRUSH_SW_TRIS = 1;
    VAC_SW_TRIS   = 1;

    BRUSH_RELAY_TRIS   = 0;
    VAC_RELAY_TRIS     = 0;
    BRUSH_REL_TRIS     = 0;
    VAC_DELAY_LED_TRIS = 0;

    BRUSH_RELAY_LAT    = 0;
    VAC_RELAY_LAT      = 0;
    BRUSH_REL_LAT      = 0;
    VAC_DELAY_LED_LAT  = 0;

    SCL_TRIS = 1;
    SDA_TRIS = 1;
    SCL_LAT  = 0;
    SDA_LAT  = 0;
}

void timer0_init(void)
{
    T0CON = 0b00001000;
    TMR0H = 0xF0;
    TMR0L = 0x60;

    INTCONbits.TMR0IF = 0;
    INTCONbits.TMR0IE = 1;
    INTCONbits.GIE    = 1;

    T0CONbits.TMR0ON = 1;
}

// ========================= ISR =======================================
void __interrupt() isr(void)
{
    if (INTCONbits.TMR0IF)
    {
        static uint16_t ms_for_1s = 0u;

        TMR0H = 0xF0;
        TMR0L = 0x60;
        INTCONbits.TMR0IF = 0;

        g_ms_counter++;
        g_boot_ms++;

        if (g_ms_counter >= TOUCH_SAMPLE_PERIOD_MS)
        {
            g_ms_counter -= TOUCH_SAMPLE_PERIOD_MS;
            g_10ms_flag = 1u;
        }

        ms_for_1s++;
        if (ms_for_1s >= 1000u)
        {
            ms_for_1s = 0u;
            g_1s_flag = 1u;
        }
    }
}

// ========================= INPUT HELPERS ==============================
bool read_brush_sw_raw(void)
{
    if (BRUSH_SW_ACTIVE_LEVEL)
    {
        return (BRUSH_SW_PORT != 0u) ? true : false;
    }
    else
    {
        return (BRUSH_SW_PORT == 0u) ? true : false;
    }
}

bool read_vac_sw_raw(void)
{
    if (VAC_SW_ACTIVE_LEVEL)
    {
        return (VAC_SW_PORT != 0u) ? true : false;
    }
    else
    {
        return (VAC_SW_PORT == 0u) ? true : false;
    }
}

// ========================= DEBOUNCE ==================================
void debounce_inputs_10ms(void)
{
    bool raw_brush = read_brush_sw_raw();
    bool raw_vac   = read_vac_sw_raw();

    if (raw_brush)
    {
        if (brush_sw_integrator < TOUCH_DEBOUNCE_STABLE_TICKS)
        {
            brush_sw_integrator++;
        }
    }
    else
    {
        if (brush_sw_integrator > 0u)
        {
            brush_sw_integrator--;
        }
    }

    if (brush_sw_integrator == 0u)
    {
        g_brush_sw_state = false;
    }
    else if (brush_sw_integrator >= TOUCH_DEBOUNCE_STABLE_TICKS)
    {
        brush_sw_integrator = TOUCH_DEBOUNCE_STABLE_TICKS;
        g_brush_sw_state = true;
    }

    if (raw_vac)
    {
        if (vac_sw_integrator < TOUCH_DEBOUNCE_STABLE_TICKS)
        {
            vac_sw_integrator++;
        }
    }
    else
    {
        if (vac_sw_integrator > 0u)
        {
            vac_sw_integrator--;
        }
    }

    if (vac_sw_integrator == 0u)
    {
        g_vac_sw_state = false;
    }
    else if (vac_sw_integrator >= TOUCH_DEBOUNCE_STABLE_TICKS)
    {
        vac_sw_integrator = TOUCH_DEBOUNCE_STABLE_TICKS;
        g_vac_sw_state = true;
    }
}

// ========================= BUTTON / TOUCH LOGIC =======================
void handle_brush_switch_10ms(void)
{
    bool brush_rising_edge  = (!g_brush_sw_prev_state && g_brush_sw_state);
    bool brush_falling_edge = ( g_brush_sw_prev_state && !g_brush_sw_state);

    if (brush_rising_edge)
    {
        g_brush_tracking_press  = true;
        g_brush_hold_ticks      = 0u;
        g_brush_long_press_done = false;
    }

    if (g_brush_tracking_press && g_brush_sw_state)
    {
        if (g_brush_hold_ticks < LONG_PRESS_TICKS)
        {
            g_brush_hold_ticks++;
        }

        if ((!g_brush_long_press_done) &&
            (g_brush_hold_ticks >= LONG_PRESS_TICKS))
        {
            g_brush_long_press_done = true;
            g_brush_on = false;
            g_brush_release_on = true;
            g_brush_release_ticks_left = BRUSH_RELEASE_ON_TICKS;

            g_graphic_arrow_blink_state = true;
            g_display_refresh_request = true;
        }
    }

    if (brush_falling_edge)
    {
        if (g_brush_tracking_press)
        {
            if (!g_brush_long_press_done)
            {
                g_brush_on = !g_brush_on;
            }

            g_brush_tracking_press  = false;
            g_brush_hold_ticks      = 0u;
            g_brush_long_press_done = false;
            g_display_refresh_request = true;
        }
    }

    if (g_brush_release_on)
    {
        if (g_brush_release_ticks_left > 0u)
        {
            g_brush_release_ticks_left--;
        }

        if (g_brush_release_ticks_left == 0u)
        {
            g_brush_release_on = false;
            g_display_refresh_request = true;
        }
    }

    g_brush_sw_prev_state = g_brush_sw_state;
}

void handle_vac_switch_10ms(void)
{
    bool vac_falling_edge = (g_vac_sw_prev_state && !g_vac_sw_state);

    if (vac_falling_edge)
    {
        if (!g_vacuum_on)
        {
            g_vacuum_on = true;
            g_vacuum_delay_active = false;
            g_vacuum_off_delay = 0u;
        }
        else
        {
            if (g_vacuum_delay_active)
            {
                g_vacuum_delay_active = false;
                g_vacuum_off_delay = 0u;
            }
            else
            {
                g_vacuum_delay_active = true;
                g_vacuum_off_delay = 0u;
            }
        }

        g_display_refresh_request = true;
    }

    g_vac_sw_prev_state = g_vac_sw_state;
}

// ========================= OUTPUT UPDATE ==============================
void update_outputs(void)
{
    BRUSH_RELAY_LAT = g_brush_on ? 1 : 0;
    VAC_RELAY_LAT   = g_vacuum_on ? 1 : 0;
    BRUSH_REL_LAT   = g_brush_release_on ? 1 : 0;
}

// ========================= MAIN LOGIC ================================
void update_logic_10ms(void)
{
    static uint8_t blink_10ms = 0u;
    static bool last_delay_active = false;
    static uint8_t graphic_arrow_blink_counter = 0u;
    static bool prev_graphic_mode = false;

    debounce_inputs_10ms();
    handle_brush_switch_10ms();
    handle_vac_switch_10ms();

    if (g_vacuum_delay_active)
    {
        if (!last_delay_active)
        {
            last_delay_active = true;
            blink_10ms = 0u;
            VAC_DELAY_LED_LAT = 1;
        }

        blink_10ms++;
        if (blink_10ms >= 30u)
        {
            blink_10ms = 0u;
            VAC_DELAY_LED_LAT ^= 1;
        }
    }
    else
    {
        last_delay_active = false;
        blink_10ms = 0u;
        VAC_DELAY_LED_LAT = 0;
    }

    if (g_brush_release_on)
    {
        if (!prev_graphic_mode)
        {
            prev_graphic_mode = true;
            graphic_arrow_blink_counter = 0u;
            g_graphic_arrow_blink_state = true;
            g_display_refresh_request = true;
        }

        graphic_arrow_blink_counter++;
        if (graphic_arrow_blink_counter >= GRAPHIC_ARROW_BLINK_TICKS)
        {
            graphic_arrow_blink_counter = 0u;
            g_graphic_arrow_blink_state = !g_graphic_arrow_blink_state;
            g_display_refresh_request = true;
        }
    }
    else
    {
        if (prev_graphic_mode)
        {
            prev_graphic_mode = false;
            graphic_arrow_blink_counter = 0u;
            g_graphic_arrow_blink_state = true;
            g_display_refresh_request = true;
        }
    }

    update_outputs();
}

void update_logic_1s(void)
{
    static bool logo_phase = false;
    static uint8_t eeprom_save_counter = 0u;

    logo_phase = !logo_phase;
    g_logo_blink_state = logo_phase;

    g_overall_seconds++;

    if (g_overall_seconds >= HOURMETER_RESET_SECONDS)
    {
        g_overall_seconds = 0u;
        hourmeter_save_to_eeprom();
    }

    if (g_brush_on)
    {
        g_brush_seconds++;
    }

    if (g_vacuum_on)
    {
        g_vacuum_seconds++;
    }

    if (g_vacuum_delay_active && g_vacuum_on)
    {
        if (g_vacuum_off_delay < VAC_OFF_DELAY_SECONDS)
        {
            g_vacuum_off_delay++;
        }

        if (g_vacuum_off_delay >= VAC_OFF_DELAY_SECONDS)
        {
            g_vacuum_on = false;
            g_vacuum_delay_active = false;
            g_vacuum_off_delay = 0u;
        }
    }
    else
    {
        g_vacuum_off_delay = 0u;
    }

    update_outputs();
    g_display_refresh_request = true;

    eeprom_save_counter++;
    if (eeprom_save_counter >= 10u)
    {
        eeprom_save_counter = 0u;
        hourmeter_save_to_eeprom();
    }
}

// ========================= I2C BIT-BANG ==============================
static void i2c_delay(void)
{
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

    SDA_TRIS = 0;
    i2c_delay();
    SCL_TRIS = 0;
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
    uint8_t i;

    for (i = 0; i < 8u; i++)
    {
        if (data & 0x80u) SDA_TRIS = 1;
        else              SDA_TRIS = 0;

        i2c_delay();
        SCL_TRIS = 1;
        i2c_delay();
        SCL_TRIS = 0;
        i2c_delay();

        data <<= 1;
    }

    SDA_TRIS = 1;
    i2c_delay();
    SCL_TRIS = 1;
    i2c_delay();
    (void)SDA_PORT;
    SCL_TRIS = 0;
    i2c_delay();

    return true;
}

// ========================= OLED CORE =================================
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
    delay_ms_soft(30);

    oled_send_command(0xAE);
    oled_send_command(0x20);
    oled_send_command(0x00);
    oled_send_command(0xB0);

    oled_send_command(0x20);
    oled_send_command(0x00);
    oled_send_command(0xB0);
    oled_send_command(0xC8);
    oled_send_command(0x00);
    oled_send_command(0x10);
    oled_send_command(0x40);
    oled_send_command(0x81);
    oled_send_command(0x7F);
    oled_send_command(0xA1);
    oled_send_command(0xA6);
    oled_send_command(0xA8);
    oled_send_command(0x3F);
    oled_send_command(0xA4);
    oled_send_command(0xD3);
    oled_send_command(0x00);
    oled_send_command(0xD5);
    oled_send_command(0x80);
    oled_send_command(0xD9);
    oled_send_command(0xF1);
    oled_send_command(0xDA);
    oled_send_command(0x12);
    oled_send_command(0xDB);
    oled_send_command(0x40);
    oled_send_command(0x8D);
    oled_send_command(0x14);
}

void oled_set_cursor(uint8_t col, uint8_t page)
{
    oled_send_command(0xB0 | (page & 0x07));
    oled_send_command(0x00 | (col & 0x0F));
    oled_send_command(0x10 | ((col >> 4) & 0x0F));
}

void oled_clear(void)
{
    uint8_t page;
    uint8_t col;

    for (page = 0; page < 8u; page++)
    {
        oled_set_cursor(0, page);
        oled_send_data_start();
        for (col = 0; col < 132u; col++)
        {
            oled_send_data_byte(0x00);
        }
        oled_send_data_stop();
    }
}

// ----------------- Tiny Font (5x7 + 1 space) -------------------------
const uint8_t* font_get_pattern(char c)
{
    static const uint8_t digits[10][5] = {
        {0x3E,0x51,0x49,0x45,0x3E},
        {0x00,0x42,0x7F,0x40,0x00},
        {0x42,0x61,0x51,0x49,0x46},
        {0x21,0x41,0x45,0x4B,0x31},
        {0x18,0x14,0x12,0x7F,0x10},
        {0x27,0x45,0x45,0x45,0x39},
        {0x3C,0x4A,0x49,0x49,0x30},
        {0x01,0x71,0x09,0x05,0x03},
        {0x36,0x49,0x49,0x49,0x36},
        {0x06,0x49,0x49,0x29,0x1E}
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
    {
        return digits[c - '0'];
    }

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

void oled_draw_text_centered(uint8_t page, const char *text)
{
    uint8_t len = 0u;
    const char *p = text;

    while (*p && len < 21u)
    {
        len++;
        p++;
    }

    {
        uint8_t width = (uint8_t)(len * 6u);
        uint8_t start_col = (uint8_t)((OLED_WIDTH - width) / 2u);
        oled_draw_text(start_col, page, text);
    }
}

// ========================= FORMATTING ================================
void format_hourmeter_string(char *buf, uint32_t seconds)
{
    uint32_t hh = seconds / 3600u;
    uint32_t mm = (seconds % 3600u) / 60u;

    if (hh > 99999u)
    {
        hh = 99999u;
    }

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

void format_runtime_string(char *buf, uint32_t seconds)
{
    uint32_t hh = seconds / 3600u;
    uint32_t mm = (seconds % 3600u) / 60u;
    uint32_t ss = seconds % 60u;

    if (hh > 99u)
    {
        hh = 99u;
    }

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

// ========================= HUGE BOOT GRAPHICS ========================
void scale4x_vertical(uint8_t in, uint8_t out[4])
{
    uint8_t r;
    uint8_t k;

    out[0] = 0u;
    out[1] = 0u;
    out[2] = 0u;
    out[3] = 0u;

    for (r = 0; r < 7u; r++)
    {
        if (in & (1u << r))
        {
            uint8_t base = (uint8_t)(4u * r);
            for (k = 0; k < 4u; k++)
            {
                uint8_t pos  = (uint8_t)(base + k);
                uint8_t byte = (uint8_t)(pos >> 3);
                uint8_t bit  = (uint8_t)(pos & 7u);
                out[byte] |= (1u << bit);
            }
        }
    }
}

void oled_draw_char_huge(uint8_t col, uint8_t page, char c)
{
    const uint8_t *pattern = font_get_pattern(c);
    uint8_t scaled[5][4];
    uint8_t i;
    uint8_t p;

    for (i = 0; i < 5u; i++)
    {
        scale4x_vertical(pattern[i], scaled[i]);
    }

    for (p = 0; p < 4u; p++)
    {
        oled_set_cursor(col, (uint8_t)(page + p));
        oled_send_data_start();

        for (i = 0; i < 5u; i++)
        {
            oled_send_data_byte(scaled[i][p]);
            oled_send_data_byte(scaled[i][p]);
            oled_send_data_byte(scaled[i][p]);
            oled_send_data_byte(scaled[i][p]);
        }

        oled_send_data_stop();
    }
}

void oled_draw_text_huge(uint8_t col, uint8_t page, const char *text)
{
    uint8_t x = col;
    uint8_t p;

    while (*text)
    {
        oled_draw_char_huge(x, page, *text++);

        if (*text)
        {
            for (p = 0; p < 4u; p++)
            {
                oled_set_cursor((uint8_t)(x + 20u), (uint8_t)(page + p));
                oled_send_data_start();
                oled_send_data_byte(0x00);
                oled_send_data_byte(0x00);
                oled_send_data_byte(0x00);
                oled_send_data_byte(0x00);
                oled_send_data_stop();
            }
            x = (uint8_t)(x + 24u);
        }
        else
        {
            x = (uint8_t)(x + 20u);
        }
    }
}

void oled_draw_underline_animated(uint8_t col, uint8_t page, uint8_t width,
                                  uint8_t pattern, uint8_t chunk, uint16_t chunk_delay_ms)
{
    uint8_t drawn = 0u;
    uint8_t i;

    while (drawn < width)
    {
        uint8_t n = (uint8_t)(width - drawn);
        if (n > chunk)
        {
            n = chunk;
        }

        oled_set_cursor((uint8_t)(col + drawn), page);
        oled_send_data_start();
        for (i = 0; i < n; i++)
        {
            oled_send_data_byte(pattern);
        }
        oled_send_data_stop();

        drawn = (uint8_t)(drawn + n);
        delay_ms_soft(chunk_delay_ms);
    }
}

// ========================= OLED GRAPHIC HELPERS =======================
void oled_clear_5x8(uint8_t x, uint8_t page)
{
    uint8_t i;

    if (x > (OLED_WIDTH - 5u))
    {
        x = OLED_WIDTH - 5u;
    }

    if (page > (OLED_PAGES - 1u))
    {
        page = OLED_PAGES - 1u;
    }

    oled_set_cursor(x, page);
    oled_send_data_start();
    for (i = 0; i < 5u; i++)
    {
        oled_send_data_byte(0x00);
    }
    oled_send_data_stop();
}

void oled_draw_rectangle(uint8_t x, uint8_t y_page, uint8_t width, uint8_t height_page)
{
    uint8_t i;

    if (width == 0u || height_page == 0u) return;
    if ((uint16_t)x + width > OLED_WIDTH) return;
    if ((uint16_t)y_page + height_page > OLED_PAGES) return;

    oled_set_cursor(x, y_page);
    oled_send_data_start();
    for (i = 0; i < width; i++)
    {
        oled_send_data_byte(0x01);
    }
    oled_send_data_stop();

    oled_set_cursor(x, (uint8_t)(y_page + height_page - 1u));
    oled_send_data_start();
    for (i = 0; i < width; i++)
    {
        oled_send_data_byte(0x80);
    }
    oled_send_data_stop();

    for (i = 0; i < height_page; i++)
    {
        oled_set_cursor(x, (uint8_t)(y_page + i));
        oled_send_data_start();
        oled_send_data_byte(0xFF);
        oled_send_data_stop();

        oled_set_cursor((uint8_t)(x + width - 1u), (uint8_t)(y_page + i));
        oled_send_data_start();
        oled_send_data_byte(0xFF);
        oled_send_data_stop();
    }
}

void oled_draw_brush_bristles(uint8_t rect_x, uint8_t rect_width, uint8_t bristle_page)
{
    uint8_t inner_start_x;
    uint8_t inner_end_x;
    uint8_t x;

    if (bristle_page >= OLED_PAGES) return;
    if (rect_width < 10u) return;

    inner_start_x = (uint8_t)(rect_x + 6u);
    inner_end_x   = (uint8_t)(rect_x + rect_width - 7u);

    oled_set_cursor(rect_x, bristle_page);
    oled_send_data_start();
    for (x = 0u; x < rect_width; x++)
    {
        oled_send_data_byte(0x00);
    }
    oled_send_data_stop();

    for (x = inner_start_x; x <= inner_end_x; x = (uint8_t)(x + 4u))
    {
        oled_set_cursor(x, bristle_page);
        oled_send_data_start();
        oled_send_data_byte(0xF8);
        if ((uint8_t)(x + 1u) < OLED_WIDTH)
        {
            oled_send_data_byte(0xF8);
        }
        oled_send_data_stop();
    }
}

void oled_draw_down_arrow_5x8(uint8_t x, uint8_t page)
{
    uint8_t i;

    if (x > (OLED_WIDTH - 5u))
    {
        x = OLED_WIDTH - 5u;
    }

    if (page > (OLED_PAGES - 1u))
    {
        page = OLED_PAGES - 1u;
    }

    oled_set_cursor(x, page);
    oled_send_data_start();
    for (i = 0; i < 5u; i++)
    {
        oled_send_data_byte(down_arrow[i]);
    }
    oled_send_data_stop();
}

// ========================= OLED SCREENS ===============================
void oled_draw_old_runtime_screen(void)
{
    char timebuf[9];
    char linebuf[24];
    uint8_t len;
    const char *p;
    uint8_t text_width;
    const uint8_t icon_width = 10u;
    uint8_t total_width;
    uint8_t start_col;
    uint8_t icon_col;
    uint8_t text_col;
    uint8_t i;

    static const uint8_t icon_brush_on[8]  = { 0x18,0x3C,0x7E,0xFF,0x7E,0x24,0x66,0x42 };
    static const uint8_t icon_brush_off[8] = { 0x18,0x24,0x42,0x81,0x42,0x24,0x18,0x00 };
    static const uint8_t icon_vac_on[8]    = { 0x3C,0x42,0x81,0xA5,0x81,0x99,0x42,0x3C };
    static const uint8_t icon_vac_off[8]   = { 0x3C,0x42,0x81,0x81,0x81,0x81,0x42,0x3C };

    oled_draw_text_centered(0, "DIMA");

    format_hourmeter_string(timebuf, g_overall_seconds);
    linebuf[0]  = 'H';
    linebuf[1]  = 'O';
    linebuf[2]  = 'U';
    linebuf[3]  = 'R';
    linebuf[4]  = ' ';
    linebuf[5]  = 'M';
    linebuf[6]  = 'E';
    linebuf[7]  = 'T';
    linebuf[8]  = 'E';
    linebuf[9]  = 'R';
    linebuf[10] = ' ';
    linebuf[11] = ' ';
    for (i = 0; i < 9u; i++) linebuf[12u + i] = timebuf[i];
    linebuf[21] = '\0';
    oled_draw_text_centered(2, linebuf);

    format_runtime_string(timebuf, g_brush_seconds);
    linebuf[0] = 'B';
    linebuf[1] = 'R';
    linebuf[2] = 'U';
    linebuf[3] = ' ';
    linebuf[4] = ' ';
    for (i = 0; i < 9u; i++) linebuf[5u + i] = timebuf[i];
    linebuf[14] = '\0';

    len = 0u;
    p = linebuf;
    while (*p && len < 23u) { len++; p++; }
    text_width = (uint8_t)(len * 6u);
    total_width = (uint8_t)(icon_width + text_width);
    start_col = (uint8_t)((OLED_WIDTH - total_width) / 2u);
    icon_col = start_col;
    text_col = (uint8_t)(start_col + icon_width);

    oled_set_cursor(icon_col, 4);
    oled_send_data_start();
    if (g_brush_on && g_logo_blink_state)
    {
        for (i = 0; i < 8u; i++) oled_send_data_byte(icon_brush_on[i]);
    }
    else
    {
        for (i = 0; i < 8u; i++) oled_send_data_byte(icon_brush_off[i]);
    }
    oled_send_data_stop();
    oled_draw_text(text_col, 4, linebuf);

    format_runtime_string(timebuf, g_vacuum_seconds);
    linebuf[0] = 'V';
    linebuf[1] = 'A';
    linebuf[2] = 'C';
    linebuf[3] = ' ';
    linebuf[4] = ' ';
    for (i = 0; i < 9u; i++) linebuf[5u + i] = timebuf[i];
    linebuf[14] = '\0';

    len = 0u;
    p = linebuf;
    while (*p && len < 23u) { len++; p++; }
    text_width = (uint8_t)(len * 6u);
    total_width = (uint8_t)(icon_width + text_width);
    start_col = (uint8_t)((OLED_WIDTH - total_width) / 2u);
    icon_col = start_col;
    text_col = (uint8_t)(start_col + icon_width);

    oled_set_cursor(icon_col, 6);
    oled_send_data_start();
    if (g_vacuum_on && g_logo_blink_state)
    {
        for (i = 0; i < 8u; i++) oled_send_data_byte(icon_vac_on[i]);
    }
    else
    {
        for (i = 0; i < 8u; i++) oled_send_data_byte(icon_vac_off[i]);
    }
    oled_send_data_stop();
    oled_draw_text(text_col, 6, linebuf);
}

void oled_draw_graphic_base(void)
{
    oled_clear();
    oled_draw_text_centered(0, "DIMA");

    oled_draw_rectangle(GRAPHIC_RECT_X,
                        GRAPHIC_RECT_PAGE,
                        GRAPHIC_RECT_W,
                        GRAPHIC_RECT_HEIGHT_PAGES);

    oled_draw_brush_bristles(GRAPHIC_RECT_X,
                             GRAPHIC_RECT_W,
                             GRAPHIC_BRISTLE_PAGE);
}

void oled_draw_graphic_screen(void)
{
    // Moved arrows outside the comb area and aligned near the rectangle ends
    uint8_t left_arrow_x  = (uint8_t)(GRAPHIC_RECT_X - 2u);
    uint8_t right_arrow_x = (uint8_t)(GRAPHIC_RECT_X + GRAPHIC_RECT_W - 3u);

    if (g_graphic_arrow_blink_state)
    {
        oled_draw_down_arrow_5x8(left_arrow_x, GRAPHIC_ARROW_PAGE);
        oled_draw_down_arrow_5x8(right_arrow_x, GRAPHIC_ARROW_PAGE);
    }
    else
    {
        oled_clear_5x8(left_arrow_x, GRAPHIC_ARROW_PAGE);
        oled_clear_5x8(right_arrow_x, GRAPHIC_ARROW_PAGE);
    }
}

void oled_update_screen(void)
{
    static bool prev_graphic_mode = false;
    bool graphic_mode = g_brush_release_on;

    if (graphic_mode)
    {
        if (!prev_graphic_mode)
        {
            oled_draw_graphic_base();
        }

        oled_draw_graphic_screen();
    }
    else
    {
        if (prev_graphic_mode)
        {
            oled_clear();
        }

        oled_draw_old_runtime_screen();
    }

    prev_graphic_mode = graphic_mode;
}

// ========================= BOOT SPLASH ================================
void oled_show_boot_screen(void)
{
    const uint8_t num_chars      = 4u;
    const uint8_t char_advance   = 24u;
    const uint8_t text_width     = (uint8_t)(char_advance * num_chars - 4u);
    const uint8_t start_col      = (uint8_t)((OLED_WIDTH - text_width) / 2u);
    const uint8_t top_page       = 2u;
    const uint8_t underline_page = 6u;
    uint8_t i;

    oled_send_command(0xAE);
    oled_clear();

    oled_draw_text_huge(start_col, top_page, "DIMA");

    oled_set_cursor(start_col, underline_page);
    oled_send_data_start();
    for (i = 0; i < text_width; i++)
    {
        oled_send_data_byte(0x00);
    }
    oled_send_data_stop();

    oled_send_command(0xAF);

    g_boot_ms = 0u;

    oled_draw_underline_animated(start_col, underline_page, text_width,
                                 0x01u,
                                 24u,
                                 5u);

    while (g_boot_ms < 1000u)
    {
    }

    oled_send_command(0xAE);
    oled_clear();
}

// ========================= EEPROM HELPERS =============================
void eeprom_write_byte(uint16_t addr, uint8_t value)
{
    while (EECON1bits.WR)
    {
    }

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

    while (EECON1bits.WR)
    {
    }

    EECON1bits.WREN = 0;
}

uint8_t eeprom_read_byte(uint16_t addr)
{
    while (EECON1bits.WR)
    {
    }

    EEADR = (uint8_t)(addr & 0xFFu);

    EECON1bits.EEPGD = 0;
    EECON1bits.CFGS  = 0;
    EECON1bits.RD    = 1;

    return EEDATA;
}

void hourmeter_save_to_eeprom(void)
{
    uint32_t val = g_overall_seconds;

    eeprom_write_byte(EEADDR_OVERALL + 0u, (uint8_t)(val & 0xFFu));
    eeprom_write_byte(EEADDR_OVERALL + 1u, (uint8_t)((val >> 8) & 0xFFu));
    eeprom_write_byte(EEADDR_OVERALL + 2u, (uint8_t)((val >> 16) & 0xFFu));
    eeprom_write_byte(EEADDR_OVERALL + 3u, (uint8_t)((val >> 24) & 0xFFu));

    eeprom_write_byte(EEADDR_MAGIC, EE_MAGIC_VALUE);
}

void hourmeter_load_from_eeprom(void)
{
    uint8_t magic = eeprom_read_byte(EEADDR_MAGIC);

    if (magic != EE_MAGIC_VALUE)
    {
        g_overall_seconds = 0u;
        g_brush_seconds   = 0u;
        g_vacuum_seconds  = 0u;
        return;
    }

    {
        uint32_t val;
        val  = (uint32_t)eeprom_read_byte(EEADDR_OVERALL + 0u);
        val |= (uint32_t)eeprom_read_byte(EEADDR_OVERALL + 1u) << 8;
        val |= (uint32_t)eeprom_read_byte(EEADDR_OVERALL + 2u) << 16;
        val |= (uint32_t)eeprom_read_byte(EEADDR_OVERALL + 3u) << 24;
        g_overall_seconds = val;
    }

    g_brush_seconds  = 0u;
    g_vacuum_seconds = 0u;
}

// ========================= MAIN ======================================
int main(void)
{
    system_init();

    hourmeter_load_from_eeprom();

    oled_show_boot_screen();

    oled_update_screen();
    oled_send_command(0xAF);

    while (1)
    {
        if (g_10ms_flag)
        {
            g_10ms_flag = 0u;
            update_logic_10ms();
        }

        if (g_1s_flag)
        {
            g_1s_flag = 0u;
            update_logic_1s();
        }

        if (g_display_refresh_request)
        {
            g_display_refresh_request = false;
            oled_update_screen();
        }
    }

    return 0;
}