/*
 * Device: PIC12F683
 * Compiler: XC8 (v3.x)
 * Function: Vacuum + Brush Controller (per operations spec)
 *
 * Operations:
 * 1) HS1 & HS2 HIGH  -> Brush ON immediately
 * 2) LEVER HIGH      -> Vacuum ON immediately
 * 3) LEVER LOW       -> Vacuum OFF after 10 s (cancel if HIGH returns)
 * 4) HS1 & HS2 LOW   -> Brush OFF immediately
 * 5) If either handle LOW for 3 s while brush is ON -> Brush OFF.
 *    If both handles HIGH again -> Brush ON immediately (timer canceled).
 *
 * Pin Map (Option 1)
 *   GP5 (pin2) -> BRUSH_RELAY  (to BC547 via 1k)
 *   GP0 (pin7) -> VACUUM_RELAY (to BC547 via 1k)
 *   GP1 (pin6) <- HANDLE_SW1   (10k pulldown, active HIGH)
 *   GP4 (pin3) <- HANDLE_SW2   (10k pulldown, active HIGH)
 *   GP3 (pin4) <- LEVER_SW     (10k pulldown, active HIGH)  [input-only]
 *   GP2 (pin5) -> LED (via 2.2k series)  [blink during 10 s vacuum-off delay]
 *
 * NOTE: MCLRE=OFF so GP3 is a digital input instead of reset.
 */

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

// ============================
// Configuration Bits (XC8 v3.x)
// ============================
#pragma config FOSC  = INTOSCIO   // internal oscillator, GP4/GP5 as digital I/O
#pragma config WDTE  = ON         // watchdog enabled (we service it in main loop)
#pragma config PWRTE = ON
#pragma config MCLRE = OFF        // GP3 as digital input (no MCLR)
#pragma config CP    = OFF
#pragma config CPD   = OFF
#pragma config BOREN = ON
#pragma config IESO  = OFF
#pragma config FCMEN = OFF

#define _XTAL_FREQ 8000000UL

// ============================
// Timing constants (ms)
// ============================
#define SYS_TICK_MS              1u
#define DEBOUNCE_MS              20u
#define VAC_OFF_DELAY_MS         10000u      // 10 seconds
#define BRUSH_OFF_DELAY_MS       3000u       // 3 seconds (was 300000 ms)
#define LED_BLINK_TOGGLE_MS      250u        // 2 Hz blink (toggle every 250 ms)

// ============================
// I/O bit helpers
// ============================
// Inputs:
#define READ_HANDLE1   ((GPIO >> 1) & 0x1)   // GP1
#define READ_HANDLE2   ((GPIO >> 4) & 0x1)   // GP4
#define READ_LEVER     ((GPIO >> 3) & 0x1)   // GP3 (input-only)

// --- Output shadow to avoid RMW hazards on GPIO ---
static uint8_t out_shadow = 0;

static inline void set_brush_pin(bool on) { if (on) out_shadow |=  (1u<<5); else out_shadow &= ~(1u<<5); } // GP5
static inline void set_vac_pin  (bool on) { if (on) out_shadow |=  (1u<<0); else out_shadow &= ~(1u<<0); } // GP0
static inline void set_led_pin  (bool on) { if (on) out_shadow |=  (1u<<2); else out_shadow &= ~(1u<<2); } // GP2
static inline void push_outputs(void)     { GPIO = out_shadow; }

// ============================
// Globals updated in ISR
// ============================
volatile uint32_t g_ms = 0;

// ============================
// Debounce
// ============================
typedef struct {
    uint8_t  stable;
    uint8_t  last;
    uint16_t cnt;
} debounce_t;

static debounce_t db_handle1 = {0,0,0};
static debounce_t db_handle2 = {0,0,0};
static debounce_t db_lever   = {0,0,0};

static inline void debounce_update_1ms(debounce_t* db, uint8_t raw)
{
    if (raw != db->last) {
        db->last = raw;
        db->cnt  = 0;
    } else if (db->cnt < DEBOUNCE_MS) {
        db->cnt++;
        if (db->cnt >= DEBOUNCE_MS) {
            db->stable = db->last;
        }
    }
}

// ============================
// Timer1: 1 ms tick @ 8 MHz
// Fosc/4 = 2 MHz => 0.5us per tick
// Prescale 1:8 => 4us per tick; 1ms => 250 counts
// Preload = 65536 - 250 = 65286
// ============================
#define TMR1_PRELOAD     (uint16_t)(65536u - 250u)
#define TMR1H_PRELOAD    (uint8_t)(TMR1_PRELOAD >> 8)
#define TMR1L_PRELOAD    (uint8_t)(TMR1_PRELOAD & 0xFF)

static void tmr1_init(void)
{
    T1CON = 0;
    T1CONbits.TMR1CS = 0;      // Fosc/4
    T1CONbits.T1CKPS = 0b11;   // 1:8
    TMR1H = TMR1H_PRELOAD;
    TMR1L = TMR1L_PRELOAD;
    PIR1bits.TMR1IF = 0;
    PIE1bits.TMR1IE = 1;
    INTCONbits.PEIE = 1;
    INTCONbits.GIE  = 1;
    T1CONbits.TMR1ON = 1;
}

void __interrupt() isr(void)
{
    if (PIR1bits.TMR1IF) {
        TMR1H = TMR1H_PRELOAD;
        TMR1L = TMR1L_PRELOAD;
        PIR1bits.TMR1IF = 0;
        g_ms++;
    }
}

// Atomic read of g_ms (avoid torn 32-bit read on 8-bit MCU)
static inline uint32_t millis(void)
{
    uint32_t t;
    uint8_t gie = INTCONbits.GIE;
    INTCONbits.GIE = 0;
    t = g_ms;
    INTCONbits.GIE = gie;
    return t;
}

static inline bool time_reached(uint32_t now, uint32_t deadline)
{
    return (int32_t)(now - deadline) >= 0;
}

// ============================
// Clock / I/O init
// ============================
static void io_clock_init(void)
{
    OSCCONbits.IRCF = 0b111; // 8MHz
    ANSEL  = 0x00;           // all digital
    CMCON0 = 0x07;           // comparators off

    TRISIO = 0;
    TRISIObits.TRISIO5 = 0; // GP5 out (BRUSH)
    TRISIObits.TRISIO0 = 0; // GP0 out (VAC)
    TRISIObits.TRISIO2 = 0; // GP2 out (LED)
    TRISIObits.TRISIO1 = 1; // GP1 in  (H1)
    TRISIObits.TRISIO4 = 1; // GP4 in  (H2)
    // GP3 is input-only by hardware; used as LEVER input.

    OPTION_REGbits.nGPPU = 1; // disable weak PU (external pulldowns)
    WPU = 0x00;

    // Outputs default OFF
    out_shadow &= ~((1u<<5) | (1u<<0) | (1u<<2));
    push_outputs();
}

// ============================
// App state
// ============================
typedef struct {
    bool brush_on;
    bool vacuum_on;

    // Vacuum delayed OFF
    bool     vac_off_pending;
    uint32_t vac_off_deadline_ms;

    // Brush delayed OFF
    bool     brush_off_pending;
    uint32_t brush_off_deadline_ms;

    // LED blink
    bool     led_state;           // current LED level
    uint32_t led_next_toggle_ms;  // next toggle deadline
} app_state_t;

static app_state_t app = { false, false, false, 0, false, 0, false, 0 };

// ============================
// Main
// ============================
void main(void)
{
    io_clock_init();
    tmr1_init();

    app.brush_on  = false;
    app.vacuum_on = false;
    app.vac_off_pending   = false;
    app.brush_off_pending = false;
    app.led_state         = false;
    app.led_next_toggle_ms= 0;

    uint32_t last_tick = millis();

    for (;;) {
        uint32_t now = millis();

        // Debounce exactly once per ms tick
        if (now != last_tick) {
            last_tick = now;

            uint8_t raw_h1  = READ_HANDLE1 ? 1u : 0u;
            uint8_t raw_h2  = READ_HANDLE2 ? 1u : 0u;
            uint8_t raw_lev = READ_LEVER   ? 1u : 0u; // from GP3

            debounce_update_1ms(&db_handle1, raw_h1);
            debounce_update_1ms(&db_handle2, raw_h2);
            debounce_update_1ms(&db_lever,   raw_lev);
        }

        uint8_t h1  = db_handle1.stable;
        uint8_t h2  = db_handle2.stable;
        uint8_t lev = db_lever.stable;

        bool both_high  = (h1 && h2);
        bool both_low   = (!h1 && !h2);
        bool either_low = (!h1 || !h2);

        // ===== Brush state machine =====
        if (both_high) {
            app.brush_on = true;
            app.brush_off_pending = false;
        }
        else if (both_low) {
            app.brush_on = false;
            app.brush_off_pending = false;
        }
        else {
            if (app.brush_on) {
                // either LOW while ON -> 3s OFF timer
                if (!app.brush_off_pending) {
                    app.brush_off_pending = true;
                    app.brush_off_deadline_ms = now + BRUSH_OFF_DELAY_MS;
                }
                if (app.brush_off_pending && time_reached(now, app.brush_off_deadline_ms) && !both_high) {
                    app.brush_on = false;
                    app.brush_off_pending = false;
                }
            } else {
                app.brush_off_pending = false;
            }
        }

        // ===== Vacuum state machine =====
        if (lev) {
            app.vacuum_on = true;
            app.vac_off_pending = false;
        } else {
            if (app.vacuum_on && !app.vac_off_pending) {
                app.vac_off_pending = true;
                app.vac_off_deadline_ms = now + VAC_OFF_DELAY_MS;
                // Start LED blinking window immediately
                app.led_state = true;
                app.led_next_toggle_ms = now + LED_BLINK_TOGGLE_MS;
            }
            if (app.vac_off_pending && !lev && time_reached(now, app.vac_off_deadline_ms)) {
                app.vacuum_on = false;
                app.vac_off_pending = false;
                // Stop LED once vacuum turns OFF
                app.led_state = false;
            }
        }

        // ===== LED behavior =====
        // LED blinks only while vacuum OFF delay is pending (lever LOW after ON)
        if (app.vac_off_pending) {
            if (time_reached(now, app.led_next_toggle_ms)) {
                app.led_state = !app.led_state;
                app.led_next_toggle_ms = now + LED_BLINK_TOGGLE_MS;
            }
        } else {
            app.led_state = false;
        }

        // Drive outputs via shadow (single port write)
        set_brush_pin(app.brush_on);
        set_vac_pin(app.vacuum_on);
        set_led_pin(app.led_state);
        push_outputs();

        // Watchdog
        CLRWDT();
    }
}

