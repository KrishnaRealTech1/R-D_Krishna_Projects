// PIC12F683 Configuration Bit Settings
#pragma config FOSC  = INTOSCIO  // Internal oscillator, GPIO on GP4/GP5
#pragma config WDTE  = OFF       // Watchdog Timer disabled
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
#define _XTAL_FREQ              4000000UL

#define RELAY_ON_SECONDS        3

#define RELAY_ON_LEVEL          1
#define RELAY_OFF_LEVEL         0

#define DIP_ACTIVE_LEVEL        1

// -----------------------------------------------------------------------------
// CUSTOM RTC TEST TRIGGER SETTINGS
// -----------------------------------------------------------------------------
// Keep this as 1 for testing a custom RTC time.
// Example:
// Current RTC time = 12:51
// Set CUSTOM_TRIGGER_HOUR = 12
// Set CUSTOM_TRIGGER_MINUTE = 55
// Relay will ON at 12:55 for 3 seconds.
//
// IMPORTANT:
// This uses the RTC's already-set real time.
// It does NOT rewrite RTC time.
#define CUSTOM_RTC_TRIGGER_TEST_ENABLE  0

// Edit these two values for testing.
#define CUSTOM_TRIGGER_HOUR             12
#define CUSTOM_TRIGGER_MINUTE           58

// -----------------------------------------------------------------------------
// Production schedule settings
// -----------------------------------------------------------------------------
// Used only when CUSTOM_RTC_TRIGGER_TEST_ENABLE = 0.
// All production fixed schedules run at HH:00.
// Test mode runs every hour at HH:00.
#define PRODUCTION_TRIGGER_MINUTE       0

// Keep this 0 because your RTC time is already fixed/set.
// Set this to 1 only when you intentionally want to write time into RTC.
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
// Pin mapping
// -----------------------------------------------------------------------------
// DIP Switch 1 - PIN 3 - GPIO4
#define DIP1_PIN                        GPIObits.GP4
#define DIP1_TRIS                       TRISIObits.TRISIO4

// DIP Switch 2 - PIN 2 - GPIO5
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
// Reset modes
// -----------------------------------------------------------------------------
typedef enum
{
    RESET_MODE_TEST = 0,
    RESET_MODE_1    = 1,
    RESET_MODE_2    = 2,
    RESET_MODE_3    = 3
} Reset_Mode;

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

static void Timer0_Init_For_Delay(void);

static void Error_Blink_Fast_Forever(void);
static void Error_Blink_Slow_Forever(void);

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

static bool DS3231_ReadTime(RTC_Time *time);

#if SET_RTC_TIME_ON_POWERUP
static bool DS3231_SetTime(const RTC_Time *time);
#endif

static Reset_Mode Read_Selected_Mode(void);
static bool Is_Mode1_Hour(uint8_t hour);
static bool Is_Mode2_Hour(uint8_t hour);
static bool Is_Mode3_Hour(uint8_t hour);
static bool Is_Scheduled_Restart_Time(Reset_Mode mode, const RTC_Time *now);
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
// Error indication using relay
// -----------------------------------------------------------------------------
// These functions toggle the relay continuously.
// Use only for serious RTC fault/debug condition.
static void Error_Blink_Fast_Forever(void)
{
    while (1)
    {
        Relay_On();
        Delay_Approx_250ms();

        Relay_Off();
        Delay_Approx_250ms();
    }
}

static void Error_Blink_Slow_Forever(void)
{
    while (1)
    {
        Relay_On();
        Delay_Approx_1s();

        Relay_Off();
        Delay_Approx_1s();
    }
}

// -----------------------------------------------------------------------------
// I2C delay
// -----------------------------------------------------------------------------
static void I2C_Delay(void)
{
    volatile uint8_t i;

    for (i = 0; i < 50; i++)
    {
        // Small delay for software I2C
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
// DIP switch mode selection
// -----------------------------------------------------------------------------
static Reset_Mode Read_Selected_Mode(void)
{
    bool dip1_high;
    bool dip2_high;

    dip1_high = (DIP1_PIN == DIP_ACTIVE_LEVEL);
    dip2_high = (DIP2_PIN == DIP_ACTIVE_LEVEL);

    if ((dip1_high == true) && (dip2_high == false))
    {
        return RESET_MODE_1;
    }

    if ((dip1_high == false) && (dip2_high == true))
    {
        return RESET_MODE_2;
    }

    if ((dip1_high == true) && (dip2_high == true))
    {
        return RESET_MODE_3;
    }

    return RESET_MODE_TEST;
}

// -----------------------------------------------------------------------------
// Production schedule checking
// -----------------------------------------------------------------------------
static bool Is_Mode1_Hour(uint8_t hour)
{
    // Mode 1:
    // 7 AM, 3 PM, 11 PM
    if (hour == 7)
    {
        return true;
    }

    if (hour == 15)
    {
        return true;
    }

    if (hour == 23)
    {
        return true;
    }

    return false;
}

static bool Is_Mode2_Hour(uint8_t hour)
{
    // Mode 2:
    // 10 AM, 10 PM
    if (hour == 10)
    {
        return true;
    }

    if (hour == 22)
    {
        return true;
    }

    return false;
}

static bool Is_Mode3_Hour(uint8_t hour)
{
    // Mode 3:
    // 10 AM only
    if (hour == 10)
    {
        return true;
    }

    return false;
}

// -----------------------------------------------------------------------------
// Main schedule checking
// -----------------------------------------------------------------------------
static bool Is_Scheduled_Restart_Time(Reset_Mode mode, const RTC_Time *now)
{
#if CUSTOM_RTC_TRIGGER_TEST_ENABLE
    // -------------------------------------------------------------------------
    // CUSTOM TEST MODE
    // -------------------------------------------------------------------------
    // When CUSTOM_RTC_TRIGGER_TEST_ENABLE = 1,
    // DIP switch mode is ignored.
    //
    // Example:
    // CUSTOM_TRIGGER_HOUR   = 12
    // CUSTOM_TRIGGER_MINUTE = 55
    //
    // Relay will trigger once when RTC time reaches 12:55.
    // It will not repeat again during the same minute.
    (void)mode;

    if (now->hour != CUSTOM_TRIGGER_HOUR)
    {
        return false;
    }

    if (now->minute != CUSTOM_TRIGGER_MINUTE)
    {
        return false;
    }

    return true;

#else
    // -------------------------------------------------------------------------
    // PRODUCTION MODE
    // -------------------------------------------------------------------------
    // When CUSTOM_RTC_TRIGGER_TEST_ENABLE = 0,
    // DIP switch selected production schedule is used.
    if (now->minute != PRODUCTION_TRIGGER_MINUTE)
    {
        return false;
    }

    switch (mode)
    {
        case RESET_MODE_1:
            return Is_Mode1_Hour(now->hour);

        case RESET_MODE_2:
            return Is_Mode2_Hour(now->hour);

        case RESET_MODE_3:
            return Is_Mode3_Hour(now->hour);

        case RESET_MODE_TEST:
            // Test Mode:
            // Every hour at HH:00.
            return true;

        default:
            return false;
    }
#endif
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
    ANSEL  = 0x00;      // Disable analog input
    ADCON0 = 0x00;      // Disable ADC
    CMCON0 = 0x07;      // Disable comparator

    OSCCONbits.IRCF = 0b110;    // 4 MHz internal oscillator
    OSCCONbits.SCS  = 1;        // Internal oscillator used for system clock

    // GP0 = Relay output
    // GP1 = SDA, software I2C open-drain style
    // GP2 = SCL, software I2C open-drain style
    // GP3 = Input only, unused
    // GP4 = DIP Switch 1 input
    // GP5 = DIP Switch 2 input
    TRISIO = 0b11111110;

    GPIO = 0x00;

    RELAY_TRIS = 0;
    Relay_Off();

    DIP1_TRIS = 1;
    DIP2_TRIS = 1;

    SDA_Release();
    SCL_Release();

    Timer0_Init_For_Delay();
}

// -----------------------------------------------------------------------------
// Main
// -----------------------------------------------------------------------------
void main(void)
{
    RTC_Time now;
    Reset_Mode selected_mode;

    uint8_t last_trigger_hour   = 0xFF;
    uint8_t last_trigger_minute = 0xFF;
    uint8_t last_trigger_date   = 0xFF;
    uint8_t last_trigger_month  = 0xFF;
    uint8_t last_trigger_year   = 0xFF;

    Init_Device();
    I2C_Init();

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
            // RTC write failed.
            // Check SDA, SCL, pull-ups, VCC, GND, and DS3231 address.
            Error_Blink_Fast_Forever();
        }
    }
#endif

    Delay_Seconds(1);

    if (!DS3231_ReadTime(&now))
    {
        // RTC read failed.
        // Check DS3231 wiring and I2C pull-up resistors.
        Error_Blink_Slow_Forever();
    }

    while (1)
    {
        selected_mode = Read_Selected_Mode();

        if (DS3231_ReadTime(&now))
        {
            if (Is_Scheduled_Restart_Time(selected_mode, &now))
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
                    Relay_Reset_Pulse();

                    last_trigger_hour   = now.hour;
                    last_trigger_minute = now.minute;
                    last_trigger_date   = now.date;
                    last_trigger_month  = now.month;
                    last_trigger_year   = now.year;
                }
            }
        }
        else
        {
            // RTC read failed during running.
            Error_Blink_Slow_Forever();
        }

        Delay_Approx_250ms();
    }
}