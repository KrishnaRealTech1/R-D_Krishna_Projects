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
#define TRIGGER_HOUR        10
#define TRIGGER_MINUTE      0
#define RELAY_ON_SECONDS    5

#define RELAY_ON_LEVEL      1
#define RELAY_OFF_LEVEL     0

// Keep this 1 only for RTC setting/testing.
// After RTC works, change to 0.
#define SET_RTC_TIME_ON_POWERUP  0

#if SET_RTC_TIME_ON_POWERUP
#define RTC_SET_SECOND      0
#define RTC_SET_MINUTE      26
#define RTC_SET_HOUR        16
#define RTC_SET_DAY_OF_WEEK 2
#define RTC_SET_DATE        12
#define RTC_SET_MONTH       5
#define RTC_SET_YEAR        26
#endif

// -----------------------------------------------------------------------------
// Pin mapping
// -----------------------------------------------------------------------------
#define RELAY_PIN           GPIObits.GP0
#define RELAY_TRIS          TRISIObits.TRISIO0

#define SDA_PIN             GPIObits.GP1
#define SDA_TRIS            TRISIObits.TRISIO1

#define SCL_PIN             GPIObits.GP2
#define SCL_TRIS            TRISIObits.TRISIO2

// -----------------------------------------------------------------------------
// DS3231 I2C address
// -----------------------------------------------------------------------------
#define DS3231_ADDR         0x68
#define DS3231_ADDR_WRITE   ((DS3231_ADDR << 1) | 0)
#define DS3231_ADDR_READ    ((DS3231_ADDR << 1) | 1)

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

// -----------------------------------------------------------------------------
// Timer0 delay
// -----------------------------------------------------------------------------
static void Timer0_Init_For_Delay(void)
{
    OPTION_REGbits.T0CS = 0;
    OPTION_REGbits.PSA  = 0;

    OPTION_REGbits.PS2  = 1;
    OPTION_REGbits.PS1  = 1;
    OPTION_REGbits.PS0  = 1;

    INTCONbits.T0IE = 0;
    INTCONbits.T0IF = 0;
}

static void Delay_Approx_65ms(void)
{
    TMR0 = 0;
    INTCONbits.T0IF = 0;

    while (INTCONbits.T0IF == 0)
    {
        // Wait
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

static void Delay_Approx_500ms(void)
{
    uint8_t i;

    for (i = 0; i < 8; i++)
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
// Debug blink using relay
// -----------------------------------------------------------------------------
static void Relay_Blink_Count(uint8_t count)
{
    uint8_t i;

    for (i = 0; i < count; i++)
    {
        Relay_On();
        Delay_Approx_250ms();

        Relay_Off();
        Delay_Approx_250ms();
    }

    Delay_Approx_1s();
}

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
        // Small delay
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
// Device initialization
// -----------------------------------------------------------------------------
static void Init_Device(void)
{
    ANSEL  = 0x00;
    ADCON0 = 0x00;
    CMCON0 = 0x07;

    OSCCONbits.IRCF = 0b110;
    OSCCONbits.SCS  = 0;

    // GP0 = relay output
    // GP1 = SDA
    // GP2 = SCL
    // GP3 = input only
    // GP4 = unused
    // GP5 = unused
    TRISIO = 0b11111110;

    GPIO = 0x00;

    RELAY_TRIS = 0;
    Relay_Off();

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

    uint8_t last_trigger_date  = 0xFF;
    uint8_t last_trigger_month = 0xFF;
    uint8_t last_trigger_year  = 0xFF;

    Init_Device();
    I2C_Init();

    // Startup indication: 2 blinks
    Relay_Blink_Count(2);

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
            // Most likely: SDA/SCL wiring, pull-up, VCC, GND, or wrong RTC module.
            Error_Blink_Fast_Forever();
        }

        // RTC write success indication: 3 blinks
        Relay_Blink_Count(3);
    }
#endif

    Delay_Seconds(2);

    if (!DS3231_ReadTime(&now))
    {
        // RTC read failed.
        Error_Blink_Slow_Forever();
    }

    // RTC read success indication: 4 blinks
    Relay_Blink_Count(4);

    while (1)
    {
        if (DS3231_ReadTime(&now))
        {
            if ((now.hour == TRIGGER_HOUR) && (now.minute == TRIGGER_MINUTE))
            {
                bool already_triggered_today;

                already_triggered_today =
                    (now.date  == last_trigger_date)  &&
                    (now.month == last_trigger_month) &&
                    (now.year  == last_trigger_year);

                if (!already_triggered_today)
                {
                    Relay_On();
                    Delay_Seconds(RELAY_ON_SECONDS);
                    Relay_Off();

                    last_trigger_date  = now.date;
                    last_trigger_month = now.month;
                    last_trigger_year  = now.year;
                }
            }
        }
        else
        {
            Error_Blink_Slow_Forever();
        }

        Delay_Approx_250ms();
    }
}