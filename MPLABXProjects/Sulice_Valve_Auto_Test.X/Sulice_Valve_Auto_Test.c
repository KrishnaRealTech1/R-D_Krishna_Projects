/*
 * Project: IoT Based Valve Control (PIC18F25K22 + HC-05 + SSD1306 OLED)
 * Toolchain: MPLAB X + XC8 (v2.x / v3.x)
 *
 * Pin Map:
 *   RA0 ? PowerDetect (Auto/Manual select, HIGH=Auto)
 *   RA1 ? Trigger (Auto mode command)
 *   RA2 ? ManualTrigger (manual start)
 *   RA3 ? OpenFeedback  (unused for manual toggle; kept as input)
 *   RA4 ? CloseFeedback (unused for manual toggle; kept as input)
 *
 *   RB0 ? LimitSwitch (pulses, INT0)
 *   RB1 ? ON/OFF Relay control   (Open drive)
 *   RB2 ? CloseRelay control     (Close drive)
 *   RB3 ? OpenStatus output
 *   RB4 ? CloseStatus output
 *   RB5 ? Heartbeat LED
 *
 *   RC3 ? I²C SCL (SSD1306 OLED)
 *   RC4 ? I²C SDA (SSD1306 OLED)
 *   RC6 ? TX (HC-05)
 *   RC7 ? RX (HC-05)
 *
 * Boot hardening (software-only, no wiring changes):
 *  - Startup WDT safety window (auto-recover on cold-power stalls), disabled after init
 *  - HFINTOSC stable wait (HFIOFS) + brief POR delay
 *  - Early heartbeat pulse before enabling interrupts
 *  - I²C bus recovery (9 SCL pulses + STOP) and MSSP reset at boot
 *  - I²C waits feed WDT
 *  - SSD1306 longer settle + retry + double clear
 *
 * Power-loss recovery policy:
 *  - Persist absolute position (`lastcnt`) frequently while running (EEPROM wear-leveled).
 *  - On next boot (unless first-time install), always converge to CLOSED:
 *      if persisted position > 0, start CLOSE and run exactly to 0.
 *    This produces the requested behavior:
 *      ? If power cut during OPEN @10/15 ? close 10 counts to 0 on boot.
 *      ? If power cut during CLOSE (10 already consumed) ? position?5 ? close 5 counts to 0.
 */

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>

/*** CONFIGURATION BITS ? your original set ***/
// CONFIG1H
#pragma config FOSC = INTIO67
#pragma config PLLCFG = OFF
#pragma config PRICLKEN = OFF
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
#pragma config P2BMX = PORTC0
#pragma config MCLRE = EXTMCLR
// CONFIG4L
#pragma config STVREN = ON
#pragma config LVP = OFF
#pragma config XINST = OFF

#define _XTAL_FREQ 16000000UL

/************ BUILD-TIME OPTIONS ************/
#define OLED_I2C_ADDR             0x3C   // SSD1306 128x64
#define ACTIVE_HIGH_RELAYS        1
#define UART_BAUD                 9600UL
#define PULSE_DEFAULT             15
#define PULSE_MIN                 1
#define PULSE_MAX                 250
#define PULSE_DEBOUNCE_MS         25
#define HEARTBEAT_MS              500

#define CMD_IDLE_FLUSH_MS         600
#define PD_SELFTEST_MS            2000U

// Manual press FSM tuning
#define MANUAL_DEBOUNCE_MS           25
#define MANUAL_PRESS_MS              40
#define MANUAL_REARM_LOW_MS          150
#define MANUAL_LOCKOUT_AFTER_START   300

// Manual/offline lever settle time
#define OFFLINE_SETTLE_MS            600

// Startup settle
#define POWER_ON_SETTLE_MS           30

// NEW: EEPROM checkpoint throttle ? write every N pulses while running.
// Use 1 for "exact resume" (more wear), 2?5 to reduce wear.
#define EE_CHECKPOINT_EVERY_N_PULSES   1

/************ WDT helpers (startup safety window) ************/
#define CLRWDT()   asm("clrwdt")
static inline void wdt_enable(void){ WDTCONbits.SWDTEN = 1; }
static inline void wdt_disable(void){ WDTCONbits.SWDTEN = 0; }

/************ PIN MACROS ************/
// Inputs
#define IN_POWER_DETECT()   (PORTAbits.RA0)  // 1=Auto, 0=Manual
#define IN_TRIGGER_AUTO()   (PORTAbits.RA1)
#define IN_TRIGGER_MAN()    (PORTAbits.RA2)
#define IN_OPEN_FB()        (PORTAbits.RA3)
#define IN_CLOSE_FB()       (PORTAbits.RA4)
#define IN_LIMIT_SW()       (PORTBbits.RB0)

// Outputs (respect ACTIVE_HIGH_RELAYS)
#if ACTIVE_HIGH_RELAYS
  #define SET_ONOFF(v)     (LATBbits.LATB1 = !!(v))
  #define SET_CLOSE(v)     (LATBbits.LATB2 = !!(v))
  #define SET_OPEN_STS(v)  (LATBbits.LATB3 = !!(v))
  #define SET_CLOSE_STS(v) (LATBbits.LATB4 = !!(v))
#else
  #define SET_ONOFF(v)     (LATBbits.LATB1 = !(v))
  #define SET_CLOSE(v)     (LATBbits.LATB2 = !(v))
  #define SET_OPEN_STS(v)  (LATBbits.LATB3 = !(v))
  #define SET_CLOSE_STS(v) (LATBbits.LATB4 = !(v))
#endif

#define TOGGLE_HEARTBEAT() (LATBbits.LATB5 ^= 1)
#define SET_HEARTBEAT(v)   (LATBbits.LATB5 = !!(v))

/************ GLOBAL TIMEBASE ************/
volatile uint32_t g_ms = 0;         // 1ms system tick
volatile uint32_t g_last_pulse_ms = 0;

// Atomic snapshot of g_ms (preserve GIE)
static inline uint32_t ms_now(void){
    uint8_t gie = INTCONbits.GIE;    // save
    INTCONbits.GIE = 0;
    uint32_t t = g_ms;
    INTCONbits.GIE = gie;            // restore
    return t;
}

/************ LIMIT SWITCH COUNTING / POSITION ************/
volatile uint16_t g_pulse_count = 0;
volatile bool     g_counting_enabled = false;

// Absolute position 0..g_setpoint (0=closed, setpoint=fully open)
volatile uint8_t  g_position = 0;

// Offline (manual lever) pulse accumulator ? used when not driving
volatile uint8_t  g_offline_count = 0;
volatile bool     g_offline_active = false;

// NEW: in-run checkpoint coordination (ISR -> main)
static volatile uint8_t g_pulses_since_checkpoint = 0;
static volatile bool    g_checkpoint_pending = false;
static volatile uint8_t g_checkpoint_pos = 0;
static volatile uint8_t g_checkpoint_status = 0;

/************ EEPROM CONFIG (wear-leveled slots) ************/
#define EE_SLOTS_START   0x00
#define EE_SLOT_SIZE     7
#define EE_NUM_SLOTS     36

static uint8_t ee_read(uint8_t addr) {
    while(EECON1bits.WR);
    EEADR = addr;
    EECON1bits.EEPGD = 0;
    EECON1bits.CFGS = 0;
    EECON1bits.RD = 1;
    NOP(); NOP();
    return EEDATA;
}
static void ee_write(uint8_t addr, uint8_t val) {
    while(EECON1bits.WR);
    EECON1bits.WREN = 1;
    EEADR = addr;
    EEDATA = val;
    EECON1bits.EEPGD = 0;
    EECON1bits.CFGS = 0;
    INTCONbits.GIE = 0;          // required unlock (only in main loop)
    EECON2 = 0x55;
    EECON2 = 0xAA;
    EECON1bits.WR = 1;
    INTCONbits.GIE = 1;
    EECON1bits.WREN = 0;
}
static uint8_t crc8_simple(uint8_t a, uint8_t b, uint8_t c, uint8_t d) {
    uint8_t s = 0xA5;
    s += a; s ^= b; s += c; s ^= d;
    return s ^ 0x5A;
}
typedef struct {
    uint8_t tag;     // 0x5A
    uint8_t count;   // 1..250
    uint8_t inv;     // ~count
    uint8_t seq;     // rollover ok
    uint8_t status;  // 0=unk/partial,1=open,2=close
    uint8_t lastcnt; // absolute position 0..count
    uint8_t crc;     // crc8_simple(tag,count,inv, seq^status, lastcnt)
} ee_slot_t;

static uint8_t g_setpoint = PULSE_DEFAULT;
static uint8_t g_slot_index = 0;
static uint8_t g_seq_last = 0;
static uint8_t g_last_status_persist = 0;
static uint8_t g_last_reachcnt_persist = 0;
static bool    g_install_pending = false;

// --- Remember run start for progress display in CLOSE (motor-driven) ---
static uint8_t  g_run_start_pos = 0;

static inline uint8_t status_code_from_outputs(void){
    if (LATBbits.LATB3 && !LATBbits.LATB4) return 1;  // open
    if (LATBbits.LATB4 && !LATBbits.LATB3) return 2;  // close
    return 0; // unknown/partial
}
static inline bool seq_newer(uint8_t a, uint8_t b){
    return (uint8_t)(a - b) < 128U;
}
static bool ee_is_all_ff(void){
    for(uint16_t i=0;i<EE_NUM_SLOTS*EE_SLOT_SIZE;i++){
        if(ee_read((uint8_t)(EE_SLOTS_START + i)) != 0xFF) return false;
    }
    return true;
}

static bool ee_slot_valid_at(uint8_t slot, ee_slot_t *out) {
    uint8_t base = EE_SLOTS_START + slot*EE_SLOT_SIZE;
    ee_slot_t s;
    s.tag    = ee_read(base+0);
    s.count  = ee_read(base+1);
    s.inv    = ee_read(base+2);
    s.seq    = ee_read(base+3);
    s.status = ee_read(base+4);
    s.lastcnt= ee_read(base+5);
    s.crc    = ee_read(base+6);
    uint8_t c = crc8_simple(s.tag, s.count, s.inv, (uint8_t)(s.seq ^ s.status));
    c ^= s.lastcnt;
    bool ok = (s.tag==0x5A) && (s.inv == (uint8_t)~s.count) && (s.crc == c) &&
              (s.count>=PULSE_MIN && s.count<=PULSE_MAX) &&
              (s.status<=2U) && (s.lastcnt<=s.count);
    if (ok && out) *out = s;
    return ok;
}

static void ee_store_full(uint8_t count, uint8_t status, uint8_t lastcnt){
    if (count < PULSE_MIN) count = PULSE_MIN;
    if (count > PULSE_MAX) count = PULSE_MAX;
    if (lastcnt > count)   lastcnt = count;
    if (status > 2U) status = 0U;

    uint8_t next = (uint8_t)((g_slot_index + 1U) % EE_NUM_SLOTS);
    uint8_t seq  = (uint8_t)(g_seq_last + 1U);

    ee_slot_t s;
    s.tag=0x5A; s.count=count; s.inv=(uint8_t)~count; s.seq=seq; s.status=status; s.lastcnt=lastcnt;
    uint8_t c = crc8_simple(s.tag, s.count, s.inv, (uint8_t)(s.seq ^ s.status));
    s.crc = (uint8_t)(c ^ s.lastcnt);

    uint8_t base = (uint8_t)(EE_SLOTS_START + next*EE_SLOT_SIZE);
    ee_write(base+0,s.tag);
    ee_write(base+1,s.count);
    ee_write(base+2,s.inv);
    ee_write(base+3,s.seq);
    ee_write(base+4,s.status);
    ee_write(base+5,s.lastcnt);
    ee_write(base+6,s.crc);

    g_setpoint = count;
    g_slot_index = next;
    g_seq_last = seq;
    g_last_status_persist   = status;
    g_last_reachcnt_persist = lastcnt;
}
static void ee_store_setpoint(uint8_t count){
    if (count < PULSE_MIN) count = PULSE_MIN;
    if (count > PULSE_MAX) count = PULSE_MAX;
    if (g_position > count) g_position = count; // keep position in-range
    ee_store_full(count, status_code_from_outputs(), g_position);
}

static void ee_load_all(void) {
    bool found = false;
    uint8_t best_slot = 0;
    uint8_t best_seq  = 0;
    for (uint8_t i=0;i<EE_NUM_SLOTS;i++) {
        ee_slot_t s;
        if (ee_slot_valid_at(i,&s)) {
            if (!found || seq_newer(s.seq, best_seq)) {
                found = true;
                best_slot = i;
                best_seq  = s.seq;
            }
        }
    }

    if (found) {
        ee_slot_t s;
        ee_slot_valid_at(best_slot,&s);
        g_setpoint = s.count;
        g_slot_index = best_slot;
        g_seq_last = s.seq;
        g_last_status_persist   = s.status;
        g_last_reachcnt_persist = s.lastcnt;
        g_position = (s.lastcnt <= g_setpoint) ? s.lastcnt : g_setpoint;
        g_install_pending = false;
    } else {
        if (ee_is_all_ff()) {
            g_setpoint = PULSE_DEFAULT;
            g_slot_index = 0;
            g_seq_last = 0;
            g_last_status_persist = 0;
            g_last_reachcnt_persist = 0;
            g_position = 0;
            ee_store_full(g_setpoint, 0, 0);
            g_install_pending = true;
        } else {
            g_setpoint = PULSE_DEFAULT;
            g_slot_index = 0;
            g_seq_last   = 0;
            g_last_status_persist   = 2U; // assume closed
            g_last_reachcnt_persist = 0U;
            g_position = 0U;
            g_install_pending = false;
        }
    }
}

/************ UART (HC-05) ************/
static void uart_init(void) {
    TXSTA1bits.BRGH = 1;
    BAUDCON1bits.BRG16 = 1;
    uint16_t spbrg = (_XTAL_FREQ/(4UL*UART_BAUD)) - 1; // ~415 for 9600 at 16MHz
    SPBRG1  = (uint8_t)(spbrg & 0xFF);
    SPBRGH1 = (uint8_t)(spbrg >> 8);

    RCSTA1bits.SPEN = 1;
    TXSTA1bits.TXEN = 1;
    RCSTA1bits.CREN = 1;

    PIR1bits.RC1IF = 0;
    PIE1bits.RC1IE = 1;
}
static void uart_putc(char c){ while(!PIR1bits.TX1IF) { } TXREG1 = c; }
static void uart_puts(const char *s){ while(*s) uart_putc(*s++); }
static void uart_putu16(uint16_t v){
    if(v==0){ uart_putc('0'); return; }
    char tmp[6]; uint8_t i=0;
    while(v>0 && i<5){ tmp[i++] = (char)('0' + (v%10)); v/=10; }
    while(i--) uart_putc(tmp[i]);
}
static void uart_putu8(uint8_t v){ uart_putu16(v); }

// RX line buffer + idle finalize (parse in main loop only)
#define RXBUF_SZ 64
volatile char rxbuf[RXBUF_SZ];
volatile uint8_t rxlen = 0;
volatile uint32_t rx_last_ms = 0;
volatile bool rx_line_ready = false;

static void process_cmd_line(char *line);
static void cmd_to_upper(char *s){ for(;*s;s++){ if(*s>='a'&&*s<='z') *s=(char)(*s-32); } }
static char* skip_ws(char* p){ while(*p==' '||*p=='\t') ++p; return p; }
static uint16_t parse_uint(const char* p, bool* ok){
    uint32_t v=0; uint8_t nd=0;
    while(*p>='0' && *p<='9'){ v = v*10u + (uint32_t)(*p-'0'); nd++; if(v>65535u) break; p++; }
    *ok = (nd>0);
    return (uint16_t)((v>65535u)?65535u:v);
}

/************ I2C (MSSP1) ? robust ************/
// Bus recovery (9 SCL pulses + STOP) before enabling MSSP
static void i2c_gpio_release(void){
    TRISCbits.TRISC3 = 1; // SCL input (released)
    TRISCbits.TRISC4 = 1; // SDA input (released)
}
static void i2c_bus_recover(void){
    // If OLED wedged the bus on power-up, these pulses free it
    i2c_gpio_release();
    for(uint8_t i=0;i<9;i++){
        // drive SCL low
        TRISCbits.TRISC3 = 0; LATCbits.LATC3 = 0; __delay_us(5);
        // release SCL high
        TRISCbits.TRISC3 = 1; __delay_us(5);
        CLRWDT();
    }
    // STOP condition (SDA high while SCL high) by releasing both
    TRISCbits.TRISC3 = 1;
    TRISCbits.TRISC4 = 1;
    __delay_us(5);
}

static void i2c_init(void){
    // Recover bus before enabling MSSP
    i2c_bus_recover();

    // Full MSSP reset
    SSP1CON1 = 0x00; SSP1CON2 = 0x00; SSP1STAT = 0x00; PIR1bits.SSP1IF = 0;

    SSP1CON1 = 0x28; // I2C Master, SSPEN=1
    SSP1CON2 = 0x00;
    SSP1ADD  = (uint8_t)((_XTAL_FREQ/(4UL*100000UL))-1); // 100kHz at 16MHz
    SSP1STAT = 0x80; // SMP=1, CKE=0

    TRISCbits.TRISC3 = 1; // SCL
    TRISCbits.TRISC4 = 1; // SDA
}
static void i2c_wait_idle(void){
    while ( (SSP1CON2 & 0x1F) || SSP1STATbits.R_W ) { CLRWDT(); }
}
static bool i2c_start(void){
    i2c_wait_idle();
    SSP1CON2bits.SEN = 1;
    while (SSP1CON2bits.SEN){ CLRWDT(); }
    __delay_us(2);
    return true;
}
static void i2c_stop(void){
    i2c_wait_idle();
    SSP1CON2bits.PEN = 1;
    while (SSP1CON2bits.PEN){ CLRWDT(); }
    __delay_us(2);
}
static bool i2c_write_byte(uint8_t b){
    i2c_wait_idle();
    PIR1bits.SSP1IF = 0;
    SSP1BUF = b;
    if (SSP1CON1bits.WCOL){
        SSP1CON1bits.WCOL = 0;
        return false;
    }
    while(!PIR1bits.SSP1IF) { CLRWDT(); }
    PIR1bits.SSP1IF = 0;
    return (SSP1CON2bits.ACKSTAT == 0);
}
static bool i2c_write_bytes(const uint8_t *data, uint8_t n){
    for (uint8_t i=0;i<n;i++){
        if (!i2c_write_byte(data[i])) return false;
    }
    return true;
}
static bool i2c_cmd(uint8_t addr7, const uint8_t *data, uint16_t n){
    const uint8_t dev = (uint8_t)((addr7<<1)|0);
    const uint8_t CHUNK = 18;

    for (uint8_t attempt=0; attempt<3; ++attempt){
        uint16_t sent = 0;
        if (!i2c_start()) continue;
        if (!i2c_write_byte(dev)){ i2c_stop(); __delay_ms(1); continue; }

        while (sent < n){
            uint8_t blk = (uint8_t)((n - sent) > CHUNK ? CHUNK : (n - sent));
            if (!i2c_write_bytes(&data[sent], blk)){ i2c_stop(); goto retry; }
            sent += blk;
            CLRWDT();
        }
        i2c_stop();
        return true;
retry:
        __delay_ms(1);
        CLRWDT();
    }
    return false;
}

/************ SSD1306 (minimal + stubborn on cold boot) ************/
static void oled_cmd1(uint8_t c){ uint8_t b[2]={0x00,c}; (void)i2c_cmd(OLED_I2C_ADDR,b,2); }
static void oled_cmd2(uint8_t c1,uint8_t c2){ uint8_t b[3]={0x00,c1,c2}; (void)i2c_cmd(OLED_I2C_ADDR,b,3); }

static void oled_clear(void){
    uint8_t ctrl_and_zeros[1 + 16];
    ctrl_and_zeros[0] = 0x40;
    for (int i=1;i<sizeof(ctrl_and_zeros);++i) ctrl_and_zeros[i]=0x00;

    for(uint8_t p=0;p<8;p++){
        oled_cmd1(0xB0 | (p & 0x07));
        oled_cmd1(0x00);
        oled_cmd1(0x10);
        for (uint8_t blk=0; blk<8; ++blk){
            (void)i2c_cmd(OLED_I2C_ADDR, ctrl_and_zeros, sizeof(ctrl_and_zeros));
            CLRWDT();
        }
    }
}

static bool oled_init_once(void){
    __delay_ms(200);          // longer cold-start settle

    oled_cmd1(0xAE);          // display off
    oled_cmd2(0xD5,0x80);     // clock divide
    oled_cmd2(0xA8,0x3F);     // multiplex
    oled_cmd2(0xD3,0x00);     // display offset
    oled_cmd1(0x40);          // start line
    oled_cmd2(0x8D,0x14);     // charge pump on
    oled_cmd2(0x20,0x00);     // horizontal addressing
    oled_cmd1(0xA1);          // segment remap
    oled_cmd1(0xC8);          // COM scan dec
    oled_cmd2(0xDA,0x12);     // COM pins
    oled_cmd2(0x81,0x7F);     // contrast
    oled_cmd1(0xA4);          // display RAM
    oled_cmd1(0xA6);          // normal display
    oled_cmd2(0xD9,0xF1);     // precharge
    oled_cmd2(0xDB,0x40);     // VCOM detect
    oled_cmd1(0x2E);          // deactivate scroll
    oled_cmd1(0xAF);          // display on
    __delay_ms(20);

    // quick sanity: try to clear once
    oled_clear();
    __delay_ms(10);
    // and again (some panels only "wake" after first bulk xfer)
    oled_clear();
    return true; // we don't have positive ACK from panel beyond i2c_cmd(), but we tried hard
}

/************ Font & text (unchanged) ************/
static const uint8_t font5x7[][5] = {
  {0x00,0x00,0x00,0x00,0x00}, /* 0x20 ' ' */
  {0x00,0x00,0x5F,0x00,0x00}, /* 0x21 '!' */
  {0x00,0x07,0x00,0x07,0x00}, /* 0x22 '"' */
  {0x14,0x7F,0x14,0x7F,0x14}, /* 0x23 '#' */
  {0x24,0x2A,0x7F,0x2A,0x12}, /* 0x24 '$' */
  {0x23,0x13,0x08,0x64,0x62}, /* 0x25 '%' */
  {0x36,0x49,0x55,0x22,0x50}, /* 0x26 '&' */
  {0x00,0x05,0x03,0x00,0x00}, /* 0x27 ''' */
  {0x00,0x1C,0x22,0x41,0x00}, /* 0x28 '(' */
  {0x00,0x41,0x22,0x1C,0x00}, /* 0x29 ')' */
  {0x14,0x08,0x3E,0x08,0x14}, /* 0x2A '*' */
  {0x08,0x08,0x3E,0x08,0x08}, /* 0x2B '+' */
  {0x00,0x50,0x30,0x00,0x00}, /* 0x2C ',' */
  {0x08,0x08,0x08,0x08,0x08}, /* 0x2D '-' */
  {0x00,0x60,0x60,0x00,0x00}, /* 0x2E '.' */
  {0x20,0x10,0x08,0x04,0x02}, /* 0x2F '/' */
  {0x3E,0x51,0x49,0x45,0x3E}, /* 0x30 '0' */
  {0x00,0x42,0x7F,0x40,0x00}, /* 0x31 '1' */
  {0x42,0x61,0x51,0x49,0x46}, /* 0x32 '2' */
  {0x21,0x41,0x45,0x4B,0x31}, /* 0x33 '3' */
  {0x18,0x14,0x12,0x7F,0x10}, /* 0x34 '4' */
  {0x27,0x45,0x45,0x45,0x39}, /* 0x35 '5' */
  {0x3C,0x4A,0x49,0x49,0x30}, /* 0x36 '6' */
  {0x01,0x71,0x09,0x05,0x03}, /* 0x37 '7' */
  {0x36,0x49,0x49,0x49,0x36}, /* 0x38 '8' */
  {0x06,0x49,0x49,0x29,0x1E}, /* 0x39 '9' */
  {0x00,0x36,0x36,0x00,0x00}, /* 0x3A ':' */
  {0x00,0x56,0x36,0x00,0x00}, /* 0x3B ';' */
  {0x08,0x14,0x22,0x41,0x00}, /* 0x3C '<' */
  {0x14,0x14,0x14,0x14,0x14}, /* 0x3D '=' */
  {0x00,0x41,0x22,0x14,0x08}, /* 0x3E '>' */
  {0x02,0x01,0x51,0x09,0x06}, /* 0x3F '?' */
  {0x32,0x49,0x79,0x41,0x3E}, /* 0x40 '@' */
  {0x7E,0x11,0x11,0x11,0x7E}, /* 0x41 'A' */
  {0x7F,0x49,0x49,0x49,0x36}, /* 0x42 'B' */
  {0x3E,0x41,0x41,0x41,0x22}, /* 0x43 'C' */
  {0x7F,0x41,0x41,0x22,0x1C}, /* 0x44 'D' */
  {0x7F,0x49,0x49,0x49,0x41}, /* 0x45 'E' */
  {0x7F,0x09,0x09,0x09,0x01}, /* 0x46 'F' */
  {0x3E,0x41,0x49,0x49,0x7A}, /* 0x47 'G' */
  {0x7F,0x08,0x08,0x08,0x7F}, /* 0x48 'H' */
  {0x00,0x41,0x7F,0x41,0x00}, /* 0x49 'I' */
  {0x20,0x40,0x41,0x3F,0x01}, /* 0x4A 'J' */
  {0x7F,0x08,0x14,0x22,0x41}, /* 0x4B 'K' */
  {0x7F,0x40,0x40,0x40,0x40}, /* 0x4C 'L' */
  {0x7F,0x02,0x0C,0x02,0x7F}, /* 0x4D 'M' */
  {0x7F,0x04,0x08,0x10,0x7F}, /* 0x4E 'N' */
  {0x3E,0x41,0x41,0x41,0x3E}, /* 0x4F 'O' */
  {0x7F,0x09,0x09,0x09,0x06}, /* 0x50 'P' */
  {0x3E,0x41,0x51,0x21,0x5E}, /* 0x51 'Q' */
  {0x7F,0x09,0x19,0x29,0x46}, /* 0x52 'R' */
  {0x46,0x49,0x49,0x49,0x31}, /* 0x53 'S' */
  {0x01,0x01,0x7F,0x01,0x01}, /* 0x54 'T' */
  {0x3F,0x40,0x40,0x40,0x3F}, /* 0x55 'U' */
  {0x1F,0x20,0x40,0x20,0x1F}, /* 0x56 'V' */
  {0x7F,0x20,0x18,0x20,0x7F}, /* 0x57 'W' */
  {0x63,0x14,0x08,0x14,0x63}, /* 0x58 'X' */
  {0x07,0x08,0x70,0x08,0x07}, /* 0x59 'Y' */
  {0x61,0x51,0x49,0x45,0x43}, /* 0x5A 'Z' */
  {0x00,0x7F,0x41,0x41,0x00}, /* 0x5B '[' */
  {0x02,0x04,0x08,0x10,0x20}, /* 0x5C '\' */
  {0x00,0x41,0x41,0x7F,0x00}, /* 0x5D ']' */
  {0x04,0x02,0x01,0x02,0x04}, /* 0x5E '^' */
  {0x40,0x40,0x40,0x40,0x40}, /* 0x5F '_' */
  {0x00,0x01,0x02,0x04,0x00}, /* 0x60 '`' */
  {0x20,0x54,0x54,0x54,0x78}, /* 0x61 'a' */
  {0x7F,0x48,0x44,0x44,0x38}, /* 0x62 'b' */
  {0x38,0x44,0x44,0x44,0x20}, /* 0x63 'c' */
  {0x38,0x44,0x44,0x48,0x7F}, /* 0x64 'd' */
  {0x38,0x54,0x54,0x54,0x18}, /* 0x65 'e' */
  {0x08,0x7E,0x09,0x01,0x02}, /* 0x66 'f' */
  {0x0C,0x52,0x52,0x52,0x3E}, /* 0x67 'g' */
  {0x7F,0x08,0x04,0x04,0x78}, /* 0x68 'h' */
  {0x00,0x44,0x7D,0x40,0x00}, /* 0x69 'i' */
  {0x20,0x40,0x44,0x3D,0x00}, /* 0x6A 'j' */
  {0x7F,0x10,0x28,0x44,0x00}, /* 0x6B 'k' */
  {0x00,0x41,0x7F,0x40,0x00}, /* 0x6C 'l' */
  {0x7C,0x04,0x18,0x04,0x78}, /* 0x6D 'm' */
  {0x7C,0x08,0x04,0x04,0x78}, /* 0x6E 'n' */
  {0x38,0x44,0x44,0x44,0x38}, /* 0x6F 'o' */
  {0x7C,0x14,0x14,0x14,0x08}, /* 0x70 'p' */
  {0x08,0x14,0x14,0x14,0x7C}, /* 0x71 'q' */
  {0x7C,0x08,0x04,0x04,0x08}, /* 0x72 'r' */
  {0x48,0x54,0x54,0x54,0x20}, /* 0x73 's' */
  {0x04,0x3F,0x44,0x40,0x20}, /* 0x74 't' */
  {0x3C,0x40,0x40,0x20,0x7C}, /* 0x75 'u' */
  {0x1C,0x20,0x40,0x20,0x1C}, /* 0x76 'v' */
  {0x3C,0x40,0x30,0x40,0x3C}, /* 0x77 'w' */
  {0x44,0x28,0x10,0x28,0x44}, /* 0x78 'x' */
  {0x0C,0x50,0x50,0x50,0x3C}, /* 0x79 'y' */
  {0x44,0x64,0x54,0x4C,0x44}, /* 0x7A 'z' */
  {0x00,0x08,0x36,0x41,0x00}, /* 0x7B '{' */
  {0x00,0x00,0x7F,0x00,0x00}, /* 0x7C '|' */
  {0x00,0x41,0x36,0x08,0x00}, /* 0x7D '}' */
  {0x02,0x01,0x02,0x04,0x02}  /* 0x7E '~' */
};

static void oled_write_char(uint8_t page, uint8_t col, char c){
    if(c < 32 || c > 126) c = '?';
    const uint8_t *glyph = font5x7[c-32];
    uint8_t buf[1+6]; buf[0]=0x40;
    for(int i=0;i<5;i++) buf[1+i]=glyph[i];
    buf[6] = 0x00; // 1px space
    // set cursor
    uint8_t cmds[3] = {0x00, (uint8_t)(0xB0 | (page & 0x07)), 0x00};
    (void)i2c_cmd(OLED_I2C_ADDR, cmds, 3);
    uint8_t hi[2] = {0x00, (uint8_t)(0x10 | ((col>>4)&0x0F))};
    uint8_t lo[2] = {0x00, (uint8_t)(0x00 | (col & 0x0F))};
    (void)i2c_cmd(OLED_I2C_ADDR, lo, 2);
    (void)i2c_cmd(OLED_I2C_ADDR, hi, 2);
    // write data
    (void)i2c_cmd(OLED_I2C_ADDR, buf, 7);
}
static void oled_print(uint8_t page, uint8_t col, const char* s){
    while(*s){
        oled_write_char(page, col, *s++);
        col += 6;
        if(col > 122) break;
        CLRWDT();
    }
}
static void oled_print_u16(uint8_t page, uint8_t col, uint16_t v){
    if(v==0){ oled_write_char(page,col,'0'); return; }
    char buf[6]; uint8_t i=0;
    while(v>0 && i<5){ buf[i++]='0'+(v%10); v/=10; }
    while(i--) { oled_write_char(page,col,buf[i]); col+=6; }
}
static uint8_t u16_digits(uint16_t v){
    if(v >= 1000U) return 4U;
    if(v >= 100U)  return 3U;
    if(v >= 10U)   return 2U;
    return 1U;
}
static uint8_t str_len21(const char* s){
    uint8_t n = 0U; 
    while(*s && n < 21U){ ++n; ++s; }
    return n;
}
static void oled_print_center(uint8_t page, const char* s){
    uint8_t w = str_len21(s);
    uint8_t col = (uint8_t)((128U - (uint16_t)w*6U)/2U);
    oled_print(page, col, s);
}

/************ TIMER1: 1ms TICK ************/
#define TMR1_PRELOAD   (uint16_t)(65536u - 500u)   // 500 * 2us = 1ms (@16MHz, 1:8)
static void tmr1_init(void){
    T1CONbits.TMR1CS = 0;     // Fosc/4
    T1CONbits.T1CKPS = 0b11;  // 1:8 -> tick 2 us
    TMR1H = (uint8_t)(TMR1_PRELOAD >> 8);
    TMR1L = (uint8_t)(TMR1_PRELOAD & 0xFF);
    PIR1bits.TMR1IF = 0;
    PIE1bits.TMR1IE = 1;
    T1CONbits.TMR1ON = 1;
}

/************ INT0: LIMIT SWITCH PULSES ************/
static void int0_init(void){
    INTCON2bits.INTEDG0 = 1; // rising edge
    INTCONbits.INT0IF = 0;
    INTCONbits.INT0IE = 1;
}

/************ DIGITAL IO / OSC INIT ************/
static void osc_init(void){
    // Internal 16 MHz; keep internal source selected
    OSCCON = 0b01110010; // IRCF=111=16MHz, SCS=10 internal
    // Wait for HFINTOSC ready (HFIOFS) with timeout
    uint16_t tmo = 30000;
    while(!OSCCONbits.HFIOFS && tmo--) { NOP(); }
}
static void io_init(void){
    // All digital
    ANSELA = 0x00;
    ANSELB = 0x00;
    ANSELC = 0x00;

    // Ensure latches are low before outputs
    LATA = 0x00;
    LATB = 0x00;
    LATC = 0x00;

    // Directions
    TRISAbits.TRISA0 = 1; // PowerDetect
    TRISAbits.TRISA1 = 1; // Trigger(Auto)
    TRISAbits.TRISA2 = 1; // ManualTrigger
    TRISAbits.TRISA3 = 1; // OpenFB
    TRISAbits.TRISA4 = 1; // CloseFB

    TRISBbits.TRISB0 = 1; // LimitSwitch
    TRISBbits.TRISB1 = 0; // ON/OFF relay
    TRISBbits.TRISB2 = 0; // Close relay
    TRISBbits.TRISB3 = 0; // Open status
    TRISBbits.TRISB4 = 0; // Close status
    TRISBbits.TRISB5 = 0; // Heartbeat

    // UART pins
    TRISCbits.TRISC6 = 0; // TX
    TRISCbits.TRISC7 = 1; // RX

    // I2C pins (MSSP controls direction)
    TRISCbits.TRISC3 = 1; // SCL
    TRISCbits.TRISC4 = 1; // SDA

    // weak pull-up on RB0 to clean up INT0 pulses
    INTCON2bits.RBPU = 0;     // global pull-ups enabled
    WPUBbits.WPUB0 = 1;       // RB0 pull-up

    // Initial outputs: all relays OFF at boot
    SET_ONOFF(0);
    SET_CLOSE(0);
    SET_OPEN_STS(0);
    SET_CLOSE_STS(0);
    SET_HEARTBEAT(0);
}

/************ SYSTEM STATE ************/
typedef enum { MODE_MANUAL=0, MODE_AUTO=1 } mode_t;
typedef enum { DIR_IDLE=0, DIR_OPEN=1, DIR_CLOSE=2 } dir_t;

static mode_t   g_mode = MODE_AUTO;
static bool     g_running = false;
static dir_t    g_run_dir = DIR_IDLE;
static uint8_t  g_display_last_mode = 0xFF;
static uint8_t  g_display_last_opensts = 0xFF;
static uint8_t  g_display_last_closests = 0xFF;
static uint16_t g_display_last_count = 0xFFFF;
static uint8_t  g_display_last_set = 0xFF;
static uint32_t g_hb_last_ms = 0;

static uint8_t  last_pd = 1;

// Debounced RA0 (1=AUTO, 0=MANUAL)
static uint8_t  pd_filtered = 1;
static uint8_t  pd_streak   = 0;
static uint8_t  pd_sample_last = 1;
static uint32_t pd_last_sample_ms = 0;

// Manual trigger debounce + FSM state
static uint8_t  man_filtered = 0;
static uint8_t  man_streak   = 0;
static uint8_t  man_sample_last = 0;
static uint32_t man_last_sample_ms = 0;

typedef enum { MS_WAIT_LOW=0, MS_WAIT_HIGH=1, MS_HELD=2 } man_state_t;
static man_state_t man_state = MS_WAIT_HIGH;
static uint8_t  man_prev = 0;
static uint32_t man_level_since_ms = 0;
static uint32_t man_cycle_lock_until = 0;

// Track last displayed running + direction
static uint8_t  g_display_last_running = 0xFF;
static uint8_t  g_display_last_dir     = 0xFF;

// Track offline display state so STS line updates on mechanical motion
static uint8_t  g_display_last_offline = 0xFF; // 0/1
static uint8_t  g_display_last_offdir  = 0xFF; // 0=unk,1=open,2=close

/************ HELPERS ************/
static inline void stop_all_relays(void){
    SET_ONOFF(0);
    SET_CLOSE(0);
}
static inline void set_open_status_only(void){
    SET_OPEN_STS(1);
    SET_CLOSE_STS(0);
}
static inline void set_close_status_only(void){
    SET_OPEN_STS(0);
    SET_CLOSE_STS(1);
}
static inline bool status_is_open(void){ return LATBbits.LATB3 && !LATBbits.LATB4; }
static inline bool status_is_close(void){ return LATBbits.LATB4 && !LATBbits.LATB3; }

static void start_open_cycle(void){
    g_pulse_count = 0;
    g_run_start_pos = g_position;   // capture start
    g_run_dir = DIR_OPEN;
    g_counting_enabled = true;
    g_running = true;
    SET_CLOSE(0);
    SET_ONOFF(1);
    SET_OPEN_STS(0);
    SET_CLOSE_STS(0);
    // reset checkpoint cadence at cycle start
    g_pulses_since_checkpoint = 0;
    g_checkpoint_pending = false;
    ee_store_full(g_setpoint, 1U, g_position);
}
static void start_close_cycle(void){
    g_pulse_count = 0;
    g_run_start_pos = g_position;   // capture start so close progress counts up
    g_run_dir = DIR_CLOSE;
    g_counting_enabled = true;
    g_running = true;
    SET_CLOSE(1);
    SET_ONOFF(1);
    SET_OPEN_STS(0);
    SET_CLOSE_STS(0);
    // reset checkpoint cadence at cycle start
    g_pulses_since_checkpoint = 0;
    g_checkpoint_pending = false;
    ee_store_full(g_setpoint, 2U, g_position);
}
static void end_cycle_open_done(void){
    stop_all_relays();
    g_counting_enabled = false;
    g_running = false;
    g_run_dir = DIR_IDLE;
    g_position = g_setpoint;              // at full open
    set_open_status_only();
    ee_store_full(g_setpoint, 1U, g_position);  // persist absolute position
}
static void end_cycle_close_done(void){
    stop_all_relays();
    g_counting_enabled = false;
    g_running = false;
    g_run_dir = DIR_IDLE;
    g_position = 0;                       // at full close
    set_close_status_only();
    ee_store_full(g_setpoint, 2U, g_position);  // persist absolute position
}

// Debounce RA0 (PowerDetect). Confirms a change after ~25ms steady.
static void debounce_mode_input(void){
    uint8_t raw = IN_POWER_DETECT() ? 1U : 0U;
    if ((uint32_t)(g_ms - pd_last_sample_ms) < 5U) return; // ~5ms sample
    pd_last_sample_ms = g_ms;
    if (raw == pd_sample_last) {
        if (pd_streak < 255U) pd_streak++;
        if (pd_streak >= 5U) { pd_filtered = raw; } // 5*5ms ? 25ms
    } else {
        pd_streak = 0U;
        pd_sample_last = raw;
    }
}

// Debounce RA2, producing a clean level in man_filtered
static void debounce_manual_trigger(void){
    uint8_t raw = IN_TRIGGER_MAN() ? 1U : 0U;
    if ((uint32_t)(g_ms - man_last_sample_ms) < 5U) return; // ~5ms sample
    man_last_sample_ms = g_ms;
    if (raw == man_sample_last) {
        if (man_streak < 255U) man_streak++;
        if (man_streak >= (MANUAL_DEBOUNCE_MS/5U)) {
            man_filtered = raw;
        }
    } else {
        man_streak = 0U;
        man_sample_last = raw;
    }
}

/************ BOOT RA0 SELF-TEST (LED only; does not touch STS) ************/
static void pd_boot_selftest(void){
#if (PD_SELFTEST_MS > 0)
    const uint32_t until = g_ms + PD_SELFTEST_MS;
    bool printed = false;
    while ((int32_t)(g_ms - until) < 0) {
        uint8_t raw = IN_POWER_DETECT() ? 1U : 0U;
        SET_HEARTBEAT(raw);     // mirror RA0 on heartbeat LED
        debounce_mode_input();
        if (!printed) {
            uart_puts("BOOT_PD RA0(raw):"); uart_putu8(raw);
            uart_puts(" PD(debounced):");   uart_putu8(pd_filtered);
            uart_putc('\n');
            printed = true;
        }
        CLRWDT();
    }
    SET_HEARTBEAT(0);
#endif
}

/************ OLED UI UPDATE ************/
static void oled_show_boot(void){
    oled_clear();
    oled_print(1, 12, "RealTech Systems");
}
static void oled_refresh(void){
    // Determine if we are in offline mechanical movement and guess direction
    bool offline = g_offline_active;
    int8_t offdir = 0; // +1 -> toward OPEN, -1 -> toward CLOSE, 0 unknown
    if(offline){
        if(status_is_close() && !status_is_open()) offdir = +1;
        else if(status_is_open() && !status_is_close()) offdir = -1;
        else offdir = 0;
    }

    // Page 2: Mode
    uint8_t this_mode = (g_mode==MODE_AUTO)?1:0;
    if(this_mode != g_display_last_mode){
        for(uint8_t c=0;c<21;c++) oled_write_char(2,c*6,' ');
        oled_print(2,0,"Mode:");
        oled_print(2,42,(g_mode==MODE_AUTO)?"AUTO":"MANUAL");
        g_display_last_mode = this_mode;
    }

    // Page 4: Status
    uint8_t os = LATBbits.LATB3;
    uint8_t cs = LATBbits.LATB4;

    if(os != g_display_last_opensts || cs != g_display_last_closests ||
       (uint8_t)g_running != g_display_last_running ||
       (uint8_t)g_run_dir != g_display_last_dir ||
       (uint8_t)offline != g_display_last_offline ||
       (uint8_t)((offdir>0)?1:(offdir<0)?2:0) != g_display_last_offdir)
    {
        for(uint8_t c=0;c<21;c++) oled_write_char(4,c*6,' ');
        oled_print(4,0,"STS:");

        if(g_running){
            if(g_run_dir==DIR_OPEN)      oled_print(4,42,"OP - Process");
            else if(g_run_dir==DIR_CLOSE)oled_print(4,42,"CL - Process");
            else                         oled_print(4,42,"BUSY ");
        } else if (offline){
            if(offdir==+1)               oled_print(4,42,"OP - Manual");
            else if(offdir==-1)          oled_print(4,42,"CL - Manual");
            else                         oled_print(4,42,"MAN - Move");
        } else {
            if(os)        oled_print(4,42,"OPEN ");
            else if(cs)   oled_print(4,42,"CLOSE");
            else          oled_print(4,42,"BUSY ");
        }

        g_display_last_opensts = os;
        g_display_last_closests = cs;
        g_display_last_running = (uint8_t)g_running;
        g_display_last_dir     = (uint8_t)g_run_dir;
        g_display_last_offline = (uint8_t)offline;
        g_display_last_offdir  = (uint8_t)((offdir>0)?1:(offdir<0)?2:0);
    }

    // live count display:
    uint16_t show = 0;
    if(g_running){
        if(g_run_dir==DIR_OPEN){
            show = (g_position<=g_setpoint) ? g_position : g_setpoint;
        } else { // DIR_CLOSE
            uint16_t prog = (g_position <= g_run_start_pos) ? (uint16_t)(g_run_start_pos - g_position) : 0U;
            if(prog > g_setpoint) prog = g_setpoint;
            show = prog;
        }
    } else if (offline){
        show = g_offline_count;
    } else {
        show = g_position;
    }

    // Page 6: "Set : <val>  |  Count : <val>"
    if (g_setpoint != g_display_last_set || show != g_display_last_count) {
        for (uint8_t c = 0; c < 21; c++) oled_write_char(6, c*6, ' ');
        const uint8_t center_col = 64;
        const uint8_t bar_col    = center_col-2;

        // Left: Set
        uint8_t set_digits = u16_digits(g_setpoint);
        uint8_t set_val_col = (uint8_t)(bar_col - 8U - set_digits*6U);
        if(set_val_col < 30U) set_val_col = 30U;
        oled_print(6, 0,  "Set:");
        oled_print_u16(6, set_val_col, g_setpoint);

        // Separator
        oled_print(6, bar_col, "|");

        // Right: Count (live)
        uint8_t cnt_digits = u16_digits(show);
        uint8_t cnt_val_col = (uint8_t)(127U - (cnt_digits*6U));
        if(cnt_val_col < (uint8_t)(bar_col + 8U + 6U*6U + 6U)) {
            cnt_val_col = (uint8_t)(bar_col + 8U + 6U*6U + 6U);
        }
        uint8_t cnt_label_col = (uint8_t)(cnt_val_col - 6U - 6U*6U);
        oled_print(6, cnt_label_col, "Count:");
        oled_print_u16(6, cnt_val_col, show);

        g_display_last_set = g_setpoint;
        g_display_last_count = show;
    }
}

/************ COMMAND PARSER (LOG, PULSE<n>, DFLT) ************/
static void cmd_send_log(void){
    const char* mode_s = (g_mode==MODE_AUTO)?"AUTO":"MANUAL";
    const char* sts_s  = status_is_open()? "OPEN" : (status_is_close()? "CLOSE" : (g_running?(g_run_dir==DIR_OPEN?"O-PRC":"C-PRC"):(g_offline_active?"MAN":"PART")));

    uart_puts("MODE:"); uart_puts(mode_s); uart_putc(' ');
    uart_puts("STS:");  uart_puts(sts_s);  uart_putc(' ');
    uart_puts("SET:");  uart_putu8(g_setpoint); uart_putc(' ');
    uart_puts("POS:");  uart_putu8(g_position); uart_putc(' ');
    uart_puts("RUN:");  uart_putu16(g_pulse_count); uart_putc(' ');
    uart_puts("LAST:"); uart_puts( (g_last_status_persist==1)?"OPEN":(g_last_status_persist==2)?"CLOSE":"UNK" );
    uart_putc('/'); uart_putu8(g_last_reachcnt_persist); uart_putc('\n');
}
static void process_cmd_line(char *line){
    if(!line) return;

    // Trim CR/LF and spaces at end
    size_t n = strlen(line);
    while(n>0 && (line[n-1]=='\r' || line[n-1]=='\n' || line[n-1]==' ' || line[n-1]=='\t')) { line[--n]=0; }
    // Trim leading spaces
    char* p = skip_ws(line);
    if(!*p) return;

    cmd_to_upper(p);

    if(strncmp(p,"LOG",3)==0){
        cmd_send_log();
        return;
    }

    if(strncmp(p,"DFLT",4)==0){
        ee_store_setpoint(PULSE_DEFAULT);
        uart_puts("OK PULSE="); uart_putu16(PULSE_DEFAULT); uart_putc('\n');
        g_display_last_set = 0xFF;
        return;
    }

    if(strncmp(p,"PULSE",5)==0){
        p += 5;
        p = skip_ws(p);
        if(*p=='='||*p==':') { ++p; p = skip_ws(p); }

        bool ok=false;
        uint16_t v = parse_uint(p,&ok);
        if(ok && v>=PULSE_MIN && v<=PULSE_MAX){
            ee_store_setpoint((uint8_t)v);
            uart_puts("OK PULSE="); uart_putu16(v); uart_putc('\n');
            g_display_last_set = 0xFF;
        } else {
            uart_puts("ERR PULSE_RANGE 1..250\n");
        }
        return;
    }

    // allow short alias: P<n>
    if(p[0]=='P' && (p[1]>='0' && p[1]<='9')){
        bool ok=false; uint16_t v = parse_uint(p+1,&ok);
        if(ok && v>=PULSE_MIN && v<=PULSE_MAX){
            ee_store_setpoint((uint8_t)v);
            uart_puts("OK PULSE="); uart_putu16(v); uart_putc('\n');
        } else {
            uart_puts("ERR PULSE_RANGE 1..250\n");
        }
        g_display_last_set = 0xFF;
        return;
    }

    uart_puts("ERR UNKNOWN_CMD\n");
}

/************ INTERRUPTS ************/
void __interrupt() isr(void){
    // UART RX (buffer only ? parsing in main loop)
    if(PIE1bits.RC1IE && PIR1bits.RC1IF){
        if(RCSTA1bits.OERR){ RCSTA1bits.CREN=0; RCSTA1bits.CREN=1; }

        char c = RCREG1;             // reading clears FERR for this byte
        if(rx_line_ready){
            // previous line not yet consumed: drop byte
        } else if(!RCSTA1bits.FERR){
            if(rxlen < (RXBUF_SZ-1)){
                if(c=='\r' || c=='\n'){
                    rxbuf[rxlen]=0;
                    rx_line_ready = true;   // main will parse
                } else {
                    rxbuf[rxlen++] = c;
                    rxbuf[rxlen] = 0;
                }
            } else {
                rxlen=0;
            }
            rx_last_ms = g_ms;
        }
        // RC1IF auto-cleared by FIFO drain
    }

    // Timer1: 1ms
    if(PIE1bits.TMR1IE && PIR1bits.TMR1IF){
        TMR1H = (uint8_t)(TMR1_PRELOAD >> 8);
        TMR1L = (uint8_t)(TMR1_PRELOAD & 0xFF);
        g_ms++;
        PIR1bits.TMR1IF = 0;
    }

    // INT0: limit switch pulse (debounced by time)
    if(INTCONbits.INT0IE && INTCONbits.INT0IF){
        uint32_t now = g_ms;
        if ((now - g_last_pulse_ms) >= PULSE_DEBOUNCE_MS){
            g_last_pulse_ms = now;

            if(g_counting_enabled){
                // in-run pulse
                g_pulse_count++;
                if(g_run_dir==DIR_OPEN){
                    if(g_position < g_setpoint) g_position++;
                } else if(g_run_dir==DIR_CLOSE){
                    if(g_position > 0) g_position--;
                }

                // NEW: request an EEPROM checkpoint every N pulses
                if (++g_pulses_since_checkpoint >= EE_CHECKPOINT_EVERY_N_PULSES){
                    g_pulses_since_checkpoint = 0;
                    g_checkpoint_pos = g_position;
                    g_checkpoint_status = (g_run_dir==DIR_OPEN)?1U:2U;
                    g_checkpoint_pending = true;
                }
            } else {
                // offline (manual lever) pulse
                if(g_offline_count < 250) g_offline_count++;
                g_offline_active = true;
            }
        }
        INTCONbits.INT0IF = 0;
    }
}

/************ MAIN LOOP ************/
int main(void){
    osc_init();
    io_init();

    // Early sign of life before Timer1/IRQs
    SET_HEARTBEAT(1);
    __delay_ms(20);
    SET_HEARTBEAT(0);

    // Startup WDT safety window
    wdt_enable();
    CLRWDT();

    // Power-on settle & clear POR/BOR flags
    __delay_ms(POWER_ON_SETTLE_MS);
    (void)RCONbits.POR; (void)RCONbits.BOR;  // read if you want; not used here
    RCONbits.POR = 0; RCONbits.BOR = 0;
    CLRWDT();

    // Peripherals
    i2c_init();   CLRWDT();
    uart_init();  CLRWDT();
    tmr1_init();  CLRWDT();
    int0_init();  CLRWDT();

    // enable global interrupts
    RCONbits.IPEN = 0;   // no priorities, single vector
    INTCONbits.GIE = 1;
    INTCONbits.PEIE = 1;

    // Load setpoint + last status/log from EEPROM
    ee_load_all();
    CLRWDT();

    // OLED init (stubborn) ? retry once if first pass was too early
    (void)oled_init_once();
    __delay_ms(10);
    (void)oled_init_once();

    // Boot branding
    oled_clear();
    oled_print_center(0, "RealTech Systems");
    for(uint8_t c=0;c<21;c++) oled_write_char(1,c*6,' '); // gap line
    CLRWDT();

    // Prime UI to repaint
    g_display_last_mode = 0xFF;
    g_display_last_opensts = 0xFF;
    g_display_last_closests = 0xFF;
    g_display_last_set = 0xFF;
    g_display_last_count = 0xFFFF;
    g_display_last_running = 0xFF;
    g_display_last_dir     = 0xFF;
    g_display_last_offline = 0xFF;
    g_display_last_offdir  = 0xFF;

    // Relays off (STS not touched here)
    stop_all_relays();

    uint32_t last_ui_ms = 0;

    // Initial debounced reads
    pd_filtered = IN_POWER_DETECT()?1U:0U;
    pd_sample_last = pd_filtered;
    pd_streak = 0U;
    pd_last_sample_ms = g_ms;

    man_filtered = IN_TRIGGER_MAN()?1U:0U;
    man_sample_last = man_filtered;
    man_streak = 0U;
    man_last_sample_ms = g_ms;

    // FSM initial state based on RA2 level
    man_prev = man_filtered;
    man_level_since_ms = g_ms;
    man_state = (man_filtered==0U) ? MS_WAIT_HIGH : MS_WAIT_LOW;
    man_cycle_lock_until = 0;

    // Boot RA0 self-test (LED only; does not touch STS)
    pd_boot_selftest();
    CLRWDT();

    // Re-apply persisted status AFTER self-test so OLED shows OPEN/CLOSE, not BUSY
    if (g_last_status_persist==1U) set_open_status_only();
    else if (g_last_status_persist==2U) set_close_status_only();
    else set_close_status_only(); // default

    // Resume policy: converge to CLOSED using persisted absolute position
    if (!g_install_pending) {
        if (g_position > 0U) {
            start_close_cycle();  // will close exactly g_position counts to 0
        }
    }

    // System is up ? leave WDT off for normal runtime (as before)
    wdt_disable();

    for(;;){
        uint32_t now = ms_now();

        // NEW: perform deferred EEPROM checkpoint outside ISR
        if (g_checkpoint_pending){
            uint8_t pos, sts;
            INTCONbits.GIE = 0;
            pos = g_checkpoint_pos;
            sts = g_checkpoint_status;
            g_checkpoint_pending = false;
            INTCONbits.GIE = 1;
            ee_store_full(g_setpoint, sts, pos);
        }

        // Heartbeat
        if((now - g_hb_last_ms) >= HEARTBEAT_MS){
            g_hb_last_ms = now;
            TOGGLE_HEARTBEAT();
        }

        // ---- Safe UART command handling in main loop ----
        if(rx_line_ready || (rxlen>0 && (now - rx_last_ms) > CMD_IDLE_FLUSH_MS)){
            char line_local[RXBUF_SZ];

            // critical section: copy and clear producer state
            INTCONbits.GIE = 0;
            if(rx_line_ready){
                size_t n = strlen((const char*)rxbuf);
                if(n >= RXBUF_SZ) n = RXBUF_SZ-1;
                memcpy(line_local, (const void*)rxbuf, n+1);
                rx_line_ready = false;
                rxlen = 0;
            } else {
                rxbuf[rxlen] = 0;
                memcpy(line_local, (const void*)rxbuf, (size_t)rxlen+1);
                rxlen = 0;
            }
            INTCONbits.GIE = 1;

            process_cmd_line(line_local);
        }

        // Update debounced RA0 and mode
        debounce_mode_input();
        uint8_t pd = pd_filtered;

        if(pd != last_pd){
            g_mode = pd ? MODE_AUTO : MODE_MANUAL;
            g_running = false;
            g_counting_enabled = false;
            g_run_dir = DIR_IDLE;
            stop_all_relays();
            g_display_last_mode = 0xFF;
            last_pd = pd;

            // Reset manual FSM on mode change to MANUAL
            debounce_manual_trigger();
            man_prev = man_filtered;
            man_level_since_ms = g_ms;
            man_state = (man_filtered==0U) ? MS_WAIT_HIGH : MS_WAIT_LOW;
            man_cycle_lock_until = g_ms + 50; // tiny guard
        }

        // First-boot installation: run one opening cycle
        if (g_install_pending && !g_running){
            start_open_cycle();
            g_install_pending = false;
        }

        // finalize offline manual pulses if settled
        if(!g_running && g_offline_active){
            if((now - g_last_pulse_ms) > OFFLINE_SETTLE_MS){
                int8_t dir_guess = 0;
                if(status_is_close() && !status_is_open()) dir_guess = +1; // moving to open
                else if(status_is_open() && !status_is_close()) dir_guess = -1; // moving to close
                else dir_guess = 0;

                uint8_t delta = g_offline_count;
                if(dir_guess==+1){
                    uint16_t pos = (uint16_t)g_position + (uint16_t)delta;
                    g_position = (uint8_t)((pos > g_setpoint) ? g_setpoint : pos);
                } else if(dir_guess==-1){
                    g_position = (g_position > delta) ? (uint8_t)(g_position - delta) : 0U;
                } else {
                    // unknown; leave position unchanged
                }

                ee_store_full(g_setpoint, 0U, g_position); // persist

                g_offline_count = 0;
                g_offline_active = false;
                g_display_last_count = 0xFFFF; // force UI refresh later
            }
        }

        if(g_mode == MODE_AUTO){
            // Level-based action using absolute position
            uint8_t want_open = IN_TRIGGER_AUTO()?1U:0U;

            if(!g_running){
                if(want_open){
                    if(g_position < g_setpoint){
                        start_open_cycle();
                    }
                } else {
                    if(g_position > 0U){
                        start_close_cycle();
                    }
                }
            }

            // Completion (position-based)
            if(g_running){
                if(g_run_dir==DIR_OPEN && g_position >= g_setpoint){
                    end_cycle_open_done();
                } else if(g_run_dir==DIR_CLOSE && g_position == 0U){
                    end_cycle_close_done();
                }
            }

        } else { // MODE_MANUAL
            // FSM-based one-shot on RA2
            debounce_manual_trigger();

            // Track how long the debounced level has been stable
            if(man_filtered != man_prev){
                man_prev = man_filtered;
                man_level_since_ms = g_ms;
            }

            bool locked = ((int32_t)(g_ms - man_cycle_lock_until) < 0);

            if(!g_running && !locked){
                switch(man_state){
                    case MS_WAIT_LOW:
                        if(man_filtered==0U && (g_ms - man_level_since_ms) >= MANUAL_REARM_LOW_MS){
                            man_state = MS_WAIT_HIGH;
                        }
                        break;

                    case MS_WAIT_HIGH:
                        if(man_filtered==1U && (g_ms - man_level_since_ms) >= MANUAL_PRESS_MS){
                            if(status_is_open()){
                                start_close_cycle();
                            } else if(status_is_close()){
                                start_open_cycle();
                            } else {
                                // unknown/partial: ignore manual toggle
                            }
                            man_state = MS_HELD;
                            man_cycle_lock_until = g_ms + MANUAL_LOCKOUT_AFTER_START;
                        }
                        break;

                    case MS_HELD:
                        if(man_filtered==0U && (g_ms - man_level_since_ms) >= MANUAL_REARM_LOW_MS){
                            man_state = MS_WAIT_HIGH;
                        }
                        break;
                }
            }

            // Complete cycles (position-based)
            if(g_running){
                if(g_run_dir==DIR_OPEN && g_position >= g_setpoint){
                    end_cycle_open_done();
                } else if(g_run_dir==DIR_CLOSE && g_position == 0U){
                    end_cycle_close_done();
                }

                // After finishing, enforce a lockout window
                if(!g_running){ // just finished
                    man_cycle_lock_until = g_ms + MANUAL_LOCKOUT_AFTER_START;
                    if(man_filtered==0U){
                        man_state = MS_WAIT_HIGH;
                        man_level_since_ms = g_ms;
                    } else {
                        man_state = MS_HELD;
                    }
                }
            }
        }

        // UI refresh ~20Hz cap
        if((now - last_ui_ms) >= 50){
            last_ui_ms = now;
            oled_refresh();
        }
    }
    // not reached
}
