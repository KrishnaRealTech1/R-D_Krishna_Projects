/*
 * File:   EAS_ITank_RTC_Interface_40min_Updated.c
 * Author: RealTech
 *
 * Created on 19 August, 2026, 5:12 PM
 */

#define _XTAL_FREQ                      4000000UL

// PIC12F683 Configuration Bit Settings
#pragma config FOSC  = INTOSCIO  // Internal oscillator, GPIO on GP4/GP5
#pragma config WDTE  = OFF       // Watchdog Timer disabled - restored like old working code
#pragma config PWRTE = ON        // Power-up Timer enabled
#pragma config MCLRE = OFF       // MCLR pin function is digital input
#pragma config CP    = OFF       // Code protection disabled
#pragma config CPD   = OFF       // Data code protection disabled
#pragma config BOREN = ON        // Brown-out Reset enabled

#include <xc.h>
#include <stdint.h>
#include <stdbool.h>

// -----------------------------------------------------------------------------
// User settings
// -----------------------------------------------------------------------------
#define RELAY_ON_SECONDS                10

#define RELAY_ON_LEVEL                  1
#define RELAY_OFF_LEVEL                 0

#define DIP_ACTIVE_LEVEL                1
#define TEST_ACTIVE_LEVEL               0

// GP3 TEST input is active LOW.
// Hardware: connect a 10 kOhm external pull-up resistor from GP3 to VDD,
// and a test switch/jumper from GP3 to GND.
// GP3 HIGH = normal production mode
// GP3 LOW  = test mode

// Test-mode DIP timing patterns:
// DIP1 OFF, DIP2 OFF : Relay OFF 30 s, ON 10 s, repeat
// DIP1 OFF, DIP2 ON  : Relay OFF 10 s, ON  5 s, repeat
// DIP1 ON,  DIP2 OFF : Relay OFF 60 s, ON 15 s, repeat
// DIP1 ON,  DIP2 ON  : Relay stays OFF (safe/unused test combination)
#define TEST_00_OFF_SECONDS             30
#define TEST_00_ON_SECONDS              10
#define TEST_01_OFF_SECONDS             10
#define TEST_01_ON_SECONDS              5
#define TEST_10_OFF_SECONDS             60
#define TEST_10_ON_SECONDS              15

// -----------------------------------------------------------------------------
// Schedule settings - 40 MINUTE INTERVAL
// -----------------------------------------------------------------------------
// Relay triggers every 40 minutes from midnight:
// 00:00, 00:40, 01:20, 02:00, 02:40, 03:20, etc.
// DIP1 ON  = Sunday enabled
// DIP1 OFF = Sunday disabled
// DIP2 ON  = operate only during the configured hour window
// DIP2 OFF = operate all 24 hours

#define TRIGGER_INTERVAL_MINUTES        40
#define RTC_DAY_SUNDAY                  1
#define DAY_WINDOW_START_HOUR           8
#define DAY_WINDOW_END_HOUR             20

// Keep this 0 because RTC already contains real-world time.
#define SET_RTC_TIME_ON_POWERUP         0

#if SET_RTC_TIME_ON_POWERUP
#define RTC_SET_SECOND                  0
#define RTC_SET_MINUTE                  0
#define RTC_SET_HOUR                    10
#define RTC_SET_DAY_OF_WEEK             2
#define RTC_SET_DATE                    12
#define RTC_SET_MONTH                   5
#define RTC_SET_YEAR                    26
#endif

// -----------------------------------------------------------------------------
// Runtime safety settings
// -----------------------------------------------------------------------------
#define RTC_READ_RETRY_COUNT            3

// EEPROM storage for last successful relay trigger.
// This prevents repeated triggering if the PIC resets during the same scheduled
// minute because of relay noise, brown-out, power dip, or I2C disturbance.
#define EEPROM_TRIGGER_MAGIC            0xA5
#define EEPROM_TRIGGER_INVALID          0x00

#define EEPROM_ADDR_TRIGGER_MAGIC       0
#define EEPROM_ADDR_TRIGGER_HOUR        1
#define EEPROM_ADDR_TRIGGER_MINUTE      2
#define EEPROM_ADDR_TRIGGER_DATE        3
#define EEPROM_ADDR_TRIGGER_MONTH       4
#define EEPROM_ADDR_TRIGGER_YEAR        5
#define EEPROM_ADDR_TRIGGER_CHECKSUM    6

// -----------------------------------------------------------------------------
// Pin mapping
// -----------------------------------------------------------------------------
// DIP Switch 1 - PIN 3 - GPIO4 - Sunday enable
#define DIP1_PIN                        GPIObits.GP4
#define DIP1_TRIS                       TRISIObits.TRISIO4

// DIP Switch 2 - PIN 2 - GPIO5 - configured hour-window enable
#define DIP2_PIN                        GPIObits.GP5
#define DIP2_TRIS                       TRISIObits.TRISIO5

// Relay Circuit - PIN 7 - GPIO0
#define RELAY_PIN                       GPIObits.GP0
#define RELAY_TRIS                      TRISIObits.TRISIO0

// SDA - PIN 6 - GPIO1
#define SDA_PIN                         GPIObits.GP1
#define SDA_TRIS                        TRISIObits.TRISIO1

// SCL - PIN 5 - GPIO2
#define SCL_PIN                         GPIObits.GP2
#define SCL_TRIS                        TRISIObits.TRISIO2

// TEST input - PIN 4 - GPIO3 (input only)
// Active LOW: use an external 10 kOhm pull-up resistor and switch/jumper GP3 to GND.
#define TEST_PIN                        GPIObits.GP3
#define TEST_TRIS                       TRISIObits.TRISIO3

// -----------------------------------------------------------------------------
// DS3231 I2C address
// -----------------------------------------------------------------------------
#define DS3231_ADDR                     0x68
#define DS3231_ADDR_WRITE               ((DS3231_ADDR << 1) | 0)
#define DS3231_ADDR_READ                ((DS3231_ADDR << 1) | 1)

// -----------------------------------------------------------------------------
// RTC structure
// -----------------------------------------------------------------------------
typedef struct
{
    uint8_t second;
    uint8_t minute;
    uint8_t hour;
    uint8_t day_of_week;
    uint8_t date;
    uint8_t month;
    uint8_t year;
} RTC_Time;

// -----------------------------------------------------------------------------
// Function prototypes
// -----------------------------------------------------------------------------
static void Delay_Approx_65ms(void);
static void Delay_Approx_250ms(void);
static void Delay_Approx_1s(void);
static void Delay_Seconds(uint8_t seconds);

static void Relay_On(void);
static void Relay_Off(void);
static void Relay_Reset_Pulse(void);
static void Relay_Test_Cycle(void);
static bool Test_Delay_Seconds_Abortable(uint8_t seconds);

static void Timer0_Init_For_Delay(void);

static void Safe_Fault_Idle_Forever(void);

static void I2C_Delay(void);
static void SDA_Low(void);
static void SDA_Release(void);
static void SCL_Low(void);
static void SCL_Release(void);
static uint8_t SDA_Read(void);

static void I2C_Init(void);
static void I2C_Start(void);
static void I2C_Stop(void);
static bool I2C_WriteByte(uint8_t data);
static uint8_t I2C_ReadByte(bool send_ack);

static uint8_t BCD_To_Dec(uint8_t bcd);

#if SET_RTC_TIME_ON_POWERUP
static uint8_t Dec_To_BCD(uint8_t dec);
#endif

static uint8_t EEPROM_ReadByte(uint8_t address);
static void EEPROM_WriteByte(uint8_t address, uint8_t value);
static uint8_t Trigger_Checksum(uint8_t hour,
                                uint8_t minute,
                                uint8_t date,
                                uint8_t month,
                                uint8_t year);
static bool EEPROM_Load_Last_Trigger(uint8_t *last_hour,
                                      uint8_t *last_minute,
                                      uint8_t *last_date,
                                      uint8_t *last_month,
                                      uint8_t *last_year);
static void EEPROM_Save_Last_Trigger(uint8_t hour,
                                      uint8_t minute,
                                      uint8_t date,
                                      uint8_t month,
                                      uint8_t year);

static bool DS3231_ReadTime(RTC_Time *time);
static bool RTC_Time_Is_Valid(const RTC_Time *time);
static bool DS3231_ReadTime_With_Retry(RTC_Time *time);

#if SET_RTC_TIME_ON_POWERUP
static bool DS3231_SetTime(const RTC_Time *time);
#endif

static bool Is_Sunday_Enabled(void);
static bool Is_Hour_Window_Enabled(uint8_t hour);
static bool Is_Test_Mode_Enabled(void);
static bool Is_Allowed_Day_And_Hour(const RTC_Time *now);
static bool Is_Scheduled_Relay_Time(const RTC_Time *now);
static bool Already_Triggered_This_Minute(const RTC_Time *now,
                                          uint8_t last_hour,
                                          uint8_t last_minute,
                                          uint8_t last_date,
                                          uint8_t last_month,
                                          uint8_t last_year);

static void Init_Device(void);

// -----------------------------------------------------------------------------
// Relay control
// -----------------------------------------------------------------------------
static void Relay_On(void)
{
    RELAY_PIN = RELAY_ON_LEVEL;
}

static void Relay_Off(void)
{
    RELAY_PIN = RELAY_OFF_LEVEL;
}

static void Relay_Reset_Pulse(void)
{
    Relay_On();
    Delay_Seconds(RELAY_ON_SECONDS);
    Relay_Off();
}

// -----------------------------------------------------------------------------
// GP3 test-mode cycle
// -----------------------------------------------------------------------------
// Test-mode delay that continuously watches GP3.
// Returns false immediately when GP3 goes HIGH, so test mode exits without
// waiting for the complete OFF/ON delay to finish.
static bool Test_Delay_Seconds_Abortable(uint8_t seconds)
{
    uint8_t sec;
    uint8_t slice;

    for (sec = 0; sec < seconds; sec++)
    {
        // 16 x ~65 ms is approximately one second.
        // Check GP3 before every short slice for fast test-mode exit.
        for (slice = 0; slice < 16; slice++)
        {
            if (!Is_Test_Mode_Enabled())
            {
                Relay_Off();
                return false;
            }

            Delay_Approx_65ms();
        }
    }

    return true;
}

static void Relay_Test_Cycle(void)
{
    bool dip1_on;
    bool dip2_on;
    uint8_t off_seconds;
    uint8_t on_seconds;

    // Test mode must still be active before starting a cycle.
    if (!Is_Test_Mode_Enabled())
    {
        Relay_Off();
        return;
    }

    dip1_on = (DIP1_PIN == DIP_ACTIVE_LEVEL);
    dip2_on = (DIP2_PIN == DIP_ACTIVE_LEVEL);

    // Safe default for DIP1 ON + DIP2 ON, or any unexpected condition.
    off_seconds = 0;
    on_seconds  = 0;

    if ((!dip1_on) && (!dip2_on))
    {
        off_seconds = TEST_00_OFF_SECONDS;
        on_seconds  = TEST_00_ON_SECONDS;
    }
    else if ((!dip1_on) && dip2_on)
    {
        off_seconds = TEST_01_OFF_SECONDS;
        on_seconds  = TEST_01_ON_SECONDS;
    }
    else if (dip1_on && (!dip2_on))
    {
        off_seconds = TEST_10_OFF_SECONDS;
        on_seconds  = TEST_10_ON_SECONDS;
    }
    else
    {
        // DIP1 ON + DIP2 ON: unused test combination. Keep relay OFF.
        Relay_Off();
        Delay_Approx_250ms();
        return;
    }

    // OFF portion. Abort immediately if GP3 goes HIGH.
    Relay_Off();
    if (!Test_Delay_Seconds_Abortable(off_seconds))
    {
        return;
    }

    // ON portion. GP3 is continuously monitored here too.
    Relay_On();
    if (!Test_Delay_Seconds_Abortable(on_seconds))
    {
        Relay_Off();
        return;
    }

    Relay_Off();
}

// -----------------------------------------------------------------------------
// Timer0 delay
// -----------------------------------------------------------------------------
static void Timer0_Init_For_Delay(void)
{
    OPTION_REGbits.T0CS = 0;    // Timer0 uses internal instruction cycle clock
    OPTION_REGbits.PSA  = 0;    // Prescaler assigned to Timer0

    OPTION_REGbits.PS2  = 1;
    OPTION_REGbits.PS1  = 1;
    OPTION_REGbits.PS0  = 1;    // 1:256 prescaler

    INTCONbits.T0IE = 0;
    INTCONbits.T0IF = 0;
}

static void Delay_Approx_65ms(void)
{
    TMR0 = 0;
    INTCONbits.T0IF = 0;

    while (INTCONbits.T0IF == 0)
    {
        // Wait for Timer0 overflow
    }
}

static void Delay_Approx_250ms(void)
{
    uint8_t i;

    for (i = 0; i < 4; i++)
    {
        Delay_Approx_65ms();
    }
}

static void Delay_Approx_1s(void)
{
    uint8_t i;

    for (i = 0; i < 16; i++)
    {
        Delay_Approx_65ms();
    }
}

static void Delay_Seconds(uint8_t seconds)
{
    while (seconds > 0)
    {
        Delay_Approx_1s();
        seconds--;
    }
}

// -----------------------------------------------------------------------------
// Safe fault handler
// -----------------------------------------------------------------------------
static void Safe_Fault_Idle_Forever(void)
{
    while (1)
    {
        Relay_Off();
        I2C_Init();
        Delay_Seconds(1);
    }
}

// -----------------------------------------------------------------------------
// I2C bit-delay
// -----------------------------------------------------------------------------
static void I2C_Delay(void)
{
    volatile uint8_t i;

    for (i = 0; i < 50; i++)
    {
        // Software I2C half-bit delay
    }
}

// -----------------------------------------------------------------------------
// Software I2C open-drain style control
// -----------------------------------------------------------------------------
static void SDA_Low(void)
{
    SDA_PIN = 0;
    SDA_TRIS = 0;
}

static void SDA_Release(void)
{
    SDA_TRIS = 1;
}

static void SCL_Low(void)
{
    SCL_PIN = 0;
    SCL_TRIS = 0;
}

static void SCL_Release(void)
{
    SCL_TRIS = 1;
}

static uint8_t SDA_Read(void)
{
    return SDA_PIN;
}

// -----------------------------------------------------------------------------
// Software I2C functions
// -----------------------------------------------------------------------------
// This section is restored close to your old working code.
// The new SCL timeout/recovery logic was removed from the normal read path
// because it can reject RTC reads and prevent the trigger check from running.
static void I2C_Init(void)
{
    SDA_Release();
    SCL_Release();
    I2C_Delay();
}

static void I2C_Start(void)
{
    SDA_Release();
    SCL_Release();
    I2C_Delay();

    SDA_Low();
    I2C_Delay();

    SCL_Low();
    I2C_Delay();
}

static void I2C_Stop(void)
{
    SDA_Low();
    I2C_Delay();

    SCL_Release();
    I2C_Delay();

    SDA_Release();
    I2C_Delay();
}

static bool I2C_WriteByte(uint8_t data)
{
    uint8_t i;
    bool ack;

    for (i = 0; i < 8; i++)
    {
        if ((data & 0x80) != 0)
        {
            SDA_Release();
        }
        else
        {
            SDA_Low();
        }

        I2C_Delay();

        SCL_Release();
        I2C_Delay();

        SCL_Low();
        I2C_Delay();

        data <<= 1;
    }

    SDA_Release();
    I2C_Delay();

    SCL_Release();
    I2C_Delay();

    ack = (SDA_Read() == 0);

    SCL_Low();
    I2C_Delay();

    return ack;
}

static uint8_t I2C_ReadByte(bool send_ack)
{
    uint8_t i;
    uint8_t data = 0;

    SDA_Release();

    for (i = 0; i < 8; i++)
    {
        data <<= 1;

        SCL_Release();
        I2C_Delay();

        if (SDA_Read() != 0)
        {
            data |= 1;
        }

        SCL_Low();
        I2C_Delay();
    }

    if (send_ack)
    {
        SDA_Low();
    }
    else
    {
        SDA_Release();
    }

    I2C_Delay();

    SCL_Release();
    I2C_Delay();

    SCL_Low();
    I2C_Delay();

    SDA_Release();

    return data;
}

// -----------------------------------------------------------------------------
// BCD conversion
// -----------------------------------------------------------------------------
static uint8_t BCD_To_Dec(uint8_t bcd)
{
    uint8_t tens;
    uint8_t ones;

    tens = (uint8_t)((bcd >> 4) & 0x0F);
    ones = (uint8_t)(bcd & 0x0F);

    return (uint8_t)((tens * 10) + ones);
}

#if SET_RTC_TIME_ON_POWERUP
static uint8_t Dec_To_BCD(uint8_t dec)
{
    uint8_t tens;
    uint8_t ones;

    tens = (uint8_t)(dec / 10);
    ones = (uint8_t)(dec % 10);

    return (uint8_t)((tens << 4) | ones);
}
#endif

// -----------------------------------------------------------------------------
// PIC12F683 EEPROM helpers
// -----------------------------------------------------------------------------
static uint8_t EEPROM_ReadByte(uint8_t address)
{
    while (EECON1bits.WR != 0)
    {
        // Wait for EEPROM write completion
    }

    EEADR = address;
    EECON1bits.RD = 1;

    return EEDAT;
}

static void EEPROM_WriteByte(uint8_t address, uint8_t value)
{
    bool gie_was_enabled;

    if (EEPROM_ReadByte(address) == value)
    {
        return;
    }

    while (EECON1bits.WR != 0)
    {
        // Wait for previous EEPROM write completion
    }

    EEADR = address;
    EEDAT = value;

    EECON1bits.WREN = 1;

    gie_was_enabled = (INTCONbits.GIE != 0);
    INTCONbits.GIE = 0;

    EECON2 = 0x55;
    EECON2 = 0xAA;
    EECON1bits.WR = 1;

    while (EECON1bits.WR != 0)
    {
        // Wait until write finishes
    }

    EECON1bits.WREN = 0;

    if (gie_was_enabled)
    {
        INTCONbits.GIE = 1;
    }
}

static uint8_t Trigger_Checksum(uint8_t hour,
                                uint8_t minute,
                                uint8_t date,
                                uint8_t month,
                                uint8_t year)
{
    return (uint8_t)(hour ^ minute ^ date ^ month ^ year ^ 0x5A);
}

static bool EEPROM_Load_Last_Trigger(uint8_t *last_hour,
                                      uint8_t *last_minute,
                                      uint8_t *last_date,
                                      uint8_t *last_month,
                                      uint8_t *last_year)
{
    uint8_t hour;
    uint8_t minute;
    uint8_t date;
    uint8_t month;
    uint8_t year;
    uint8_t checksum;

    if (EEPROM_ReadByte(EEPROM_ADDR_TRIGGER_MAGIC) != EEPROM_TRIGGER_MAGIC)
    {
        return false;
    }

    hour     = EEPROM_ReadByte(EEPROM_ADDR_TRIGGER_HOUR);
    minute   = EEPROM_ReadByte(EEPROM_ADDR_TRIGGER_MINUTE);
    date     = EEPROM_ReadByte(EEPROM_ADDR_TRIGGER_DATE);
    month    = EEPROM_ReadByte(EEPROM_ADDR_TRIGGER_MONTH);
    year     = EEPROM_ReadByte(EEPROM_ADDR_TRIGGER_YEAR);
    checksum = EEPROM_ReadByte(EEPROM_ADDR_TRIGGER_CHECKSUM);

    if (checksum != Trigger_Checksum(hour, minute, date, month, year))
    {
        return false;
    }

    if (hour > 23)
    {
        return false;
    }

    if (minute > 59)
    {
        return false;
    }

    if ((date < 1) || (date > 31))
    {
        return false;
    }

    if ((month < 1) || (month > 12))
    {
        return false;
    }

    if (year > 99)
    {
        return false;
    }

    *last_hour   = hour;
    *last_minute = minute;
    *last_date   = date;
    *last_month  = month;
    *last_year   = year;

    return true;
}

static void EEPROM_Save_Last_Trigger(uint8_t hour,
                                      uint8_t minute,
                                      uint8_t date,
                                      uint8_t month,
                                      uint8_t year)
{
    uint8_t checksum;

    checksum = Trigger_Checksum(hour, minute, date, month, year);

    // Invalidate first. If power fails while writing, old/bad data will not
    // be accepted as valid during next boot.
    EEPROM_WriteByte(EEPROM_ADDR_TRIGGER_MAGIC, EEPROM_TRIGGER_INVALID);

    EEPROM_WriteByte(EEPROM_ADDR_TRIGGER_HOUR, hour);
    EEPROM_WriteByte(EEPROM_ADDR_TRIGGER_MINUTE, minute);
    EEPROM_WriteByte(EEPROM_ADDR_TRIGGER_DATE, date);
    EEPROM_WriteByte(EEPROM_ADDR_TRIGGER_MONTH, month);
    EEPROM_WriteByte(EEPROM_ADDR_TRIGGER_YEAR, year);
    EEPROM_WriteByte(EEPROM_ADDR_TRIGGER_CHECKSUM, checksum);

    // Mark valid only after all fields are written.
    EEPROM_WriteByte(EEPROM_ADDR_TRIGGER_MAGIC, EEPROM_TRIGGER_MAGIC);
}

// -----------------------------------------------------------------------------
// DS3231 read current time
// -----------------------------------------------------------------------------
static bool DS3231_ReadTime(RTC_Time *time)
{
    uint8_t raw_second;
    uint8_t raw_minute;
    uint8_t raw_hour;
    uint8_t raw_day;
    uint8_t raw_date;
    uint8_t raw_month;
    uint8_t raw_year;

    I2C_Start();

    if (!I2C_WriteByte(DS3231_ADDR_WRITE))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(0x00))
    {
        I2C_Stop();
        return false;
    }

    I2C_Start();

    if (!I2C_WriteByte(DS3231_ADDR_READ))
    {
        I2C_Stop();
        return false;
    }

    raw_second = I2C_ReadByte(true);
    raw_minute = I2C_ReadByte(true);
    raw_hour   = I2C_ReadByte(true);
    raw_day    = I2C_ReadByte(true);
    raw_date   = I2C_ReadByte(true);
    raw_month  = I2C_ReadByte(true);
    raw_year   = I2C_ReadByte(false);

    I2C_Stop();

    time->second = BCD_To_Dec((uint8_t)(raw_second & 0x7F));
    time->minute = BCD_To_Dec((uint8_t)(raw_minute & 0x7F));

    if ((raw_hour & 0x40) != 0)
    {
        uint8_t hour_12;
        bool is_pm;

        hour_12 = BCD_To_Dec((uint8_t)(raw_hour & 0x1F));
        is_pm = ((raw_hour & 0x20) != 0);

        if (is_pm)
        {
            if (hour_12 != 12)
            {
                hour_12 = (uint8_t)(hour_12 + 12);
            }
        }
        else
        {
            if (hour_12 == 12)
            {
                hour_12 = 0;
            }
        }

        time->hour = hour_12;
    }
    else
    {
        time->hour = BCD_To_Dec((uint8_t)(raw_hour & 0x3F));
    }

    time->day_of_week = BCD_To_Dec((uint8_t)(raw_day & 0x07));
    time->date        = BCD_To_Dec((uint8_t)(raw_date & 0x3F));
    time->month       = BCD_To_Dec((uint8_t)(raw_month & 0x1F));
    time->year        = BCD_To_Dec(raw_year);

    return true;
}

static bool RTC_Time_Is_Valid(const RTC_Time *time)
{
    if (time->second > 59)
    {
        return false;
    }

    if (time->minute > 59)
    {
        return false;
    }

    if (time->hour > 23)
    {
        return false;
    }

    if ((time->day_of_week < 1) || (time->day_of_week > 7))
    {
        return false;
    }

    if ((time->date < 1) || (time->date > 31))
    {
        return false;
    }

    if ((time->month < 1) || (time->month > 12))
    {
        return false;
    }

    if (time->year > 99)
    {
        return false;
    }

    return true;
}

static bool DS3231_ReadTime_With_Retry(RTC_Time *time)
{
    uint8_t attempt;

    for (attempt = 0; attempt < RTC_READ_RETRY_COUNT; attempt++)
    {
        I2C_Init();

        if (DS3231_ReadTime(time))
        {
            if (RTC_Time_Is_Valid(time))
            {
                return true;
            }
        }

        Relay_Off();
        I2C_Init();
        Delay_Approx_250ms();
    }

    return false;
}

#if SET_RTC_TIME_ON_POWERUP
// -----------------------------------------------------------------------------
// DS3231 set current time
// -----------------------------------------------------------------------------
static bool DS3231_SetTime(const RTC_Time *time)
{
    I2C_Start();

    if (!I2C_WriteByte(DS3231_ADDR_WRITE))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(0x00))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(Dec_To_BCD(time->second)))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(Dec_To_BCD(time->minute)))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(Dec_To_BCD(time->hour)))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(Dec_To_BCD(time->day_of_week)))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(Dec_To_BCD(time->date)))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(Dec_To_BCD(time->month)))
    {
        I2C_Stop();
        return false;
    }

    if (!I2C_WriteByte(Dec_To_BCD(time->year)))
    {
        I2C_Stop();
        return false;
    }

    I2C_Stop();

    return true;
}
#endif

// -----------------------------------------------------------------------------
// DIP switch schedule control - NEW REQUIREMENT ONLY
// -----------------------------------------------------------------------------
static bool Is_Sunday_Enabled(void)
{
    return (DIP1_PIN == DIP_ACTIVE_LEVEL);
}

static bool Is_Hour_Window_Enabled(uint8_t hour)
{
    bool dip2_enabled;

    dip2_enabled = (DIP2_PIN == DIP_ACTIVE_LEVEL);

    // DIP2 OFF = process all 24 hours.
    if (!dip2_enabled)
    {
        return true;
    }

    // DIP2 ON = process only 08:00 through 18:00 inclusive.
    if ((hour >= DAY_WINDOW_START_HOUR) && (hour <= DAY_WINDOW_END_HOUR))
    {
        return true;
    }

    return false;
}

static bool Is_Test_Mode_Enabled(void)
{
    return (TEST_PIN == TEST_ACTIVE_LEVEL);
}

static bool Is_Allowed_Day_And_Hour(const RTC_Time *now)
{
    // DIP1 OFF = Sunday disabled.
    if ((now->day_of_week == RTC_DAY_SUNDAY) && !Is_Sunday_Enabled())
    {
        return false;
    }

    // DIP2 OFF = 24 hours; DIP2 ON = 08:00 through 18:00.
    if (!Is_Hour_Window_Enabled(now->hour))
    {
        return false;
    }

    return true;
}

static bool Is_Scheduled_Relay_Time(const RTC_Time *now)
{
    uint16_t minutes_from_midnight;

    if (!Is_Allowed_Day_And_Hour(now))
    {
        return false;
    }

    // Convert current RTC time to total minutes from midnight.
    // Trigger sequence:
    // 00:00, 00:40, 01:20, 02:00, 02:40, 03:20, ...
    minutes_from_midnight =
        ((uint16_t)now->hour * 60U) + (uint16_t)now->minute;

    // Trigger every 40 minutes.
    return ((minutes_from_midnight % TRIGGER_INTERVAL_MINUTES) == 0U);
}

static bool Already_Triggered_This_Minute(const RTC_Time *now,
                                          uint8_t last_hour,
                                          uint8_t last_minute,
                                          uint8_t last_date,
                                          uint8_t last_month,
                                          uint8_t last_year)
{
    if (now->hour != last_hour)
    {
        return false;
    }

    if (now->minute != last_minute)
    {
        return false;
    }

    if (now->date != last_date)
    {
        return false;
    }

    if (now->month != last_month)
    {
        return false;
    }

    if (now->year != last_year)
    {
        return false;
    }

    return true;
}

// -----------------------------------------------------------------------------
// Device initialization
// -----------------------------------------------------------------------------
static void Init_Device(void)
{
    ANSEL = 0x00;       // Disable analog input
    ADCON0 = 0x00;     // Disable ADC
    CMCON0 = 0x07;     // Disable comparator

    OSCCONbits.IRCF = 0b110;    // 4 MHz internal oscillator
    OSCCONbits.SCS  = 1;        // Internal oscillator used for system clock

    GPIO = 0x00;

    // GP0 = Relay output
    RELAY_TRIS = 0;
    Relay_Off();

    // GP1 = SDA, software I2C open-drain style
    SDA_Release();

    // GP2 = SCL, software I2C open-drain style
    SCL_Release();

    // GP3 = TEST input (input only)
    TEST_TRIS = 1;

    // GP4 = DIP Switch 1 input
    DIP1_TRIS = 1;

    // GP5 = DIP Switch 2 input
    DIP2_TRIS = 1;

    Timer0_Init_For_Delay();
}

// -----------------------------------------------------------------------------
// Main
// -----------------------------------------------------------------------------
void main(void)
{
    RTC_Time now;

    uint8_t last_trigger_hour   = 0xFF;
    uint8_t last_trigger_minute = 0xFF;
    uint8_t last_trigger_date   = 0xFF;
    uint8_t last_trigger_month  = 0xFF;
    uint8_t last_trigger_year   = 0xFF;

    Init_Device();
    I2C_Init();

    // Production duplicate-trigger protection survives resets.
    (void)EEPROM_Load_Last_Trigger(&last_trigger_hour,
                                   &last_trigger_minute,
                                   &last_trigger_date,
                                   &last_trigger_month,
                                   &last_trigger_year);

#if SET_RTC_TIME_ON_POWERUP
    {
        RTC_Time set_time;
        bool set_ok;

        set_time.second      = RTC_SET_SECOND;
        set_time.minute      = RTC_SET_MINUTE;
        set_time.hour        = RTC_SET_HOUR;
        set_time.day_of_week = RTC_SET_DAY_OF_WEEK;
        set_time.date        = RTC_SET_DATE;
        set_time.month       = RTC_SET_MONTH;
        set_time.year        = RTC_SET_YEAR;

        set_ok = DS3231_SetTime(&set_time);

        if (!set_ok)
        {
            Safe_Fault_Idle_Forever();
        }
    }
#endif

    Delay_Seconds(1);

    while (!DS3231_ReadTime_With_Retry(&now))
    {
        Relay_Off();
        I2C_Init();
        Delay_Seconds(1);
    }

    while (1)
    {
        // GP3 LOW = dedicated bench test mode.
        // Test mode uses only the DIP-selected OFF/ON cycle timings and does not
        // write EEPROM. Production RTC scheduling is suspended while GP3 is LOW.
        if (Is_Test_Mode_Enabled())
        {
            Relay_Test_Cycle();
            continue;
        }

        // Normal production mode: use DS3231 real-world time.
        if (DS3231_ReadTime_With_Retry(&now))
        {
            if (Is_Scheduled_Relay_Time(&now))
            {
                bool already_triggered;

                already_triggered = Already_Triggered_This_Minute(&now,
                                                                   last_trigger_hour,
                                                                   last_trigger_minute,
                                                                   last_trigger_date,
                                                                   last_trigger_month,
                                                                   last_trigger_year);

                if (!already_triggered)
                {
                    // Save production trigger BEFORE relay ON so a reset/noise event
                    // cannot cause another pulse during the same scheduled minute.
                    EEPROM_Save_Last_Trigger(now.hour,
                                             now.minute,
                                             now.date,
                                             now.month,
                                             now.year);

                    last_trigger_hour   = now.hour;
                    last_trigger_minute = now.minute;
                    last_trigger_date   = now.date;
                    last_trigger_month  = now.month;
                    last_trigger_year   = now.year;

                    Relay_Reset_Pulse();
                }
            }
        }
        else
        {
            Relay_Off();
            I2C_Init();
            Delay_Approx_1s();
        }

        Delay_Approx_250ms();
    }
}
