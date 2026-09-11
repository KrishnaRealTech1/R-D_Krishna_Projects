
/*****************************************************************************
*  Copyright Statement:
*  --------------------
*  This software is protected by Copyright and the information contained
*  herein is confidential. The software may not be copied and the information
*  contained herein may not be used or disclosed except with the written
*  permission of Quectel Co., Ltd. 2013
*
*****************************************************************************/
/*****************************************************************************
 *
 * Filename:
 * ---------
 *   main.c
 *
 * Project:
 * --------
 *   OpenCPU
 *
 * Description:
 * ------------
 *   This app demonstrates how to send AT command with RIL API, and transparently
 *   transfer the response through MAIN UART. And how to use UART port.
 *   Developer can program the application based on this example.
 * 
 ****************************************************************************/
#ifdef __CUSTOMER_CODE__
#include "custom_feature_def.h"
#include "ril.h"
#include "ril_sms.h"
#include "ril_util.h"
#include "ril_telephony.h"
#include "ql_stdlib.h"
#include "ql_error.h"
#include "ql_trace.h"
#include "ql_uart.h"
#include "ql_gpio.h"
#include "ql_gprs.h"
#include "ql_system.h"
#include "ql_adc.h"
#include "ql_eint.h"
#include "ql_timer.h"
#include "ql_time.h"
#include "ql_iic.h"
#include "ql_spi.h"
#include "ril_bluetooth.h"
#include "string.h"

#define DEBUG_ENABLE 1
#if DEBUG_ENABLE > 0
#define DEBUG_PORT  UART_PORT1
#define DBG_BUF_LEN   512
static char DBG_BUFFER[DBG_BUF_LEN];
#define APP_DEBUG(FORMAT,...) {\
    Ql_memset(DBG_BUFFER, 0, DBG_BUF_LEN);\
    Ql_sprintf(DBG_BUFFER,FORMAT,##__VA_ARGS__); \
    if (UART_PORT2 == (DEBUG_PORT)) \
    {\
        Ql_Debug_Trace(DBG_BUFFER);\
    } else {\
        Ql_UART_Write((Enum_SerialPort)(DEBUG_PORT), (u8*)(DBG_BUFFER), Ql_strlen((const char *)(DBG_BUFFER)));\
    }\
}
#else
#define APP_DEBUG(FORMAT,...) 
#endif


static void CallBack_UART_Hdlr(Enum_SerialPort port, Enum_UARTEventType msg, bool level, void* customizedPara);
static s32 ATResponse_Handler(char* line, u32 len, void* userData);
static void BT_Callback(s32 event, s32 errCode, void* param1, void* param2);



#define SERIAL_RX_BUFFER_LEN  2048
#define MQTT_CONTEXT         0


typedef struct{
u8 index;
u8* prefix;
s32 data;
u32 length;
}MQTT_Param;
static MQTT_Param mqtt_param;

/************************************************************************/
/* Definition for GPRS PDP context                                      */
/************************************************************************/
static ST_GprsConfig m_GprsConfig = {
    "www",    // APN name
    "",         // User name for APN
    "",         // Password for APN
    0,
    NULL,
    NULL,
};

static ST_BT_BasicInfo pSppRecHdl;
static char pinCode[BT_PIN_LEN] = {0};
static ST_BT_DevInfo cur_btdev = {0};
static ST_BT_DevInfo BTSppDev1 = {0};
static ST_BT_DevInfo BTSppDev2 = {0};
static u8 btbuf[1024];

ST_Time time;
ST_Time* pTime = NULL;
u64 totalSeconds;

char version[]="V1.0";

#define TIMEOUT_COUNT 2
static u32 Stack_timer = 0x102; // timerId =99; timerID is Specified by customer, but must ensure the timer id is unique in the opencpu task
static u32 ST_Interval = 1000;
static s32 m_param1 = 0;

static u32 GP_timer = 0x101;
static u32 GPT_Interval =1000;
static s32 m_param2 = 0;
u8 time1=0,time2=0,time3=0;
static void Timer_handler(u32 timerId, void* param);

#define FAST_REGISTER
Enum_PinName pinname = PINNAME_DTR;  // rf interrupt pin

#define RF_RES PINNAME_GPIO0
#define INDICATION PINNAME_GPIO1
#define BUZZ PINNAME_GPIO2
#define SIREN PINNAME_GPIO3
#define POWER_SENS PINNAME_GPIO4

u8 spi_usr_type = 1;

u8 nodeid=1;
u8 networkid=1;
u8 tonodeid=0;

u8 _address=0;
u8 _mode;
u8 payloadlen=0;
u8 ack_received=0;
u8 ack_requested=0;
u8 senderid=0;
u8 targetid=0;
u8 bat_per=0;

bool last_power=0,bat_flag=0;
int promiscuousmode=0;
char data[61];
u8 datalen;
s32 RSSI;
char send_data[15]={0};
u8 rf_int=0;
char d_t[19]={0};
char temp_mem[1100],devicename[20];

char id_read[20];
char z[5];
bool locflag=0;
bool security_flag=0;
bool pir_flag=0;
bool light_status=0;
bool light_flag=0;
bool bat_low=0;
bool entry_flag=0;
u8 okflag,waitcount = 0,msg_success=0;
bool sms_flag=0,gsm_initflag=0,sms_readflag=0;
bool btrcvflag=0;
bool btconflag=0;
bool rf_change = 0,rf_change2=0;
bool net_reg=0;
bool lcd_disp=0;
char display=0;
char apn_id=4;
char smsbuf[10];
char msgbuf[100],command[200];
char mobilenum[15],gsm_msg[200];
static u32 nMsgRef;
// Define the UART port and the receive data buffer
static Enum_SerialPort m_myUartPort  = UART_PORT1;
static u8 m_RxBuf_Uart1[SERIAL_RX_BUFFER_LEN],task_fota=0;
static char imei[20]={0},mqtt_live[1500]="mqtt started",tcpport[8]="1883",host[50]="rtsiot.com",apn[20]="",m_username[25]="rtsiot",
		m_password[25]="realAndroid7",gprsbuf[300]={0},mqtt_pubtopic[50],mqtt_subtopic[50],mqtt_client[50],fotaapn[30],fotaun[30],fotapw[30],fotaurl[100]="";
bool mqtt_rec_flag=0;
static u32 mqtt_port;
bool atflag=0;
int gprsflag=0;
bool gprs_ok=0;
static u32 ADC_CustomParam = 1;
u32 TIME_TEST= 0;
u32 mutex_id_serial= 0,mutex_id_lcd=0,mutex_id_eeprom=0,mutex_id_spi=0;
u8 touch_flag=0;
u8 sigstr=0;

u8 _addr;
u8 _displayfunction;
u8 _displaycontrol;
u8 _displaymode;
u8 _cols;
u8 _rows;
u8 _charsize;
u8 _backlightval;

// commands
#define LCD_CLEARDISPLAY 0x01
#define LCD_RETURNHOME 0x02
#define LCD_ENTRYMODESET 0x04
#define LCD_DISPLAYCONTROL 0x08
#define LCD_CURSORSHIFT 0x10
#define LCD_FUNCTIONSET 0x20
#define LCD_SETCGRAMADDR 0x40
#define LCD_SETDDRAMADDR 0x80

// flags for display entry mode
#define LCD_ENTRYRIGHT 0x00
#define LCD_ENTRYLEFT 0x02
#define LCD_ENTRYSHIFTINCREMENT 0x01
#define LCD_ENTRYSHIFTDECREMENT 0x00

// flags for display on/off control
#define LCD_DISPLAYON 0x04
#define LCD_DISPLAYOFF 0x00
#define LCD_CURSORON 0x02
#define LCD_CURSOROFF 0x00
#define LCD_BLINKON 0x01
#define LCD_BLINKOFF 0x00

// flags for display/cursor shift
#define LCD_DISPLAYMOVE 0x08
#define LCD_CURSORMOVE 0x00
#define LCD_MOVERIGHT 0x04
#define LCD_MOVELEFT 0x00

// flags for function set
#define LCD_8BITMODE 0x10
#define LCD_4BITMODE 0x00
#define LCD_2LINE 0x08
#define LCD_1LINE 0x00
#define LCD_5x10DOTS 0x04
#define LCD_5x8DOTS 0x00

// flags for backlight control
#define LCD_BACKLIGHT 0x08
#define LCD_NOBACKLIGHT 0x00

#define En 0x04  // Enable bit
#define Rw 0x02  // Read/Write bit
#define Rs 0x01  // Register select bit




void LiquidCrystal_I2C(u8 lcd_addr, u8 lcd_cols, u8 lcd_rows, u8 charsize);
void LiquidCrystal_I2C_begin();
void LiquidCrystal_I2C_clear();
void LiquidCrystal_I2C_home();
void LiquidCrystal_I2C_noDisplay();
void LiquidCrystal_I2C_display();
void LiquidCrystal_I2C_noBlink();
void LiquidCrystal_I2C_blink();
void LiquidCrystal_I2C_noCursor();
void LiquidCrystal_I2C_cursor();
void LiquidCrystal_I2C_scrollDisplayLeft();
void LiquidCrystal_I2C_scrollDisplayRight();
void LiquidCrystal_I2C_leftToRight();
void LiquidCrystal_I2C_rightToLeft();
void LiquidCrystal_I2C_noBacklight();
void LiquidCrystal_I2C_backlight();
bool LiquidCrystal_I2C_getBacklight();
void LiquidCrystal_I2C_autoscroll();
void LiquidCrystal_I2C_noAutoscroll();
void LiquidCrystal_I2C_createChar(u8, u8[]);
void LiquidCrystal_I2C_setCursor(u8, u8);
u8 LiquidCrystal_I2C_write(u8);
void LiquidCrystal_I2C_command(u8);
void LiquidCrystal_I2C_blink_on() { LiquidCrystal_I2C_blink(); }
void LiquidCrystal_I2C_blink_off() { LiquidCrystal_I2C_noBlink(); }
void LiquidCrystal_I2C_cursor_on() { LiquidCrystal_I2C_cursor(); }
void LiquidCrystal_I2C_cursor_off() { LiquidCrystal_I2C_noCursor(); }
void LiquidCrystal_I2C_send(u8, u8);
void LiquidCrystal_I2C_write4bits(u8);
void LiquidCrystal_I2C_expanderWrite(u8);
void LiquidCrystal_I2C_pulseEnable(u8);


///////////////////////////////////////////////////////////////////////////////////////////////

long map(long x, long in_min, long in_max, long out_min, long out_max)
{
  return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
}

u8 constrain(int x,int a,int b)
{
	if((x>a)&&(x<b))
	{
		return x;
	}
	else if(x<a)
	{
		return a;
	}
	else
	{
		return b;
	}
}


void buzz()
{
	Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
	Ql_Sleep(300);
	Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
	Ql_Sleep(300);
	Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
	Ql_Sleep(200);
	Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
	Ql_Sleep(200);
	Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
	Ql_Sleep(100);
	Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
}

/////////////////////////////////////////////////////////////////////////////////////////////////
void LiquidCrystal_I2C(u8 lcd_addr, u8 lcd_cols, u8 lcd_rows, u8 charsize)
{
   _addr = lcd_addr;
   _cols = lcd_cols;
   _rows = lcd_rows;
   _charsize = charsize;
   _backlightval = LCD_BACKLIGHT;
}
void LiquidCrystal_I2C_begin()
{

   _displayfunction = LCD_4BITMODE | LCD_1LINE | LCD_5x8DOTS;

   if (_rows > 1) {
      _displayfunction |= LCD_2LINE;
   }

   // for some 1 line displays you can select a 10 pixel high font
   if ((_charsize != 0) && (_rows == 1)) {
      _displayfunction |= LCD_5x10DOTS;
   }

   // SEE PAGE 45/46 FOR INITIALIZATION SPECIFICATION!
   // according to datasheet, we need at least 40ms after power rises above 2.7V
   // before sending commands. Arduino can turn on way befer 4.5V so we'll wait 50
   Ql_Sleep(50);

   // Now we pull both RS and R/W low to begin commands
   LiquidCrystal_I2C_expanderWrite(_backlightval);   // reset expanderand turn backlight off (Bit 8 =1)
   Ql_Sleep(1000);

   //put the LCD into 4 bit mode
   // this is according to the hitachi HD44780 datasheet
   // figure 24, pg 46

   // we start in 8bit mode, try to set 4 bit mode
   LiquidCrystal_I2C_write4bits(0x03 << 4);
   Ql_Sleep(5); // wait min 4.1ms

   // second try
   LiquidCrystal_I2C_write4bits(0x03 << 4);
   Ql_Sleep(5); // wait min 4.1ms

   // third go!
   LiquidCrystal_I2C_write4bits(0x03 << 4);
   Ql_Sleep(1);

   // finally, set to 4-bit interface
   LiquidCrystal_I2C_write4bits(0x02 << 4);

   // set # lines, font size, etc.
   LiquidCrystal_I2C_command(LCD_FUNCTIONSET | _displayfunction);

   // turn the display on with no cursor or blinking default
   _displaycontrol = LCD_DISPLAYON | LCD_CURSOROFF | LCD_BLINKOFF;
   LiquidCrystal_I2C_display();

   // clear it off
   LiquidCrystal_I2C_clear();

   // Initialize to default text direction (for roman languages)
   _displaymode = LCD_ENTRYLEFT | LCD_ENTRYSHIFTDECREMENT;

   // set the entry mode
   LiquidCrystal_I2C_command(LCD_ENTRYMODESET | _displaymode);

   LiquidCrystal_I2C_home();
}

/********** high level commands, for the user! */
void LiquidCrystal_I2C_clear(){
	LiquidCrystal_I2C_command(LCD_CLEARDISPLAY);// clear display, set cursor position to zero
   Ql_Sleep(2);  // this command takes a long time!
}

void LiquidCrystal_I2C_home(){
	LiquidCrystal_I2C_command(LCD_RETURNHOME);  // set cursor position to zero
   Ql_Sleep(2);  // this command takes a long time!
}

void LiquidCrystal_I2C_setCursor(u8 col, u8 row){
   int row_offsets[] = { 0x00, 0x40, 0x14, 0x54 };
   if (row > _rows) {
      row = _rows-1;    // we count rows starting w/0
   }
   LiquidCrystal_I2C_command(LCD_SETDDRAMADDR | (col + row_offsets[row]));
}

// Turn the display on/off (quickly)
void LiquidCrystal_I2C_noDisplay() {
   _displaycontrol &= ~LCD_DISPLAYON;
   LiquidCrystal_I2C_command(LCD_DISPLAYCONTROL | _displaycontrol);
}
void LiquidCrystal_I2C_display() {
   _displaycontrol |= LCD_DISPLAYON;
   LiquidCrystal_I2C_command(LCD_DISPLAYCONTROL | _displaycontrol);
}

// Turns the underline cursor on/off
void LiquidCrystal_I2C_noCursor() {
   _displaycontrol &= ~LCD_CURSORON;
   LiquidCrystal_I2C_command(LCD_DISPLAYCONTROL | _displaycontrol);
}
void LiquidCrystal_I2C_cursor() {
   _displaycontrol |= LCD_CURSORON;
   LiquidCrystal_I2C_command(LCD_DISPLAYCONTROL | _displaycontrol);
}

// Turn on and off the blinking cursor
void LiquidCrystal_I2C_noBlink() {
   _displaycontrol &= ~LCD_BLINKON;
   LiquidCrystal_I2C_command(LCD_DISPLAYCONTROL | _displaycontrol);
}
void LiquidCrystal_I2C_blink() {
   _displaycontrol |= LCD_BLINKON;
   LiquidCrystal_I2C_command(LCD_DISPLAYCONTROL | _displaycontrol);
}

// These commands scroll the display without changing the RAM
void LiquidCrystal_I2C_scrollDisplayLeft(void) {
	LiquidCrystal_I2C_command(LCD_CURSORSHIFT | LCD_DISPLAYMOVE | LCD_MOVELEFT);
}
void LiquidCrystal_I2C_scrollDisplayRight(void) {
	LiquidCrystal_I2C_command(LCD_CURSORSHIFT | LCD_DISPLAYMOVE | LCD_MOVERIGHT);
}

// This is for text that flows Left to Right
void LiquidCrystal_I2C_leftToRight(void) {
   _displaymode |= LCD_ENTRYLEFT;
   LiquidCrystal_I2C_command(LCD_ENTRYMODESET | _displaymode);
}

// This is for text that flows Right to Left
void LiquidCrystal_I2C_rightToLeft(void) {
   _displaymode &= ~LCD_ENTRYLEFT;
   LiquidCrystal_I2C_command(LCD_ENTRYMODESET | _displaymode);
}

// This will 'right justify' text from the cursor
void LiquidCrystal_I2C_autoscroll(void) {
   _displaymode |= LCD_ENTRYSHIFTINCREMENT;
   LiquidCrystal_I2C_command(LCD_ENTRYMODESET | _displaymode);
}

// This will 'left justify' text from the cursor
void LiquidCrystal_I2C_noAutoscroll(void) {
   _displaymode &= ~LCD_ENTRYSHIFTINCREMENT;
   LiquidCrystal_I2C_command(LCD_ENTRYMODESET | _displaymode);
}

// Allows us to fill the first 8 CGRAM locations
// with custom characters
void LiquidCrystal_I2C_createChar(u8 location, u8 charmap[]) {
   location &= 0x7; // we only have 8 locations 0-7
   LiquidCrystal_I2C_command(LCD_SETCGRAMADDR | (location << 3));
   for (int i=0; i<8; i++) {
	   LiquidCrystal_I2C_write(charmap[i]);
   }
}

// Turn the (optional) backlight off/on
void LiquidCrystal_I2C_noBacklight(void) {
   _backlightval=LCD_NOBACKLIGHT;
   LiquidCrystal_I2C_expanderWrite(0);
}

void LiquidCrystal_I2C_backlight(void) {
   _backlightval=LCD_BACKLIGHT;
   LiquidCrystal_I2C_expanderWrite(0);
}
bool LiquidCrystal_I2C_getBacklight() {
  return _backlightval == LCD_BACKLIGHT;
}


/*********** mid level commands, for sending data/cmds */

void LiquidCrystal_I2C_command(u8 value) {
	LiquidCrystal_I2C_send(value, 0);
}

u8 LiquidCrystal_I2C_write(u8 value) {
	LiquidCrystal_I2C_send(value, Rs);
   return 1;
}


/************ low level data pushing commands **********/

// write either command or data
void LiquidCrystal_I2C_send(u8 value, u8 mode) {
   u8 highnib=value&0xf0;
   u8 lownib=(value<<4)&0xf0;
   LiquidCrystal_I2C_write4bits((highnib)|mode);
   LiquidCrystal_I2C_write4bits((lownib)|mode);
}

void LiquidCrystal_I2C_write4bits(u8 value) {
	LiquidCrystal_I2C_expanderWrite(value);
	LiquidCrystal_I2C_pulseEnable(value);
}

void LiquidCrystal_I2C_expanderWrite(u8 _data){

	u8 lcd_write[3];
	s32 ret;
	lcd_write[0] = (((int)(_data) )| _backlightval);

	ret=Ql_IIC_Write(1,_addr,lcd_write,1);
	Ql_Sleep(1);
	if(ret < 0)
	{
		Ql_OS_TakeMutex(mutex_id_serial);
		APP_DEBUG("\r\n<--iic lcd failed\r\n");
		Ql_OS_GiveMutex(mutex_id_serial);
	}


}

void LiquidCrystal_I2C_pulseEnable(u8 _data){
	LiquidCrystal_I2C_expanderWrite(_data | En);   // En high
   Ql_Sleep(1);   // enable pulse must be >450ns

   LiquidCrystal_I2C_expanderWrite(_data & ~En);   // En low
   Ql_Sleep(1);     // commands need > 37us to settle
}

void LiquidCrystal_I2C_printstr(char _data[])
{
	int len=Ql_strlen(_data);
	Ql_OS_TakeMutex(mutex_id_serial);
	APP_DEBUG("\r\n<--len=%d----->\r\n",len);
	Ql_OS_GiveMutex(mutex_id_serial);
	for(u8 i=0;i<len;i++)
	{
		if(_data[i]!=NULL)
		{
			LiquidCrystal_I2C_write((int)_data[i]);
		}

	}
}
void LiquidCrystal_I2C_printchar(char _data)
{

	LiquidCrystal_I2C_write((int)_data);

}
void LiquidCrystal_I2C_printnstr(char _data[],u8 len)
{

	for(u8 i=0;i<len;i++)
	{

			LiquidCrystal_I2C_write((int)_data[i]);


	}
}
////////////////////////////////////////////////////////////////////////////////////////////////

/*****************************************************************
* callback function
******************************************************************/

static void callback_eint_handle(Enum_PinName eintPinName, Enum_PinLevel pinLevel, void* customParam)
{
    s32 ret;
    //mask the specified EINT pin.
    Ql_EINT_Mask(pinname);

    APP_DEBUG("<--Eint callback: pin(%d), levle(%d)-->\r\n",eintPinName,pinLevel);
    ret = Ql_EINT_GetLevel(eintPinName);
    APP_DEBUG("<--Get Level, pin(%d), levle(%d)-->\r\n",eintPinName,ret);
    if(ret==1)
    {
    	rf_int=1;
    }
    //unmask the specified EINT pin
    Ql_EINT_Unmask(pinname);
}


///////////////////////////////////////////////////////////////////////////////////////////////////
void spi_flash_cs(bool CS)
{
	if (!spi_usr_type)
	{
		if (CS)
			Ql_GPIO_SetLevel(PINNAME_PCM_CLK,PINLEVEL_HIGH);
		else
			Ql_GPIO_SetLevel(PINNAME_PCM_CLK,PINLEVEL_LOW);
	}
}

void writereg(u8 addr,u8 data)
{
	s32 ret;
	u8 wr_buff[3];

	wr_buff[0] =addr|(0x80);
	wr_buff[1]=data;

	Ql_OS_TakeMutex(mutex_id_spi);
	spi_flash_cs(0);
	ret =Ql_SPI_Write(1, wr_buff, 2);
	spi_flash_cs(1);
	Ql_OS_GiveMutex(mutex_id_spi);

	 Ql_Sleep(10);

}
u8 readreg(u8 addr)
{
	u8 rd_buff[1];
	u8 rd[1];

	rd_buff[0]=addr;

	rd[0]=0;

	Ql_OS_TakeMutex(mutex_id_spi);
	spi_flash_cs(0);
	Ql_SPI_WriteRead(1,rd_buff,1,rd,1);
	spi_flash_cs(1);
	Ql_OS_GiveMutex(mutex_id_spi);

	//APP_DEBUG("\r\n<-- Ql_SPI_READ  =%d -->\r\n",rd[0]);
	//Ql_Sleep(10);
	return rd[0];
}

void initialise_rfm(void)
{
		APP_DEBUG("initial\r\n");
		time1=0;
	   do
	   {

		   writereg(0x2F,0xAA);  //////sync value
		   APP_DEBUG("sync value");

	   }while(((readreg(0x2F))!=(0xaa))&&(time1<=3));

	   time1=0;
	   do
	   {
		   writereg(0x2F,0x55);  //////sync value
	   }
	   while(((readreg(0x2F))!=(0x55))&&(time1<=3));

	   writereg(0x01,(0x00|0x00|0x04));
	   writereg(0x02,0x00);
	   writereg(0x03,0x02);
	   writereg(0x04,0x40);
	   writereg(0x05,0x03);
	   writereg(0x06,0x33);
	   writereg(0x07,(u8)0x6c);
	   writereg(0x08,(u8)0x80);
	   writereg(0x09,(u8)0x00);
	  // writereg(0x11,(0x80|0x00|0x00|0x1f));
	  // writereg(0x13,(0x1A|0x0A));
	   writereg(0x19,(0x40|0x00|0x02));
	   writereg(0x25,0x40);
	   writereg(0x26,0x07);
	   writereg(0x28,0x10);
	   writereg(0x29,0xDC);
	  // writereg(0x2D,0x03);
	   writereg(0x2E,(0x80|0x00|0x08|0x00));
	   writereg(0x2F,0x2D);
	   writereg(0x30,networkid);
	   writereg(0x37,(0x80|0x00|0x10|0x00|0x02));
	   writereg(0x38,0x66);
	   writereg(0x39,nodeid);
	   writereg(0x3C,(0x80|0x0F));
	   writereg(0x3D,(0x10|0x02|0x00));
	   writereg(0x6F,0x30);
	   writereg(0xFF,0x00);


	   writereg(0x13,0x0F);
	   writereg(0x11,(readreg(0x11)&0x1F)|0x40|0x20);
	   set_mode(1);
	   time1=0;
	   while(((readreg(0x27)&0x80)==0)&&(time1<=3));
	   _address=nodeid;
	   APP_DEBUG("initial\r\n");
}

void set_mode(u8 newmode)
{
   if(newmode==_mode)
   return;
   switch(newmode)
   {
      case 4:     //tx mode
         writereg(0x01,(readreg(0x01)&0xE3)|0x0C);
         sethigh_powerregs(1);
         break;

      case 3:      //rx mode
         writereg(0x01,(readreg(0x01)&0xE3)|0x10);
         sethigh_powerregs(0);
         break;

      case 2:      //synth mode
         writereg(0x01,(readreg(0x01)&0xE3)|0x08);
         break;

      case 1:        //standby mode
         writereg(0x01,(readreg(0x01)&0xE3)|0x04);
         break;

      case 0:        // sleep mode
         writereg(0x01,(readreg(0x01)&0xE3)|0x00);
         break;

      default:
      return;
   }
   while((_mode==0)&&(readreg(0x27)&0x80)==0x00);
   _mode=newmode;
   readreg(0x01);
}

void sethigh_powerregs(u8 onoff)
{
   writereg(0x5A,onoff? 0x5D : 0x55);
   writereg(0x5C,onoff? 0x7C : 0x70);
}

void setpowerlevel(u8 powerlevel)
{
   powerlevel=(powerlevel>31?31:powerlevel);
   powerlevel/=2;
   writereg(0x11,(readreg(0x11)&0xE0)|powerlevel);
}

int cansend()
{
   if((_mode==3) && (payloadlen==0))
   {
      set_mode(1);
      return 1;
   }
   return 0;
}

void _send(u8 toaddress,char *buffer,u8 buffersize,int requestack)
{
   writereg(0x3D,(readreg(0x3D)&0xFB)|0x04);
   time1=0;
   while(!cansend()&&time1<=3)
   {
   receivedone();
   }

   sendframe(toaddress,buffer,buffersize,requestack,0);
}
int ackreceived(u8 fromnodeid)
{
   if(receivedone())
   return ((senderid==fromnodeid||fromnodeid==255) && ack_received);
   return 0;
}
int ackrequested()
{
   return ack_requested && (targetid!=255);
}

void sendack(char *buffer,u8 buffersize)
{
   ack_requested=0;
   u8 sender=senderid;
   int _rssi=RSSI;
   writereg(0x3D,(readreg(0x3D)&0xFB)|0x04);
   while(!cansend())
   receivedone();
   senderid=sender;
   sendframe(sender,buffer,buffersize,0,1);
   RSSI=_rssi;
}


void sendframe(u8 toaddress,char *buffer,u8 buffersize,int requestack,int sendack)
{

	APP_DEBUG("sending\r\n");
	for(u8 K=0;K<buffersize;K++)
	  APP_DEBUG("data=%c\r\n",buffer[K]);

   set_mode(1);
   while((readreg(0x27)&0x80)==0x00);
   writereg(0x25,0x00);
   if(buffersize>61)
   buffersize=61;

   u8 ctlbyte=0x00;
   if(sendack)
   ctlbyte=0x80;
   else if(requestack)
   ctlbyte=0x40;
	char wr_buff[255];

	wr_buff[0] =(0x00)|(0x80);
	wr_buff[1]=buffersize+3;
	wr_buff[2]=toaddress;
	wr_buff[3]=_address;
	wr_buff[4]=ctlbyte;
	for(u8 i=0;i<buffersize;i++)
	wr_buff[5+i]=buffer[i];

	spi_flash_cs(0);
	Ql_SPI_Write(1, wr_buff, buffersize+5);
	spi_flash_cs(1);

   set_mode(4);
   while(Ql_EINT_GetLevel(pinname)==0)
   Ql_Sleep(1000);
   set_mode(1);

}

void interrupthandler()
{

    u8 rd[255];
    u8 rd_buff[2];

    APP_DEBUG("mode=%d\r\n",_mode);
    if((_mode==3)&&(readreg(0x28)&(0x04)))
   {
	  APP_DEBUG("interrupt hand\r\n");
      set_mode(1);


      rd_buff[0]=0;

      spi_flash_cs(0);
      Ql_SPI_WriteRead(1,rd_buff,1,rd,16);
      spi_flash_cs(1);
      APP_DEBUG("RD[0]=%d\r\n",rd[0]);
      payloadlen=rd[0];
      payloadlen=payloadlen>66?66:payloadlen;
      targetid=rd[1];
      APP_DEBUG("payloadlen=%d\r\n",payloadlen);
      if((payloadlen<3))
      {
         payloadlen=0;
         receivebegin();
         return;
      }
      datalen=payloadlen-3;
      senderid=rd[2];
      u8 ctlbyte=rd[3];
      ack_received=ctlbyte&0x80;
      ack_requested=ctlbyte&0x40;
      for(int i=0;i<datalen;i++)
      {
         data[i]=rd[4+i];
      }
      if(datalen<61)
         data[datalen]=0;

      set_mode(3);
      for(int i=0;i<datalen;i++)
	  {
    	  APP_DEBUG("i=%d,data=%c\r\n",i,data[i]);
	  }

   }
   RSSI=readrssi(0);
}

void receivebegin(void)
{
	//APP_DEBUG("receive begin\r\n");
   datalen=0;
   senderid=0;
   targetid=0;
   payloadlen=0;
   ack_requested=0;
   ack_received=0;
   RSSI=0;
   if(readreg(0x28)&0x04)
      writereg(0x3D,(readreg(0x3D)&0xFB)|0x04);
   writereg(0x25,0x40);
   set_mode(3);
}

int receivedone(void)
{
	//Ql_EINT_Mask(pinname);

    if(_mode==3 && payloadlen>0)
    {
      set_mode(1);
     // Ql_EINT_Unmask(pinname);
      return 1;
    }
    else if(_mode==3)
    {
    	// Ql_EINT_Unmask(pinname);
      return 0;
    }
    receivebegin();
    return 0;
}
s32 readrssi(int forcetrigger)
{
   s32 rssi=0;
   if(forcetrigger)
   {
      writereg(0x23,0x01);
      while((readreg(0x23)&0x02)==0x00);
   }
   rssi=0-readreg(0x24);
   rssi>>=1;
   return rssi;
}
///////////////////////////////////////////////////////////////////////////////////////////////////
u8 touch_to_int()
{
	if(!strncasecmp(m_RxBuf_Uart1,"*1#",3))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 1;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*2#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 2;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*3#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 3;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*4#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 4;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*5#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 5;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*6#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 6;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*7#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 7;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*8#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 8;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*9#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(300);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 9;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*10#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 10;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*11#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 11;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*12#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 12;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*13#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 13;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*14#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 14;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*15#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 15;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*16#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 16;
	}
	else
	{
		return 0;
	}
}
s8 get_number()
{
	if(!strncasecmp(m_RxBuf_Uart1,"*1#",3))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 1;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*2#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 2;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*3#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 3;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*5#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 4;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*6#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 5;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*7#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 6;
	}

	else if(!strncasecmp(m_RxBuf_Uart1,"*9#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 7;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*10#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 8;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*11#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 9;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*13#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 13;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*14#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 0;
	}
	else if(!strncasecmp(m_RxBuf_Uart1,"*15#",4))
	{
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
		Ql_Sleep(100);
		Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
		return 15;
	}

	else
	{
		return -1;
	}
}

u8 get_id()
{
	u8 count=0;
	for(u8 i=0;i<20;i++)
	{
		id_read[i]=NULL;
	}
	time1=0;
	while(count<6 && time1<10)
	{
		if(touch_flag==1)
		{
			touch_flag=0;
			time2=0;
			s8 num=get_number();

			if(num==15)
			{
				time1=10;
				time2=10;
				Ql_OS_TakeMutex(mutex_id_lcd);
				LiquidCrystal_I2C_clear();
				Ql_OS_GiveMutex(mutex_id_lcd);
			}
			else if(num==13)
			{
				time1=0;
				count--;
				id_read[count]='_';

				Ql_OS_TakeMutex(mutex_id_lcd);
				LiquidCrystal_I2C_setCursor(0,1);
				LiquidCrystal_I2C_printstr(id_read);
				Ql_OS_GiveMutex(mutex_id_lcd);
			}
			else if(num>=0)
			{
				time1=0;
				id_read[count++]=(num+48);

				Ql_OS_TakeMutex(mutex_id_lcd);
				LiquidCrystal_I2C_setCursor(0,1);
				LiquidCrystal_I2C_printstr(id_read);
				Ql_OS_GiveMutex(mutex_id_lcd);
			}
		}
		Ql_Sleep(10);
	}

		return count;
}


void eeprom_write(u32 address,u8 data)
{
	u8 eep_write[3];
	s32 ret;

	eep_write[0] =(address) >> 8;
	eep_write[1] =address;
	eep_write[2] =data;

	Ql_OS_TakeMutex(mutex_id_eeprom);
	ret=Ql_IIC_Write(1,0xA0,eep_write,3);
	if(ret < 0)
	{
		APP_DEBUG("\r\n<--iic failed\r\n");
	}
	Ql_OS_GiveMutex(mutex_id_eeprom);

	Ql_Sleep(10);
}

u8 eeprom_read(u32 address)
{
	u8 eep_read[2];
	u8 eep_read1[1];
	s32 ret;

	eep_read1[0]=0;

	eep_read[0] = (address) >> 8;
	eep_read[1] = address;

	Ql_OS_TakeMutex(mutex_id_eeprom);
	ret = Ql_IIC_Write_Read(1, 0xA0, eep_read, 2,eep_read1, 1);
	if(ret < 0)
	{
		APP_DEBUG("\r\n<--iic failed\r\n");
	}
	Ql_OS_GiveMutex(mutex_id_eeprom);

	//APP_DEBUG("<-- EEPROM READ type=%d\r\n",eep_read1[0]);
	Ql_Sleep(10);
	return eep_read1[0];
}
///////////////////////////////////////////////////////////////////////////////////////////////////
void wait_ok(void)
{
   while((okflag == 0) && (waitcount <= 10))
   {
      waitcount++;
      Ql_Sleep(100);
   }
   okflag = 2;
}

void del_msg(void)
{
   okflag = waitcount = 0;
   Ql_RIL_SendATCmd("AT+CMGD=1,4",Ql_strlen("AT+CMGD=1,4"),ATResponse_Handler,NULL,0);
   wait_ok();
}

void sms_text(void)
{
   okflag = waitcount = 0;
   Ql_RIL_SendATCmd("AT+CMGF=1",Ql_strlen("AT+CMGF=1"),ATResponse_Handler,NULL,0);
   wait_ok();
}

void gsm_init(void)
{
   if(gsm_initflag == 1)
   {
	  gsm_initflag=0;
	  sms_text();
      del_msg();

      okflag = waitcount = 0;
      Ql_RIL_SendATCmd("AT+CREG?",Ql_strlen("AT+CREG?"),ATResponse_Handler,NULL,0);
      wait_ok();

      okflag = waitcount = 0;
      Ql_RIL_SendATCmd("AT+CSQ",Ql_strlen("AT+CSQ"),ATResponse_Handler,NULL,0);
      wait_ok();

      okflag = waitcount = 0;
      Ql_RIL_SendATCmd("AT+CBC",Ql_strlen("AT+CBC"),ATResponse_Handler,NULL,0);
      wait_ok();

      okflag = waitcount = 0;
      Ql_RIL_SendATCmd("AT+QSPN?",Ql_strlen("AT+QSPN?"),ATResponse_Handler,NULL,0);
      wait_ok();


      okflag = waitcount = 0;
      atflag = 1;
      Ql_RIL_SendATCmd("AT+GSN",Ql_strlen("AT+GSN"),ATResponse_Handler,NULL,0);
      wait_ok();

      okflag = waitcount = 0;

      Ql_RIL_SendATCmd("AT+CIMI",Ql_strlen("AT+CIMI"),ATResponse_Handler,NULL,0);
      wait_ok();

      okflag = waitcount = 0;
      Ql_RIL_SendATCmd("ATI",Ql_strlen("ATI"),ATResponse_Handler,NULL,0);
      wait_ok();

      okflag = waitcount = 0;
      Ql_RIL_SendATCmd("AT+CCLK?",Ql_strlen("AT+CCLK?"),ATResponse_Handler,NULL,0);
      wait_ok();

      time.year = 2000+(((int)d_t[0])-48)*10+(((int)d_t[1])-48);
      time.month =(((int)d_t[3])-48)*10+(((int)d_t[4])-48);
      time.day = (((int)d_t[6])-48)*10+(((int)d_t[7])-48);
      time.hour =(((int)d_t[9])-48)*10+(((int)d_t[10])-48);
      time.minute = (((int)d_t[12])-48)*10+(((int)d_t[13])-48);
      time.second = (((int)d_t[15])-48)*10+(((int)d_t[16])-48);
      time.timezone = 22; // +05:30, one digit expresses a quarter of an hour
      s32 ret = Ql_SetLocalTime(&time);
      APP_DEBUG("<-- Ql_SetLocalTime(%d.%02d.%02d %02d:%02d:%02d timezone=%02d)=%d -->\n\r",time.year, time.month, time.day, time.hour, time.minute, time.second, time.timezone, ret);


   }

}
char QSDK_Get_Str(char *src_string,  char *dest_string, unsigned char index)
{
    char SentenceCnt = 0;
    char ItemSum = 0;
    char ItemLen = 0, Idx = 0;
    char len = 0;
    unsigned int  i = 0;

    if (src_string ==NULL)
    {
        return FALSE;
    }
    len = Ql_strlen(src_string);
   for ( i = 0;  i < len; i++)
   {
      if (*(src_string + i) == ',')
      {
         ItemLen = i - ItemSum - SentenceCnt;
         ItemSum  += ItemLen;
            if (index == SentenceCnt )
            {
                if (ItemLen == 0)
                {
                    return FALSE;
                }
              else
                {
                    Ql_memcpy(dest_string, src_string + Idx, ItemLen);
                    *(dest_string + ItemLen) = '\0';
                    return TRUE;
                }
            }
         SentenceCnt++;
         Idx = i + 1;
      }
   }
    if (index == SentenceCnt && (len - Idx) != 0)
    {
        Ql_memcpy(dest_string, src_string + Idx, len - Idx);
        *(dest_string + len) = '\0';
        return TRUE;
    }
    else
    {
        return FALSE;
    }
}
static s32 ATResponse_mqtt_handler_test(char* line, u32 len, void* userdata)
{
   APP_DEBUG("***test handler--%s--%d***",line,len);
}
static s32 ATResponse_mqtt_handler(char* line, u32 len, void* userdata)
{
   APP_DEBUG("***mqtt handler--%s--%d--%s***",line,len,userdata);
    MQTT_Param *mqtt_param = (MQTT_Param *)userdata;
    char *head = Ql_RIL_FindString(line, len, mqtt_param->prefix); //continue wait
    if(head)
    {
            char strTmp[10];
            char* p1 = NULL;
            char* p2 = NULL;
            Ql_memset(strTmp, 0x0, sizeof(strTmp));
            p1 = Ql_strstr(head, ":");
            if (p1)
            {
                QSDK_Get_Str((p1+1),strTmp, mqtt_param->index);
             mqtt_param->data= Ql_atoi(strTmp);
            }
        return  RIL_ATRSP_SUCCESS;
    }

    head = Ql_RIL_FindString(line, len, "QMTSTAT");
   if(head)
   {
      return  2;
   }


    head = Ql_RIL_FindString(line, len, "OK");
    if(head)
    {
        return  RIL_ATRSP_CONTINUE;
    }
    head = Ql_RIL_FindString(line, len, "ERROR");
    if(head)
    {
        return  RIL_ATRSP_FAILED;
    }
    head = Ql_RIL_FindString(line, len, "+CME ERROR:");//fail
    if(head)
    {
        return  RIL_ATRSP_FAILED;
    }
    head = Ql_RIL_FindString(line, len, "+CMS ERROR:");//fail
    if(head)
    {
        return  RIL_ATRSP_FAILED;
    }
    return RIL_ATRSP_CONTINUE; //continue wait
}


static s32 ATResponse_mqtt_handler_pub(char* line, u32 len, void* userdata)
{
    u8 *head = NULL;
    u8 uCtrlZ = 0x1A;

   MQTT_Param *mqtt_param = (MQTT_Param *)userdata;

   head = Ql_RIL_FindString(line, len, "\r\n>");
    if(head)
    {
        Ql_RIL_WriteDataToCore (mqtt_live,Ql_strlen(mqtt_live));
        Ql_RIL_WriteDataToCore(&uCtrlZ,1);

        return RIL_ATRSP_CONTINUE;
    }
    head = Ql_RIL_FindString(line, len, "ERROR");
    if(head)
    {
        return  RIL_ATRSP_FAILED;
    }
    head = Ql_RIL_FindString(line, len, "OK");
    if(head)
    {
        return  RIL_ATRSP_SUCCESS;
    }
    head = Ql_RIL_FindString(line, len, "+CMS ERROR:");//fail
    if(head)
    {
        return  RIL_ATRSP_FAILED;
    }
    return RIL_ATRSP_CONTINUE; //continue wait
}

s32 RIL_MQTT_QMTOPEN( u8 connectID,u8* hostName, u32 port)
{
    s32 ret = RIL_AT_SUCCESS;
    char strAT[200];

    Ql_memset(strAT, 0, sizeof(strAT));
   mqtt_param.prefix="+QMTOPEN:";
   mqtt_param.index = 1;
    mqtt_param.data = 255;

    Ql_sprintf(strAT, "AT+QMTOPEN=%d,\"%s\",%d\n", connectID,hostName,port);
    ret = Ql_RIL_SendATCmd(strAT,Ql_strlen(strAT),ATResponse_mqtt_handler,(void* )&mqtt_param,0);
    APP_DEBUG("<-- Send AT:%s, ret = %d -->\r\n",strAT, ret);
    APP_DEBUG("QMTOPEN return---%s--%d--%d--",mqtt_param.prefix,mqtt_param.index,mqtt_param.data)
    if(RIL_AT_SUCCESS != ret)
    {
        APP_DEBUG("\r\n<-- send AT command failure -->\r\n");
        return ret;
    }
    else if(0 != mqtt_param.data)
    {
        APP_DEBUG("\r\n<--MQTT OPEN failure, =%d -->\r\n", mqtt_param.data);
        return QL_RET_ERR_RIL_MQTT_FAIL;
    }
    return ret;
}
s32 RIL_MQTT_QMTCONN(u8 connectID, u8* clientID,u8* username,u8* password)
{
    s32 ret = RIL_AT_SUCCESS;
    char strAT[200];

    Ql_memset(strAT, 0, sizeof(strAT));
    mqtt_param.prefix="+QMTCONN:";
   mqtt_param.index = 1;
    mqtt_param.data = 255;

   if(NULL != username && NULL !=password)
   {
      Ql_sprintf(strAT, "AT+QMTCONN=%d,\"%s\",\"%s\",\"%s\"\n", connectID,clientID,username,password);
   }
   else
   {
      Ql_sprintf(strAT, "AT+QMTCONN=%d,\"%s\"\n", connectID,clientID);
   }
   APP_DEBUG("<--befor Send AT:%s, ret = %d -->\r\n",strAT, ret);
    ret = Ql_RIL_SendATCmd(strAT,Ql_strlen(strAT),ATResponse_mqtt_handler,(void* )&mqtt_param,300000);
    APP_DEBUG("<-- Send AT:%s, ret = %d -->\r\n",strAT, ret);
    if(RIL_AT_SUCCESS != ret)
    {
        APP_DEBUG("\r\n<-- send AT command failure -->\r\n");
        return ret;
    }
    else if(0 != mqtt_param.data)
    {
        APP_DEBUG("\r\n<--MQTT connect failure, =%d -->\r\n", mqtt_param.data);
        return QL_RET_ERR_RIL_MQTT_FAIL;
    }

    return ret;
}

s32 RIL_MQTT_QMTPUB(u8 connectID, u8 msgId,u8 qos,u8 retain, u8* topic,u8* data,u8 length)
{
    s32 ret = RIL_AT_SUCCESS;
    char strAT[200];

    Ql_memset(strAT, 0, sizeof(strAT));
    mqtt_param.prefix=data;
   mqtt_param.length= length;
   Ql_sprintf(topic,"ihome/%s",imei);
   Ql_sprintf(strAT, "AT+QMTPUB=%d,%d,%d,%d,\"%s\"\n", connectID,msgId,qos,retain,topic);
    ret = Ql_RIL_SendATCmd(strAT,Ql_strlen(strAT),ATResponse_mqtt_handler_pub,(void* )&mqtt_param,0);
    APP_DEBUG("<-- Send AT:%s, ret = %d -->\r\n",strAT, ret);
    if(RIL_AT_SUCCESS != ret)
    {
        APP_DEBUG("\r\n<-- send AT command failure -->\r\n");
        return ret;
    }
    return ret;
}

s32 RIL_MQTT_QMTSUB(u8 connectID, u8 msgId,u8* topic, u8 qos,u8* others)
{
    s32 ret = RIL_AT_SUCCESS;
    char strAT[200];

   Ql_Sleep(1000);

    Ql_memset(strAT, 0, sizeof(strAT));
    mqtt_param.prefix="+QMTSUB:";
   mqtt_param.index = 2;
    mqtt_param.data = 255;

    Ql_sprintf(topic,"ihome/%s/sub",imei);
    Ql_sprintf(strAT, "AT+QMTSUB=%d,%d,\"%s\",%d\n", connectID,msgId,topic,qos);
    ret = Ql_RIL_SendATCmd(strAT,Ql_strlen(strAT),ATResponse_mqtt_handler,(void* )&mqtt_param,0);
    APP_DEBUG("<-- Send AT:%s, ret = %d -->\r\n",strAT, ret);
    if(RIL_AT_SUCCESS != ret)
    {
        APP_DEBUG("\r\n<-- send AT command failure -->\r\n");
        return ret;
    }
    else if(0 != mqtt_param.data)
    {
        APP_DEBUG("\r\n<--MQTT subscribe failure, =%d -->\r\n", mqtt_param.data);
        return QL_RET_ERR_RIL_MQTT_FAIL;
    }

    return ret;
}

s32 RIL_MQTT_QMTCLOSE(u8 connectID)
{
    s32 ret = RIL_AT_SUCCESS;
    char strAT[200];

    Ql_memset(strAT, 0, sizeof(strAT));
    Ql_sprintf(strAT, "AT+QMTCLOSE=%d\n",connectID);

    ret = Ql_RIL_SendATCmd(strAT,Ql_strlen(strAT),NULL,NULL,0);
    APP_DEBUG("<-- Send AT:%s, ret = %d -->\r\n",strAT, ret);
    if (RIL_AT_SUCCESS != ret)
    {
        return ret;
    }
    return ret;
}

s32 RIL_MQTT_QMTCFG_Ali( u8 connectID)//,u8* product_key,u8* device_name,u8* device_secret)
{
    s32 ret = RIL_AT_SUCCESS;
    char strAT[200];

    Ql_memset(strAT, 0, sizeof(strAT));
   Ql_sprintf(strAT, "AT+QMTCFG=\"VERSION\",%d,1",connectID);
   ret = Ql_RIL_SendATCmd(strAT,Ql_strlen(strAT),ATResponse_mqtt_handler_test,NULL,0);
   APP_DEBUG("<-- Send AT:%s, ret = %d -->\r\n",strAT, ret);

    if (RIL_AT_SUCCESS != ret)
    {
        return ret;
    }
    return ret;
}

void gprs_init()
{
	s32 ret;

		  ret = RIL_NW_SetGPRSContext(MQTT_CONTEXT);
	      APP_DEBUG("<-- Set GPRS context, ret=%d -->\r\n", ret);
	      if(ret != 0)
	         goto mqtt_deactivate; //mqtt_end;

	      ret = RIL_NW_SetAPN(1, apn, NULL,NULL);
	      APP_DEBUG("<-- Set GPRS APN, ret=%d APN-%s UN-%s PW-%s-->\r\n", ret,apn,m_username,m_password);

	      ret = RIL_NW_OpenPDPContext();
	      APP_DEBUG("<-- Open PDP context, ret=%d -->\r\n", ret);
	      if(ret != 0)
	         goto mqtt_deactivate; //mqtt_end;

	     // ret =  RIL_MQTT_QMTCFG_Ali(0);
	     // APP_DEBUG("<-- mqtt config, ret=%d -->\r\n", ret);
	     // if(ret != 0)
	     //    goto mqtt_deactivate; //mqtt_end;

	      mqtt_port = Ql_atoi(tcpport);
	      ret = RIL_MQTT_QMTOPEN( 0,host, mqtt_port) ;
	      APP_DEBUG("<-- mqtt open, ret=%d -->\r\n",ret);
	      if(ret != 0)
	         goto mqtt_deactivate;
	      Ql_sprintf(mqtt_client,"ihome/client-%s",imei);
	      ret = RIL_MQTT_QMTCONN(0,mqtt_client,m_username,m_password);
	      APP_DEBUG("<-- mqtt connect,ret=%d -->\r\n", ret);
	      if(ret != 0)
	         goto mqtt_deactivate;

	      Ql_sprintf(mqtt_subtopic,"ihome/%s/sub",imei);
	      ret = RIL_MQTT_QMTSUB(0, 1,mqtt_subtopic, 0,NULL);
	      APP_DEBUG("<-- mqtt subscribe, ret=%d -->\r\n",ret);
	      if(ret != 0)
	         goto mqtt_deactivate;

	      Ql_Sleep(1000);

	      Ql_sprintf(mqtt_pubtopic,"ihome/%s",imei);
	      ret = RIL_MQTT_QMTPUB(0, 0,0,0,mqtt_pubtopic,mqtt_live,Ql_strlen(mqtt_live));
	      APP_DEBUG("<-- mqtt publish, ret=%d Topic-%s \npacket-%s-->\r\n", ret,mqtt_pubtopic,mqtt_live);

	mqtt_deactivate:
	      ///mqtt_end:


	      if(ret != 0)
	      {
	         okflag = waitcount = 0;
	         APP_DEBUG("<-- AT+QIDEACT -->\r\n");
	         ret = Ql_RIL_SendATCmd("AT+QIDEACT",Ql_strlen("AT+QIDEACT"),ATResponse_Handler,NULL,0);
	         wait_ok();
	         //ret = RIL_NW_ClosePDPContext();
	         APP_DEBUG("<-- Set GPRS DEACTIVATE, ret=%d -->\r\n", ret);
	         gprs_ok=0;

	      }
	      if(ret != 0)
	      {
	         APP_DEBUG("MQTT INIT FAIL\n");
	      }
	      else
	      {
	    	  gprs_ok=1;
	      }

	    APP_DEBUG("< Finish >\r\n");
}

void bt_init(void)
{
		APP_DEBUG("BLUETOOTH INIT\n");

		okflag = waitcount = 0;
		Ql_RIL_SendATCmd("AT+QBTPWR=1",Ql_strlen("AT+QBTPWR=1"),ATResponse_Handler,NULL,0);
		wait_ok();

		okflag = waitcount = 0;
		Ql_RIL_SendATCmd("AT+QBTNAME?\0",Ql_strlen("AT+QBTNAME?\0"),ATResponse_Handler,NULL,0);
		wait_ok();

		okflag = waitcount = 0;
		Ql_RIL_SendATCmd("AT+QBTVISB=0",Ql_strlen("AT+QBTVISB=0"),ATResponse_Handler,NULL,0);
		wait_ok();

		okflag = waitcount = 0;
		Ql_RIL_SendATCmd("AT+QBTADDR?",Ql_strlen("AT+QBTADDR?"),ATResponse_Handler,NULL,0);
		wait_ok();

		s32 ret = RIL_BT_Initialize(BT_Callback);
		if(RIL_AT_SUCCESS != ret)
		{
				APP_DEBUG("BT initialization failed.\r\n");
				return;
		}
		APP_DEBUG("BT callback function register successful.\r\n");
}

void get_time()
{
	if((Ql_GetLocalTime(&time)))
	{
		Ql_OS_TakeMutex(mutex_id_serial);
		APP_DEBUG("\r\n<--Local time successfuly determined: %d.%i.%i %i:%i:%i timezone=%i-->\r\n", time.day, time.month, time.year, time.hour, time.minute, time.second,time.timezone);

		d_t[0]=((time.year%100)/10)+48;
		d_t[1]=((time.year%100)%10)+48;
		d_t[2]='/';

		d_t[3]=(time.month/10)+48;
		d_t[4]=(time.month%10)+48;
		d_t[5]='/';

		d_t[6]=(time.day/10)+48;
		d_t[7]=(time.day%10)+48;
		d_t[8]=' ';

		d_t[9]=(time.hour/10)+48;
		d_t[10]=(time.hour%10)+48;
		d_t[11]=':';

		d_t[12]=(time.minute/10)+48;
		d_t[13]=(time.minute%10)+48;
		d_t[14]=':';

		d_t[15]=(time.second/10)+48;
		d_t[16]=(time.second%10)+48;
		d_t[17]=';';

		APP_DEBUG("\r\n<--%s-------->\r\n",d_t);
		Ql_OS_GiveMutex(mutex_id_serial);
	}
}

void read_msg(void)
{

	 sms_text();
	 okflag = waitcount = 0;
	 Ql_memset(smsbuf,0,sizeof(smsbuf));
	 Ql_sprintf(smsbuf,"AT+CMGR=1");
	 Ql_RIL_SendATCmd(smsbuf,sizeof(smsbuf),ATResponse_Handler,NULL,0);
	 wait_ok();
     del_msg();

}
static void Callback_OnADCSampling(Enum_ADCPin adcPin, u32 adcValue, void *customParam)
{
	// APP_DEBUG("<-- Callback_OnADCSampling: sampling voltage(mV)=%d  times=%d -->\r\n", adcValue, *((s32*)customParam))
	  //  *((s32*)customParam) += 1;
	float batvolt=adcValue/1000.0;
	batvolt=(batvolt*37.7)/4.7;
	//APP_DEBUG("\r\n<--batvolt=%f",batvolt);
	bat_per=(batvolt/8.0)*100.0;

}


void device_add(void)
{
	Ql_OS_TakeMutex(mutex_id_lcd);
	LiquidCrystal_I2C_clear();
	LiquidCrystal_I2C_setCursor(0,0);
	LiquidCrystal_I2C_printstr("ENTER THE ID:");
	Ql_OS_GiveMutex(mutex_id_lcd);
	if(get_id()==6)
	{
		u32 i=0;
		u32 pos=0;
		for(i=0;i<280;i=i+31)
		{

			Ql_OS_TakeMutex(mutex_id_serial);
			APP_DEBUG("\r\n<--i=%d",i);
			Ql_OS_GiveMutex(mutex_id_serial);
			if(temp_mem[i]==0)
			{
				break;
			}
		}

		pos=i;
		temp_mem[i++]='D';
		temp_mem[i++]=(pos/31)+48;
		temp_mem[i++]='-';
		temp_mem[i++]='D';
		temp_mem[i++]='O';
		temp_mem[i++]='O';
		temp_mem[i++]='R';
		temp_mem[i++]=(pos/31)+48;
		temp_mem[pos+18]='*';

		for(u8 j=0;j<6;j++)
		{
			temp_mem[pos+19+j]=id_read[j];
		}
		temp_mem[pos+25]='#';

		Ql_OS_TakeMutex(mutex_id_lcd);
		LiquidCrystal_I2C_clear();
		LiquidCrystal_I2C_setCursor(0,0);
		LiquidCrystal_I2C_printstr("ADDED SUCCESS");
		LiquidCrystal_I2C_setCursor(0,1);
		for(u8 j=pos;j<(pos+10);j++)
		{
			LiquidCrystal_I2C_printchar(temp_mem[j]);
		}
		for(u8 j=(pos+19);j<(pos+26);j++)
		{
			LiquidCrystal_I2C_printchar(temp_mem[j]);
		}

		Ql_OS_GiveMutex(mutex_id_lcd);

		Ql_Sleep(10);
		for(u8 j=pos;j<(pos+31);j++)
		{
			eeprom_write(j,temp_mem[j]);
		}
		buzz();

	}
	touch_flag=0;
}
void device_delete(void)
{
	Ql_OS_TakeMutex(mutex_id_lcd);
	LiquidCrystal_I2C_clear();
	LiquidCrystal_I2C_setCursor(0,0);
	LiquidCrystal_I2C_printstr("ENTER DOOR NUMBER:");
	Ql_OS_GiveMutex(mutex_id_lcd);

	time1=0;
	while(touch_flag==0 && time1<10)
	{
		 Ql_Sleep(10);
	}
	touch_flag=0;
	if(time1<10)
	{
		u8 num=get_number();
		num=num+(num*30);
		if(temp_mem[num]!=NULL)
		{
			for(u8 j=0;j<31;j++)
			{
				temp_mem[j+num]=NULL;
				eeprom_write(j+num,NULL);
			}
			Ql_Sleep(100);
			Ql_OS_TakeMutex(mutex_id_lcd);
			LiquidCrystal_I2C_clear();
			LiquidCrystal_I2C_setCursor(0,0);
			LiquidCrystal_I2C_printstr("DELETED SUCCESS");
			Ql_OS_GiveMutex(mutex_id_lcd);
		}
		else
		{
			Ql_OS_TakeMutex(mutex_id_lcd);
			LiquidCrystal_I2C_clear();
			LiquidCrystal_I2C_setCursor(0,0);
			LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
			Ql_OS_GiveMutex(mutex_id_lcd);
		}
		buzz();
		touch_flag=0;
	}

}
void device_enable(void)
{
	Ql_OS_TakeMutex(mutex_id_lcd);
	LiquidCrystal_I2C_clear();
	LiquidCrystal_I2C_setCursor(0,0);
	LiquidCrystal_I2C_printstr("ENTER DOOR NUMBER:");
	Ql_OS_GiveMutex(mutex_id_lcd);
	time1=0;
	while(touch_flag==0 && time1<10)
	{
		 Ql_Sleep(10);
	}
	touch_flag=0;
	if(time1<10)
	{
		u8 num=get_number();
		num=num+(num*30);
		if(temp_mem[num]!=NULL)
		{
			temp_mem[num+27]='E';
			eeprom_write(num+27,'E');
			Ql_Sleep(100);
			Ql_OS_TakeMutex(mutex_id_lcd);
			LiquidCrystal_I2C_clear();
			LiquidCrystal_I2C_setCursor(0,0);
			LiquidCrystal_I2C_printstr("ENABLED SUCCESS");
			Ql_OS_GiveMutex(mutex_id_lcd);
		}
		else
		{
			Ql_OS_TakeMutex(mutex_id_lcd);
			LiquidCrystal_I2C_clear();
			LiquidCrystal_I2C_setCursor(0,0);
			LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
			Ql_OS_GiveMutex(mutex_id_lcd);
		}
		buzz();

		touch_flag=0;
	}

}
void device_disable(void)
{
	Ql_OS_TakeMutex(mutex_id_lcd);
	LiquidCrystal_I2C_clear();
	LiquidCrystal_I2C_setCursor(0,0);
	LiquidCrystal_I2C_printstr("ENTER DOOR NUMBER:");
	Ql_OS_GiveMutex(mutex_id_lcd);
	time1=0;
	while(touch_flag==0 && time1<10)
	{
		 Ql_Sleep(10);
	}
	touch_flag=0;
	if(time1<10)
	{
		u8 num=get_number();
		num=num+(num*30);
		if(temp_mem[num]!=NULL)
		{
			temp_mem[num+27]='D';
			eeprom_write(num+27,'D');
			Ql_Sleep(100);
			Ql_OS_TakeMutex(mutex_id_lcd);
			LiquidCrystal_I2C_clear();
			LiquidCrystal_I2C_setCursor(0,0);
			LiquidCrystal_I2C_printstr("DISABLED SUCCESS");
			Ql_OS_GiveMutex(mutex_id_lcd);
		}
		else
		{
			Ql_OS_TakeMutex(mutex_id_lcd);
			LiquidCrystal_I2C_clear();
			LiquidCrystal_I2C_setCursor(0,0);
			LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
			Ql_OS_GiveMutex(mutex_id_lcd);
		}
		buzz();

		touch_flag=0;
	}
}


void door_status(int count,char *status)
{
			int i = 0;
			int l = 0,n=0,a1,k=0;
			float bat_vol1, bat_vol2;


				i = count + (count * 3 * 10);
				for (int s = 0; s < 30; s++)
				{
					status[s] = NULL;
				}
				for (n = 0; n < 18; n++)
				{
				  devicename[n] = temp_mem[i + n];
				  status[n] = devicename[n];
				  if (temp_mem[i + n + 1] == NULL)
					break;
				}
				status[2]='_';
				if (n < 18)
				  n++;

				status[n++] = '_';

				char a[3];

				a[0] = (char)temp_mem[i + 28];
				a[1] = (char)temp_mem[i + 29];
				a[2] = (char)temp_mem[i + 30];

				unsigned int decValue = 0;
				int nextInt;

				for (k = 0; k < 3; k++)
				{

				  nextInt = (int)(a[k]);
				  if (nextInt >= 48 && nextInt <= 57) nextInt = map(nextInt, 48, 57, 0, 9);
				  if (nextInt >= 65 && nextInt <= 70) nextInt = map(nextInt, 65, 70, 10, 15);
				  if (nextInt >= 97 && nextInt <= 102) nextInt = map(nextInt, 97, 102, 10, 15);
				  nextInt = constrain(nextInt, 0, 15);

				  decValue = (decValue * 16) + nextInt;
				}


				a1 = decValue;

				bat_vol1 = a1 * (1.024 / 1024.0);

				bat_vol2 = bat_vol1 * 3.2;


				l = bat_vol2 * 100;
				k = l / 100;
				z[0] = (char)(k + 48);

				k = (l % 100) / 10;
				z[1] = (char)(k + 48);
				k = l % 10;
				z[2] = (char)(k + 48);



			  if (temp_mem[i + 23] == '0' && temp_mem[i + 24] == '1')
			  {
				if (temp_mem[i + 26] == '1' || temp_mem[i + 26] == '0')
					status[n++] = temp_mem[i + 26];
				else
					status[n++] = '0';

			  }
			  else
			  {

				  if(temp_mem[i + 26]=='1')
					{
					  status[n++] = '1';

					}
					else
					{
						status[n++] = '0';
					}
			  }


			  status[n++] = '_';
			  status[n++] = z[0];
			  status[n++] = '.';
			  status[n++] = z[1];
			  status[n++] = '_';
			  status[n++] = temp_mem[i + 27] ;
			  status[n++] = '_';
			  for (k = 0; k < 6; k++, n++)
			  {
				  status[n] = temp_mem[i + 19 + k];
			  }
			  status[n++] = ',';

}
void light_status1(int count,char *status)
{
	int i = 0,k=0;
	i = count + (count * 30) + 620;
	int n = 0;

		for (int s = 0; s < 30; s++)
		{
			status[s] = NULL;
		}
		for (n = 0; n < 18; n++)
		{
		  devicename[n] = temp_mem[i + n];
		  status[n] = devicename[n];
		  if (temp_mem[i + n + 1] == NULL)
			break;
		}
		status[2]='_';
		if (n < 18)
		  n++;

		status[n++] = '_';


	  if (temp_mem[i + 26] == '1')
	  {

		  status[n++] = '1';

	  }
	  else
	  {

		  status[n++] = '0';

	  }
	  status[n++] = '_';
	  status[n++] = temp_mem[i + 27] ;
	  status[n++] = '_';
	  for (k = 0; k < 6; k++, n++)
	  {
		  status[n] = temp_mem[i + 19 + k];
	  }
	  status[n++] = ',';

}

static void ADC_Program(void)
{
    Enum_PinName adcPin = PIN_ADC0;

    // Register callback foR ADC
    APP_DEBUG("<-- Register callback for ADC -->\r\n")
    Ql_ADC_Register(adcPin, Callback_OnADCSampling, (void *)&ADC_CustomParam);

    // Initialize ADC (sampling count, sampling interval)
    APP_DEBUG("<-- Initialize ADC (sampling count=5, sampling interval=200ms) -->\r\n")
    Ql_ADC_Init(adcPin, 5, 200);

    // Start ADC sampling
    APP_DEBUG("<-- Start ADC sampling -->\r\n")
    Ql_ADC_Sampling(adcPin, TRUE);

    // Stop  sampling ADC
    //Ql_ADC_Sampling(adcPin, FALSE);
}
void proc_main_task(s32 taskId)
{
    s32 ret;
    ST_MSG msg;


    entry_flag=1;
    gsm_initflag=1;

    mutex_id_serial=Ql_OS_CreateMutex("serial");
    mutex_id_lcd=Ql_OS_CreateMutex("lcd");
    mutex_id_eeprom=Ql_OS_CreateMutex("eeprom");
    mutex_id_spi=Ql_OS_CreateMutex("spi");

    Ql_GPIO_Init(PINNAME_GPIO3,PINDIRECTION_OUT,PINLEVEL_LOW,PINPULLSEL_PULLDOWN);
	Ql_GPIO_Init(PINNAME_GPIO2,PINDIRECTION_OUT,PINLEVEL_LOW,PINPULLSEL_PULLDOWN);
	Ql_GPIO_Init(PINNAME_GPIO1,PINDIRECTION_OUT,PINLEVEL_LOW,PINPULLSEL_PULLDOWN);
	Ql_GPIO_Init(PINNAME_GPIO0,PINDIRECTION_OUT,PINLEVEL_LOW,PINPULLSEL_PULLDOWN);

	Ql_GPIO_Init(POWER_SENS,PINDIRECTION_IN,PINLEVEL_LOW,PINPULLSEL_PULLDOWN);

	Ql_GPIO_SetLevel(PINNAME_GPIO0,PINLEVEL_LOW);
	Ql_GPIO_SetLevel(PINNAME_GPIO1,PINLEVEL_LOW);
	Ql_GPIO_SetLevel(PINNAME_GPIO2,PINLEVEL_LOW);
	Ql_GPIO_SetLevel(PINNAME_GPIO3,PINLEVEL_LOW);

	int sens=Ql_GPIO_GetLevel(POWER_SENS);
    last_power=sens;

	// Register & open UART port
    ret = Ql_UART_Register(m_myUartPort, CallBack_UART_Hdlr, NULL);
    Ql_OS_TakeMutex(mutex_id_serial);
    if (ret < QL_RET_OK)
    {
        Ql_Debug_Trace("Fail to register serial port[%d], ret=%d\r\n", m_myUartPort, ret);
    }
    ret = Ql_UART_Open(m_myUartPort, 9600, FC_NONE);
    if (ret < QL_RET_OK)
    {
        Ql_Debug_Trace("Fail to open serial port[%d], ret=%d\r\n", m_myUartPort, ret);
    }
    ret = Ql_UART_Register(UART_PORT2, CallBack_UART_Hdlr, NULL);

	if (ret < QL_RET_OK)
	{
		Ql_Debug_Trace("Fail to register serial port[%d], ret=%d\r\n", UART_PORT2, ret);
	}
	ret = Ql_UART_Open(UART_PORT2, 9600, FC_NONE);
	if (ret < QL_RET_OK)
	{
		Ql_Debug_Trace("Fail to open serial port[%d], ret=%d\r\n", UART_PORT2, ret);
	}
    APP_DEBUG("OpenCPU: my Application\r\n");
    ADC_Program();

	#ifdef FAST_REGISTER
    /*************************************************************
    * Registers an EINT I/O, and specify the interrupt handler.
    *The EINT, that is registered by calling this function, is a lower-level interrupt.
    *The response for interrupt request is more timely.
    *Please don't add any task schedule in the interrupt handler.
    *And the interrupt handler cannot consume much CPU time.
    *Or it causes system exception or reset.
    **************************************************************/
    ret = Ql_EINT_RegisterFast(pinname,callback_eint_handle, NULL);
    #else
    //Registers an EINT I/O, and specify the interrupt handler.
    ret = Ql_EINT_Register(pinname,callback_eint_handle, NULL);
    #endif
    ret = Ql_EINT_Init(pinname, EINT_LEVEL_TRIGGERED, 0, 5,0);

    ret = Ql_Timer_Register(Stack_timer, Timer_handler, &m_param1);

	if(ret <0)
	{
		APP_DEBUG("\r\n<--failed!!, Ql_Timer_Register: timer(%d) fail ,ret = %d -->\r\n",Stack_timer,ret);
	}
	//APP_DEBUG("\r\n<--Register: timerId=%d, param = %d,ret = %d -->\r\n", Stack_timer ,m_param1,ret);

	//register  a GP-Timer
	ret = Ql_Timer_RegisterFast(GP_timer, Timer_handler, &m_param2);
	if(ret <0)
	{
		APP_DEBUG("\r\n<--failed!!, Ql_Timer_RegisterFast: GP_timer(%d) fail ,ret = %d -->\r\n",GP_timer,ret);
	}
	//APP_DEBUG("\r\n<--RegisterFast: timerId=%d, param = %d,ret = %d -->\r\n", GP_timer ,m_param1,ret);

	//start a timer,repeat=true;
	ret = Ql_Timer_Start(Stack_timer,ST_Interval,TRUE);
	if(ret < 0)
	{
		APP_DEBUG("\r\n<--failed!! stack timer Ql_Timer_Start ret=%d-->\r\n",ret);
	}
	//APP_DEBUG("\r\n<--stack timer Ql_Timer_Start(ID=%d,Interval=%d,) ret=%d-->\r\n",Stack_timer,ST_Interval,ret);

	//start a GPTimer ,repeat=false
	ret = Ql_Timer_Start(GP_timer,GPT_Interval,FALSE);
	if(ret < 0)
	{
		APP_DEBUG("\r\n<--failed!! GP-timer Ql_Timer_Start fail, ret=%d-->\r\n",ret);
	}
	//APP_DEBUG("\r\n<--GP-timer Ql_Timer_Start(ID=%d,Interval=%d) ret=%d-->\r\n",GP_timer,ST_Interval,ret);

	ret = Ql_Timer_Start(GP_timer,GPT_Interval,FALSE);


	ret=Ql_IIC_Init(1,PINNAME_RI,PINNAME_DCD,1);
	if(ret < 0)
	{
		APP_DEBUG("\r\n<--iic init failed\r\n");
	}



//////////////////////////////////////////////////////////////////////////////////////////////////
	 ret = Ql_SPI_Init(1,PINNAME_PCM_IN,PINNAME_PCM_SYNC,PINNAME_PCM_OUT,PINNAME_PCM_CLK,spi_usr_type);

	if(ret <0)
	{
		APP_DEBUG("\r\n<-- Failed!! Ql_SPI_Init fail , ret =%d-->\r\n",ret)
	}
	else
	{
		APP_DEBUG("\r\n<-- Ql_SPI_Init ret =%d -->\r\n",ret)
	}
	ret = Ql_SPI_Config(1,1,0,0,10000); //config sclk about 10MHz;
	if(ret <0)
	{
		APP_DEBUG("\r\n<--Failed!! Ql_SPI_Config fail  ret=%d -->\r\n",ret)
	}
	else
	{
		APP_DEBUG("\r\n<-- Ql_SPI_Config  =%d -->\r\n",ret)
	}
	if (!spi_usr_type)
	{
		Ql_GPIO_Init(PINNAME_PCM_CLK,PINDIRECTION_OUT,PINLEVEL_HIGH,PINPULLSEL_PULLUP);   //CS high
	}

	writereg(0x2f,0x11);
	Ql_Sleep(1000);
	readreg(0x2f);
	initialise_rfm();

	readreg(0x6f);

	set_mode(3);
	Ql_OS_GiveMutex(mutex_id_serial);
	///////////////////////////////////////////////////////////////////////////////////////////////////
    // START MESSAGE LOOP OF THIS TASK
    while(TRUE)
    {
        Ql_OS_GetMessage(&msg);
        switch(msg.message)
        {
        case MSG_ID_RIL_READY:
        	Ql_OS_TakeMutex(mutex_id_serial);
        	APP_DEBUG("<-- RIL is ready -->\r\n");
            Ql_RIL_Initialize();
            Ql_OS_GiveMutex(mutex_id_serial);
            break;
        case MSG_ID_URC_INDICATION:
            //APP_DEBUG("<-- Received URC: type: %d, -->\r\n", msg.param1);
            switch (msg.param1)
            {


            case URC_MQTT_RECV_IND:
			   {
				   Ql_OS_TakeMutex(mutex_id_serial);
				   Ql_Sleep(10);
				   APP_DEBUG("<-- RECV DATA:%s -->\r\n", msg.param2);
				   Ql_Sleep(10);
				   Ql_OS_GiveMutex(mutex_id_serial);
				   buzz();
				   mqtt_rec_flag=1;
				   Ql_memset(gprsbuf,0,sizeof(gprsbuf));
				   Ql_sprintf(gprsbuf,"%s",msg.param2);

				  break;
			   }
			case URC_MQTT_STAT_IND:
				Ql_OS_TakeMutex(mutex_id_serial);
				APP_DEBUG("<-- RECV STAT:%s-->\r\n", msg.param2);
				Ql_OS_GiveMutex(mutex_id_serial);
				break;
            case URC_SYS_INIT_STATE_IND:
            	Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("<-- Sys Init Status %d -->\r\n", msg.param2);
                Ql_OS_GiveMutex(mutex_id_serial);
                break;
            case URC_SIM_CARD_STATE_IND:
            	Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("<-- SIM Card Status:%d -->\r\n", msg.param2);
                Ql_OS_GiveMutex(mutex_id_serial);
                break;
            case URC_GSM_NW_STATE_IND:
            	Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("<-- GSM Network Status:%d -->\r\n", msg.param2);
                Ql_OS_GiveMutex(mutex_id_serial);
                break;
            case URC_GPRS_NW_STATE_IND:
            	Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("<-- GPRS Network Status:%d -->\r\n", msg.param2);

                gprsflag = msg.param2;

                Ql_OS_GiveMutex(mutex_id_serial);
                break;
            case URC_CFUN_STATE_IND:
            	Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("<-- CFUN Status:%d -->\r\n", msg.param2);
                Ql_OS_GiveMutex(mutex_id_serial);
                break;
            case URC_COMING_CALL_IND:
                {
                	Ql_OS_TakeMutex(mutex_id_serial);
                	ST_ComingCall* pComingCall = (ST_ComingCall*)msg.param2;
                    APP_DEBUG("<-- Coming call, number:%s, type:%d -->\r\n", pComingCall->phoneNumber, pComingCall->type);
                    Ql_OS_GiveMutex(mutex_id_serial);
                    break;
                }
            case URC_CALL_STATE_IND:
            	{
            		Ql_OS_TakeMutex(mutex_id_serial);
            		Ql_Sleep(100);
            		APP_DEBUG("<-- Call state:%d\r\n", msg.param2);
            		Ql_Sleep(100);
            		Ql_OS_GiveMutex(mutex_id_serial);
            		buzz();
            	}
                break;
            case URC_NEW_SMS_IND:
            	{
					Ql_OS_TakeMutex(mutex_id_serial);
					Ql_Sleep(100);
					APP_DEBUG("<-- New SMS Arrives: index=%d\r\n", msg.param2);
					Ql_Sleep(100);
					Ql_OS_GiveMutex(mutex_id_serial);
					sms_flag=1;
					buzz();
            	}
                break;
            case URC_MODULE_VOLTAGE_IND:
            	Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("<-- VBatt Voltage Ind: type=%d\r\n", msg.param2);
                Ql_OS_GiveMutex(mutex_id_serial);
                break;
            default:
            	Ql_OS_TakeMutex(mutex_id_serial);
            	APP_DEBUG("<-- Other URC: type=%d,RECV DATA:%s -->\r\n", msg.param1, msg.param2);
            	Ql_OS_GiveMutex(mutex_id_serial);
            	break;
            }
            break;
            case MSG_ID_FOTA:
			 {
				 Ql_OS_TakeMutex(mutex_id_serial);
				 Ql_Sleep(1000);
				 APP_DEBUG("<-- MSG_ID_FOTA -%d-%d-->\r\n",msg.param1,msg.param2);
				 Ql_Sleep(1000);
				 Ql_OS_GiveMutex(mutex_id_serial);
				switch (msg.param1)
				{
				   case 1:
					   Ql_OS_TakeMutex(mutex_id_serial);
					   Ql_Sleep(1000);
					   APP_DEBUG("<-- FOTA started - %d -->\r\n", msg.param2);
					   Ql_Sleep(1000);
					   Ql_OS_GiveMutex(mutex_id_serial);
					  ST_GprsConfig apnCfg;
					  Ql_memcpy(apnCfg.apnName,   apn, Ql_strlen(apn));
					  Ql_memcpy(apnCfg.apnUserId, fotaun, Ql_strlen(fotaun));
					  Ql_memcpy(apnCfg.apnPasswd, fotapw, Ql_strlen(fotapw));
					  lcd_disp=1;
					  Ql_FOTA_StartUpgrade(fotaurl, &apnCfg, NULL);
					  task_fota = 0;
					  break;
				   default:
					  task_fota = 0;
					  break;
				}
			 }
			 break;
            case MSG_ID_MSG:
            {
            	Ql_OS_TakeMutex(mutex_id_serial);
            	APP_DEBUG("<-- MSG_ID_MSG_DATA-%d-%d-->\r\n",msg.param1,msg.param2);
            	Ql_OS_GiveMutex(mutex_id_serial);

				switch (msg.param1)
				{
					case 1:
					{

						Ql_OS_TakeMutex(mutex_id_serial);
						APP_DEBUG("<-- MSG started - %d -->\r\n", msg.param2);
						ret = RIL_SMS_SendSMS_Text(mobilenum,Ql_strlen(mobilenum),LIB_SMS_CHARSET_GSM,gsm_msg, Ql_strlen(gsm_msg), &nMsgRef);
						Ql_OS_GiveMutex(mutex_id_serial);

						if(ret==0)
						{

							APP_DEBUG("<-- MSG sending success-->\r\n");


							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,0);
							LiquidCrystal_I2C_printstr("Message Sent.");
							Ql_OS_GiveMutex(mutex_id_lcd);
							msg_success=1;
						}
						else
						{
							APP_DEBUG("<-- MSG sending failed-->\r\n");


							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,0);
							LiquidCrystal_I2C_printstr("Message failed.");
							Ql_OS_GiveMutex(mutex_id_lcd);
							msg_success=0;
						}



						buzz();
					}
					break;

					default:
						break;
				}


            }
            break;
            case MSG_ID_SEND:
			{
				Ql_OS_TakeMutex(mutex_id_serial);
				APP_DEBUG("<-- MSG_ID_SEND-%d-%d-->\r\n",msg.param1,msg.param2);
				Ql_OS_GiveMutex(mutex_id_serial);

				switch (msg.param1)
				{
					case 1:
					{
						set_mode(3);

						_send(_address, send_data, 13, 1);

						Ql_Sleep(1);

						set_mode(3);
					}
					break;
					case 2:
					{
						if(gprs_ok)
						{
								Ql_OS_TakeMutex(mutex_id_serial);
								Ql_Sleep(10);
								Ql_sprintf(mqtt_pubtopic,"ihome/%s",imei);
								s32 ret = RIL_MQTT_QMTPUB(0, 0,0,0,mqtt_pubtopic,mqtt_live,Ql_strlen(mqtt_live));
								APP_DEBUG("<-- mqtt publish, ret=%d Topic-%s \npacket-%s-->\r\n", ret,mqtt_pubtopic,mqtt_live);
								if(ret != 0)
								{
									 okflag = waitcount = 0;
									 APP_DEBUG("<-- AT+QIDEACT -->\r\n");
									 ret = Ql_RIL_SendATCmd("AT+QIDEACT",Ql_strlen("AT+QIDEACT"),ATResponse_Handler,NULL,0);
									 wait_ok();
									 //ret = RIL_NW_ClosePDPContext();
									 APP_DEBUG("<-- Set GPRS DEACTIVATE, ret=%d -->\r\n", ret);
									 gprs_ok=0;
								 }
								if(ret != 0)
								APP_DEBUG("MQTT INIT FAIL\n");
								Ql_Sleep(10);
								Ql_OS_GiveMutex(mutex_id_serial);
								if(!lcd_disp)
								{
									Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_clear();
									LiquidCrystal_I2C_setCursor(0,0);
									LiquidCrystal_I2C_printstr("MQTT data sent.");
									Ql_OS_GiveMutex(mutex_id_lcd);
								}
						}
					}
					break;

				}
			}
			 break;
            case MSG_ID_gprs_init:
            			{
            				Ql_OS_TakeMutex(mutex_id_serial);
            				APP_DEBUG("<-- MSG_ID_gprs_init-%d-%d-->\r\n",msg.param1,msg.param2);
            				Ql_OS_GiveMutex(mutex_id_serial);

            				switch (msg.param1)
            				{
            					case 1:
            					{
            						if((gprsflag == 1) || (gprsflag == 5))
									{
										Ql_OS_TakeMutex(mutex_id_serial);
										gprs_init();
										Ql_OS_GiveMutex(mutex_id_serial);
									}
            					}
            					break;
            				}
            			}
            			 break;
        default:
            break;
        }
    }
}
void proc_subtask1(s32 taskId)
{

	while(1)
	{

			if(touch_flag==1)
			{

				touch_flag=0;
				u8 butt=touch_to_int();
				time2=0;
				switch(butt)
				{
					case 4:		// options //
					{
start1:
						lcd_disp=1;
						Ql_OS_TakeMutex(mutex_id_lcd);
						LiquidCrystal_I2C_clear();
						LiquidCrystal_I2C_blink();
						LiquidCrystal_I2C_setCursor(0,0);
						LiquidCrystal_I2C_printstr("ADD     DELETE");
						LiquidCrystal_I2C_setCursor(0,1);
						LiquidCrystal_I2C_printstr("ENABLE  DISABLE");
						LiquidCrystal_I2C_setCursor(0,0);
						Ql_OS_GiveMutex(mutex_id_lcd);
add:
						time1=0;
						while(touch_flag==0 && time1<10)
						{
							time2=0;
							Ql_Sleep(10);
						}
						if(time1<10)
						{
							touch_flag=0;
							butt=touch_to_int();

							switch(butt)
							{
								case 4:		// add //
								{

door_add:							Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_clear();
									LiquidCrystal_I2C_blink();
									LiquidCrystal_I2C_setCursor(0,0);
									LiquidCrystal_I2C_printstr("DOOR  PIR");
									LiquidCrystal_I2C_setCursor(0,1);
									LiquidCrystal_I2C_printstr("LIGHT");
									LiquidCrystal_I2C_setCursor(0,0);
									Ql_OS_GiveMutex(mutex_id_lcd);
									time1=0;
									while(touch_flag==0 && time1<10)
									{
										time2=0;
										Ql_Sleep(10);
									}
									if(time1<10)
									{
										touch_flag=0;
										butt=touch_to_int();

										switch(butt)
										{
											case 4://door add
											{
												Ql_OS_TakeMutex(mutex_id_lcd);
												LiquidCrystal_I2C_clear();
												LiquidCrystal_I2C_setCursor(0,0);
												LiquidCrystal_I2C_printstr("ENTER THE ID:");
												Ql_OS_GiveMutex(mutex_id_lcd);
												if(get_id()==6)
												{
													u32 i=0;
													u32 pos=0;

													Ql_OS_TakeMutex(mutex_id_lcd);
													LiquidCrystal_I2C_clear();
													LiquidCrystal_I2C_setCursor(0,0);
													LiquidCrystal_I2C_printstr("ADDING DOOR SENSOR:");
													LiquidCrystal_I2C_setCursor(0,1);
													for(u32 j=0;j<6;j++)
													{

														LiquidCrystal_I2C_printchar(id_read[j]);
													}
													Ql_OS_GiveMutex(mutex_id_lcd);
													for(i=0;i<280;i=i+31)
													{

														Ql_OS_TakeMutex(mutex_id_serial);
														APP_DEBUG("\r\n<--i=%d",i);
														Ql_OS_GiveMutex(mutex_id_serial);
														if(temp_mem[i]==0)
														{
															break;
														}
													}

													pos=i;
													temp_mem[i++]='D';
													temp_mem[i++]=(pos/31)+48;
													temp_mem[i++]='-';
													temp_mem[i++]='D';
													temp_mem[i++]='O';
													temp_mem[i++]='O';
													temp_mem[i++]='R';
													temp_mem[i++]=(pos/31)+48;
													temp_mem[pos+18]='*';

													for(u8 j=0;j<6;j++)
													{
														temp_mem[pos+19+j]=id_read[j];
													}
													temp_mem[pos+25]='#';
													temp_mem[pos+27]='E';
													Ql_Sleep(1000);
													Ql_OS_TakeMutex(mutex_id_lcd);
													LiquidCrystal_I2C_clear();
													LiquidCrystal_I2C_setCursor(0,0);
													LiquidCrystal_I2C_printstr("ADDED SUCCESS");
													LiquidCrystal_I2C_setCursor(0,1);
													for(u32 j=pos;j<(pos+10);j++)
													{
														LiquidCrystal_I2C_printchar(temp_mem[j]);
													}
													for(u32 j=(pos+19);j<(pos+26);j++)
													{
														LiquidCrystal_I2C_printchar(temp_mem[j]);
													}

													Ql_OS_GiveMutex(mutex_id_lcd);

													Ql_Sleep(10);
													for(u32 j=pos;j<(pos+31);j++)
													{
														eeprom_write(j,temp_mem[j]);
													}
													buzz();

												}
												touch_flag=0;


											}
											break;
											case 8:
											{
pir_add:										Ql_OS_TakeMutex(mutex_id_lcd);
												LiquidCrystal_I2C_setCursor(6,0);
												Ql_OS_GiveMutex(mutex_id_lcd);
												time1=0;
												while(touch_flag==0 && time1<10)
												{
													time2=0;
													Ql_Sleep(10);
												}
												if(time1<10)
												{
													touch_flag=0;
													butt=touch_to_int();

													switch(butt)
													{
														case 4:  // pir status
														{
															Ql_OS_TakeMutex(mutex_id_lcd);
															LiquidCrystal_I2C_clear();
															LiquidCrystal_I2C_setCursor(0,0);
															LiquidCrystal_I2C_printstr("ENTER THE ID:");
															Ql_OS_GiveMutex(mutex_id_lcd);
															if(get_id()==6)
															{
																Ql_OS_TakeMutex(mutex_id_lcd);
																LiquidCrystal_I2C_clear();
																LiquidCrystal_I2C_setCursor(0,0);
																LiquidCrystal_I2C_printstr("ADDING PIR SENSOR:");
																LiquidCrystal_I2C_setCursor(0,1);
																for(u32 j=0;j<6;j++)
																{

																	LiquidCrystal_I2C_printchar(id_read[j]);
																}
																Ql_OS_GiveMutex(mutex_id_lcd);

																u32 i=0;
																u32 pos=0;
																for(i=310;i<590;i=i+31)
																{

																	Ql_OS_TakeMutex(mutex_id_serial);
																	APP_DEBUG("\r\n<--i=%d",i);
																	Ql_OS_GiveMutex(mutex_id_serial);
																	if(temp_mem[i]==0)
																	{
																		break;
																	}
																}

																pos=i;
																temp_mem[i++]='P';
																temp_mem[i++]=((pos-310)/31)+48;
																temp_mem[i++]='-';
																temp_mem[i++]='P';
																temp_mem[i++]='I';
																temp_mem[i++]='R';
																temp_mem[i++]=((pos-310)/31)+48;
																temp_mem[pos+18]='*';

																for(u8 j=0;j<6;j++)
																{
																	temp_mem[pos+19+j]=id_read[j];
																}
																temp_mem[pos+25]='#';
																temp_mem[pos+27]='E';
																Ql_Sleep(1000);
																Ql_OS_TakeMutex(mutex_id_lcd);
																LiquidCrystal_I2C_clear();
																LiquidCrystal_I2C_setCursor(0,0);
																LiquidCrystal_I2C_printstr("ADDED SUCCESS");
																LiquidCrystal_I2C_setCursor(0,1);
																for(u32 j=pos;j<(pos+10);j++)
																{
																	LiquidCrystal_I2C_printchar(temp_mem[j]);
																}
																for(u32 j=(pos+19);j<(pos+26);j++)
																{
																	LiquidCrystal_I2C_printchar(temp_mem[j]);
																}

																Ql_OS_GiveMutex(mutex_id_lcd);

																Ql_Sleep(10);
																for(u32 j=pos;j<(pos+31);j++)
																{
																	eeprom_write(j,temp_mem[j]);
																}
																buzz();

															}
															touch_flag=0;



														}
														break;
														case 8:
														{
light_add:													Ql_OS_TakeMutex(mutex_id_lcd);
															LiquidCrystal_I2C_setCursor(0,1);
															Ql_OS_GiveMutex(mutex_id_lcd);
															time1=0;
															while(touch_flag==0 && time1<10)
															{
																time2=0;
																Ql_Sleep(10);
															}
															if(time1<10)
															{
																touch_flag=0;
																butt=touch_to_int();

																switch(butt)
																{
																	case 4:	// light status
																	{
																		Ql_OS_TakeMutex(mutex_id_lcd);
																		LiquidCrystal_I2C_clear();
																		LiquidCrystal_I2C_setCursor(0,0);
																		LiquidCrystal_I2C_printstr("ENTER THE ID:");
																		Ql_OS_GiveMutex(mutex_id_lcd);
																		if(get_id()==6)
																		{
																			u32 i=0;
																			u32 pos=0;
																			Ql_OS_TakeMutex(mutex_id_lcd);
																			LiquidCrystal_I2C_clear();
																			LiquidCrystal_I2C_setCursor(0,0);
																			LiquidCrystal_I2C_printstr("ADDING LIGHT SENSOR:");
																			LiquidCrystal_I2C_setCursor(0,1);
																			for(u32 j=0;j<6;j++)
																			{

																				LiquidCrystal_I2C_printchar(id_read[j]);
																			}
																			Ql_OS_GiveMutex(mutex_id_lcd);
																			for(i=620;i<900;i=i+31)
																			{

																				Ql_OS_TakeMutex(mutex_id_serial);
																				APP_DEBUG("\r\n<--i=%d",i);
																				Ql_OS_GiveMutex(mutex_id_serial);
																				if(temp_mem[i]==0)
																				{
																					break;
																				}
																			}

																			pos=i;
																			temp_mem[i++]='L';
																			temp_mem[i++]=((pos-620)/31)+48;;
																			temp_mem[i++]='-';
																			temp_mem[i++]='L';
																			temp_mem[i++]='i';
																			temp_mem[i++]='g';
																			temp_mem[i++]='h';
																			temp_mem[i++]='t';
																			temp_mem[i++]=((pos-620)/31)+48;;
																			temp_mem[pos+18]='*';

																			for(u8 j=0;j<6;j++)
																			{
																				temp_mem[pos+19+j]=id_read[j];
																			}
																			temp_mem[pos+25]='#';
																			temp_mem[pos+27]='E';
																			temp_mem[pos+28]='0';
																			temp_mem[pos+29]='0';
																			temp_mem[pos+30]='0';
																			Ql_Sleep(1000);
																			Ql_OS_TakeMutex(mutex_id_lcd);
																			LiquidCrystal_I2C_clear();
																			LiquidCrystal_I2C_setCursor(0,0);
																			LiquidCrystal_I2C_printstr("ADDED SUCCESS");
																			LiquidCrystal_I2C_setCursor(0,1);
																			for(u32 j=pos;j<(pos+10);j++)
																			{
																				LiquidCrystal_I2C_printchar(temp_mem[j]);
																			}
																			for(u32 j=(pos+19);j<(pos+26);j++)
																			{
																				LiquidCrystal_I2C_printchar(temp_mem[j]);
																			}

																			Ql_OS_GiveMutex(mutex_id_lcd);

																			Ql_Sleep(10);
																			for(u32 j=pos;j<(pos+31);j++)
																			{
																				eeprom_write(j,temp_mem[j]);
																			}
																			buzz();

																		}
																		touch_flag=0;


																	}
																	break;
																	case 8:
																	{
																		goto door_add;
																	}
																	break;
																	case 16:
																	{
																		goto door_add;
																	}
																	break;
																	case 15:
																	{
																		goto door_add;
																	}
																	break;
																	case 12:
																	{
																		goto pir_add;
																	}
																	break;
																	default:
																		break;
																}
															}

														}
														break;
														case 12:
														{
															goto door_add;
														}
														break;
														case 16:
														{
															goto light_add;
														}
														break;
														case 15:
														{
															goto light_add;
														}
														break;
														default:
															break;
													}
												}


											}
											break;
											case 16:
											{
												goto light_add;
											}
											break;
											default:
												break;
										}
									}

								}
								break;
								case 8:		// delete //
								{

									Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_setCursor(8,0);
									Ql_OS_GiveMutex(mutex_id_lcd);
delete:
									time1=0;
									while(touch_flag==0 && time1<10)
									{
										time2=0;
										Ql_Sleep(10);
									}
									if(time1<10)
									{
										touch_flag=0;
										butt=touch_to_int();

										switch(butt)
										{
											case 4:		// delete //
											{
door_delete:																		Ql_OS_TakeMutex(mutex_id_lcd);
													LiquidCrystal_I2C_clear();
													LiquidCrystal_I2C_blink();
													LiquidCrystal_I2C_setCursor(0,0);
													LiquidCrystal_I2C_printstr("DOOR  PIR");
													LiquidCrystal_I2C_setCursor(0,1);
													LiquidCrystal_I2C_printstr("LIGHT");
													LiquidCrystal_I2C_setCursor(0,0);
													Ql_OS_GiveMutex(mutex_id_lcd);
													time1=0;
													while(touch_flag==0 && time1<10)
													{
														time2=0;
														Ql_Sleep(10);
													}
													if(time1<10)
													{
														touch_flag=0;
														butt=touch_to_int();

														switch(butt)
														{
															case 4://door delete
															{

																int i=0,position=0,position1=0;
																char status[25]={0};int row=0;
next_door_delete:																				door_status(i,&status);

																	Ql_OS_TakeMutex(mutex_id_lcd);
																	LiquidCrystal_I2C_clear();
																	Ql_OS_GiveMutex(mutex_id_lcd);

																if(status[0]!=NULL)
																{
																	Ql_OS_TakeMutex(mutex_id_lcd);
																	LiquidCrystal_I2C_setCursor(0,0);
																	LiquidCrystal_I2C_printstr(status);
																	LiquidCrystal_I2C_setCursor(0,0);
																	Ql_OS_GiveMutex(mutex_id_lcd);
																	position=i;
																	row++;
																}
																else
																{

																	i++;
																	if(i<10)
																	goto next_door_delete;
																}
																i++;
next_door_delete1:																				door_status(i,&status);
																if(status[0]!=NULL && i<10)
																{
																	Ql_OS_TakeMutex(mutex_id_lcd);

																	LiquidCrystal_I2C_setCursor(0,1);
																	LiquidCrystal_I2C_printstr(status);
																	LiquidCrystal_I2C_setCursor(0,0);
																	Ql_OS_GiveMutex(mutex_id_lcd);
																	position1=i;

																}
																else
																{
																	i++;
																	if(i<10)
																	goto next_door_delete1;
																	else
																	{
																		Ql_OS_TakeMutex(mutex_id_lcd);
																		LiquidCrystal_I2C_setCursor(0,1);
																		LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																		LiquidCrystal_I2C_setCursor(0,0);
																		Ql_OS_GiveMutex(mutex_id_lcd);
																	}
																}
																i++;
																time1=0;
																while(touch_flag==0 && time1<10)
																{
																	time2=0;
																	Ql_Sleep(10);
																}
																if(time1<10)
																{
																	touch_flag=0;
																	butt=touch_to_int();

																	switch(butt)
																	{
																		case 16:
																		{
																			Ql_OS_TakeMutex(mutex_id_lcd);
																			LiquidCrystal_I2C_setCursor(0,1);
																			Ql_OS_GiveMutex(mutex_id_lcd);
																			time1=0;
																			while(touch_flag==0 && time1<10)
																			{
																				time2=0;
																				Ql_Sleep(10);
																			}
																			if(time1<10)
																			{
																				touch_flag=0;
																				butt=touch_to_int();

																				switch(butt)
																				{
																					case 16:
																					{
																						if(i<10)
																						{
																						goto next_door_delete;
																						}
																						else
																						{
																							Ql_OS_TakeMutex(mutex_id_lcd);
																							LiquidCrystal_I2C_clear();
																							LiquidCrystal_I2C_setCursor(0,0);
																							LiquidCrystal_I2C_printstr("END");
																							Ql_OS_GiveMutex(mutex_id_lcd);
																							//goto status_end;
																						}
																					}
																					break;
																					case 15:
																					{
																						i=0;
																						goto next_door_delete;

																					}
																					break;
																					case 4:
																					{
																						int num=position1+(position1*30);
																						for(u8 j=0;j<31;j++)
																						{
																							temp_mem[j+num]=NULL;
																							eeprom_write(j+num,NULL);
																						}
																						Ql_Sleep(100);
																						Ql_OS_TakeMutex(mutex_id_lcd);
																						LiquidCrystal_I2C_clear();
																						LiquidCrystal_I2C_setCursor(0,0);
																						LiquidCrystal_I2C_printstr("DELETED SUCCESS");
																						Ql_OS_GiveMutex(mutex_id_lcd);
																					}
																					break;
																					default:
																						break;
																				}
																			}

																		}
																		break;
																		case 4:
																		{
																			int num=position+(position*30);
																			for(u8 j=0;j<31;j++)
																			{
																				temp_mem[j+num]=NULL;
																				eeprom_write(j+num,NULL);
																			}
																			Ql_Sleep(100);
																			Ql_OS_TakeMutex(mutex_id_lcd);
																			LiquidCrystal_I2C_clear();
																			LiquidCrystal_I2C_setCursor(0,0);
																			LiquidCrystal_I2C_printstr("DELETED SUCCESS");
																			Ql_OS_GiveMutex(mutex_id_lcd);
																		}
																		default:
																			break;
																	}
																}

															}
															break;
															case 8:
															{
pir_delete:																						Ql_OS_TakeMutex(mutex_id_lcd);
																LiquidCrystal_I2C_setCursor(6,0);
																Ql_OS_GiveMutex(mutex_id_lcd);
																time1=0;
																while(touch_flag==0 && time1<10)
																{
																	time2=0;
																	Ql_Sleep(10);
																}
																if(time1<10)
																{
																	touch_flag=0;
																	butt=touch_to_int();

																	switch(butt)
																	{
																		case 4:  // pir status
																		{

																			int i=10,position=0,position1=0;
																			char status[25]={0};int row=0;
next_pir_delete:															door_status(i,&status);

																			Ql_OS_TakeMutex(mutex_id_lcd);
																			LiquidCrystal_I2C_clear();
																			Ql_OS_GiveMutex(mutex_id_lcd);
																			if(status[0]!=NULL)
																			{
																				Ql_OS_TakeMutex(mutex_id_lcd);
																				LiquidCrystal_I2C_setCursor(0,0);
																				LiquidCrystal_I2C_printstr(status);
																				LiquidCrystal_I2C_setCursor(0,0);
																				Ql_OS_GiveMutex(mutex_id_lcd);
																				position=i;
																				row++;
																			}
																			else
																			{

																				i++;
																				if(i<20)
																				goto next_pir_delete;
																			}
																			i++;
next_pir_delete1:																							door_status(i,&status);
																			if(status[0]!=NULL&& i<20)
																			{
																				Ql_OS_TakeMutex(mutex_id_lcd);

																				LiquidCrystal_I2C_setCursor(0,1);
																				LiquidCrystal_I2C_printstr(status);
																				LiquidCrystal_I2C_setCursor(0,0);
																				Ql_OS_GiveMutex(mutex_id_lcd);
																				position1=i;

																			}
																			else
																			{
																				i++;
																				if(i<20)
																				goto next_pir_delete1;
																				else
																				{
																					Ql_OS_TakeMutex(mutex_id_lcd);
																					LiquidCrystal_I2C_setCursor(0,1);
																					LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																					LiquidCrystal_I2C_setCursor(0,0);
																					Ql_OS_GiveMutex(mutex_id_lcd);
																				}
																			}
																			i++;
																			time1=0;
																			while(touch_flag==0 && time1<10)
																			{
																				time2=0;
																				Ql_Sleep(10);
																			}
																			if(time1<10)
																			{
																				touch_flag=0;
																				butt=touch_to_int();

																				switch(butt)
																				{
																					case 16:
																					{
																						Ql_OS_TakeMutex(mutex_id_lcd);
																						LiquidCrystal_I2C_setCursor(0,1);
																						Ql_OS_GiveMutex(mutex_id_lcd);
																						time1=0;
																						while(touch_flag==0 && time1<10)
																						{
																							time2=0;
																							Ql_Sleep(10);
																						}
																						if(time1<10)
																						{
																							touch_flag=0;
																							butt=touch_to_int();

																							switch(butt)
																							{
																								case 16:
																								{
																									if(i<20)
																									{
																									goto next_pir_delete;
																									}
																									else
																									{
																										Ql_OS_TakeMutex(mutex_id_lcd);
																										LiquidCrystal_I2C_clear();
																										LiquidCrystal_I2C_setCursor(0,0);
																										LiquidCrystal_I2C_printstr("END");
																										Ql_OS_GiveMutex(mutex_id_lcd);
																									}
																								}
																								break;
																								case 4:
																								{
																									int num=position+(position*30);
																									for(u8 j=0;j<31;j++)
																									{
																										temp_mem[j+num]=NULL;
																										eeprom_write(j+num,NULL);
																									}
																									Ql_Sleep(100);
																									Ql_OS_TakeMutex(mutex_id_lcd);
																									LiquidCrystal_I2C_clear();
																									LiquidCrystal_I2C_setCursor(0,0);
																									LiquidCrystal_I2C_printstr("DELETED SUCCESS");
																									Ql_OS_GiveMutex(mutex_id_lcd);
																								}
																								default:
																									break;
																							}
																						}

																					}
																					break;
																					case 4:
																					{
																						int num=position+(position*30);
																						for(u8 j=0;j<31;j++)
																						{
																							temp_mem[j+num]=NULL;
																							eeprom_write(j+num,NULL);
																						}
																						Ql_Sleep(100);
																						Ql_OS_TakeMutex(mutex_id_lcd);
																						LiquidCrystal_I2C_clear();
																						LiquidCrystal_I2C_setCursor(0,0);
																						LiquidCrystal_I2C_printstr("DELETED SUCCESS");
																						Ql_OS_GiveMutex(mutex_id_lcd);
																					}
																					default:
																						break;

																				}
																			}

																		}
																		break;
																		case 8:
																		{
light_delete:																								Ql_OS_TakeMutex(mutex_id_lcd);
																			LiquidCrystal_I2C_setCursor(0,1);
																			Ql_OS_GiveMutex(mutex_id_lcd);
																			time1=0;
																			while(touch_flag==0 && time1<10)
																			{
																				time2=0;
																				Ql_Sleep(10);
																			}
																			if(time1<10)
																			{
																				touch_flag=0;
																				butt=touch_to_int();

																				switch(butt)
																				{
																					case 4:	// light status
																					{
																						int i=0,position=0,position1=0;
																						char status[25]={0};int row=0;
next_light_delete:																		light_status1(i,&status);

																						Ql_OS_TakeMutex(mutex_id_lcd);
																						LiquidCrystal_I2C_clear();
																						Ql_OS_GiveMutex(mutex_id_lcd);
																						if(status[0]!=NULL)
																						{
																							Ql_OS_TakeMutex(mutex_id_lcd);
																							LiquidCrystal_I2C_setCursor(0,0);
																							LiquidCrystal_I2C_printstr(status);
																							LiquidCrystal_I2C_setCursor(0,0);
																							Ql_OS_GiveMutex(mutex_id_lcd);
																							position=i;
																							row++;
																						}
																						else
																						{

																							i++;
																							if(i<10)
																							goto next_light_delete;
																						}
																						i++;
next_light_delete1:																										light_status1(i,&status);
																						if(status[0]!=NULL&& i<10)
																						{
																							Ql_OS_TakeMutex(mutex_id_lcd);

																							LiquidCrystal_I2C_setCursor(0,1);
																							LiquidCrystal_I2C_printstr(status);
																							LiquidCrystal_I2C_setCursor(0,0);
																							Ql_OS_GiveMutex(mutex_id_lcd);
																							position1=i;
																						}
																						else
																						{
																							i++;
																							if(i<10)
																							goto next_light_delete1;
																							else
																							{
																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_setCursor(0,1);
																								LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																								LiquidCrystal_I2C_setCursor(0,0);
																								Ql_OS_GiveMutex(mutex_id_lcd);
																							}
																						}
																						i++;
																						time1=0;
																						while(touch_flag==0 && time1<10)
																						{
																							time2=0;
																							Ql_Sleep(10);
																						}
																						if(time1<10)
																						{
																							touch_flag=0;
																							butt=touch_to_int();

																							switch(butt)
																							{
																								case 16:
																								{
																									Ql_OS_TakeMutex(mutex_id_lcd);
																									LiquidCrystal_I2C_setCursor(0,1);
																									Ql_OS_GiveMutex(mutex_id_lcd);
																									time1=0;
																									while(touch_flag==0 && time1<10)
																									{
																										time2=0;
																										Ql_Sleep(10);
																									}
																									if(time1<10)
																									{
																										touch_flag=0;
																										butt=touch_to_int();

																										switch(butt)
																										{
																											case 16:
																											{
																												if(i<10)
																												{
																												goto next_light_delete;
																												}
																												else
																												{
																													Ql_OS_TakeMutex(mutex_id_lcd);
																													LiquidCrystal_I2C_clear();
																													LiquidCrystal_I2C_setCursor(0,0);
																													LiquidCrystal_I2C_printstr("END");
																													Ql_OS_GiveMutex(mutex_id_lcd);
																												}
																											}
																											break;
																											case 4:
																											{
																												int num=position1+(position1*30)+620;
																												for(u8 j=0;j<31;j++)
																												{
																													temp_mem[j+num]=NULL;
																													eeprom_write(j+num,NULL);
																												}
																												Ql_Sleep(100);
																												Ql_OS_TakeMutex(mutex_id_lcd);
																												LiquidCrystal_I2C_clear();
																												LiquidCrystal_I2C_setCursor(0,0);
																												LiquidCrystal_I2C_printstr("DELETED SUCCESS");
																												Ql_OS_GiveMutex(mutex_id_lcd);
																											}
																											break;
																											default:
																												break;

																										}
																									}

																								}
																								break;
																								case 4:
																								{
																									int num=position+(position*30)+620;
																									for(u8 j=0;j<31;j++)
																									{
																										temp_mem[j+num]=NULL;
																										eeprom_write(j+num,NULL);
																									}
																									Ql_Sleep(100);
																									Ql_OS_TakeMutex(mutex_id_lcd);
																									LiquidCrystal_I2C_clear();
																									LiquidCrystal_I2C_setCursor(0,0);
																									LiquidCrystal_I2C_printstr("DELETED SUCCESS");
																									Ql_OS_GiveMutex(mutex_id_lcd);
																								}
																								break;
																								default:
																									break;
																							}
																						}


																					}
																					break;
																					case 8:
																					{
																						goto door_delete;
																					}
																					break;
																					case 16:
																					{
																						goto door_delete;
																					}
																					break;
																					case 15:
																					{
																						goto door_delete;
																					}
																					break;
																					case 12:
																					{
																						goto pir_delete;
																					}
																					break;
																					default:
																						break;
																				}
																			}

																		}
																		break;
																		case 12:
																		{
																			goto door_delete;
																		}
																		break;
																		case 16:
																		{
																			goto light_delete;
																		}
																		break;
																		case 15:
																		{
																			goto light_delete;
																		}
																		break;
																		default:
																			break;
																	}
																}


															}
															break;
															case 16:
															{
																goto light_delete;
															}
															break;
															default:
																break;
														}
													}

											}
											break;
											case 8:		// enable //
											{

												Ql_OS_TakeMutex(mutex_id_lcd);
												LiquidCrystal_I2C_setCursor(0,1);
												Ql_OS_GiveMutex(mutex_id_lcd);
enable:
												time1=0;
												while(touch_flag==0 && time1<10)
												{
													time2=0;
													Ql_Sleep(10);
												}
												if(time1<10)
												{
													touch_flag=0;
													butt=touch_to_int();
													switch(butt)
													{
														case 4:		// enable //
														{
door_enable:												Ql_OS_TakeMutex(mutex_id_lcd);
															LiquidCrystal_I2C_clear();
															LiquidCrystal_I2C_blink();
															LiquidCrystal_I2C_setCursor(0,0);
															LiquidCrystal_I2C_printstr("DOOR  PIR");
															LiquidCrystal_I2C_setCursor(0,1);
															LiquidCrystal_I2C_printstr("LIGHT");
															LiquidCrystal_I2C_setCursor(0,0);
															Ql_OS_GiveMutex(mutex_id_lcd);
															time1=0;
															while(touch_flag==0 && time1<10)
															{
																time2=0;
																Ql_Sleep(10);
															}
															if(time1<10)
															{
																touch_flag=0;
																butt=touch_to_int();

																switch(butt)
																{
																	case 4://door enable
																	{

																		int i=0,position=0,position1=0;
																		char status[25]={0};int row=0;
next_door_enable:														door_status(i,&status);
																		Ql_OS_TakeMutex(mutex_id_lcd);
																		LiquidCrystal_I2C_clear();
																		Ql_OS_GiveMutex(mutex_id_lcd);
																		if(status[0]!=NULL)
																		{
																			Ql_OS_TakeMutex(mutex_id_lcd);
																			LiquidCrystal_I2C_setCursor(0,0);
																			LiquidCrystal_I2C_printstr(status);
																			LiquidCrystal_I2C_setCursor(0,0);
																			Ql_OS_GiveMutex(mutex_id_lcd);
																			position=i;
																			row++;
																		}
																		else
																		{

																			i++;
																			if(i<10)
																			goto next_door_enable;
																		}
																		i++;
next_door_enable1:														door_status(i,&status);
																		if(status[0]!=NULL&& i<10)
																		{
																			Ql_OS_TakeMutex(mutex_id_lcd);

																			LiquidCrystal_I2C_setCursor(0,1);
																			LiquidCrystal_I2C_printstr(status);
																			LiquidCrystal_I2C_setCursor(0,0);
																			Ql_OS_GiveMutex(mutex_id_lcd);
																			position1=i;

																		}
																		else
																		{
																			i++;
																			if(i<10)
																			goto next_door_enable1;
																			else
																			{
																				Ql_OS_TakeMutex(mutex_id_lcd);
																				LiquidCrystal_I2C_setCursor(0,1);
																				LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																				LiquidCrystal_I2C_setCursor(0,0);
																				Ql_OS_GiveMutex(mutex_id_lcd);
																			}
																		}
																		i++;
																		time1=0;
																		while(touch_flag==0 && time1<10)
																		{
																			time2=0;
																			Ql_Sleep(10);
																		}
																		if(time1<10)
																		{
																			touch_flag=0;
																			butt=touch_to_int();

																			switch(butt)
																			{
																				case 16:
																				{
																					Ql_OS_TakeMutex(mutex_id_lcd);
																					LiquidCrystal_I2C_setCursor(0,1);
																					Ql_OS_GiveMutex(mutex_id_lcd);
																					time1=0;
																					while(touch_flag==0 && time1<10)
																					{
																						time2=0;
																						Ql_Sleep(10);
																					}
																					if(time1<10)
																					{
																						touch_flag=0;
																						butt=touch_to_int();

																						switch(butt)
																						{
																							case 16:
																							{
																								if(i<10)
																								{
																								goto next_door_enable;
																								}
																								else
																								{
																									Ql_OS_TakeMutex(mutex_id_lcd);
																									LiquidCrystal_I2C_clear();
																									LiquidCrystal_I2C_setCursor(0,0);
																									LiquidCrystal_I2C_printstr("END");
																									Ql_OS_GiveMutex(mutex_id_lcd);
																									//goto status_end;
																								}
																							}
																							break;
																							case 15:
																							{
																								i=0;
																								goto next_door_enable;

																							}
																							break;
																							case 4:
																							{
																								int num=position1+(position1*30);
																								temp_mem[num+27]='E';
																								eeprom_write(num+27,'E');
																								Ql_Sleep(100);
																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_clear();
																								LiquidCrystal_I2C_setCursor(0,0);
																								LiquidCrystal_I2C_printstr("ENABLED SUCCESS");
																								Ql_OS_GiveMutex(mutex_id_lcd);
																							}
																							break;
																							default:
																								break;
																						}
																					}

																				}
																				break;
																				case 4:
																				{
																					int num=position+(position*30);
																					temp_mem[num+27]='E';
																					eeprom_write(num+27,'E');
																					Ql_Sleep(100);
																					Ql_OS_TakeMutex(mutex_id_lcd);
																					LiquidCrystal_I2C_clear();
																					LiquidCrystal_I2C_setCursor(0,0);
																					LiquidCrystal_I2C_printstr("ENABLED SUCCESS");
																					Ql_OS_GiveMutex(mutex_id_lcd);
																				}
																				default:
																					break;
																			}
																		}

																	}
																	break;
																	case 8:
																	{
pir_enable:																Ql_OS_TakeMutex(mutex_id_lcd);
																		LiquidCrystal_I2C_setCursor(6,0);
																		Ql_OS_GiveMutex(mutex_id_lcd);
																		time1=0;
																		while(touch_flag==0 && time1<10)
																		{
																			time2=0;
																			Ql_Sleep(10);
																		}
																		if(time1<10)
																		{
																			touch_flag=0;
																			butt=touch_to_int();

																			switch(butt)
																			{
																				case 4:  // pir status
																				{

																					int i=10,position=0,position1=0;
																																													char status[25]={0};int row=0;
next_pir_enable:																	door_status(i,&status);
																					Ql_OS_TakeMutex(mutex_id_lcd);
																					LiquidCrystal_I2C_clear();
																					Ql_OS_GiveMutex(mutex_id_lcd);
																					if(status[0]!=NULL)
																					{
																						Ql_OS_TakeMutex(mutex_id_lcd);
																						LiquidCrystal_I2C_setCursor(0,0);
																						LiquidCrystal_I2C_printstr(status);
																						LiquidCrystal_I2C_setCursor(0,0);
																						Ql_OS_GiveMutex(mutex_id_lcd);
																						position=i;
																						row++;
																					}
																					else
																					{

																						i++;
																						if(i<20)
																						goto next_pir_enable;
																					}
																					i++;
		next_pir_enable1:																							door_status(i,&status);
																					if(status[0]!=NULL&& i<20)
																					{
																						Ql_OS_TakeMutex(mutex_id_lcd);

																						LiquidCrystal_I2C_setCursor(0,1);
																						LiquidCrystal_I2C_printstr(status);
																						LiquidCrystal_I2C_setCursor(0,0);
																						Ql_OS_GiveMutex(mutex_id_lcd);
																						position1=i;

																					}
																					else
																					{
																						i++;
																						if(i<20)
																						goto next_pir_enable1;
																						else
																						{
																							Ql_OS_TakeMutex(mutex_id_lcd);
																							LiquidCrystal_I2C_setCursor(0,1);
																							LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																							LiquidCrystal_I2C_setCursor(0,0);
																							Ql_OS_GiveMutex(mutex_id_lcd);
																						}
																					}
																					i++;
																					time1=0;
																					while(touch_flag==0 && time1<10)
																					{
																						time2=0;
																						Ql_Sleep(10);
																					}
																					if(time1<10)
																					{
																						touch_flag=0;
																						butt=touch_to_int();

																						switch(butt)
																						{
																							case 16:
																							{
																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_setCursor(0,1);
																								Ql_OS_GiveMutex(mutex_id_lcd);
																								time1=0;
																								while(touch_flag==0 && time1<10)
																								{
																									time2=0;
																									Ql_Sleep(10);
																								}
																								if(time1<10)
																								{
																									touch_flag=0;
																									butt=touch_to_int();

																									switch(butt)
																									{
																										case 16:
																										{
																											if(i<20)
																											{
																											goto next_pir_enable;
																											}
																											else
																											{
																												Ql_OS_TakeMutex(mutex_id_lcd);
																												LiquidCrystal_I2C_clear();
																												LiquidCrystal_I2C_setCursor(0,0);
																												LiquidCrystal_I2C_printstr("END");
																												Ql_OS_GiveMutex(mutex_id_lcd);
																											}
																										}
																										break;
																										case 4:
																										{
																											int num=position+(position*30);
																											temp_mem[num+27]='E';
																											eeprom_write(num+27,'E');
																											Ql_Sleep(100);
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_clear();
																											LiquidCrystal_I2C_setCursor(0,0);
																											LiquidCrystal_I2C_printstr("ENABLED SUCCESS");
																											Ql_OS_GiveMutex(mutex_id_lcd);
																										}
																										default:
																											break;
																									}
																								}

																							}
																							break;
																							case 4:
																							{
																								int num=position+(position*30);
																								temp_mem[num+27]='E';
																								eeprom_write(num+27,'E');
																								Ql_Sleep(100);
																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_clear();
																								LiquidCrystal_I2C_setCursor(0,0);
																								LiquidCrystal_I2C_printstr("ENABLED SUCCESS");
																								Ql_OS_GiveMutex(mutex_id_lcd);
																							}
																							default:
																								break;

																						}
																					}

																				}
																				break;
																				case 8:
																				{
light_enable:																		Ql_OS_TakeMutex(mutex_id_lcd);
																					LiquidCrystal_I2C_setCursor(0,1);
																					Ql_OS_GiveMutex(mutex_id_lcd);
																					time1=0;
																					while(touch_flag==0 && time1<10)
																					{
																						time2=0;
																						Ql_Sleep(10);
																					}
																					if(time1<10)
																					{
																						touch_flag=0;
																						butt=touch_to_int();

																						switch(butt)
																						{
																							case 4:	// light status
																							{
																								int i=0,position=0,position1=0;
																								char status[25]={0};int row=0;
next_light_enable:																				light_status1(i,&status);
																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_clear();
																								Ql_OS_GiveMutex(mutex_id_lcd);
																								if(status[0]!=NULL)
																								{
																									Ql_OS_TakeMutex(mutex_id_lcd);
																									LiquidCrystal_I2C_setCursor(0,0);
																									LiquidCrystal_I2C_printstr(status);
																									LiquidCrystal_I2C_setCursor(0,0);
																									Ql_OS_GiveMutex(mutex_id_lcd);
																									position=i;
																									row++;
																								}
																								else
																								{

																									i++;
																									if(i<10)
																									goto next_light_enable;
																								}
																								i++;
next_light_enable1:																				light_status1(i,&status);
																								if(status[0]!=NULL&& i<10)
																								{
																									Ql_OS_TakeMutex(mutex_id_lcd);

																									LiquidCrystal_I2C_setCursor(0,1);
																									LiquidCrystal_I2C_printstr(status);
																									LiquidCrystal_I2C_setCursor(0,0);
																									Ql_OS_GiveMutex(mutex_id_lcd);
																									position1=i;
																								}
																								else
																								{
																									i++;
																									if(i<10)
																									goto next_light_enable1;
																									else
																									{
																										Ql_OS_TakeMutex(mutex_id_lcd);
																										LiquidCrystal_I2C_setCursor(0,1);
																										LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																										LiquidCrystal_I2C_setCursor(0,0);
																										Ql_OS_GiveMutex(mutex_id_lcd);
																									}
																								}
																								i++;
																								time1=0;
																								while(touch_flag==0 && time1<10)
																								{
																									time2=0;
																									Ql_Sleep(10);
																								}
																								if(time1<10)
																								{
																									touch_flag=0;
																									butt=touch_to_int();

																									switch(butt)
																									{
																										case 16:
																										{
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_setCursor(0,1);
																											Ql_OS_GiveMutex(mutex_id_lcd);
																											time1=0;
																											while(touch_flag==0 && time1<10)
																											{
																												time2=0;
																												Ql_Sleep(10);
																											}
																											if(time1<10)
																											{
																												touch_flag=0;
																												butt=touch_to_int();

																												switch(butt)
																												{
																													case 16:
																													{
																														if(i<10)
																														{
																															goto next_light_enable;
																														}
																														else
																														{
																															Ql_OS_TakeMutex(mutex_id_lcd);
																															LiquidCrystal_I2C_clear();
																															LiquidCrystal_I2C_setCursor(0,0);
																															LiquidCrystal_I2C_printstr("END");
																															Ql_OS_GiveMutex(mutex_id_lcd);
																														}
																													}
																													break;
																													case 4:
																													{
																														int num=position1+(position1*30)+620;
																														temp_mem[num+27]='E';
																														eeprom_write(num+27,'E');
																														Ql_Sleep(100);
																														Ql_OS_TakeMutex(mutex_id_lcd);
																														LiquidCrystal_I2C_clear();
																														LiquidCrystal_I2C_setCursor(0,0);
																														LiquidCrystal_I2C_printstr("ENABLED SUCCESS");
																														Ql_OS_GiveMutex(mutex_id_lcd);
																													}
																													break;
																													default:
																														break;

																												}
																											}

																										}
																										break;
																										case 4:
																										{
																											int num=position+(position*30)+620;
																											temp_mem[num+27]='E';
																											eeprom_write(num+27,'E');
																											Ql_Sleep(100);
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_clear();
																											LiquidCrystal_I2C_setCursor(0,0);
																											LiquidCrystal_I2C_printstr("ENABLED SUCCESS");
																											Ql_OS_GiveMutex(mutex_id_lcd);
																										}
																										break;
																										default:
																											break;
																									}
																								}


																							}
																							break;
																							case 8:
																							{
																								goto door_enable;
																							}
																							break;
																							case 16:
																							{
																								goto door_enable;
																							}
																							break;
																							case 15:
																							{
																								goto door_enable;
																							}
																							break;
																							case 12:
																							{
																								goto pir_enable;
																							}
																							break;
																							default:
																								break;
																						}
																					}

																				}
																				break;
																				case 12:
																				{
																					goto door_enable;
																				}
																				break;
																				case 16:
																				{
																					goto light_enable;
																				}
																				break;
																				case 15:
																				{
																					goto light_enable;
																				}
																				break;
																				default:
																					break;
																			}
																		}


																	}
																	break;
																	case 16:
																	{
																		goto light_enable;
																	}
																	break;
																	default:
																		break;
																}
															}

														}
														break;
														case 8:		// disable //
														{

															Ql_OS_TakeMutex(mutex_id_lcd);
															LiquidCrystal_I2C_setCursor(8,1);
															Ql_OS_GiveMutex(mutex_id_lcd);
disable:
															time1=0;
															while(touch_flag==0 && time1<10)
															{
																time2=0;
																Ql_Sleep(10);
															}
															if(time1<10)
															{
																touch_flag=0;
																butt=touch_to_int();

																switch(butt)
																{
																	case 4:
																	{
door_disable:															Ql_OS_TakeMutex(mutex_id_lcd);
																		LiquidCrystal_I2C_clear();
																		LiquidCrystal_I2C_blink();
																		LiquidCrystal_I2C_setCursor(0,0);
																		LiquidCrystal_I2C_printstr("DOOR  PIR");
																		LiquidCrystal_I2C_setCursor(0,1);
																		LiquidCrystal_I2C_printstr("LIGHT");
																		LiquidCrystal_I2C_setCursor(0,0);
																		Ql_OS_GiveMutex(mutex_id_lcd);
																		time1=0;
																		while(touch_flag==0 && time1<10)
																		{
																			time2=0;
																			Ql_Sleep(10);
																		}
																		if(time1<10)
																		{
																			touch_flag=0;
																			butt=touch_to_int();

																			switch(butt)
																			{
																				case 4://door disable
																				{

																					int i=0,position=0,position1=0;
																					char status[25]={0};int row=0;
next_door_disable:																	door_status(i,&status);
																					Ql_OS_TakeMutex(mutex_id_lcd);
																					LiquidCrystal_I2C_clear();
																					Ql_OS_GiveMutex(mutex_id_lcd);
																					if(status[0]!=NULL)
																					{
																						Ql_OS_TakeMutex(mutex_id_lcd);
																						LiquidCrystal_I2C_setCursor(0,0);
																						LiquidCrystal_I2C_printstr(status);
																						LiquidCrystal_I2C_setCursor(0,0);
																						Ql_OS_GiveMutex(mutex_id_lcd);
																						position=i;
																						row++;
																					}
																					else
																					{

																						i++;
																						if(i<10)
																						goto next_door_disable;
																					}
																					i++;
next_door_disable1:																	door_status(i,&status);
																					if(status[0]!=NULL&& i<10)
																					{
																						Ql_OS_TakeMutex(mutex_id_lcd);

																						LiquidCrystal_I2C_setCursor(0,1);
																						LiquidCrystal_I2C_printstr(status);
																						LiquidCrystal_I2C_setCursor(0,0);
																						Ql_OS_GiveMutex(mutex_id_lcd);
																						position1=i;

																					}
																					else
																					{
																						i++;
																						if(i<10)
																						goto next_door_disable1;
																						else
																						{
																							Ql_OS_TakeMutex(mutex_id_lcd);
																							LiquidCrystal_I2C_setCursor(0,1);
																							LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																							LiquidCrystal_I2C_setCursor(0,0);
																							Ql_OS_GiveMutex(mutex_id_lcd);
																						}
																					}
																					i++;
																					time1=0;
																					while(touch_flag==0 && time1<10)
																					{
																						time2=0;
																						Ql_Sleep(10);
																					}
																					if(time1<10)
																					{
																						touch_flag=0;
																						butt=touch_to_int();

																						switch(butt)
																						{
																							case 16:
																							{
																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_setCursor(0,1);
																								Ql_OS_GiveMutex(mutex_id_lcd);
																								time1=0;
																								while(touch_flag==0 && time1<10)
																								{
																									time2=0;
																									Ql_Sleep(10);
																								}
																								if(time1<10)
																								{
																									touch_flag=0;
																									butt=touch_to_int();

																									switch(butt)
																									{
																										case 16:
																										{
																											if(i<10)
																											{
																											goto next_door_disable;
																											}
																											else
																											{
																												Ql_OS_TakeMutex(mutex_id_lcd);
																												LiquidCrystal_I2C_clear();
																												LiquidCrystal_I2C_setCursor(0,0);
																												LiquidCrystal_I2C_printstr("END");
																												Ql_OS_GiveMutex(mutex_id_lcd);
																												//goto status_end;
																											}
																										}
																										break;
																										case 15:
																										{
																											i=0;
																											goto next_door_disable;

																										}
																										break;
																										case 4:
																										{
																											int num=position1+(position1*30);
																											temp_mem[num+27]='D';
																											eeprom_write(num+27,'D');
																											Ql_Sleep(100);
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_clear();
																											LiquidCrystal_I2C_setCursor(0,0);
																											LiquidCrystal_I2C_printstr("DISABLED SUCCESS");
																											Ql_OS_GiveMutex(mutex_id_lcd);
																										}
																										break;
																										default:
																											break;
																									}
																								}

																							}
																							break;
																							case 4:
																							{
																								int num=position+(position*30);
																								temp_mem[num+27]='D';
																								eeprom_write(num+27,'D');
																								Ql_Sleep(100);
																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_clear();
																								LiquidCrystal_I2C_setCursor(0,0);
																								LiquidCrystal_I2C_printstr("DISABLED SUCCESS");
																								Ql_OS_GiveMutex(mutex_id_lcd);
																							}
																							default:
																								break;
																						}
																					}

																				}
																				break;
																				case 8:
																				{
pir_disable:																		Ql_OS_TakeMutex(mutex_id_lcd);
																					LiquidCrystal_I2C_setCursor(6,0);
																					Ql_OS_GiveMutex(mutex_id_lcd);
																					time1=0;
																					while(touch_flag==0 && time1<10)
																					{
																						time2=0;
																						Ql_Sleep(10);
																					}
																					if(time1<10)
																					{
																						touch_flag=0;
																						butt=touch_to_int();

																						switch(butt)
																						{
																							case 4:  // pir status
																							{

																								int i=10,position=0,position1=0;
																																																char status[25]={0};int row=0;
next_pir_disable:																				door_status(i,&status);

																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_clear();
																								Ql_OS_GiveMutex(mutex_id_lcd);
																								if(status[0]!=NULL)
																								{
																									Ql_OS_TakeMutex(mutex_id_lcd);
																									LiquidCrystal_I2C_setCursor(0,0);
																									LiquidCrystal_I2C_printstr(status);
																									LiquidCrystal_I2C_setCursor(0,0);
																									Ql_OS_GiveMutex(mutex_id_lcd);
																									position=i;
																									row++;
																								}
																								else
																								{

																									i++;
																									if(i<20)
																									goto next_pir_disable;
																								}
																								i++;
next_pir_disable1:																				door_status(i,&status);
																								if(status[0]!=NULL&& i<20)
																								{
																									Ql_OS_TakeMutex(mutex_id_lcd);

																									LiquidCrystal_I2C_setCursor(0,1);
																									LiquidCrystal_I2C_printstr(status);
																									LiquidCrystal_I2C_setCursor(0,0);
																									Ql_OS_GiveMutex(mutex_id_lcd);
																									position1=i;

																								}
																								else
																								{
																									i++;
																									if(i<20)
																									goto next_pir_disable1;
																									else
																									{
																										Ql_OS_TakeMutex(mutex_id_lcd);
																										LiquidCrystal_I2C_setCursor(0,1);
																										LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																										LiquidCrystal_I2C_setCursor(0,0);
																										Ql_OS_GiveMutex(mutex_id_lcd);
																									}
																								}
																								i++;
																								time1=0;
																								while(touch_flag==0 && time1<10)
																								{
																									time2=0;
																									Ql_Sleep(10);
																								}
																								if(time1<10)
																								{
																									touch_flag=0;
																									butt=touch_to_int();

																									switch(butt)
																									{
																										case 16:
																										{
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_setCursor(0,1);
																											Ql_OS_GiveMutex(mutex_id_lcd);
																											time1=0;
																											while(touch_flag==0 && time1<10)
																											{
																												time2=0;
																												Ql_Sleep(10);
																											}
																											if(time1<10)
																											{
																												touch_flag=0;
																												butt=touch_to_int();

																												switch(butt)
																												{
																													case 16:
																													{
																														if(i<20)
																														{
																														goto next_pir_disable;
																														}
																														else
																														{
																															Ql_OS_TakeMutex(mutex_id_lcd);
																															LiquidCrystal_I2C_clear();
																															LiquidCrystal_I2C_setCursor(0,0);
																															LiquidCrystal_I2C_printstr("END");
																															Ql_OS_GiveMutex(mutex_id_lcd);
																														}
																													}
																													break;
																													case 4:
																													{
																														int num=position+(position*30);
																														temp_mem[num+27]='D';
																														eeprom_write(num+27,'D');
																														Ql_Sleep(100);
																														Ql_OS_TakeMutex(mutex_id_lcd);
																														LiquidCrystal_I2C_clear();
																														LiquidCrystal_I2C_setCursor(0,0);
																														LiquidCrystal_I2C_printstr("DISABLED SUCCESS");
																														Ql_OS_GiveMutex(mutex_id_lcd);
																													}
																													default:
																														break;
																												}
																											}

																										}
																										break;
																										case 4:
																										{
																											int num=position+(position*30);
																											temp_mem[num+27]='D';
																											eeprom_write(num+27,'D');
																											Ql_Sleep(100);
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_clear();
																											LiquidCrystal_I2C_setCursor(0,0);
																											LiquidCrystal_I2C_printstr("DISABLED SUCCESS");
																											Ql_OS_GiveMutex(mutex_id_lcd);
																										}
																										default:
																											break;

																									}
																								}

																							}
																							break;
																							case 8:
																							{
light_disable:																					Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_setCursor(0,1);
																								Ql_OS_GiveMutex(mutex_id_lcd);
																								time1=0;
																								while(touch_flag==0 && time1<10)
																								{
																									time2=0;
																									Ql_Sleep(10);
																								}
																								if(time1<10)
																								{
																									touch_flag=0;
																									butt=touch_to_int();

																									switch(butt)
																									{
																										case 4:	// light status
																										{
																											int i=0,position=0,position1=0;
																											char status[25]={0};int row=0;
next_light_disable:																							light_status1(i,&status);
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_clear();
																											Ql_OS_GiveMutex(mutex_id_lcd);
																											if(status[0]!=NULL)
																											{
																												Ql_OS_TakeMutex(mutex_id_lcd);
																												LiquidCrystal_I2C_setCursor(0,0);
																												LiquidCrystal_I2C_printstr(status);
																												LiquidCrystal_I2C_setCursor(0,0);
																												Ql_OS_GiveMutex(mutex_id_lcd);
																												position=i;
																												row++;
																											}
																											else
																											{

																												i++;
																												if(i<10)
																												goto next_light_disable;
																											}
																											i++;
next_light_disable1:																						light_status1(i,&status);
																											if(status[0]!=NULL&& i<10)
																											{
																												Ql_OS_TakeMutex(mutex_id_lcd);

																												LiquidCrystal_I2C_setCursor(0,1);
																												LiquidCrystal_I2C_printstr(status);
																												LiquidCrystal_I2C_setCursor(0,0);
																												Ql_OS_GiveMutex(mutex_id_lcd);
																												position1=i;
																											}
																											else
																											{
																												i++;
																												if(i<10)
																												goto next_light_disable1;
																												else
																												{
																													Ql_OS_TakeMutex(mutex_id_lcd);
																													LiquidCrystal_I2C_setCursor(0,1);
																													LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																													LiquidCrystal_I2C_setCursor(0,0);
																													Ql_OS_GiveMutex(mutex_id_lcd);
																												}
																											}
																											i++;
																											time1=0;
																											while(touch_flag==0 && time1<10)
																											{
																												time2=0;
																												Ql_Sleep(10);
																											}
																											if(time1<10)
																											{
																												touch_flag=0;
																												butt=touch_to_int();

																												switch(butt)
																												{
																													case 16:
																													{
																														Ql_OS_TakeMutex(mutex_id_lcd);
																														LiquidCrystal_I2C_setCursor(0,1);
																														Ql_OS_GiveMutex(mutex_id_lcd);
																														time1=0;
																														while(touch_flag==0 && time1<10)
																														{
																															time2=0;
																															Ql_Sleep(10);
																														}
																														if(time1<10)
																														{
																															touch_flag=0;
																															butt=touch_to_int();

																															switch(butt)
																															{
																																case 16:
																																{
																																	if(i<10)
																																	{
																																		goto next_light_disable;
																																	}
																																	else
																																	{
																																		Ql_OS_TakeMutex(mutex_id_lcd);
																																		LiquidCrystal_I2C_clear();
																																		LiquidCrystal_I2C_setCursor(0,0);
																																		LiquidCrystal_I2C_printstr("END");
																																		Ql_OS_GiveMutex(mutex_id_lcd);
																																	}
																																}
																																break;
																																case 4:
																																{
																																	int num=position1+(position1*30);
																																	temp_mem[num+27]='D';
																																	eeprom_write(num+27,'D');
																																	Ql_Sleep(100);
																																	Ql_OS_TakeMutex(mutex_id_lcd);
																																	LiquidCrystal_I2C_clear();
																																	LiquidCrystal_I2C_setCursor(0,0);
																																	LiquidCrystal_I2C_printstr("DISABLED SUCCESS");
																																	Ql_OS_GiveMutex(mutex_id_lcd);
																																}
																																break;
																																default:
																																	break;

																															}
																														}

																													}
																													break;
																													case 4:
																													{
																														int num=position+(position*30);
																														temp_mem[num+27]='D';
																														eeprom_write(num+27,'D');
																														Ql_Sleep(100);
																														Ql_OS_TakeMutex(mutex_id_lcd);
																														LiquidCrystal_I2C_clear();
																														LiquidCrystal_I2C_setCursor(0,0);
																														LiquidCrystal_I2C_printstr("DISABLED SUCCESS");
																														Ql_OS_GiveMutex(mutex_id_lcd);
																													}
																													break;
																													default:
																														break;
																												}
																											}


																										}
																										break;
																										case 8:
																										{
																											goto door_disable;
																										}
																										break;
																										case 16:
																										{
																											goto door_disable;
																										}
																										break;
																										case 15:
																										{
																											goto door_disable;
																										}
																										break;
																										case 12:
																										{
																											goto pir_disable;
																										}
																										break;
																										default:
																											break;
																									}
																								}

																							}
																							break;
																							case 12:
																							{
																								goto door_disable;
																							}
																							break;
																							case 16:
																							{
																								goto light_disable;
																							}
																							break;
																							case 15:
																							{
																								goto light_disable;
																							}
																							break;
																							default:
																								break;
																						}
																					}


																				}
																				break;
																				case 16:
																				{
																					goto light_disable;
																				}
																				break;
																				default:
																					break;
																			}
																		}

																	}
																	break;
																	case 8:
																	{
status:
																		Ql_OS_TakeMutex(mutex_id_lcd);

																		LiquidCrystal_I2C_clear();
																		LiquidCrystal_I2C_blink();
																		LiquidCrystal_I2C_setCursor(0,0);
																		LiquidCrystal_I2C_printstr("STATUS");
																		LiquidCrystal_I2C_setCursor(0,0);
																		Ql_OS_GiveMutex(mutex_id_lcd);
																		time1=0;
																		while(touch_flag==0 && time1<10)
																		{
																			time2=0;
																			Ql_Sleep(10);
																		}
																		if(time1<10)
																		{
																			touch_flag=0;
																			butt=touch_to_int();

																			switch(butt)
																			{
																				case 4:
																				{
door_status:																		Ql_OS_TakeMutex(mutex_id_lcd);
																					LiquidCrystal_I2C_clear();
																					LiquidCrystal_I2C_blink();
																					LiquidCrystal_I2C_setCursor(0,0);
																					LiquidCrystal_I2C_printstr("DOOR  PIR");
																					LiquidCrystal_I2C_setCursor(0,1);
																					LiquidCrystal_I2C_printstr("LIGHT");
																					LiquidCrystal_I2C_setCursor(0,0);
																					Ql_OS_GiveMutex(mutex_id_lcd);
																					time1=0;
																					while(touch_flag==0 && time1<10)
																					{
																						time2=0;
																						Ql_Sleep(10);
																					}
																					if(time1<10)
																					{
																						touch_flag=0;
																						butt=touch_to_int();

																						switch(butt)
																						{
																							case 4://door status
																							{

																								int i=0;
																								char status[25]={0};int row=0;
next_door_status:																				door_status(i,&status);
																								Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_clear();
																								Ql_OS_GiveMutex(mutex_id_lcd);
																								if(status[0]!=NULL)
																								{
																									Ql_OS_TakeMutex(mutex_id_lcd);

																									LiquidCrystal_I2C_setCursor(0,row);
																									LiquidCrystal_I2C_printstr(status);
																									LiquidCrystal_I2C_setCursor(0,0);
																									Ql_OS_GiveMutex(mutex_id_lcd);

																									row++;
																								}
																								else
																								{

																									i++;
																									if(i<10)
																									goto next_door_status;
																								}
																								i++;
next_door_status1:																				door_status(i,&status);
																								if(status[0]!=NULL&& i<10)
																								{
																									Ql_OS_TakeMutex(mutex_id_lcd);

																									LiquidCrystal_I2C_setCursor(0,row);
																									LiquidCrystal_I2C_printstr(status);
																									LiquidCrystal_I2C_setCursor(0,0);
																									Ql_OS_GiveMutex(mutex_id_lcd);
																									if(row==1)
																									row=0;
																									else
																										row++;
																								}
																								else
																								{
																									i++;
																									if(i<10)
																									goto next_door_status1;
																									else
																									{
																										Ql_OS_TakeMutex(mutex_id_lcd);
																										LiquidCrystal_I2C_setCursor(0,1);
																										LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																										LiquidCrystal_I2C_setCursor(0,0);
																										Ql_OS_GiveMutex(mutex_id_lcd);
																									}

																								}
																								i++;
																								time1=0;
																								while(touch_flag==0 && time1<10)
																								{
																									time2=0;
																									Ql_Sleep(10);
																								}
																								if(time1<10)
																								{
																									touch_flag=0;
																									butt=touch_to_int();

																									switch(butt)
																									{
																										case 16:
																										{
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_setCursor(0,1);
																											Ql_OS_GiveMutex(mutex_id_lcd);
																											time1=0;
																											while(touch_flag==0 && time1<10)
																											{
																												time2=0;
																												Ql_Sleep(10);
																											}
																											if(time1<10)
																											{
																												touch_flag=0;
																												butt=touch_to_int();

																												switch(butt)
																												{
																													case 16:
																													{
																														if(i<10)
																														{
																														goto next_door_status;
																														}
																														else
																														{
																															Ql_OS_TakeMutex(mutex_id_lcd);
																															LiquidCrystal_I2C_clear();
																															LiquidCrystal_I2C_setCursor(0,0);
																															LiquidCrystal_I2C_printstr("END");
																															Ql_OS_GiveMutex(mutex_id_lcd);
																															//goto status_end;
																														}
																													}
																													break;
																													case 15:
																													{
																														i=0;
																														goto next_door_status;

																													}
																													break;
																													default:
																														break;
																												}
																											}

																										}
																										break;
																										default:
																											break;
																									}
																								}

																							}
																							break;
																							case 8:
																							{
pir_status:																						Ql_OS_TakeMutex(mutex_id_lcd);
																								LiquidCrystal_I2C_setCursor(6,0);
																								Ql_OS_GiveMutex(mutex_id_lcd);
																								time1=0;
																								while(touch_flag==0 && time1<10)
																								{
																									time2=0;
																									Ql_Sleep(10);
																								}
																								if(time1<10)
																								{
																									touch_flag=0;
																									butt=touch_to_int();

																									switch(butt)
																									{
																										case 4:  // pir status
																										{

																											int i=10;
																											char status[25]={0};int row=0;
next_pir_status:																							door_status(i,&status);
																											Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_clear();
																											Ql_OS_GiveMutex(mutex_id_lcd);
																											if(status[0]!=NULL)
																											{
																												Ql_OS_TakeMutex(mutex_id_lcd);

																												LiquidCrystal_I2C_setCursor(0,row);
																												LiquidCrystal_I2C_printstr(status);
																												LiquidCrystal_I2C_setCursor(0,0);
																												Ql_OS_GiveMutex(mutex_id_lcd);

																												row++;
																											}
																											else
																											{

																												i++;
																												if(i<20)
																												goto next_pir_status;
																											}
																											i++;
next_pir_status1:																							door_status(i,&status);
																											if(status[0]!=NULL && i<20)
																											{
																												Ql_OS_TakeMutex(mutex_id_lcd);

																												LiquidCrystal_I2C_setCursor(0,row);
																												LiquidCrystal_I2C_printstr(status);
																												LiquidCrystal_I2C_setCursor(0,0);
																												Ql_OS_GiveMutex(mutex_id_lcd);
																												if(row==1)
																												row=0;
																												else
																													row++;
																											}
																											else
																											{
																												i++;
																												if(i<20)
																												goto next_pir_status1;
																												else
																												{
																													Ql_OS_TakeMutex(mutex_id_lcd);
																													LiquidCrystal_I2C_setCursor(0,1);
																													LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																													LiquidCrystal_I2C_setCursor(0,0);
																													Ql_OS_GiveMutex(mutex_id_lcd);
																												}
																											}
																											i++;
																											time1=0;
																											while(touch_flag==0 && time1<10)
																											{
																												time2=0;
																												Ql_Sleep(10);
																											}
																											if(time1<10)
																											{
																												touch_flag=0;
																												butt=touch_to_int();

																												switch(butt)
																												{
																													case 16:
																													{
																														Ql_OS_TakeMutex(mutex_id_lcd);
																														LiquidCrystal_I2C_setCursor(0,1);
																														Ql_OS_GiveMutex(mutex_id_lcd);
																														time1=0;
																														while(touch_flag==0 && time1<10)
																														{
																															time2=0;
																															Ql_Sleep(10);
																														}
																														if(time1<10)
																														{
																															touch_flag=0;
																															butt=touch_to_int();

																															switch(butt)
																															{
																																case 16:
																																{
																																	if(i<20)
																																	{
																																	goto next_pir_status;
																																	}
																																	else
																																	{
																																		Ql_OS_TakeMutex(mutex_id_lcd);
																																		LiquidCrystal_I2C_clear();
																																		LiquidCrystal_I2C_setCursor(0,0);
																																		LiquidCrystal_I2C_printstr("END");
																																		Ql_OS_GiveMutex(mutex_id_lcd);
																																	}
																																}
																																break;
																															}
																														}

																													}
																													break;
																													default:
																														break;
																												}
																											}

																										}
																										break;
																										case 8:
																										{
light_status:																								Ql_OS_TakeMutex(mutex_id_lcd);
																											LiquidCrystal_I2C_setCursor(0,1);
																											Ql_OS_GiveMutex(mutex_id_lcd);
																											time1=0;
																											while(touch_flag==0 && time1<10)
																											{
																												time2=0;
																												Ql_Sleep(10);
																											}
																											if(time1<10)
																											{
																												touch_flag=0;
																												butt=touch_to_int();

																												switch(butt)
																												{
																													case 4:	// light status
																													{
																														int i=0;
																														char status[25]={0};int row=0;
next_light_status:																										light_status1(i,&status);
																														Ql_OS_TakeMutex(mutex_id_lcd);
																														LiquidCrystal_I2C_clear();
																														Ql_OS_GiveMutex(mutex_id_lcd);
																														if(status[0]!=NULL)
																														{
																															Ql_OS_TakeMutex(mutex_id_lcd);

																															LiquidCrystal_I2C_setCursor(0,row);
																															LiquidCrystal_I2C_printstr(status);
																															LiquidCrystal_I2C_setCursor(0,0);
																															Ql_OS_GiveMutex(mutex_id_lcd);

																															row++;
																														}
																														else
																														{

																															i++;
																															if(i<10)
																															goto next_light_status;
																														}
																														i++;
next_light_status1:																										light_status1(i,&status);
																														if(status[0]!=NULL&& i<10)
																														{
																															Ql_OS_TakeMutex(mutex_id_lcd);

																															LiquidCrystal_I2C_setCursor(0,row);
																															LiquidCrystal_I2C_printstr(status);
																															LiquidCrystal_I2C_setCursor(0,0);
																															Ql_OS_GiveMutex(mutex_id_lcd);
																															if(row==1)
																															row=0;
																															else
																																row++;
																														}
																														else
																														{
																															i++;
																															if(i<10)
																															goto next_light_status1;
																															else
																															{
																																Ql_OS_TakeMutex(mutex_id_lcd);
																																LiquidCrystal_I2C_setCursor(0,1);
																																LiquidCrystal_I2C_printstr("NO DEVICE FOUND");
																																LiquidCrystal_I2C_setCursor(0,0);
																																Ql_OS_GiveMutex(mutex_id_lcd);
																															}
																														}
																														i++;
																														time1=0;
																														while(touch_flag==0 && time1<10)
																														{
																															time2=0;
																															Ql_Sleep(10);
																														}
																														if(time1<10)
																														{
																															touch_flag=0;
																															butt=touch_to_int();

																															switch(butt)
																															{
																																case 16:
																																{
																																	Ql_OS_TakeMutex(mutex_id_lcd);
																																	LiquidCrystal_I2C_setCursor(0,1);
																																	Ql_OS_GiveMutex(mutex_id_lcd);
																																	time1=0;
																																	while(touch_flag==0 && time1<10)
																																	{
																																		time2=0;
																																		Ql_Sleep(10);
																																	}
																																	if(time1<10)
																																	{
																																		touch_flag=0;
																																		butt=touch_to_int();

																																		switch(butt)
																																		{
																																			case 16:
																																			{
																																				if(i<10)
																																				{
																																				goto next_light_status;
																																				}
																																				else
																																				{
																																					Ql_OS_TakeMutex(mutex_id_lcd);
																																					LiquidCrystal_I2C_clear();
																																					LiquidCrystal_I2C_setCursor(0,0);
																																					LiquidCrystal_I2C_printstr("END");
																																					Ql_OS_GiveMutex(mutex_id_lcd);
																																				}
																																			}
																																			break;
																																			default:
																																				break;

																																		}
																																	}

																																}
																																break;
																																default:
																																	break;
																															}
																														}


																													}
																													break;
																													case 8:
																													{
																														goto door_status;
																													}
																													break;
																													case 16:
																													{
																														goto door_status;
																													}
																													break;
																													case 15:
																													{
																														goto door_status;
																													}
																													break;
																													case 12:
																													{
																														goto pir_status;
																													}
																													break;
																													default:
																														break;
																												}
																											}

																										}
																										break;
																										case 12:
																										{
																											goto door_status;
																										}
																										break;
																										case 16:
																										{
																											goto light_status;
																										}
																										break;
																										case 15:
																										{
																											goto light_status;
																										}
																										break;
																										default:
																											break;
																									}
																								}


																							}
																							break;
																							case 16:
																							{
																								goto light_status;
																							}
																							break;
																							default:
																								break;
																						}
																					}

																				}
																				break;
																				case 15:
																				{
																					goto start1;
																				}
																				break;
																				case 16:
																				{
																					goto start1;
																				}
																				break;
																				case 12:
																				{
																					goto start1;
																				}
																				break;
																				default:
																					break;
																			}
																		}



																	}
																	break;
																	case 12:		// start //
																	{
																		Ql_OS_TakeMutex(mutex_id_lcd);
																		LiquidCrystal_I2C_setCursor(0,1);
																		Ql_OS_GiveMutex(mutex_id_lcd);
																		goto enable;
																	}
																	break;
																	case 15:		// start //
																	{
																		Ql_OS_TakeMutex(mutex_id_lcd);
																		LiquidCrystal_I2C_setCursor(8,0);
																		Ql_OS_GiveMutex(mutex_id_lcd);
																		goto delete;
																	}
																	break;
																	case 16:		// start //
																	{
																		Ql_OS_TakeMutex(mutex_id_lcd);
																		LiquidCrystal_I2C_setCursor(0,0);
																		Ql_OS_GiveMutex(mutex_id_lcd);
																		goto status;
																	}
																	break;
																	default:
																		break;
																}
															}
														}
														break;

														case 12:
														{
															Ql_OS_TakeMutex(mutex_id_lcd);
															LiquidCrystal_I2C_setCursor(8,0);
															Ql_OS_GiveMutex(mutex_id_lcd);
															goto delete;
														}
														break;
														case 16:
														{
															Ql_OS_TakeMutex(mutex_id_lcd);
															LiquidCrystal_I2C_setCursor(0,0);
															Ql_OS_GiveMutex(mutex_id_lcd);
															goto add;
														}
														break;
														case 15:
														{
															Ql_OS_TakeMutex(mutex_id_lcd);
															LiquidCrystal_I2C_setCursor(0,0);
															Ql_OS_GiveMutex(mutex_id_lcd);
															goto status;
														}
														break;
														default:
															break;
													}
												}
											}
											break;

											case 12:
											{
												Ql_OS_TakeMutex(mutex_id_lcd);
												LiquidCrystal_I2C_setCursor(0,0);
												Ql_OS_GiveMutex(mutex_id_lcd);
												goto add;
											}
											break;
											case 16:
											{
												Ql_OS_TakeMutex(mutex_id_lcd);
												LiquidCrystal_I2C_setCursor(8,1);
												Ql_OS_GiveMutex(mutex_id_lcd);
												goto disable;
											}
											break;
											case 15:
											{
												Ql_OS_TakeMutex(mutex_id_lcd);
												LiquidCrystal_I2C_setCursor(8,1);
												Ql_OS_GiveMutex(mutex_id_lcd);
												goto disable;
											}
											break;
											default:
												break;


										}
									}

								}
								break;
								case 16:		// enable  //
								{
									Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_setCursor(0,1);
									Ql_OS_GiveMutex(mutex_id_lcd);
									goto enable;
								}
								break;
								case 15:		// enable //
								{
									Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_setCursor(0,1);
									Ql_OS_GiveMutex(mutex_id_lcd);
									goto enable;
								}
								break;
								case 12:		// disable //
								{
									Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_setCursor(8,1);
									Ql_OS_GiveMutex(mutex_id_lcd);
									goto disable;
								}
								break;
								default:
									break;

							}
						}
						lcd_disp=0;
					}
					break;
					case 8:
					{

						if(!locflag)
						{

							int m = 0, j = 0,i=0;
							char str[150];
							char arr[30],devicename[18],clo[]="clos",op[]="open";
							int k, s,n,out;

							int no_enable = 0, no_ones = 0;
							while (m < 10)
							{
					              i = m + (m * 3 * 10);
					              if (temp_mem[i + 27] == 'E')
					              {
					                for (s = 0; s < 30; s++)
					                  arr[s] = NULL;

					                no_enable++;
					                for (n = 0; n < 18; n++)
					                {
					                  devicename[n] = temp_mem[i + n];
					                  arr[n] = devicename[n];
					                  if (temp_mem[i + n + 1] == NULL)
					                    break;
					                }


					                arr[n++] = ':';

					                if (temp_mem[i + 26] == '1')
					                {
					                  no_ones++;
					                  for ( s = 0; s < 6; n++, s++)
					                    arr[n] = clo[s];

					                }
					                else
					                {
					                  for (s = 0; s < 6; n++, s++)
					                    arr[n] = op[s];

					                }
					                arr[n++] = ',';
					                arr[n++] = '\n';


					                Ql_strncat(str, arr, n);



					              }
					              m++;
					          }

					            if (no_enable == no_ones)
					            {
					            	locflag = 1;
					            	eeprom_write(1084,'1');
					            	Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_clear();
									LiquidCrystal_I2C_setCursor(0,0);
									LiquidCrystal_I2C_printstr("SECURITY MODE IS");
									LiquidCrystal_I2C_setCursor(0,1);
									LiquidCrystal_I2C_printstr("ACTIVATED!.");
									Ql_OS_GiveMutex(mutex_id_lcd);
									get_time();

									Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY ON.",d_t);
									Ql_OS_TakeMutex(mutex_id_serial);
									APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
									Ql_OS_GiveMutex(mutex_id_serial);



									 m=1;
									 while (m <= 5)
									 {
										 i = m + (m * 12) + 1000;


											 if(temp_mem[i]!=NULL)
											 {

												 for (j = 0; j <= 15; j++)
												 {
													 mobilenum[j] = NULL;
												 }
												 for (j = 0; j <= 12; j++)
												 {
													 mobilenum[j] =  temp_mem[i + j];
												 }

												Ql_OS_TakeMutex(mutex_id_serial);
												APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
												Ql_OS_GiveMutex(mutex_id_serial);
												Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
												Ql_Sleep(1000);
											 }


										 m++;
									 }
					            }
					            else
					            {
					            	Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_clear();
									LiquidCrystal_I2C_setCursor(0,0);
									LiquidCrystal_I2C_printstr("DOOR NOT CLOSED");
									LiquidCrystal_I2C_setCursor(0,1);
									LiquidCrystal_I2C_printstr("CHECK THE DOORS");
									Ql_OS_GiveMutex(mutex_id_lcd);
					            }



						}
						else
						{
							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,0);
							LiquidCrystal_I2C_printstr("SECURITY MODE IS");
							LiquidCrystal_I2C_setCursor(0,1);
							LiquidCrystal_I2C_printstr("ALREADY ACTIVATED");
							Ql_OS_GiveMutex(mutex_id_lcd);
						}


					}
					break;
					case 12:
					{

						if(locflag)
						{
							Ql_GPIO_SetLevel(SIREN,PINLEVEL_LOW);
							locflag=0;
							eeprom_write(1084,'0');
							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,0);
							LiquidCrystal_I2C_printstr("SECURITY MODE IS");
							LiquidCrystal_I2C_setCursor(0,1);
							LiquidCrystal_I2C_printstr("DEACTIVATED!.");
							Ql_OS_GiveMutex(mutex_id_lcd);
							get_time();

							Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY OFF.",d_t);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);

							int i=0,j=0,m=0;

							 m=1;
							 while (m <= 5)
							 {
								 i = m + (m * 12) + 1000;


									 if(temp_mem[i]!=NULL)
									 {

										 for (j = 0; j <= 15; j++)
										 {
											 mobilenum[j] = NULL;
										 }
										 for (j = 0; j <= 12; j++)
										 {
											 mobilenum[j] =  temp_mem[i + j];
										 }

										Ql_OS_TakeMutex(mutex_id_serial);
										APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
										Ql_OS_GiveMutex(mutex_id_serial);
										Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
										Ql_Sleep(1000);
									 }


								 m++;
							 }

						}
						else
						{
							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,0);
							LiquidCrystal_I2C_printstr("SECURITY MODE IS");
							LiquidCrystal_I2C_setCursor(0,1);
							LiquidCrystal_I2C_printstr("ALREADY DEACTIVATED");
							Ql_OS_GiveMutex(mutex_id_lcd);
						}


					}
					break;
					case 16:
					{

						Ql_OS_TakeMutex(mutex_id_lcd);
						LiquidCrystal_I2C_clear();
						LiquidCrystal_I2C_setCursor(0,0);
						LiquidCrystal_I2C_printstr("BT visiblity ON");
						LiquidCrystal_I2C_setCursor(0,1);
						LiquidCrystal_I2C_printstr("for 60 secs.");
						Ql_OS_GiveMutex(mutex_id_lcd);
						okflag = waitcount = 0;
						Ql_RIL_SendATCmd("AT+QBTVISB=2,60",Ql_strlen("AT+QBTVISB=2,60"),ATResponse_Handler,NULL,0);
						wait_ok();
						 Ql_Sleep(2000);
						/*time2=0;
						Ql_OS_TakeMutex(mutex_id_lcd);
						LiquidCrystal_I2C_clear();
						LiquidCrystal_I2C_setCursor(0,0);
						LiquidCrystal_I2C_printstr("    SIREN ");
						LiquidCrystal_I2C_setCursor(0,1);
						LiquidCrystal_I2C_printstr("ON           OFF");
						Ql_OS_GiveMutex(mutex_id_lcd);
						touch_flag=0;
						time1=0;
						while(touch_flag==0 && time1<10)
						{
							 Ql_Sleep(10);
						}
						if(time1<10)
						{
							touch_flag=0;
							butt=touch_to_int();
							switch(butt)
							{
								case 1:
								{
									Ql_GPIO_SetLevel(SIREN,PINLEVEL_HIGH);
									Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_clear();
									LiquidCrystal_I2C_setCursor(0,0);
									LiquidCrystal_I2C_printstr("SIREN is ON ");
									Ql_OS_GiveMutex(mutex_id_lcd);
								}
								break;
								case 4:
								{
									Ql_GPIO_SetLevel(SIREN,PINLEVEL_LOW);
									Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_clear();
									LiquidCrystal_I2C_setCursor(0,0);
									LiquidCrystal_I2C_printstr("SIREN is OFF ");
									Ql_OS_GiveMutex(mutex_id_lcd);
								}
								break;
							}

						}*/
					}
					break;
					default:
						break;

				}
				time2=10;
			}
			else
			{
				Ql_OS_TakeMutex(mutex_id_serial);
				APP_DEBUG("\r\n<--Task 1 running----->\r\n");
				Ql_OS_GiveMutex(mutex_id_serial);
			}
		    Ql_Sleep(100);
	}
}
void proc_subtask2(s32 taskId)
{


	while(1)
	{

		u8 rec_done=0;
		u8 out,m,addr,k=0;
		u32 i=0;
		char ch[10];
		int a1;
		float bat_vol1, bat_vol2;
		int l = 0;

		if(rf_int==1)
		{
			rf_int=0;

			Ql_OS_TakeMutex(mutex_id_serial);
			Ql_Sleep(10);
			APP_DEBUG("\r\n<--rf message\r\n");
			Ql_Sleep(10);
			interrupthandler();
			Ql_Sleep(10);
			rec_done=receivedone();
			Ql_Sleep(10);
			readrssi(0);
			APP_DEBUG("\r\n<--rssi=%d",RSSI);
			Ql_OS_GiveMutex(mutex_id_serial);
			if(rec_done)
			{

				lcd_disp=1;
				out = 1; m = 0;
				 while (m < 10 && out)
				 {
					  if (data[5] == '0' && data[6] == '1')
					  {
						i = m + (m * 3 * 10);
					  }
					  else if (data[5] == '0' && data[6] == '2')
					  {
						i = m + (m * 3 * 10) + 310;
					  }
					  else if (data[5] == '0' && data[6] == '3')
					  {
						i = m + (m * 3 * 10) + 620;
					  }
					  else
					  {
						i = 0;
					  }

					  ch[0] = temp_mem[i + 19];
					  ch[1] = temp_mem[i + 20];
					  ch[2] = temp_mem[i + 21];
					  ch[3] = temp_mem[i + 22];
					  ch[4] = temp_mem[i + 23];
					  ch[5] = temp_mem[i + 24];
					   Ql_OS_TakeMutex(mutex_id_serial);
					   Ql_Sleep(10);
					   APP_DEBUG("\r\n<-- %s,%s-------->\r\n",ch,data);
					   Ql_Sleep(10);
					   Ql_OS_GiveMutex(mutex_id_serial);

					  if ((ch[0] == data[1]) && (ch[1] == data[2]) && (ch[2] == data[3]) && (ch[3] == data[4]) && (ch[4] == data[5]) && (ch[5] == data[6]))
					  {
						addr = (((int)data[1] - 48) * (1000)) + (((int)data[2] - 48) * (100)) + (((int)data[3] - 48) * (10)) + (((int)data[4] - 48) * (1));
						set_mode(3);
						payloadlen = 0;
						Ql_Sleep(500);

						Ql_OS_TakeMutex(mutex_id_serial);
						_send(addr, "ok", 2, 1);
						Ql_OS_GiveMutex(mutex_id_serial);

						if (data[5] == '0' && data[6] == '1')
						{
						  if (data[7] != (temp_mem[i + 26]))
							out = 0;
						}
						else
						  out = 0;
						if ((data[5] == '0' && data[6] == '1')||(data[5] == '0' && data[6] == '2'))
						{
							eeprom_write(i+26,data[7]);
							temp_mem[i + 26] = data[7];
							eeprom_write(i + 28, data[8]);
							temp_mem[i + 28] = data[8];
							eeprom_write(i+29, data[9]);
							temp_mem[i + 29] = data[9];
							eeprom_write(i + 30, data[10]);
							temp_mem[i + 30] = data[10];
						}
						else
						{
							if(data[7] == 'L')
							{
								if(data[8] == '1')
								{
									eeprom_write(i+28,data[9]);
									temp_mem[i + 28] = data[9];
									if(data[10] == ',')
									{
										if(data[11] == 'L' && data[12] == '2')
										{
											eeprom_write(i+29,data[13]);
											temp_mem[i + 29] = data[13];
										}
										if(data[15] == 'L' && data[16] == '3')
										{
											eeprom_write(i+30,data[17]);
											temp_mem[i + 30] = data[17];
										}

									}
								}
								else if(data[8] == '2')
								{
									eeprom_write(i+29,data[9]);
									temp_mem[i + 29] = data[9];
								}
								else if(data[8] == '3')
								{
									eeprom_write(i+30,data[11]);
									temp_mem[i + 30] = data[11];
								}
							}
							else if(data[7] == 'F')
							{
								if(data[8] == '1')
								{
									eeprom_write(i+30,data[11]);
									temp_mem[i + 30] = data[11];
								}
							}
						}
						rf_change2=1;
						buzz();
					  }
					  m++;
				}

				m--;

				if (out == 0)
				{
					time2=0;
					for (u8 n = 0; n < 18; n++)
				   {
					 devicename[n] = temp_mem[i + n];
				   }
				   if (data[5] == '0' && data[6] == '1')
				   {
					   Ql_OS_TakeMutex(mutex_id_serial);
					   APP_DEBUG("\r\n<--Device name: %s",devicename);
					   Ql_OS_GiveMutex(mutex_id_serial);
					   if (temp_mem[i + 26] == '1')
					   {
						   Ql_OS_TakeMutex(mutex_id_serial);
						   APP_DEBUG("\r\n<--: closed");
						   Ql_OS_GiveMutex(mutex_id_serial);

						   Ql_OS_TakeMutex(mutex_id_lcd);
						   LiquidCrystal_I2C_clear();
						   LiquidCrystal_I2C_setCursor(0,0);

						   for (u8 n = 0; n < 18; n++)
						   {
							   LiquidCrystal_I2C_printchar(devicename[n]);
							   if(devicename[n+1]==NULL)
								   break;
						   }
						   LiquidCrystal_I2C_setCursor(0,1);
						   LiquidCrystal_I2C_printnstr(": ClOS ",7);
						   Ql_OS_GiveMutex(mutex_id_lcd);

					   }
					   else
					   {
						   Ql_OS_TakeMutex(mutex_id_serial);
						   APP_DEBUG("\r\n<--: opened");
						   Ql_OS_GiveMutex(mutex_id_serial);

						   Ql_OS_TakeMutex(mutex_id_lcd);
						   LiquidCrystal_I2C_clear();
						   LiquidCrystal_I2C_setCursor(0,0);
						   for (u8 n = 0; n < 18; n++)
						   {
							   LiquidCrystal_I2C_printchar(devicename[n]);
							   if(devicename[n+1]==NULL)
								   break;
						   }
						   LiquidCrystal_I2C_setCursor(0,1);
						   LiquidCrystal_I2C_printnstr(": OPEN ",7);
						   Ql_OS_GiveMutex(mutex_id_lcd);
					   }

					 int logi = 0;
					 for (u8 j = 0; j < 5; j++)
					 {
					   u8 k = j + (j * 3 * 10);
					   ch[0] = temp_mem[k + 18];
					   ch[1] = temp_mem[k + 27];
					   ch[2] = temp_mem[k + 26];

					   if (ch[0] == '*' && ch[1] == 'E' && ch[2] == '0')
						 logi = 1;
					 }

					 if (logi == 0)
					 {
						 Ql_GPIO_SetLevel(INDICATION,PINLEVEL_HIGH);
					 }

					 else
					 {

						 Ql_GPIO_SetLevel(INDICATION,PINLEVEL_LOW);

					 }
					 if (locflag == 1)
					 {
					   if (temp_mem[i + 27] == 'E')
					   {
						 u8 j = 0;
						 while (j < 15)
						 {

							 Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
							 Ql_Sleep(200);
							 Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
							 Ql_Sleep(200);
							 j++;
							 if (locflag == 0)
							 {
								 break;
							 }

						 }
						 if (locflag == 1)
						 {
							 Ql_GPIO_SetLevel(SIREN,PINLEVEL_HIGH);
							 Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
							 security_flag = 1;

						 }
					   }
					 }
				   }

				   else if (data[5] == '0' && data[6] == '2')
				   {
					   Ql_OS_TakeMutex(mutex_id_serial);
					   APP_DEBUG("\r\n<--Device name: %s",devicename);
					   APP_DEBUG("\r\n<--Motion Detected!!!!!!!!!!!!");
					   Ql_OS_GiveMutex(mutex_id_serial);


					   Ql_OS_TakeMutex(mutex_id_lcd);
					   LiquidCrystal_I2C_clear();
					   LiquidCrystal_I2C_setCursor(0,0);

					   for (u8 n = 0; n < 18; n++)
					   {
						   LiquidCrystal_I2C_printchar(devicename[n]);
						   if(devicename[n+1]==NULL)
							   break;
					   }
					   LiquidCrystal_I2C_setCursor(0,1);
					   LiquidCrystal_I2C_printnstr(":motion detected",16);
					   Ql_OS_GiveMutex(mutex_id_lcd);

					   if (locflag == 1)
					   {
						 if (temp_mem[i + 27] == 'E')
						 {
							 Ql_GPIO_SetLevel(SIREN,PINLEVEL_HIGH);
							 pir_flag = 1;
						 }
					   }
					}

					else if (data[5] == '0' && data[6] == '3')
					{

						Ql_OS_TakeMutex(mutex_id_serial);
						APP_DEBUG("\r\n<--Device name: %s",devicename);
						Ql_OS_GiveMutex(mutex_id_serial);
						 Ql_OS_TakeMutex(mutex_id_lcd);
						   LiquidCrystal_I2C_clear();
						   LiquidCrystal_I2C_setCursor(0,0);

						   for (u8 n = 0; n < 18; n++)
						   {
							   LiquidCrystal_I2C_printchar(devicename[n]);
							   if(devicename[n+1]==NULL)
								   break;
						   }
						   if(data[7] == 'L')
							{
								if(data[8] == '1')
								{
									if(data[10]=='#')
									{
										LiquidCrystal_I2C_printnstr(" 1",2);
										if(data[9]=='1')
										{

											LiquidCrystal_I2C_setCursor(0,1);
											LiquidCrystal_I2C_printnstr(":Light on",9);
											Ql_OS_GiveMutex(mutex_id_lcd);
										}
										else
										{
											LiquidCrystal_I2C_setCursor(0,1);
											LiquidCrystal_I2C_printnstr(":Light off",10);
											Ql_OS_GiveMutex(mutex_id_lcd);
										}
									}
									else if(data[10]==',')
									{
										LiquidCrystal_I2C_setCursor(0,1);
										LiquidCrystal_I2C_printnstr(":all lights off",15);
										Ql_OS_GiveMutex(mutex_id_lcd);
									}

								}
								else if(data[8] == '2')
								{
									LiquidCrystal_I2C_printnstr(" 2",2);
									if(data[9]=='1')
									{

										LiquidCrystal_I2C_setCursor(0,1);
										LiquidCrystal_I2C_printnstr(":Light on",9);
										Ql_OS_GiveMutex(mutex_id_lcd);
									}
									else
									{
										LiquidCrystal_I2C_setCursor(0,1);
										LiquidCrystal_I2C_printnstr(":Light off",10);
										Ql_OS_GiveMutex(mutex_id_lcd);
									}
								}
								else if(data[8] == '3')
								{
									LiquidCrystal_I2C_printnstr(" 3",2);
									if(data[9]=='1')
									{

										LiquidCrystal_I2C_setCursor(0,1);
										LiquidCrystal_I2C_printnstr(":Light on",9);
										Ql_OS_GiveMutex(mutex_id_lcd);
									}
									else
									{
										LiquidCrystal_I2C_setCursor(0,1);
										LiquidCrystal_I2C_printnstr(":Light off",10);
										Ql_OS_GiveMutex(mutex_id_lcd);
									}
								}


							}
							else if(data[7] == 'F')
							{
								if(data[8] == '1')
								{
									LiquidCrystal_I2C_printnstr(" 1",2);
									if(data[9]=='1')
									{

										LiquidCrystal_I2C_setCursor(0,1);
										LiquidCrystal_I2C_printnstr(":Fan on  S:",11);
										LiquidCrystal_I2C_printchar(data[11]);
										Ql_OS_GiveMutex(mutex_id_lcd);

									}
									else
									{
										LiquidCrystal_I2C_setCursor(0,1);
										LiquidCrystal_I2C_printnstr(":Fan off",10);
										Ql_OS_GiveMutex(mutex_id_lcd);
									}
								}

							}



						 light_flag = 1;


					}
					else
					{

					}

				   	if ((data[5] == '0' && data[6] == '1')|| (data[5] == '0' && data[6] == '2'))
					{

					   char a[3];
					   Ql_OS_TakeMutex(mutex_id_serial);
					   APP_DEBUG("\r\n<--BATTERY TEST");
					   Ql_OS_GiveMutex(mutex_id_serial);

					   a[0] = (char)temp_mem[i + 28];
					   a[1] = (char)temp_mem[i + 29];
					   a[2] = (char)temp_mem[i + 30];

					   unsigned int decValue = 0;
					   int nextint;

					   for (int i = 0; i < 3; i++)
					   {

						 nextint = (int)a[i];
						 if ((nextint >= 48) && (nextint <= 57))
						 {
							 nextint = map(nextint, 48, 57, 0, 9);
						 }
						 if ((nextint >= 65) && (nextint <= 70))
						 {
							 nextint = map(nextint, 65, 70, 10, 15);
						 }
						 if ((nextint >= 97) && (nextint <= 102))
						 {
							 nextint = map(nextint, 97, 102, 10, 15);
						 }

						 nextint = constrain(nextint, 0, 15);

						 decValue = (decValue * 16) + nextint;
					   }


					   a1 = decValue;

					   bat_vol1 = a1 * (1.024 / 1024.0);

					   bat_vol2 = bat_vol1 * 3.2;

					   Ql_OS_TakeMutex(mutex_id_serial);
					   APP_DEBUG("\r\n<--BATTERY voltage=%f  dec=%d ",bat_vol2,a1);
					   Ql_OS_GiveMutex(mutex_id_serial);

					   if ( bat_vol2 <= 2.50)
					   {

						 bat_low = 1;
						 Ql_OS_TakeMutex(mutex_id_serial);
						 APP_DEBUG("\r\n<--BATTERY LOW");
						 Ql_OS_GiveMutex(mutex_id_serial);

					   }
						 l = bat_vol2 * 100;
						 k = l / 100;
						 z[0] =(char)(k + 48);
						 z[1] = '.';
						 k = (l % 100) / 10;
						 z[2] =(char)(k + 48);
						 k = l % 10;
						 z[3] =(char)(k + 48);

						 Ql_OS_TakeMutex(mutex_id_serial);
						 for (u8 j = 0; j < 5; j++)
						 {
						 APP_DEBUG("\r\n<--BATTERY=%d",z[j]);
						 }
						 Ql_OS_GiveMutex(mutex_id_serial);


						 Ql_OS_TakeMutex(mutex_id_lcd);
						 for (u8 j = 0; j < 4; j++)
						 {
							 LiquidCrystal_I2C_printchar((char)z[j]);
						 }
						 Ql_OS_GiveMutex(mutex_id_lcd);

					 }
				   	rf_change = 1;


				}

			}
			lcd_disp=0;
			rec_done=0;
			set_mode(3);

		}

		else
		{
			Ql_OS_TakeMutex(mutex_id_serial);
			APP_DEBUG("\r\n<--Task 2 running----->\r\n");
			Ql_OS_GiveMutex(mutex_id_serial);
		}
		Ql_Sleep(100);
	}
}

void proc_subtask3(s32 taskId)
{

		while(1)
		{
			if(entry_flag==1)
			{

				entry_flag=0;

				s32 ret=Ql_IIC_Config(1,TRUE,(0x27<<1), 100);
				 if(ret < 0)
				{
					 Ql_OS_TakeMutex(mutex_id_serial);
					 APP_DEBUG("\r\n<--iic config failed\r\n");
					 Ql_OS_GiveMutex(mutex_id_serial);
				}

				 Ql_OS_TakeMutex(mutex_id_lcd);
				 LiquidCrystal_I2C((0x27<<1),16,2, 0);
				 LiquidCrystal_I2C_begin();
				 LiquidCrystal_I2C_setCursor(0,0);
				 LiquidCrystal_I2C_printstr(">>>> i Home <<<<");
				 LiquidCrystal_I2C_setCursor(0,1);
				 LiquidCrystal_I2C_printstr("FWV: ");
				 LiquidCrystal_I2C_printstr(version);
				 Ql_OS_GiveMutex(mutex_id_lcd);
				Ql_Sleep(100);


				ret=Ql_IIC_Config(1,TRUE, 0xA0, 100);

				 if(ret < 0)
				{
					 Ql_OS_TakeMutex(mutex_id_serial);
					APP_DEBUG("\r\n<--iic config failed\r\n");
					Ql_OS_GiveMutex(mutex_id_serial);
				}

				/*for(u32 j=0;j<1100;j++)
				{
					eeprom_write(j,0);
				}*/

				for(u32 j=0;j<1100;j++)
				{
					temp_mem[j]=eeprom_read(j);
				}

				if(temp_mem[1084]=='1')
					locflag =1;
				else
					locflag =0;


				/* u32 j=0;
				 for(j=0;j<30;j++)
				{
					Ql_OS_TakeMutex(mutex_id_serial);
					Ql_Sleep(100);
					APP_DEBUG("\r\n<--DATA[%d]=%c----->\r\n",j,temp_mem[j]);
					Ql_Sleep(100);
					Ql_OS_GiveMutex(mutex_id_serial);
				}

				 for(j=620;j<900;j++)
				{
					Ql_OS_TakeMutex(mutex_id_serial);
					Ql_Sleep(100);
					APP_DEBUG("\r\n<--DATA[%d]=%c----->\r\n",j,temp_mem[j]);
					Ql_Sleep(100);
					Ql_OS_GiveMutex(mutex_id_serial);
				}*/

				Ql_OS_TakeMutex(mutex_id_lcd);
				LiquidCrystal_I2C_setCursor(0,1);
				LiquidCrystal_I2C_printstr("Initialising....");
				Ql_OS_GiveMutex(mutex_id_lcd);
				gsm_init();
				bt_init();
				Ql_OS_SendMessage(0,MSG_ID_gprs_init , 1, 0);
				Ql_OS_TakeMutex(mutex_id_lcd);
				LiquidCrystal_I2C_setCursor(0,1);
				LiquidCrystal_I2C_printstr("Ready...........");
				Ql_OS_GiveMutex(mutex_id_lcd);

			}
			else if(security_flag||pir_flag)
			{

				u32 i,j;
				int m=1;

				if(security_flag)
				{
					Ql_sprintf(gsm_msg,"i Home:\n ALERT:\n %s:opened.",devicename);
				}
				else if(pir_flag)
				{
					Ql_sprintf(gsm_msg,"i Home:\n ALERT:\n %s:Motion detected.",devicename);
				}
				security_flag=0;
				pir_flag=0;
				while (m <= 5)
				{
				  if(locflag)
				  {
					  i = m + (m * 12) + 1000;

					if(temp_mem[i]!=NULL)
					{
						for (j = 0; j <= 15; j++)
						{
							mobilenum[j] = NULL;
						}
						for (j = 0; j <= 12; j++)
						{
							mobilenum[j] =  temp_mem[i + j];
						}

						Ql_OS_TakeMutex(mutex_id_serial);
						APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
						Ql_OS_GiveMutex(mutex_id_serial);



						RIL_SMS_SendSMS_Text(mobilenum,Ql_strlen(mobilenum),LIB_SMS_CHARSET_GSM,gsm_msg, Ql_strlen(gsm_msg), &nMsgRef);
						Ql_Sleep(500);
					}
				  }
				  else
					  m=5;

				  m++;
				  Ql_Sleep(1000);
				}
				m=1;
				buzz();
				Ql_Sleep(1000);
				time1=0;
				//Ql_OS_SendMessage(0, MSG_ID_SECURE, 1, 0);
				while (m <= 5)
				{
				  if(locflag)
				  {
					Ql_OS_TakeMutex(mutex_id_serial);
					APP_DEBUG("\r\n<---calling---->\r\n");
					Ql_OS_GiveMutex(mutex_id_serial);
					  i = m + (m * 12) + 1000;

					if(temp_mem[i]!=NULL)
					{
						for (j = 0; j <= 15; j++)
						{
							mobilenum[j] = NULL;
						}
						for (j = 0; j <= 12; j++)
						{
							mobilenum[j] =  temp_mem[i + j];
						}
						Ql_Sleep(100);
						Ql_sprintf(gsm_msg,"ATD%s;",mobilenum);


						//RIL_Telephony_Dial(0,mobilenum,ret);
						okflag = waitcount = 0;
						Ql_RIL_SendATCmd(gsm_msg, Ql_strlen(gsm_msg), ATResponse_Handler,NULL, 0);
						wait_ok();

						if(okflag)
						{
							Ql_Sleep(1000);
							u8 j=0;
							time1=0;
							 while (time1<10)
							 {

								Ql_GPIO_SetLevel(BUZZ,PINLEVEL_HIGH);
								 Ql_Sleep(100);
								 Ql_GPIO_SetLevel(BUZZ,PINLEVEL_LOW);
								 Ql_Sleep(100);

								 if (locflag == 0)
								 {
									 break;
								 }

							 }


							RIL_Telephony_Hangup();
							Ql_Sleep(100);

						}
					}
				  }
				  else
					  m=5;

				  m++;
				}
			}
			else if((time2>5)&&(!lcd_disp))
			{
				time2=0;
				int sens=Ql_GPIO_GetLevel(POWER_SENS);

				if(last_power!=sens)
				{

						last_power=sens;
						int i =1013,j=0;
						Ql_OS_TakeMutex(mutex_id_serial);

						APP_DEBUG("\r\n<---Power changed---->\r\n");

						Ql_OS_GiveMutex(mutex_id_serial);
						i =1013;

						if(temp_mem[i]!=NULL)
						{
							for (j = 0; j <= 15; j++)
							{
								mobilenum[j] = NULL;
							}
							for (j = 0; j <= 12; j++)
							{
								mobilenum[j] =  temp_mem[i + j];
							}

							if(sens)
							{
								Ql_sprintf(gsm_msg,"i Home:\n Power ON.");
							}
							else
							{
								Ql_sprintf(gsm_msg,"i Home:\n Power OFF.");
							}
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);

							RIL_SMS_SendSMS_Text(mobilenum,Ql_strlen(mobilenum),LIB_SMS_CHARSET_GSM,gsm_msg, Ql_strlen(gsm_msg), &nMsgRef);
							Ql_Sleep(500);
						}


				}
				if((bat_per<=75)&&(!bat_flag))
				{
					int i =1013,j=0;
					bat_flag=1;
					if(temp_mem[i]!=NULL)
					{
						for (j = 0; j <= 15; j++)
						{
							mobilenum[j] = NULL;
						}
						for (j = 0; j <= 12; j++)
						{
							mobilenum[j] =  temp_mem[i + j];
						}

						Ql_sprintf(gsm_msg,"i Home:\n Bat Low.");

						Ql_OS_TakeMutex(mutex_id_serial);
						APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
						Ql_OS_GiveMutex(mutex_id_serial);

						RIL_SMS_SendSMS_Text(mobilenum,Ql_strlen(mobilenum),LIB_SMS_CHARSET_GSM,gsm_msg, Ql_strlen(gsm_msg), &nMsgRef);
						Ql_Sleep(500);
					}

				}
				else if((bat_per>90)&&(bat_flag))
				{
					bat_flag=0;
				}
				Ql_OS_TakeMutex(mutex_id_lcd);
				LiquidCrystal_I2C_clear();
				LiquidCrystal_I2C_setCursor(0,0);
				LiquidCrystal_I2C_printstr(">>>> i Safe <<<<");
				display++;
				if(display==1)
				{

					if(locflag)
					{
						LiquidCrystal_I2C_setCursor(0,1);
						LiquidCrystal_I2C_printstr("SECURITY :ON");
					}
					else
					{
						LiquidCrystal_I2C_setCursor(0,1);
						LiquidCrystal_I2C_printstr("SECURITY :OFF");
					}

					Ql_OS_GiveMutex(mutex_id_lcd);
				}
				else if(display==2)
				{
	                switch(apn_id)
	                {
	                    case 0:
	                    	LiquidCrystal_I2C_setCursor(0,1);
	                    	LiquidCrystal_I2C_printstr("Airtel  ");
	                        break;

	                    case 1:
	                    	LiquidCrystal_I2C_setCursor(0,1);
	                    	LiquidCrystal_I2C_printstr("Vodafone");
	                        break;
	                    case 2:
	                    	LiquidCrystal_I2C_setCursor(0,1);
	                    	LiquidCrystal_I2C_printstr("Idea    ");
	                        break;
	                    case 3:
	                    	LiquidCrystal_I2C_setCursor(0,1);
	                    	LiquidCrystal_I2C_printstr("Bsnl    ");
	                        break;
	                    default:
	                    	LiquidCrystal_I2C_setCursor(0,1);
	                    	LiquidCrystal_I2C_printstr("NO SIM");
	                        break;

	                }
	                if(net_reg)
	                {
	                    okflag = waitcount = 0;
	                    Ql_RIL_SendATCmd("AT+CSQ",Ql_strlen("AT+CSQ"),ATResponse_Handler,NULL,0);
	                    wait_ok();
	                    Ql_sprintf(gsm_msg,"Sig:%d%%",sigstr);
	                	LiquidCrystal_I2C_printstr(" ");
	                	LiquidCrystal_I2C_printstr(gsm_msg);
	                }
	                else
	                {
						LiquidCrystal_I2C_printstr(" ");
						LiquidCrystal_I2C_printstr("NO SIG");
						okflag = waitcount = 0;
						Ql_RIL_SendATCmd("AT+CREG?",Ql_strlen("AT+CREG?"),ATResponse_Handler,NULL,0);
						wait_ok();

	                }
	                Ql_OS_GiveMutex(mutex_id_lcd);

				}
				else if(display==3)
				{

					int sens=Ql_GPIO_GetLevel(POWER_SENS);
					LiquidCrystal_I2C_setCursor(0,1);
					if(sens)
					{
						LiquidCrystal_I2C_printstr("Pow:ON");
					}
					else
					{
						LiquidCrystal_I2C_printstr("Pow:OFF");
					}
					LiquidCrystal_I2C_setCursor(9,1);
					LiquidCrystal_I2C_printstr("Bat:");
					char z[2]={0};
					u8 k = bat_per / 10;
					z[0] = (char)(k + 48);
					k = bat_per % 10;
					z[1] = (char)(k + 48);
					LiquidCrystal_I2C_setCursor(13,1);
					LiquidCrystal_I2C_printchar(z[0]);
					LiquidCrystal_I2C_setCursor(14,1);
					LiquidCrystal_I2C_printchar(z[1]);
					LiquidCrystal_I2C_setCursor(15,1);
					LiquidCrystal_I2C_printchar('%');
					Ql_OS_GiveMutex(mutex_id_lcd);
				}
				else
				{
					display=0;
					get_time();
					LiquidCrystal_I2C_setCursor(0,1);

	                for(int i=9;i<14;i++)
	                	LiquidCrystal_I2C_printchar(d_t[i]);
	                LiquidCrystal_I2C_printchar(' '); LiquidCrystal_I2C_printchar(' '); LiquidCrystal_I2C_printchar(' ');
	                LiquidCrystal_I2C_printchar(d_t[6]);
	                LiquidCrystal_I2C_printchar(d_t[7]);
	                LiquidCrystal_I2C_printchar('/');
	                LiquidCrystal_I2C_printchar(d_t[3]);
	                LiquidCrystal_I2C_printchar(d_t[4]);
	                LiquidCrystal_I2C_printchar('/');
					LiquidCrystal_I2C_printchar(d_t[0]);
					LiquidCrystal_I2C_printchar(d_t[1]);
					Ql_OS_GiveMutex(mutex_id_lcd);
				}

			}
			else if(time3>30 && (gprsflag==1||gprsflag==5))
			{
				time3=0;
				  int l = 0;
				  float bat_vol1, bat_vol2;
				  int m = 0,n=0,a1;
				  char arr[30];
				  char str[200];

				  int k, s, msg = 0;

				  for (s = 0; s < 10; s++)
					arr[s] = NULL;

				  for (s = 0; s < 160; s++)
					str[s] = NULL;

				  str[0] = '*';
				  str[1] = ',';
				  m=0;
				  while (m < 20)
				  {
					  int i = 0;
					  i = m + (m * 3 * 10);

					  if (temp_mem[i] == 'D' || temp_mem[i] == 'P')
					  {
						for (s = 0; s < 30; s++)
						{
						  arr[s] = NULL;
						}
						for (n = 0; n < 18; n++)
						{
						  devicename[n] = temp_mem[i + n];
						  arr[n] = devicename[n];
						  if (temp_mem[i + n + 1] == NULL)
							break;
						}
						arr[2]='_';
						if (n < 18)
						  n++;

						arr[n++] = '_';

						char a[3];

						a[0] = (char)temp_mem[i + 28];
						a[1] = (char)temp_mem[i + 29];
						a[2] = (char)temp_mem[i + 30];

						unsigned int decValue = 0;
						int nextInt;

						for (int i = 0; i < 3; i++)
						{

						  nextInt = (int)(a[i]);
						  if (nextInt >= 48 && nextInt <= 57) nextInt = map(nextInt, 48, 57, 0, 9);
						  if (nextInt >= 65 && nextInt <= 70) nextInt = map(nextInt, 65, 70, 10, 15);
						  if (nextInt >= 97 && nextInt <= 102) nextInt = map(nextInt, 97, 102, 10, 15);
						  nextInt = constrain(nextInt, 0, 15);

						  decValue = (decValue * 16) + nextInt;
						}


						a1 = decValue;

						bat_vol1 = a1 * (1.024 / 1024.0);

						bat_vol2 = bat_vol1 * 3.2;


						l = bat_vol2 * 100;
						k = l / 100;
						z[0] = (char)(k + 48);

						k = (l % 100) / 10;
						z[1] = (char)(k + 48);
						k = l % 10;
						z[2] = (char)(k + 48);



					  if (temp_mem[i + 23] == '0' && temp_mem[i + 24] == '1')
					  {
						if (temp_mem[i + 26] == '1' || temp_mem[i + 26] == '0')
						  arr[n++] = temp_mem[i + 26];
						else
						  arr[n++] = '0';

					  }
					  else
					  {

						if(temp_mem[i + 26]=='1')
						{
							arr[n++] = '1';

						}
						else
						{
							arr[n++] = '0';
						}
					  }


					  arr[n++] = '_';
					  arr[n++] = z[0];
					  arr[n++] = '.';
					  arr[n++] = z[1];
					  arr[n++] = '_';
					  arr[n++] = temp_mem[i + 27] ;
					  arr[n++] = '_';
					  for (k = 0; k < 6; k++, n++)
					  {
						arr[n] = temp_mem[i + 19 + k];
					  }
					  arr[n++] = ',';
					  Ql_strncat(str, arr, n);

					}
					m++;
				  }

				  m = 0;
				  while (m < 10)
				  {



				  	int i = 0;
					i = m + (m * 30) + 620;
					n = 0;
					if (temp_mem[i] == 'L')
					{
						for (s = 0; s < 30; s++)
						{
						  arr[s] = NULL;
						}
						for (n = 0; n < 18; n++)
						{
						  devicename[n] = temp_mem[i + n];
						  arr[n] = devicename[n];
						  if (temp_mem[i + n + 1] == NULL)
							break;
						}
						arr[2]='_';
						if (n < 18)
						  n++;

					  arr[n++] = '_';

					  arr[n++] = 'L';
					  arr[n++] = '1';

					  arr[n++] =temp_mem[i + 28];
					  arr[n++] = '_';
					  arr[n++] = 'L';
					  arr[n++] = '2';


					  arr[n++] =temp_mem[i + 29];

					  arr[n++] = '_';
					  arr[n++] = 'L';
					  arr[n++] = '3';

					  arr[n++] =temp_mem[i + 30];

					  arr[n++] = '_';
					  arr[n++] = temp_mem[i + 27] ;
					  arr[n++] = '_';
					  for (k = 0; k < 6; k++, n++)
					  {
						arr[n] = temp_mem[i + 19 + k];
					  }
					  arr[n++] = ',';

					  Ql_strncat(str, arr, n);


					}
					m++;
				  }



				  {
					  Ql_strncat(str, "AUL0,", 5);

				  }
				  if (locflag == 1)
					  Ql_strncat(str, "SEC_1,", 6);
				  else
					  Ql_strncat(str, "SEC_0,", 6);



				  int sens=Ql_GPIO_GetLevel(POWER_SENS);

					memset(mqtt_live,0,sizeof(str));
					Ql_sprintf(mqtt_live,"%sPOWER_%d_%d%%,#\n",str,sens,bat_per);
					Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
			}


			else
			{
				Ql_OS_TakeMutex(mutex_id_serial);
				APP_DEBUG("\r\n<--Task 3 running----->\r\n");
				Ql_OS_GiveMutex(mutex_id_serial);
			}
			Ql_Sleep(100);
		}
}

void proc_subtask4(s32 taskId)
{

		while(1)
		{
			u8 m;
			u32 i,j;
			if(sms_flag==1)
			{
				sms_flag=0;
				read_msg();
				if(sms_readflag==2)
				{
					u8 m;
					u32 i,j;
					bool match_flag=0;
					sms_readflag=0;
					char ch[15];

					Ql_OS_TakeMutex(mutex_id_serial);
					Ql_Sleep(100);
					APP_DEBUG("\r\n<--msg_buf=%s----->\r\n",msgbuf);
					Ql_Sleep(100);
					APP_DEBUG("\r\n<--command=%s----->\r\n",command);
					Ql_Sleep(100);
					Ql_OS_GiveMutex(mutex_id_serial);
					Ql_OS_TakeMutex(mutex_id_lcd);
					LiquidCrystal_I2C_clear();
					LiquidCrystal_I2C_setCursor(0,0);
					LiquidCrystal_I2C_printstr("Message Received.");
					Ql_OS_GiveMutex(mutex_id_lcd);
					if(command[1]=='A'&&command[2]=='1'&&command[3]=='+')
					{

						Ql_OS_TakeMutex(mutex_id_lcd);
						LiquidCrystal_I2C_clear();
						LiquidCrystal_I2C_setCursor(0,1);
						LiquidCrystal_I2C_printstr("Adding admin ....");
						Ql_OS_GiveMutex(mutex_id_lcd);


						m = (int)command[2] - 48;

						i = m + (m * 12) + 1000;
						if(temp_mem[i]!='+')
						{
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<--ADDING=%d----->\r\n",i);
							Ql_OS_GiveMutex(mutex_id_serial);
							for (j = 0; j <= 15; j++)
							{
								mobilenum[j] = NULL;
							}
							eeprom_write(i,'+');
							temp_mem[i] ='+';

							eeprom_write(i+1,'9');
							temp_mem[i + 1] ='9';

							eeprom_write(i+2,'1');
							temp_mem[i + 2] ='1';


							for (j = 0; j <10; j++)
							{
								eeprom_write(i+j+3,command[4+j]);
								temp_mem[i + j+3] = command[4+j];
								mobilenum[j] = command[4+j];
							}

							for(j=1013;j<1030;j++)
							{
								Ql_OS_TakeMutex(mutex_id_serial);
								Ql_Sleep(1000);
								APP_DEBUG("\r\n<--DATA[%d]=%c----->\r\n",j,eeprom_read(j));
								Ql_Sleep(1000);
								Ql_OS_GiveMutex(mutex_id_serial);
							}

							get_time();

							Ql_sprintf(gsm_msg,"%s\ni Home:\nYour number %s is (ADMIN) added successfully.",d_t,mobilenum);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);
							for (j = 0; j <= 15; j++)
							{
								mobilenum[j] = NULL;
							}
							for (j = 0; j <13; j++)
							{
								mobilenum[j] = msgbuf[j + 23];
							}
							Ql_sprintf(gsm_msg,"%s\ni Home:\nYour number %s is (ADMIN) added successfully.",d_t,mobilenum);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);

						}
						else
						{
							for (j = 0; j <= 15; j++)
							{
								mobilenum[j] = NULL;
							}
							for (j = 0; j <13; j++)
							{
								mobilenum[j] = msgbuf[j + 23];
							}
							get_time();

							Ql_sprintf(gsm_msg,"%s\ni Home:\n ADMIN is already added.",d_t);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);
						}

					}
					else if(command[1]=='M'&&command[2]=='R'&&command[3]=='+')
					{
						Ql_OS_TakeMutex(mutex_id_lcd);
						LiquidCrystal_I2C_clear();
						LiquidCrystal_I2C_setCursor(0,1);
						LiquidCrystal_I2C_printstr("Master adding....");
						Ql_OS_GiveMutex(mutex_id_lcd);

						i = 1000;
						if(temp_mem[i]!='+')
						{
							 for (j = 0; j <= 15; j++)
							 {
								mobilenum[j] = NULL;
							 }
							 for (j = 0; j <= 12; j++)
							 {
								eeprom_write(i+j,msgbuf[j + 23]);
								temp_mem[i + j] = msgbuf[j + 23];
								mobilenum[j] = msgbuf[j + 23];
							 }
							 get_time();

							 Ql_sprintf(gsm_msg,"%s\ni Home:\n your number %s is (MASTER) added successfully.",d_t,mobilenum);
							 Ql_OS_TakeMutex(mutex_id_serial);
							 APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							 Ql_OS_GiveMutex(mutex_id_serial);
							 Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							 Ql_Sleep(1000);
						}
						else
						{
							for (j = 0; j <= 15; j++)
							{
								mobilenum[j] = NULL;
							}
							for (j = 0; j <13; j++)
							{
								mobilenum[j] = msgbuf[j + 23];
							}
							get_time();

							Ql_sprintf(gsm_msg,"%s\ni Home:\n Master is already added.",d_t);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);
						}

					}
					else if(command[1]=='A'&& command[3]=='+')
					{
						m = ((int)command[2]) - 48;

						if (m > 1 && m <= 5)
						{

							  	  m = 1;
							  	  match_flag=0;
								u32 req_num = 0;
								for (j = 0; j <= 15; j++)
								{
									mobilenum[j] = NULL;
								}
								for (j = 0; j <= 12; j++)
								{
									mobilenum[j] = msgbuf[j + 23];
								}

								i = m + (m * 12) + 1000;
								for (j = 0; j < 10; j++)
								{
									ch[j] = temp_mem[i + 3 + j];
								}

								if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
								{
									match_flag = 1;

									req_num = i;

								}

								if (match_flag == 0)
								{
									i = 1000;
									for (j = 0; j < 10; j++)
									{
										ch[j] = temp_mem[i + 3 + j];
									}

									if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
									{
										match_flag = 1;


									}

								}


							  if (match_flag)
							  {


									Ql_OS_TakeMutex(mutex_id_lcd);
									LiquidCrystal_I2C_clear();
									LiquidCrystal_I2C_setCursor(0,1);
									LiquidCrystal_I2C_printstr("Adding number.");
									Ql_OS_GiveMutex(mutex_id_lcd);

								   i = m + (m * 12) + 1000;
									for (j = 0; j <= 15; j++)
									{
										mobilenum[j] = NULL;
									}

									eeprom_write(i,'+');
									temp_mem[i] ='+';
									mobilenum[0] ='+';

									eeprom_write(i+1,'9');
									temp_mem[i + 1] = '9';
									mobilenum[1] ='9';

									eeprom_write(i+2,'1');
									temp_mem[i + 2] ='1';
									mobilenum[2] ='1';

									for (j = 0; j < 10; j++)
									{
										eeprom_write(i+j+3,command[j + 4]);
										temp_mem[i +j+3] = command[j + 4];
										mobilenum[j+3] = command[j + 4];
									}
									get_time();

									Ql_sprintf(gsm_msg,"%s\ni Home:\n your number %s is added successfully.",d_t,mobilenum);
									Ql_OS_TakeMutex(mutex_id_serial);
									APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
									Ql_OS_GiveMutex(mutex_id_serial);
									Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);

									Ql_Sleep(2000);

									Ql_sprintf(gsm_msg,"%s\ni Home:\n This number %s is added successfully.",d_t,mobilenum);
									for (j = 0; j <= 15; j++)
									{
										mobilenum[j] = NULL;
									}
									for (j = 0; j <= 12; j++)
									{
										mobilenum[j] = msgbuf[j + 23];
									}
									Ql_OS_TakeMutex(mutex_id_serial);
									APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
									Ql_OS_GiveMutex(mutex_id_serial);
									Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
									Ql_Sleep(1000);
							  }


						}

					}

					else if(command[1]=='I'&& command[2]=='D')
					{

						Ql_OS_TakeMutex(mutex_id_serial);
						APP_DEBUG("\r\n<---ID---->\r\n");
						Ql_OS_GiveMutex(mutex_id_serial);
						for (j = 0; j <= 15; j++)
						{
							mobilenum[j] = NULL;
						}
						for (j = 0; j <= 12; j++)
						{
							mobilenum[j] = msgbuf[j + 23];
						}


						m = 1;
						u8 out = 1;
						u32 req_num = 0;
						while (m <= 5 && out)
						{
							i = m + (m * 12) + 1000;
							for (j = 0; j < 10; j++)
							{
								ch[j] = temp_mem[i + 3 + j];
							}

							if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
							{
								match_flag = 1;
								out = 0;
								req_num = i;

							}
							m++;
						}
						if (match_flag == 0)
						{
							i = 1000;
							for (j = 0; j < 10; j++)
							{
								ch[j] = temp_mem[i + 3 + j];
							}

							if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
							{
								match_flag = 1;

								req_num = 1000;
							}

						}

						if (match_flag == 1)
						{
							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,1);
							LiquidCrystal_I2C_printstr("Taking added numbers.");
							Ql_OS_GiveMutex(mutex_id_lcd);
							m = 1;
							char id[100];

						   for (u8 s = 0; s < 100; s++)
						   {

								   id[s] = NULL;

						   }
						   u8 n=0;
							while (m <= 5)
						    {

								i = m + (m * 12) + 1000;
								if(temp_mem[i]=='+')
								{
									id[n++] = 'A';
									id[n++] = (char)(m + 48);
									id[n++] = ':';


									for (u8 j = 0; j < 13; j++)
									{

									   id[n++] = temp_mem[i + j];

									}
									id[n++] = '\n';
								}

								m++;
						    }
							get_time();

							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---id=%s---->\r\n",id);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_sprintf(gsm_msg,"%s\ni Home:\nAdded Numbers:\n%s",d_t,id);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
						   	Ql_OS_GiveMutex(mutex_id_serial);
						   	Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
						   	Ql_Sleep(1000);

						}

					}
					else if(command[1]=='A'&& command[3]=='-')
					{
						char d[20];
						bool out=1;

						for (j = 0; j <= 15; j++)
						{
							mobilenum[j] = NULL;
						}
						for (j = 0; j <= 12; j++)
						{
							mobilenum[j] = msgbuf[j + 23];
						}


						m = 1;

						u32 req_num = 0;

						i = m + (m * 12) + 1000;
						for (j = 0; j < 10; j++)
						{
							ch[j] = temp_mem[i + 3 + j];
						}

						if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
						{
							match_flag = 1;
							out = 0;
							req_num = i;

						}

						if (match_flag == 0)
						{
							i = 1000;
							for (j = 0; j < 10; j++)
							{
								ch[j] = temp_mem[i + 3 + j];
							}

							if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
							{
								match_flag = 1;


							}

						}

						if (match_flag == 1)
						{
							m = ((int)command[2]) - 48;

							i = m + (m * 12) + 1000;
							for (j = 0; j <= 12; j++)
							  {
								 eeprom_write(i+j,0);
								 temp_mem[i + j] = 0;
								 out=0;
							  }


							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,1);
							LiquidCrystal_I2C_printstr("Number deleting.");
							Ql_OS_GiveMutex(mutex_id_lcd);

							get_time();

							Ql_sprintf(gsm_msg,"%s\ni Home:\n Your number is deleted successfully.",d_t);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);
						}


					}
					else if(command[1]=='M'&& command[2]=='R'&& command[3]=='-')
					{
						for (j = 0; j <= 15; j++)
						{
							mobilenum[j] = NULL;
						}
						for (j = 0; j <= 12; j++)
						{
							mobilenum[j] = msgbuf[j + 23];
						}
						i =1000;
						for (j = 0; j < 10; j++)
						{
							ch[j] = temp_mem[i + 3 + j];
						}

						if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
						{
							match_flag = 1;
							i =1000;
							for (j = 0; j <= 12; j++)
							{
								 eeprom_write(i+j,0);
								 temp_mem[i + j] = 0;

							}
							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,1);
							LiquidCrystal_I2C_printstr("Master deleting.");
							Ql_OS_GiveMutex(mutex_id_lcd);

							get_time();

							Ql_sprintf(gsm_msg,"%s\ni Home:\n Your number (Master) is %s deleted successfully.",d_t,mobilenum);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);

						}
						else
						{


							get_time();

							Ql_sprintf(gsm_msg,"%s\ni Home:\n Wrong Number.",d_t);
							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);
						}


					}
					else if(command[1]=='L')
					{
							match_flag = 0;

							m = 0; j = 0;
							char str[150];
							char arr[30],devicename[18],clo[]="clos",op[]="open";
							int k, s,n,out;

							for (k = 0,n=0; n < 30; n++)
							{
								for (s = 0; s < 10; s++)
									arr[s] = '\0';
							}

							for (s = 0; s < 150; s++)
							{
								str[s] = '\0';
							}

							for (j = 0; j <= 15; j++)
							{
								mobilenum[j] = NULL;
							}
							for (j = 0; j <= 12; j++)
							{
								mobilenum[j] = msgbuf[j + 23];
							}

							m = 1; out = 1;
							u32 req_num = 0;

							while (m <= 5 && out)
							{
								i = m + (m * 12) + 1000;
								for (j = 0; j < 10; j++)
									ch[j] = temp_mem[i + 3 + j];

								if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
								{
									match_flag = 1;
									out = 0;
									req_num = i;

								}
								m++;
							}

							if (match_flag == 0)
							{
								i = 1000;
								for (j = 0; j < 10; j++)
									ch[j] = temp_mem[i + 3 + j];

								if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
								{
									match_flag = 1;

									req_num = 1000;
								}

							}

							if (match_flag == 1)
						    {

								m = 0;j=0;
								int no_enable = 0, no_ones = 0;
								while (m < 10)
								{
						              i = m + (m * 3 * 10);
						              if (temp_mem[i + 27] == 'E')
						              {
						                for (s = 0; s < 30; s++)
						                  arr[s] = NULL;

						                no_enable++;
						                for (n = 0; n < 18; n++)
						                {
						                  devicename[n] = temp_mem[i + n];
						                  arr[n] = devicename[n];
						                  if (temp_mem[i + n + 1] == NULL)
						                    break;
						                }


						                arr[n++] = ':';

						                if (temp_mem[i + 26] == '1')
						                {
						                  no_ones++;
						                  for ( s = 0; s < 6; n++, s++)
						                    arr[n] = clo[s];

						                }
						                else
						                {
						                  for (s = 0; s < 6; n++, s++)
						                    arr[n] = op[s];

						                }
						                arr[n++] = ',';
						                arr[n++] = '\n';


						                Ql_strncat(str, arr, n);



						              }
						              m++;
						          }

						            if (no_enable == no_ones)
						            {
						              locflag = 1;
						              eeprom_write(1084,'1');
						            }
						            else
						            {
						              locflag = 0;
						              eeprom_write(1084,'0');
						            }


						            m = 1;

						            if (locflag == 1)
						            {

						              buzz();
						              Ql_OS_TakeMutex(mutex_id_lcd);
						              LiquidCrystal_I2C_clear();
						              LiquidCrystal_I2C_setCursor(0,1);
						              LiquidCrystal_I2C_printstr("Lock mode activating.");
						              Ql_OS_GiveMutex(mutex_id_lcd);

						              LiquidCrystal_I2C_setCursor(0,1);
						              LiquidCrystal_I2C_printstr("SEC:ON");

						              get_time();

						              Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY ON.",d_t);
						              Ql_OS_TakeMutex(mutex_id_serial);
						              APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
						              Ql_OS_GiveMutex(mutex_id_serial);
						              Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
						              Ql_Sleep(1000);
						              m=1;
						              while (m <= 5)
						              {
						                i = m + (m * 12) + 1000;
						                if (i != req_num)
						                {
						                	if(temp_mem[i]!=NULL)
						                	{
												for (j = 0; j <= 15; j++)
												{
													mobilenum[j] = NULL;
												}
												for (j = 0; j <= 12; j++)
												{
													mobilenum[j] =  temp_mem[i + j];
												}

												Ql_OS_TakeMutex(mutex_id_serial);
												APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
												Ql_OS_GiveMutex(mutex_id_serial);
												Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
												Ql_Sleep(1000);
						                	}

						                }
						                m++;
						              }

						            }
						            if (locflag == 0)
						            {
						            	get_time();

							              Ql_OS_TakeMutex(mutex_id_lcd);
							              LiquidCrystal_I2C_clear();
							              LiquidCrystal_I2C_setCursor(0,1);
							              LiquidCrystal_I2C_printstr("Door not closed.");
							              Ql_OS_GiveMutex(mutex_id_lcd);

						            	Ql_sprintf(gsm_msg,"%s\ni Home:\nDOOR NOT CLOSED\n%s.",d_t,str);
						            	Ql_OS_TakeMutex(mutex_id_serial);
						            	APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
						            	Ql_OS_GiveMutex(mutex_id_serial);
						            	Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);

						            	Ql_Sleep(1000);

						            }
						        }

					}
					else if(command[1]=='U'&&command[2]=='L')
					{
						for (j = 0; j <= 15; j++)
						{
							mobilenum[j] = NULL;
						}
						for (j = 0; j <= 12; j++)
						{
							mobilenum[j] = msgbuf[j + 23];
						}

						m = 1;
						u8 out = 1;
						u32 req_num = 0;

						while (m <= 5 && out)
						{
							i = m + (m * 12) + 1000;
							for (j = 0; j < 10; j++)
								ch[j] = temp_mem[i + 3 + j];

							if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
							{
								match_flag = 1;
								out = 0;
								req_num = i;

							}
							m++;
						}

						if (match_flag == 0)
						{
							i = 1000;
							for (j = 0; j < 10; j++)
								ch[j] = temp_mem[i + 3 + j];

							if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
							{
								match_flag = 1;

								req_num = 1000;
							}

						}

						if (match_flag == 1)
						{
							if(locflag==1)
							{
								Ql_OS_TakeMutex(mutex_id_lcd);
								LiquidCrystal_I2C_clear();
								LiquidCrystal_I2C_setCursor(0,1);
								LiquidCrystal_I2C_printstr("Unlocking...");
								Ql_OS_GiveMutex(mutex_id_lcd);
								locflag=0;
								Ql_GPIO_SetLevel(SIREN,PINLEVEL_LOW);
								eeprom_write(1084,'0');

								get_time();

								Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY OFF.",d_t);
								Ql_OS_TakeMutex(mutex_id_serial);
								APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
								Ql_OS_GiveMutex(mutex_id_serial);
								Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
								Ql_Sleep(1000);


								 m=1;
								 while (m <= 5)
								 {
									 i = m + (m * 12) + 1000;
									 if (i != req_num)
									 {

										 if(temp_mem[i]!=NULL)
										 {

											 for (j = 0; j <= 15; j++)
											 {
												 mobilenum[j] = NULL;
											 }
											 for (j = 0; j <= 12; j++)
											 {
												 mobilenum[j] =  temp_mem[i + j];
											 }

											Ql_OS_TakeMutex(mutex_id_serial);
											APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
											Ql_OS_GiveMutex(mutex_id_serial);
											Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
											Ql_Sleep(1000);
										 }

									 }
									 m++;
								 }
							}
							else if(locflag==0)
							{
								get_time();

								Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY ALREADY OFF.",d_t);
								Ql_OS_TakeMutex(mutex_id_serial);
								APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
								Ql_OS_GiveMutex(mutex_id_serial);
								Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
								Ql_Sleep(1000);
							}
							else
							{

							}

						}

					}
					else if(command[1]=='?')
					{
						match_flag = 0;
						int l = 0,n,a1;
						float bat_vol1, bat_vol2;
						m = 0; j = 1;
						char arr[30],devicename[18],clo[]="clos",op[]="open";
						char str[150];

						int k, s, msg = 0;
						for (k = 0,n=0; n < 30; n++)
						{

								arr[n] = NULL;
						}
						for (s = 0; s < 160; s++)
							str[s]=NULL;

						for (j = 0; j <= 15; j++)
						{
							mobilenum[j] = NULL;
						}
						for (j = 0; j <= 12; j++)
						{
							mobilenum[j] = msgbuf[j + 23];
						}

						m = 1;
						u8 out = 1;
						u32 req_num = 0;

						while (m <= 5 && out)
						{
							i = m + (m * 12) + 1000;
							for (j = 0; j < 10; j++)
								ch[j] = temp_mem[i + 3 + j];

							if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
							{
								match_flag = 1;
								out = 0;
								req_num = i;

							}
							m++;
						}

						if (match_flag == 0)
						{
							i = 1000;
							for (j = 0; j < 10; j++)
								ch[j] = temp_mem[i + 3 + j];

							if (ch[0] == mobilenum[3] && ch[1] == mobilenum[4] && ch[2] == mobilenum[5] && ch[3] == mobilenum[6] && ch[4] ==mobilenum[7] && ch[5] ==mobilenum[8] && ch[6] == mobilenum[9] && ch[7] == mobilenum[10] && ch[8] == mobilenum[11] && ch[9] == mobilenum[12])
							{
								match_flag = 1;

								req_num = 1000;
							}

						}

						if (match_flag == 1)
						{
							Ql_OS_TakeMutex(mutex_id_lcd);
							LiquidCrystal_I2C_clear();
							LiquidCrystal_I2C_setCursor(0,1);
							LiquidCrystal_I2C_printstr("Taking status.");
							Ql_OS_GiveMutex(mutex_id_lcd);
							m=0;
							while (m < 10)
							{
							  int i = 0;
							  i = m + (m * 3 * 10);

							  if (temp_mem[i + 27] == 'E')
							  {
								for (s = 0; s < 30; s++)
								{
								  arr[s] = NULL;
								}
								for (n = 0; n < 18; n++)
								{
								  devicename[n] = temp_mem[i + n];
								  arr[n] = devicename[n];
								  if (temp_mem[i + n + 1] == NULL)
									break;
								}

								if (n < 18)
								  n++;

								arr[n++] = '-';

								char a[3];

								a[0] = (char)temp_mem[i + 28];
								a[1] = (char)temp_mem[i + 29];
								a[2] = (char)temp_mem[i + 30];

								unsigned int decValue = 0;
								int nextInt;

								for (int i = 0; i < 3; i++)
								{

								  nextInt = (int)(a[i]);
								  if (nextInt >= 48 && nextInt <= 57) nextInt = map(nextInt, 48, 57, 0, 9);
								  if (nextInt >= 65 && nextInt <= 70) nextInt = map(nextInt, 65, 70, 10, 15);
								  if (nextInt >= 97 && nextInt <= 102) nextInt = map(nextInt, 97, 102, 10, 15);
								  nextInt = constrain(nextInt, 0, 15);

								  decValue = (decValue * 16) + nextInt;
								}


								a1 = decValue;

								bat_vol1 = a1 * (1.024 / 1024.0);

								bat_vol2 = bat_vol1 * 3.2;


								l = bat_vol2 * 100;
								k = l / 100;
								z[0] = (char)(k + 48);

								k = (l % 100) / 10;
								z[1] = (char)(k + 48);
								k = l % 10;
								z[2] = (char)(k + 48);



								  if (temp_mem[i + 26] == '1')
								  {
									for (s = 0; s < 4; s++, n++)
									  arr[n] = clo[s];

								  }
								  else
								  {
									for (s = 0; s < 4; s++, n++)
									  arr[n] = op[s];

								  }

								arr[n++] = '-';
								arr[n++] = z[0];
								arr[n++] = '.';
								arr[n++] = z[1];

								arr[n++] = '\n';

								Ql_strncat(str, arr, n);
							  }
							  m++;
							}
							get_time();

							okflag = waitcount = 0;
							Ql_RIL_SendATCmd("AT+CSQ",Ql_strlen("AT+CSQ"),ATResponse_Handler,NULL,0);
							wait_ok();

							if(locflag)
							{
								int sens=Ql_GPIO_GetLevel(POWER_SENS);
								if(sens)
								{
									Ql_sprintf(gsm_msg,"%s\ni Home:%s\nPow:ON-%d%%\nSTR:%d%%\nSEC:ON\n%s.",d_t,version,bat_per,sigstr,str);
								}
								else
								{
									Ql_sprintf(gsm_msg,"%s\ni Home:%s\nPow:OFF-%d%%\nSTR:%d%%\nSEC:ON\n%s.",d_t,version,bat_per,sigstr,str);
								}

							}
							else
							{
								int sens=Ql_GPIO_GetLevel(POWER_SENS);
								if(sens)
								{
									Ql_sprintf(gsm_msg,"%s\ni Home:%s\nPow:ON-%d%%\nSTR:%d%%\nSEC:OFF\n%s.",d_t,version,bat_per,sigstr,str);
								}
								else
								{
									Ql_sprintf(gsm_msg,"%s\ni Home:%s\nPow:OFF-%d%%\nSTR:%d%%\nSEC:OFF\n%s.",d_t,version,bat_per,sigstr,str);
								}
							}

							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);

							Ql_Sleep(1000);
						}
					}
					else if((command[1] =='F') && (command[2] =='O')  && (command[5] =='S') && (command[9] =='T'))
					{

						 Ql_memset(fotaurl,0,sizeof(fotaurl));
						 Ql_memset(fotaapn,0,sizeof(fotaapn));
						 Ql_memset(fotaun,0,sizeof(fotaun));
						 Ql_memset(fotapw,0,sizeof(fotapw));
						 u8 start_num = 11;
						 u8 url_num = 0;
						  while((start_num <= 99) && (command[start_num] != '#'))
						  {
							 fotaurl[url_num] = command[start_num];
							 start_num++;
							 url_num++;
						  }

						  Ql_memset(gsm_msg,0,sizeof(gsm_msg));
						  Ql_OS_TakeMutex(mutex_id_serial);
						  APP_DEBUG("\r\n<--FOTA START URL:%s*%s*%s*%s*%s*-->\r\n",fotaurl,apn,fotaun,fotapw);
						  Ql_OS_GiveMutex(mutex_id_serial);
						  Ql_sprintf(gsm_msg,"FOTA started...\nLINK-%s\nAPN-%s\nUSER-%s\nPASSWORD-%s\n!%s!",fotaurl,apn,fotaun,fotapw);
						   for (j = 0; j <= 15; j++)
						   {
						   	mobilenum[j] = NULL;
						   }
						   for (j = 0; j <13; j++)
						   {
						   	mobilenum[j] = msgbuf[j + 23];
						   }

						   Ql_OS_TakeMutex(mutex_id_serial);
						   APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
						   Ql_OS_GiveMutex(mutex_id_serial);
						   Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
						  Ql_Sleep(3000);
						  task_fota = 1;
						   Ql_OS_SendMessage(0, MSG_ID_FOTA, 1, 0);
						   while(task_fota == 1)
						   {
							   Ql_Sleep(100);
						   }


					}
					else
					{

					}
					del_msg();

				}
			}
			else if(btrcvflag)
			{

				btrcvflag=0;
				if (btbuf[0] == 'V' && btbuf[1] == 'I' && btbuf[2] == 'E' && btbuf[3] == 'W')
				{
				  int l = 0;
				  float bat_vol1, bat_vol2;
				  int m = 0,n=0,a1;
				  char arr[30];
				  char str[200];

				  int k, s, msg = 0;

				  for (s = 0; s < 10; s++)
					arr[s] = NULL;

				  for (s = 0; s < 160; s++)
					str[s] = NULL;

				  str[0] = '*';
				  str[1] = ',';
				  m=0;
				  while (m < 20)
				  {
					  i = 0;
					  i = m + (m * 3 * 10);

					  if (temp_mem[i] == 'D' || temp_mem[i] == 'P')
					  {
						for (s = 0; s < 30; s++)
						{
						  arr[s] = NULL;
						}
						for (n = 0; n < 18; n++)
						{
						  devicename[n] = temp_mem[i + n];
						  arr[n] = devicename[n];
						  if (temp_mem[i + n + 1] == NULL)
							break;
						}
						arr[2]='_';
						if (n < 18)
						  n++;

						arr[n++] = '_';

						char a[3];

						a[0] = (char)temp_mem[i + 28];
						a[1] = (char)temp_mem[i + 29];
						a[2] = (char)temp_mem[i + 30];

						unsigned int decValue = 0;
						int nextInt;

						for (int i = 0; i < 3; i++)
						{

						  nextInt = (int)(a[i]);
						  if (nextInt >= 48 && nextInt <= 57) nextInt = map(nextInt, 48, 57, 0, 9);
						  if (nextInt >= 65 && nextInt <= 70) nextInt = map(nextInt, 65, 70, 10, 15);
						  if (nextInt >= 97 && nextInt <= 102) nextInt = map(nextInt, 97, 102, 10, 15);
						  nextInt = constrain(nextInt, 0, 15);

						  decValue = (decValue * 16) + nextInt;
						}


						a1 = decValue;

						bat_vol1 = a1 * (1.024 / 1024.0);

						bat_vol2 = bat_vol1 * 3.2;


						l = bat_vol2 * 100;
						k = l / 100;
						z[0] = (char)(k + 48);

						k = (l % 100) / 10;
						z[1] = (char)(k + 48);
						k = l % 10;
						z[2] = (char)(k + 48);



					  if (temp_mem[i + 23] == '0' && temp_mem[i + 24] == '1')
					  {
						if (temp_mem[i + 26] == '1' || temp_mem[i + 26] == '0')
						  arr[n++] = temp_mem[i + 26];
						else
						  arr[n++] = '0';

					  }
					  else
					  {

						  if(temp_mem[i + 26]=='1')
							{
								arr[n++] = '1';

							}
							else
							{
								arr[n++] = '0';
							}
					  }


					  arr[n++] = '_';
					  arr[n++] = z[0];
					  arr[n++] = '.';
					  arr[n++] = z[1];
					  arr[n++] = '_';
					  arr[n++] = temp_mem[i + 27] ;
					  arr[n++] = '_';
					  for (k = 0; k < 6; k++, n++)
					  {
						arr[n] = temp_mem[i + 19 + k];
					  }
					  arr[n++] = ',';
					  Ql_strncat(str, arr, n);

					}
					m++;
				  }

				  m = 0;
				  while (m < 10)
				  {



				  	i = 0;
					i = m + (m * 30) + 620;
					n = 0;
					if (temp_mem[i] == 'L')
					{
						for (s = 0; s < 30; s++)
						{
						  arr[s] = NULL;
						}
						for (n = 0; n < 18; n++)
						{
						  devicename[n] = temp_mem[i + n];
						  arr[n] = devicename[n];
						  if (temp_mem[i + n + 1] == NULL)
							break;
						}
						arr[2]='_';
						if (n < 18)
						  n++;

					  arr[n++] = '_';

					  arr[n++] = 'L';
				      arr[n++] = '1';

					  arr[n++] =temp_mem[i + 28];
					  arr[n++] = '_';
					  arr[n++] = 'L';
					  arr[n++] = '2';


					  arr[n++] =temp_mem[i + 29];

					  arr[n++] = '_';
					  arr[n++] = 'L';
					  arr[n++] = '3';

					  arr[n++] =temp_mem[i + 30];


					  arr[n++] = '_';
					  arr[n++] = temp_mem[i + 27] ;
					  arr[n++] = '_';
					  for (k = 0; k < 6; k++, n++)
					  {
						arr[n] = temp_mem[i + 19 + k];
					  }
					  arr[n++] = ',';

					  Ql_strncat(str, arr, n);


					}
					m++;
				  }


				  /*if (auto_flag == 1)
				  {
					strncat(str, "L", 1);
					arr[0][0] = char(lig_id + 48);
					strncat(str, arr[0], 1);
					strncat(str, "-F", 2);
					arr[0][0] = char((int(ft / 10)) + 48);
					arr[0][1] = char((int(ft % 10)) + 48);
					strncat(str, arr[0], 2);
					strncat(str, "-T", 2);
					arr[0][0] = char((int(tt / 10)) + 48);
					arr[0][1] = char((int(tt % 10)) + 48);
					strncat(str, arr[0], 2);
					strncat(str, ",", 1);
					total_ch = total_ch + 16;
				  }
				  else*/
				  {
					  Ql_strncat(str, "AUL0,", 5);

				  }
				  if (locflag == 1)
					  Ql_strncat(str, "SEC_1,", 6);
				  else
					  Ql_strncat(str, "SEC_0,", 6);


				  Ql_OS_TakeMutex(mutex_id_serial);
				  APP_DEBUG("\r\n<---str=%s---->\r\n",str);
					Ql_OS_GiveMutex(mutex_id_serial);
					memset(btbuf,0,sizeof(btbuf));
					int sens=Ql_GPIO_GetLevel(POWER_SENS);
					Ql_sprintf(btbuf,"%sPOWER_%d_%d%%,#\n",str,sens,bat_per);


					s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
				   if(RIL_AT_SUCCESS == ret)
				   {
					  APP_DEBUG("Send successful.\r\n");
				   }
				   else
				   {
					  APP_DEBUG("Send failed.\r\n");
				   }

				   buzz();

				}
				else if (btbuf[0] == 'A' && btbuf[1] == 'd' && btbuf[2] == 'd')
				{

				  if (btbuf[4] == 'D')
				  {
					i = (int)btbuf[5] - 48;
					i = i + (i * 3 * 10);
				  }
				  else if (btbuf[4] == 'P')
				  {
					i = (int)btbuf[5] - 48;
					i = i + (i * 3 * 10) + 310;
				  }
				  else if (btbuf[4] == 'L')
				  {
					i = (int)btbuf[5] - 48;
					i = i + (i *30) + 620;
				  }
				  else
				  {
					//
				  }
				  int id =0;
				  for (j = 0; j < 18; j++)
				  {

					eeprom_write(i+j,btbuf[4+j]);
					temp_mem[i + j] = btbuf[4+j];
					if (btbuf[4+j+1] == '-')
					  break;
					id++;

				  }
				  id = id + 6;
				  for (j = 0; j <= 7; j++)
				  {

					eeprom_write(i + j + 18,btbuf[j+id]);
					temp_mem[i + j + 18] = btbuf[j + id];

				  }
				  eeprom_write( i + 27, 'D');
				  temp_mem[i + 27] = 'D';
				  memset(btbuf,0,sizeof(btbuf));
					Ql_sprintf(btbuf,"ADDED\n");
					s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
				   if(RIL_AT_SUCCESS == ret)
				   {
					  APP_DEBUG("Send successful.\r\n");
				   }
				   else
				   {
					  APP_DEBUG("Send failed.\r\n");
				   }

				   buzz();



				}

				else if (btbuf[0] == 'D' && btbuf[1] == 'e' && btbuf[2] == 'l' && btbuf[3] == 'e' && btbuf[4] == 't' && btbuf[5] == 'e')
				{


					if (btbuf[7] == 'D')
					  {
						i = (int)btbuf[8] - 48;
						i = i + (i * 3 * 10);
					  }
					  else if (btbuf[7] == 'P')
					  {
						i = (int)btbuf[8] - 48;
						i = i + (i * 3 * 10) + 310;
					  }
					  else if (btbuf[7] == 'L')
					  {
						i = (int)btbuf[8] - 48;
						i = i + (i *30) + 620;
					  }
					  else
					  {
						//
					  }

				  char s = temp_mem[i + 18];

				  if (s != '*')
				  {
					  memset(btbuf,0,sizeof(btbuf));
						Ql_sprintf(btbuf,"NO DEVICE FOUND\n");
						s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					   if(RIL_AT_SUCCESS == ret)
					   {
						  APP_DEBUG("Send successful.\r\n");
					   }
					   else
					   {
						  APP_DEBUG("Send failed.\r\n");
					   }

					   buzz();

				  }
				  else
				  {
					char a = '0';
					for (j = 0; j <= 30 ; j++)
					{
						eeprom_write(i+j,a);
						temp_mem[i + j ] = a;
					}
					for (j = 0; j <= 17; j++)
					{
						eeprom_write(i+j,0);
						temp_mem[i + j ] = 0;
					}

					memset(btbuf,0,sizeof(btbuf));
					Ql_sprintf(btbuf,"DELETED\n");
					s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
				   if(RIL_AT_SUCCESS == ret)
				   {
					  APP_DEBUG("Send successful.\r\n");
				   }
				   else
				   {
					  APP_DEBUG("Send failed.\r\n");
				   }

				   buzz();

				  }
				}
				else if (btbuf[0] == 'E' && btbuf[1] == 'n' && btbuf[2] == 'a' && btbuf[3] == 'b' && btbuf[4] == 'l' && btbuf[5] == 'e')
				{


					if (btbuf[7] == 'D')
					  {
						i = (int)btbuf[8] - 48;
						i = i + (i * 3 * 10);
					  }
					  else if (btbuf[7] == 'P')
					  {
						i = (int)btbuf[8] - 48;
						i = i + (i * 3 * 10) + 310;
					  }
					  else if (btbuf[7] == 'L')
					  {
						i = (int)btbuf[8] - 48;
						i = i + (i *30) + 620;
					  }
					  else
					  {
						//
					  }

				  char s = temp_mem[i + 18];

				  if (s != '*')
				  {
					  memset(btbuf,0,sizeof(btbuf));
						Ql_sprintf(btbuf,"NO DEVICE FOUND\n");
						s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					   if(RIL_AT_SUCCESS == ret)
					   {
						  APP_DEBUG("Send successful.\r\n");
					   }
					   else
					   {
						  APP_DEBUG("Send failed.\r\n");
					   }

					   buzz();

				  }
				  else
				  {

					  	eeprom_write(i+27,'E');

						temp_mem[i + 27] = 'E';
						memset(btbuf,0,sizeof(btbuf));
						Ql_sprintf(btbuf,"Enabled\n");
						s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					   if(RIL_AT_SUCCESS == ret)
					   {
						  APP_DEBUG("Send successful.\r\n");
					   }
					   else
					   {
						  APP_DEBUG("Send failed.\r\n");
					   }

					   buzz();
				  }
				}
				else if (btbuf[0] == 'D' && btbuf[1] == 'i' && btbuf[2] == 's' && btbuf[3] == 'a' && btbuf[4] == 'b' && btbuf[5] == 'l'&& btbuf[6] == 'e')
				{


					if (btbuf[8] == 'D')
					  {
						i = (int)btbuf[9] - 48;
						i = i + (i * 3 * 10);
					  }
					  else if (btbuf[8] == 'P')
					  {
						i = (int)btbuf[9] - 48;
						i = i + (i * 3 * 10) + 310;
					  }
					  else if (btbuf[8] == 'L')
					  {
						i = (int)btbuf[9] - 48;
						i = i + (i *30) + 620;
					  }
					  else
					  {
						//
					  }

				  char s = temp_mem[i + 18];

				  if (s != '*')
				  {
					  memset(btbuf,0,sizeof(btbuf));
						Ql_sprintf(btbuf,"NO DEVICE FOUND\n");
						s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					   if(RIL_AT_SUCCESS == ret)
					   {
						  APP_DEBUG("Send successful.\r\n");
					   }
					   else
					   {
						  APP_DEBUG("Send failed.\r\n");
					   }

					   buzz();

				  }
				  else
				  {

					  	eeprom_write(i+27,'D');

						temp_mem[i + 27] = 'D';
						memset(btbuf,0,sizeof(btbuf));
						Ql_sprintf(btbuf,"disabled\n");
						s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					   if(RIL_AT_SUCCESS == ret)
					   {
						  APP_DEBUG("Send successful.\r\n");
					   }
					   else
					   {
						  APP_DEBUG("Send failed.\r\n");
					   }

					   buzz();
				  }
				}

				else if ((btbuf[0] == 'L' && btbuf[2] == '_' && btbuf[5] == 'O') || (btbuf[0] == 'L' && btbuf[2] == '_' && btbuf[5] == 'O'&& btbuf[6] == 'F'))
				{

					if(btbuf[3] == '1')
					{
						i =((int)btbuf[1])-48;

						  i = i + (i * 30) + 620;

						  send_data[0] = temp_mem[i + 18];
						  send_data[1] = temp_mem[i + 19];
						  send_data[2] = temp_mem[i + 20];
						  send_data[3] = temp_mem[i + 21];
						  send_data[4] = temp_mem[i + 22];
						  send_data[5] = temp_mem[i + 23];
						  send_data[6] = temp_mem[i + 24];
						  send_data[7] = 'L';
						  send_data[8] = '1';
						  if (temp_mem[i + 18] == '*')
						  {


							if (btbuf[0] == 'L' && btbuf[2] == '_' && btbuf[5] == 'O'&& btbuf[6] == 'N')
								send_data[9] = '1';
							else
								send_data[9] = '0';

							send_data[10] = temp_mem[i + 25];



							_address = ((((int)send_data[1]) - 48) * (1000)) + ((((int)send_data[2]) - 48) * (100)) + ((((int)send_data[3]) - 48) * (10)) + ((((int)send_data[4]) - 48) * (1));

							Ql_OS_SendMessage(0, MSG_ID_SEND, 1, 0);

						  }
					}
					else if(btbuf[3] == '2')
					{
						i =((int)btbuf[1])-48;

						  i = i + (i * 30) + 620;

						  send_data[0] = temp_mem[i + 18];
						  send_data[1] = temp_mem[i + 19];
						  send_data[2] = temp_mem[i + 20];
						  send_data[3] = temp_mem[i + 21];
						  send_data[4] = temp_mem[i + 22];
						  send_data[5] = temp_mem[i + 23];
						  send_data[6] = temp_mem[i + 24];
						  send_data[7] = 'L';
						  send_data[8] = '2';
						  if (temp_mem[i + 18] == '*')
						  {


							if (btbuf[0] == 'L' && btbuf[2] == '_' && btbuf[5] == 'O'&& btbuf[6] == 'N')
								send_data[9] = '1';
							else
								send_data[9] = '0';

							send_data[10] = temp_mem[i + 25];



							_address = ((((int)send_data[1]) - 48) * (1000)) + ((((int)send_data[2]) - 48) * (100)) + ((((int)send_data[3]) - 48) * (10)) + ((((int)send_data[4]) - 48) * (1));

							Ql_OS_SendMessage(0, MSG_ID_SEND, 1, 0);

						  }
					}
					else if(btbuf[3] == '3')
					{
						i =((int)btbuf[1])-48;

					  i = i + (i * 30) + 620;

					  send_data[0] = temp_mem[i + 18];
					  send_data[1] = temp_mem[i + 19];
					  send_data[2] = temp_mem[i + 20];
					  send_data[3] = temp_mem[i + 21];
					  send_data[4] = temp_mem[i + 22];
					  send_data[5] = temp_mem[i + 23];
					  send_data[6] = temp_mem[i + 24];
					  send_data[7] = 'L';
					  send_data[8] = '3';
					  if (temp_mem[i + 18] == '*')
					  {


						if (btbuf[0] == 'L' && btbuf[2] == '_' && btbuf[5] == 'O'&& btbuf[6] == 'N')
							send_data[9] = '1';
						else
							send_data[9] = '0';

						send_data[10] = temp_mem[i + 25];



						_address = ((((int)send_data[1]) - 48) * (1000)) + ((((int)send_data[2]) - 48) * (100)) + ((((int)send_data[3]) - 48) * (10)) + ((((int)send_data[4]) - 48) * (1));

						Ql_OS_SendMessage(0, MSG_ID_SEND, 1, 0);

					  }
					}

				}
				else if ((btbuf[0] == 'F' && btbuf[2] == '_' && btbuf[3] == 'O') || (btbuf[0] == 'F' && btbuf[2] == '_' && btbuf[3] == 'O'&& btbuf[4] == 'F'))
				{


						i =((int)btbuf[1])-48;

						  i = i + (i * 30) + 620;

						  send_data[0] = temp_mem[i + 18];
						  send_data[1] = temp_mem[i + 19];
						  send_data[2] = temp_mem[i + 20];
						  send_data[3] = temp_mem[i + 21];
						  send_data[4] = temp_mem[i + 22];
						  send_data[5] = temp_mem[i + 23];
						  send_data[6] = temp_mem[i + 24];
						  send_data[7] = 'F';
						  send_data[8] = '1';
						  if (temp_mem[i + 18] == '*')
						  {


							if (btbuf[0] == 'F' && btbuf[2] == '_' && btbuf[3] == 'O'&& btbuf[4] == 'N')
								send_data[9] = '1';
							else
								send_data[9] = '0';
							send_data[10]=',';
							send_data[11]=btbuf[6];
							send_data[12] = temp_mem[i + 25];



							_address = ((((int)send_data[1]) - 48) * (1000)) + ((((int)send_data[2]) - 48) * (100)) + ((((int)send_data[3]) - 48) * (10)) + ((((int)send_data[4]) - 48) * (1));

							Ql_OS_SendMessage(0, MSG_ID_SEND, 1, 0);

						  }

				}
				else if (btbuf[0] == 'L' && btbuf[1] == 'O' && btbuf[2] == 'C' && btbuf[3] == 'K')
				{
				  u8 logi = 0,k=0;
				  for (j = 0; j < 10; j++)
				  {
					k = j + (j * 3 * 10);

					if (temp_mem[k + 18] == '*' && temp_mem[k + 27] == 'E' && temp_mem[k + 26] == '0')
					  logi = 1;
				  }
				  if (logi == 0)
				  {
					locflag = 1;
					eeprom_write(1084,'1');
					m = 1;
					memset(btbuf,0,sizeof(btbuf));
					Ql_sprintf(btbuf,"LOCKED\n");
					s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					buzz();


					  Ql_OS_TakeMutex(mutex_id_lcd);

					  LiquidCrystal_I2C_clear();
					  LiquidCrystal_I2C_setCursor(0,1);
					  LiquidCrystal_I2C_printstr("Lock mode activating.");
					  Ql_OS_GiveMutex(mutex_id_lcd);

					  get_time();

					  Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY ON.",d_t);
					  Ql_OS_TakeMutex(mutex_id_serial);
					  APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
					  Ql_OS_GiveMutex(mutex_id_serial);

					  m=1;
					  while (m <= 5)
					  {
						i = m + (m * 12) + 1000;

							if(temp_mem[i]!=NULL)
							{
								for (j = 0; j <= 15; j++)
								{
									mobilenum[j] = NULL;
								}
								for (j = 0; j <= 12; j++)
								{
									mobilenum[j] =  temp_mem[i + j];
								}

								Ql_OS_TakeMutex(mutex_id_serial);
								APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
								Ql_OS_GiveMutex(mutex_id_serial);
								Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
								Ql_Sleep(1000);
							}


						m++;
					  }



				  }
				  else
				  {
					  memset(btbuf,0,sizeof(btbuf));
					  Ql_sprintf(btbuf,"CANT LOCK\n");
					  s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					  buzz();

				  }
				}
				else if (btbuf[0] == 'U' && btbuf[1] == 'L')
				{
					locflag = 0;
					Ql_GPIO_SetLevel(SIREN,PINLEVEL_LOW);
					eeprom_write(1084,'0');
					m = 1;
					memset(btbuf,0,sizeof(btbuf));
					Ql_sprintf(btbuf,"UNLOCKED\n");
					s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					buzz();


					  Ql_OS_TakeMutex(mutex_id_lcd);

					  LiquidCrystal_I2C_clear();
					  LiquidCrystal_I2C_setCursor(0,1);
					  LiquidCrystal_I2C_printstr("Lock mode Deactivating.");
					  Ql_OS_GiveMutex(mutex_id_lcd);


					  get_time();

					  Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY OFF.",d_t);
					  Ql_OS_TakeMutex(mutex_id_serial);
					  APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
					  Ql_OS_GiveMutex(mutex_id_serial);

					  m=1;
					  while (m <= 5)
					  {
						i = m + (m * 12) + 1000;

							if(temp_mem[i]!=NULL)
							{
								for (j = 0; j <= 15; j++)
								{
									mobilenum[j] = NULL;
								}
								for (j = 0; j <= 12; j++)
								{
									mobilenum[j] =  temp_mem[i + j];
								}

								Ql_OS_TakeMutex(mutex_id_serial);
								APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
								Ql_OS_GiveMutex(mutex_id_serial);
								Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
								Ql_Sleep(1000);
							}


						m++;
					  }



				}

			}
			else if(btconflag)
			{
				if(rf_change)
				{
					rf_change = 0;
					int l = 0;
					  float bat_vol1, bat_vol2;
					  int m = 0,n=0,a1;
					  char arr[30];
					  char str[200];

					  int k, s, msg = 0;

					  for (s = 0; s < 10; s++)
						arr[s] = NULL;

					  for (s = 0; s < 160; s++)
						str[s] = NULL;

					  str[0] = '*';
					  str[1] = ',';
					  m=0;
					  while (m < 20)
					  {
						  i = 0;
						  i = m + (m * 3 * 10);

						  if (temp_mem[i] == 'D' || temp_mem[i] == 'P')
						  {
							for (s = 0; s < 30; s++)
							{
							  arr[s] = NULL;
							}
							for (n = 0; n < 18; n++)
							{
							  devicename[n] = temp_mem[i + n];
							  arr[n] = devicename[n];
							  if (temp_mem[i + n + 1] == NULL)
								break;
							}
							arr[2]='_';
							if (n < 18)
							  n++;

							arr[n++] = '_';

							char a[3];

							a[0] = (char)temp_mem[i + 28];
							a[1] = (char)temp_mem[i + 29];
							a[2] = (char)temp_mem[i + 30];

							unsigned int decValue = 0;
							int nextInt;

							for (int i = 0; i < 3; i++)
							{

							  nextInt = (int)(a[i]);
							  if (nextInt >= 48 && nextInt <= 57) nextInt = map(nextInt, 48, 57, 0, 9);
							  if (nextInt >= 65 && nextInt <= 70) nextInt = map(nextInt, 65, 70, 10, 15);
							  if (nextInt >= 97 && nextInt <= 102) nextInt = map(nextInt, 97, 102, 10, 15);
							  nextInt = constrain(nextInt, 0, 15);

							  decValue = (decValue * 16) + nextInt;
							}


							a1 = decValue;

							bat_vol1 = a1 * (1.024 / 1024.0);

							bat_vol2 = bat_vol1 * 3.2;


							l = bat_vol2 * 100;
							k = l / 100;
							z[0] = (char)(k + 48);

							k = (l % 100) / 10;
							z[1] = (char)(k + 48);
							k = l % 10;
							z[2] = (char)(k + 48);



						  if (temp_mem[i + 23] == '0' && temp_mem[i + 24] == '1')
						  {
							if (temp_mem[i + 26] == '1' || temp_mem[i + 26] == '0')
							  arr[n++] = temp_mem[i + 26];
							else
							  arr[n++] = '0';

						  }
						  else
						  {

							  if(temp_mem[i + 26]=='1')
								{
									arr[n++] = '1';
									temp_mem[i + 26]='0';

								}
								else
								{
									arr[n++] = '0';
								}
						  }


						  arr[n++] = '_';
						  arr[n++] = z[0];
						  arr[n++] = '.';
						  arr[n++] = z[1];
						  arr[n++] = '_';
						  arr[n++] = temp_mem[i + 27] ;
						  arr[n++] = '_';
						  for (k = 0; k < 6; k++, n++)
						  {
							arr[n] = temp_mem[i + 19 + k];
						  }
						  arr[n++] = ',';
						  Ql_strncat(str, arr, n);

						}
						m++;
					  }

					  m = 0;
					  while (m < 10)
					  {



						i = 0;
						i = m + (m * 30) + 620;
						n = 0;
						if (temp_mem[i] == 'L')
						{
							for (s = 0; s < 30; s++)
							{
							  arr[s] = NULL;
							}
							for (n = 0; n < 18; n++)
							{
							  devicename[n] = temp_mem[i + n];
							  arr[n] = devicename[n];
							  if (temp_mem[i + n + 1] == NULL)
								break;
							}
							arr[2]='_';
							if (n < 18)
							  n++;


						  arr[n++] = '_';

						  arr[n++] = 'L';
						  arr[n++] = '1';

						  arr[n++] =temp_mem[i + 28];
						  arr[n++] = '_';
						  arr[n++] = 'L';
						  arr[n++] = '2';


						  arr[n++] =temp_mem[i + 29];

						  arr[n++] = '_';
						  arr[n++] = 'L';
						  arr[n++] = '3';

						  arr[n++] =temp_mem[i + 30];

						  arr[n++] = '_';
						  arr[n++] = temp_mem[i + 27] ;
						  arr[n++] = '_';
						  for (k = 0; k < 6; k++, n++)
						  {
							arr[n] = temp_mem[i + 19 + k];
						  }
						  arr[n++] = ',';

						  Ql_strncat(str, arr, n);


						}
						m++;
					  }


					  /*if (auto_flag == 1)
					  {
						strncat(str, "L", 1);
						arr[0][0] = char(lig_id + 48);
						strncat(str, arr[0], 1);
						strncat(str, "-F", 2);
						arr[0][0] = char((int(ft / 10)) + 48);
						arr[0][1] = char((int(ft % 10)) + 48);
						strncat(str, arr[0], 2);
						strncat(str, "-T", 2);
						arr[0][0] = char((int(tt / 10)) + 48);
						arr[0][1] = char((int(tt % 10)) + 48);
						strncat(str, arr[0], 2);
						strncat(str, ",", 1);
						total_ch = total_ch + 16;
					  }
					  else*/
					  {
						  Ql_strncat(str, "AUL0,", 5);

					  }
					  if (locflag == 1)
						  Ql_strncat(str, "SEC_1,", 6);
					  else
						  Ql_strncat(str, "SEC_0,", 6);


					  Ql_OS_TakeMutex(mutex_id_serial);
					  APP_DEBUG("\r\n<---str=%s---->\r\n",str);
						Ql_OS_GiveMutex(mutex_id_serial);
						memset(btbuf,0,sizeof(btbuf));

						int sens=Ql_GPIO_GetLevel(POWER_SENS);
						Ql_sprintf(btbuf,"%sPOWER_%d_%d%%,#\n",str,sens,bat_per);
						s32 ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
					   if(RIL_AT_SUCCESS == ret)
					   {
						  APP_DEBUG("Send successful.\r\n");
					   }
					   else
					   {
						  APP_DEBUG("Send failed.\r\n");
					   }



				}
			}
			else
			{
				Ql_OS_TakeMutex(mutex_id_serial);
				APP_DEBUG("\r\n<--Task 4 running----->\r\n");
				Ql_OS_GiveMutex(mutex_id_serial);
			}


			Ql_Sleep(100);


		}
}
void proc_subtask5(s32 taskId)
{

	while(1)
	{

		if(mqtt_rec_flag)
		{
			u8 m;
			u32 i,j;

			mqtt_rec_flag=0;
			if (gprsbuf[30] == 'V' && gprsbuf[31] == 'I' && gprsbuf[32] == 'E' && gprsbuf[33] == 'W')
			{
			  int l = 0;
			  float bat_vol1, bat_vol2;
			  int m = 0,n=0,a1;
			  char arr[30];
			  char str[200];

			  int k, s, msg = 0;

			  for (s = 0; s < 10; s++)
				arr[s] = NULL;

			  for (s = 0; s < 160; s++)
				str[s] = NULL;

			  str[0] = '*';
			  str[1] = ',';
			  m=0;
			  while (m < 20)
			  {
				   i = 0;
				  i = m + (m * 3 * 10);

				  if (temp_mem[i] == 'D' || temp_mem[i] == 'P')
				  {
					for (s = 0; s < 30; s++)
					{
					  arr[s] = NULL;
					}
					for (n = 0; n < 18; n++)
					{
					  devicename[n] = temp_mem[i + n];
					  arr[n] = devicename[n];
					  if (temp_mem[i + n + 1] == NULL)
						break;
					}
					arr[2]='_';
					if (n < 18)
					  n++;

					arr[n++] = '_';

					char a[3];

					a[0] = (char)temp_mem[i + 28];
					a[1] = (char)temp_mem[i + 29];
					a[2] = (char)temp_mem[i + 30];

					unsigned int decValue = 0;
					int nextInt;

					for (int i = 0; i < 3; i++)
					{

					  nextInt = (int)(a[i]);
					  if (nextInt >= 48 && nextInt <= 57) nextInt = map(nextInt, 48, 57, 0, 9);
					  if (nextInt >= 65 && nextInt <= 70) nextInt = map(nextInt, 65, 70, 10, 15);
					  if (nextInt >= 97 && nextInt <= 102) nextInt = map(nextInt, 97, 102, 10, 15);
					  nextInt = constrain(nextInt, 0, 15);

					  decValue = (decValue * 16) + nextInt;
					}


					a1 = decValue;

					bat_vol1 = a1 * (1.024 / 1024.0);

					bat_vol2 = bat_vol1 * 3.2;


					l = bat_vol2 * 100;
					k = l / 100;
					z[0] = (char)(k + 48);

					k = (l % 100) / 10;
					z[1] = (char)(k + 48);
					k = l % 10;
					z[2] = (char)(k + 48);



				  if (temp_mem[i + 23] == '0' && temp_mem[i + 24] == '1')
				  {
					if (temp_mem[i + 26] == '1' || temp_mem[i + 26] == '0')
					  arr[n++] = temp_mem[i + 26];
					else
					  arr[n++] = '0';

				  }
				  else
				  {

					  if(temp_mem[i + 26]=='1')
						{
							arr[n++] = '1';

						}
						else
						{
							arr[n++] = '0';
						}
				  }


				  arr[n++] = '_';
				  arr[n++] = z[0];
				  arr[n++] = '.';
				  arr[n++] = z[1];
				  arr[n++] = '_';
				  arr[n++] = temp_mem[i + 27] ;
				  arr[n++] = '_';
				  for (k = 0; k < 6; k++, n++)
				  {
					arr[n] = temp_mem[i + 19 + k];
				  }
				  arr[n++] = ',';
				  Ql_strncat(str, arr, n);

				}
				m++;
			  }

			  m = 0;
			  while (m < 10)
			  {



				i = 0;
				i = m + (m * 30) + 620;
				n = 0;
				if (temp_mem[i] == 'L')
				{
					for (s = 0; s < 30; s++)
					{
					  arr[s] = NULL;
					}
					for (n = 0; n < 18; n++)
					{
					  devicename[n] = temp_mem[i + n];
					  arr[n] = devicename[n];
					  if (temp_mem[i + n + 1] == NULL)
						break;
					}
					arr[2]='_';
					if (n < 18)
					  n++;

				  arr[n++] = '_';

				  arr[n++] = 'L';
				  arr[n++] = '1';

				  arr[n++] =temp_mem[i + 28];
				  arr[n++] = '_';
				  arr[n++] = 'L';
				  arr[n++] = '2';


				  arr[n++] =temp_mem[i + 29];

				  arr[n++] = '_';
				  arr[n++] = 'L';
				  arr[n++] = '3';

				  arr[n++] =temp_mem[i + 30];

				  arr[n++] = '_';
				  arr[n++] = temp_mem[i + 27] ;
				  arr[n++] = '_';
				  for (k = 0; k < 6; k++, n++)
				  {
					arr[n] = temp_mem[i + 19 + k];
				  }
				  arr[n++] = ',';

				  Ql_strncat(str, arr, n);


				}
				m++;
			  }


			  /*if (auto_flag == 1)
			  {
				strncat(str, "L", 1);
				arr[0][0] = char(lig_id + 48);
				strncat(str, arr[0], 1);
				strncat(str, "-F", 2);
				arr[0][0] = char((int(ft / 10)) + 48);
				arr[0][1] = char((int(ft % 10)) + 48);
				strncat(str, arr[0], 2);
				strncat(str, "-T", 2);
				arr[0][0] = char((int(tt / 10)) + 48);
				arr[0][1] = char((int(tt % 10)) + 48);
				strncat(str, arr[0], 2);
				strncat(str, ",", 1);
				total_ch = total_ch + 16;
			  }
			  else*/
			  {
				  Ql_strncat(str, "AUL0,", 5);

			  }
			  if (locflag == 1)
				  Ql_strncat(str, "SEC_1,", 6);
			  else
				  Ql_strncat(str, "SEC_0,", 6);



			  int sens=Ql_GPIO_GetLevel(POWER_SENS);

			  memset(mqtt_live,0,sizeof(str));
			  Ql_sprintf(mqtt_live,"%sPOWER_%d_%d%%,#\n",str,sens,bat_per);
			  Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
			  buzz();

			}
			else if (gprsbuf[30] == 'A' && gprsbuf[31] == 'd' && gprsbuf[32] == 'd')
			{

			  if (gprsbuf[34] == 'D')
			  {
				i = (int)gprsbuf[35] - 48;
				i = i + (i * 3 * 10);
			  }
			  else if (gprsbuf[34] == 'P')
			  {
				i = (int)gprsbuf[35] - 48;
				i = i + (i * 3 * 10) + 310;
			  }
			  else if (gprsbuf[34] == 'L')
			  {
				i = (int)gprsbuf[35] - 48;
				i = i + (i *30) + 620;
			  }
			  else
			  {
				//
			  }
			  int id =0;
			  for (int j = 0; j < 18; j++)
			  {

				eeprom_write(i+j,gprsbuf[34+j]);
				temp_mem[i + j] = gprsbuf[34+j];
				if (gprsbuf[34+j+1] == '-')
				  break;
				id++;

			  }
			  id = id + 6;
			  for (j = 0; j <= 7; j++)
			  {

				eeprom_write(i + j + 18,gprsbuf[30+j+id]);
				temp_mem[i + j + 18] = gprsbuf[30+j+id];

			  }
			  eeprom_write( i + 27, 'E');
			  temp_mem[i + 27] = 'E';
			  memset(gprsbuf,0,sizeof(gprsbuf));


			  memset(mqtt_live,0,sizeof(mqtt_live));
			  Ql_sprintf(mqtt_live,"ADDED\n");
			  Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
			  buzz();

			}

			else if (gprsbuf[30] == 'D' && gprsbuf[31] == 'e' && gprsbuf[32] == 'l' && gprsbuf[33] == 'e' && gprsbuf[34] == 't' && gprsbuf[35] == 'e')
			{


				if (gprsbuf[37] == 'D')
				  {
					i = (int)gprsbuf[38] - 48;
					i = i + (i * 3 * 10);
				  }
				  else if (gprsbuf[37] == 'P')
				  {
					i = (int)gprsbuf[38] - 48;
					i = i + (i * 3 * 10) + 310;
				  }
				  else if (gprsbuf[37] == 'L')
				  {
					i = (int)gprsbuf[38] - 48;
					i = i + (i *30) + 620;
				  }
				  else
				  {
					//
				  }

			  char s = temp_mem[i + 18];

			  if (s != '*')
			  {


				  memset(mqtt_live,0,sizeof(mqtt_live));
				  Ql_sprintf(mqtt_live,"NO DEVICE FOUND\n");

				  buzz();

			  }
			  else
			  {
				char a = '0';
				for (j = 0; j <= 30 ; j++)
				{
					eeprom_write(i+j,a);
					temp_mem[i + j ] = a;
				}
				for (j = 0; j <= 17; j++)
				{
					eeprom_write(i+j,0);
					temp_mem[i + j ] = 0;
				}



				  memset(mqtt_live,0,sizeof(mqtt_live));
				  Ql_sprintf(mqtt_live,"DELETED\n");
				  Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
				  buzz();

			  }
			}
			else if (gprsbuf[30] == 'E' && gprsbuf[31] == 'n' && gprsbuf[32] == 'a' && gprsbuf[33] == 'b' && gprsbuf[34] == 'l' && gprsbuf[35] == 'e')
			{


				if (gprsbuf[37] == 'D')
				  {
					i = (int)gprsbuf[38] - 48;
					i = i + (i * 3 * 10);
				  }
				  else if (gprsbuf[37] == 'P')
				  {
					i = (int)gprsbuf[38] - 48;
					i = i + (i * 3 * 10) + 310;
				  }
				  else if (gprsbuf[37] == 'L')
				  {
					i = (int)gprsbuf[38] - 48;
					i = i + (i *30) + 620;
				  }
				  else
				  {
					//
				  }

			  char s = temp_mem[i + 18];

			  if (s != '*')
			  {


				  memset(mqtt_live,0,sizeof(mqtt_live));
				  Ql_sprintf(mqtt_live,"NO DEVICE FOUND\n");
				  Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
				  buzz();

			  }
			  else
			  {

					eeprom_write(i+27,'E');
					temp_mem[i + 27] = 'E';
					memset(mqtt_live,0,sizeof(mqtt_live));
					Ql_sprintf(mqtt_live,"Enabled\n");
					Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
					buzz();
			  }
			}
			else if (gprsbuf[30] == 'D' && gprsbuf[31] == 'i' && gprsbuf[32] == 's' && gprsbuf[33] == 'a' && gprsbuf[34] == 'b' && gprsbuf[35] == 'l'&& gprsbuf[36] == 'e')
			{


				if (gprsbuf[38] == 'D')
				  {
					i = (int)gprsbuf[39] - 48;
					i = i + (i * 3 * 10);
				  }
				  else if (gprsbuf[38] == 'P')
				  {
					i = (int)gprsbuf[39] - 48;
					i = i + (i * 3 * 10) + 310;
				  }
				  else if (gprsbuf[38] == 'L')
				  {
					i = (int)gprsbuf[39] - 48;
					i = i + (i *30) + 620;
				  }
				  else
				  {
					//
				  }

			  char s = temp_mem[i + 18];

			  if (s != '*')
			  {
					  Ql_OS_TakeMutex(mutex_id_serial);
					  memset(mqtt_live,0,sizeof(mqtt_live));
					  Ql_sprintf(mqtt_live,"NO DEVICE FOUND\n");
					  Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
					  buzz();

			  }
			  else
			  {

					eeprom_write(i+27,'D');
					temp_mem[i + 27] = 'D';
					memset(mqtt_live,0,sizeof(mqtt_live));
					Ql_sprintf(mqtt_live,"Disabled\n");
					Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
					buzz();
			  }
			}
			else if (gprsbuf[30] == 'L' && gprsbuf[31] == 'O' && gprsbuf[32] == 'C' && gprsbuf[33] == 'K')
			{
			  u8 logi = 0,k=0;
			  for (j = 0; j < 10; j++)
			  {
				k = j + (j * 3 * 10);

				if (temp_mem[k + 18] == '*' && temp_mem[k + 27] == 'E' && temp_mem[k + 26] == '0')
				  logi = 1;
			  }
			  if (logi == 0)
			  {
				locflag = 1;
				eeprom_write(1084,'1');
				int m = 1;


				memset(mqtt_live,0,sizeof(mqtt_live));
				Ql_sprintf(mqtt_live,"LOCKED\n");
				Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
				buzz();


				  Ql_OS_TakeMutex(mutex_id_lcd);

				  LiquidCrystal_I2C_clear();
				  LiquidCrystal_I2C_setCursor(0,1);
				  LiquidCrystal_I2C_printstr("Lock mode activating.");
				  Ql_OS_GiveMutex(mutex_id_lcd);

				  get_time();

				  Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY ON.",d_t);
				  Ql_OS_TakeMutex(mutex_id_serial);
				  APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
				  Ql_OS_GiveMutex(mutex_id_serial);

				  m=1;
				  while (m <= 5)
				  {
					i = m + (m * 12) + 1000;

						if(temp_mem[i]!=NULL)
						{
							for (j = 0; j <= 15; j++)
							{
								mobilenum[j] = NULL;
							}
							for (j = 0; j <= 12; j++)
							{
								mobilenum[j] =  temp_mem[i + j];
							}

							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);
						}


					m++;
				  }



			  }
			  else
			  {


					memset(mqtt_live,0,sizeof(mqtt_live));
					Ql_sprintf(mqtt_live,"CANT LOCK\n");
					Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
					buzz();

			  }
			}
			else if (gprsbuf[30] == 'U' && gprsbuf[31] == 'L')
			{
				locflag = 0;
				Ql_GPIO_SetLevel(SIREN,PINLEVEL_LOW);
				eeprom_write(1084,'0');
				m = 1;
				memset(mqtt_live,0,sizeof(mqtt_live));
				Ql_sprintf(mqtt_live,"UNLOCKED\n");
				Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
				buzz();


				  Ql_OS_TakeMutex(mutex_id_lcd);

				  LiquidCrystal_I2C_clear();
				  LiquidCrystal_I2C_setCursor(0,1);
				  LiquidCrystal_I2C_printstr("Lock mode Deactivating.");
				  Ql_OS_GiveMutex(mutex_id_lcd);


				  get_time();

				  Ql_sprintf(gsm_msg,"%s\ni Home:\n SECURITY OFF.",d_t);
				  Ql_OS_TakeMutex(mutex_id_serial);
				  APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
				  Ql_OS_GiveMutex(mutex_id_serial);

				  m=1;
				  while (m <= 5)
				  {
					i = m + (m * 12) + 1000;

						if(temp_mem[i]!=NULL)
						{
							for (j = 0; j <= 15; j++)
							{
								mobilenum[j] = NULL;
							}
							for (j = 0; j <= 12; j++)
							{
								mobilenum[j] =  temp_mem[i + j];
							}

							Ql_OS_TakeMutex(mutex_id_serial);
							APP_DEBUG("\r\n<---gsm_msg=%s---->\r\n",gsm_msg);
							Ql_OS_GiveMutex(mutex_id_serial);
							Ql_OS_SendMessage(0, MSG_ID_MSG, 1, 0);
							Ql_Sleep(1000);
						}


					m++;
				  }



			}


		}
		else if((rf_change2) && (gprsflag==1||gprsflag==5))
		{
			rf_change2=0;
			  int l = 0;
			  float bat_vol1, bat_vol2;
			  int m = 0,n=0,a1;
			  char arr[30];
			  char str[200];

			  int k, s, msg = 0;

			  for (s = 0; s < 10; s++)
				arr[s] = NULL;

			  for (s = 0; s < 160; s++)
				str[s] = NULL;

			  str[0] = '*';
			  str[1] = ',';
			  m=0;
			  while (m < 20)
			  {
				  int i = 0;
				  i = m + (m * 3 * 10);

				  if (temp_mem[i] == 'D' || temp_mem[i] == 'P')
				  {
					for (s = 0; s < 30; s++)
					{
					  arr[s] = NULL;
					}
					for (n = 0; n < 18; n++)
					{
					  devicename[n] = temp_mem[i + n];
					  arr[n] = devicename[n];
					  if (temp_mem[i + n + 1] == NULL)
						break;
					}
					arr[2]='_';
					if (n < 18)
					  n++;

					arr[n++] = '_';

					char a[3];

					a[0] = (char)temp_mem[i + 28];
					a[1] = (char)temp_mem[i + 29];
					a[2] = (char)temp_mem[i + 30];

					unsigned int decValue = 0;
					int nextInt;

					for (int i = 0; i < 3; i++)
					{

					  nextInt = (int)(a[i]);
					  if (nextInt >= 48 && nextInt <= 57) nextInt = map(nextInt, 48, 57, 0, 9);
					  if (nextInt >= 65 && nextInt <= 70) nextInt = map(nextInt, 65, 70, 10, 15);
					  if (nextInt >= 97 && nextInt <= 102) nextInt = map(nextInt, 97, 102, 10, 15);
					  nextInt = constrain(nextInt, 0, 15);

					  decValue = (decValue * 16) + nextInt;
					}


					a1 = decValue;

					bat_vol1 = a1 * (1.024 / 1024.0);

					bat_vol2 = bat_vol1 * 3.2;


					l = bat_vol2 * 100;
					k = l / 100;
					z[0] = (char)(k + 48);

					k = (l % 100) / 10;
					z[1] = (char)(k + 48);
					k = l % 10;
					z[2] = (char)(k + 48);



				  if (temp_mem[i + 23] == '0' && temp_mem[i + 24] == '1')
				  {
					if (temp_mem[i + 26] == '1' || temp_mem[i + 26] == '0')
					  arr[n++] = temp_mem[i + 26];
					else
					  arr[n++] = '0';

				  }
				  else
				  {

					  if(temp_mem[i + 26]=='1')
						{
							arr[n++] = '1';

						}
						else
						{
							arr[n++] = '0';
						}
				  }


				  arr[n++] = '_';
				  arr[n++] = z[0];
				  arr[n++] = '.';
				  arr[n++] = z[1];
				  arr[n++] = '_';
				  arr[n++] = temp_mem[i + 27] ;
				  arr[n++] = '_';
				  for (k = 0; k < 6; k++, n++)
				  {
					arr[n] = temp_mem[i + 19 + k];
				  }
				  arr[n++] = ',';
				  Ql_strncat(str, arr, n);

				}
				m++;
			  }

			  m = 0;
			  while (m < 10)
			  {

			  	int i = 0;
				i = m + (m * 30) + 620;
				n = 0;
				if (temp_mem[i] == 'L')
				{
					for (s = 0; s < 30; s++)
					{
					  arr[s] = NULL;
					}
					for (n = 0; n < 18; n++)
					{
					  devicename[n] = temp_mem[i + n];
					  arr[n] = devicename[n];
					  if (temp_mem[i + n + 1] == NULL)
						break;
					}
					arr[2]='_';
					if (n < 18)
					  n++;

				  arr[n++] = '_';

				  arr[n++] = 'L';
				  arr[n++] = '1';

				  arr[n++] =temp_mem[i + 28];
				  arr[n++] = '_';
				  arr[n++] = 'L';
				  arr[n++] = '2';


				  arr[n++] =temp_mem[i + 29];

				  arr[n++] = '_';
				  arr[n++] = 'L';
				  arr[n++] = '3';

				  arr[n++] =temp_mem[i + 30];

				  arr[n++] = '_';
				  arr[n++] = temp_mem[i + 27] ;
				  arr[n++] = '_';
				  for (k = 0; k < 6; k++, n++)
				  {
					arr[n] = temp_mem[i + 19 + k];
				  }
				  arr[n++] = ',';

				  Ql_strncat(str, arr, n);


				}
				m++;
			  }

			  {
				  Ql_strncat(str, "AUL0,", 5);
			  }
			  if (locflag == 1)
				  Ql_strncat(str, "SEC_1,", 6);
			  else
				  Ql_strncat(str, "SEC_0,", 6);



			  int sens=Ql_GPIO_GetLevel(POWER_SENS);

				memset(mqtt_live,0,sizeof(str));
				Ql_sprintf(mqtt_live,"%sPOWER_%d_%d%%,#\n",str,sens,bat_per);
				Ql_OS_SendMessage(0, MSG_ID_SEND, 2, 0);
		}
		else
		{
			Ql_OS_TakeMutex(mutex_id_serial);
			APP_DEBUG("\r\n<--Task 5 running----->\r\n");
			Ql_OS_GiveMutex(mutex_id_serial);
		}
		Ql_Sleep(100);
	}
}
static s32 ReadSerialPort(Enum_SerialPort port, /*[out]*/u8* pBuffer, /*[in]*/u32 bufLen)
{
    s32 rdLen = 0;
    s32 rdTotalLen = 0;
    if (NULL == pBuffer || 0 == bufLen)
    {
        return -1;
    }
    Ql_memset(pBuffer, 0x0, bufLen);
    while (1)
    {
        rdLen = Ql_UART_Read(port, pBuffer + rdTotalLen, bufLen - rdTotalLen);
        if (rdLen <= 0)  // All data is read out, or Serial Port Error!
        {
            break;
        }
        rdTotalLen += rdLen;
        // Continue to read...
    }
    if (rdLen < 0) // Serial Port Error!
    {
        APP_DEBUG("Fail to read from port[%d]\r\n", port);
        return -99;
    }
    return rdTotalLen;
}

static void CallBack_UART_Hdlr(Enum_SerialPort port, Enum_UARTEventType msg, bool level, void* customizedPara)
{
   // APP_DEBUG("CallBack_UART_Hdlr: port=%d, event=%d, level=%d, p=%x\r\n", port, msg, level, customizedPara);
    switch (msg)
    {
    case EVENT_UART_READY_TO_READ:
        {
            if (m_myUartPort == port)
            {
                s32 totalBytes = ReadSerialPort(port, m_RxBuf_Uart1, sizeof(m_RxBuf_Uart1));
                if (totalBytes <= 0)
                {
                    APP_DEBUG("<-- No data in UART buffer! -->\r\n");
                    return;
                }
                {// Read data from UART
                    s32 ret;
                    char* pCh = NULL;
                    
                    // Echo
                    Ql_UART_Write(m_myUartPort, m_RxBuf_Uart1, totalBytes);

                    pCh = Ql_strstr((char*)m_RxBuf_Uart1, "\r\n");
                    if (pCh)
                    {
                        *(pCh + 0) = '\0';
                        *(pCh + 1) = '\0';
                    }

                    // No permission for single <cr><lf>
                    if (Ql_strlen((char*)m_RxBuf_Uart1) == 0)
                    {
                        return;
                    }
                    ret = Ql_RIL_SendATCmd((char*)m_RxBuf_Uart1, totalBytes, ATResponse_Handler, NULL, 0);
                    if(ret==0)
                    {
                    	APP_DEBUG("<-- AT COMMEND OK_2-->\r\n");
                    }
                }

            }
            else if(UART_PORT2 == port)
			{
            	s32 totalBytes = ReadSerialPort(port, m_RxBuf_Uart1, sizeof(m_RxBuf_Uart1));
				if (totalBytes <= 0)
				{
					APP_DEBUG("<-- No data in UART buffer! -->\r\n");
					return;
				}
				touch_flag=1;

				// Echo
				Ql_UART_Write(m_myUartPort, m_RxBuf_Uart1, totalBytes);

			}
            break;
        }
    case EVENT_UART_READY_TO_WRITE:
        break;
    default:
        break;
    }
}
static void BT_Callback(s32 event, s32 errCode, void* param1, void* param2)
{
    ST_BT_BasicInfo *pstNewBtdev = NULL;
    ST_BT_BasicInfo *pstconBtdev = NULL;
    s32 ret = RIL_AT_SUCCESS;
    s32 connid = -1;
    char btatsend[100];


    switch(event)
    {
        case MSG_BT_SCAN_IND :

            if(URC_BT_SCAN_FINISHED== errCode)
            {
            	 Ql_OS_TakeMutex(mutex_id_serial);
            	 APP_DEBUG("Scan is over.\r\n");
            	 Ql_OS_GiveMutex(mutex_id_serial);
                //APP_DEBUG("Pair/Connect if need.\r\n");
            	 RIL_BT_GetDevListInfo();
                //here obtain ril layer table list for later use
//                g_dev_info = RIL_BT_GetDevListPointer();
//                g_pair_search = TRUE;
            }
            if(URC_BT_SCAN_FOUND == errCode)
            {
                pstNewBtdev = (ST_BT_BasicInfo *)param1;
                //you can manage the scan device here,or you don't need to manage it ,for ril layer already handle it
                Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("BTHdl[0x%08x] Addr[%s] Name[%s]\r\n",pstNewBtdev->devHdl,pstNewBtdev->addr,pstNewBtdev->name);
                Ql_OS_GiveMutex(mutex_id_serial);
            }
            break;
         case MSG_BT_PAIR_IND :
		  if(URC_BT_NEED_PASSKEY == errCode)
		  {
				//must ask for pincode;
				pstconBtdev = (ST_BT_BasicInfo*)param1;
				Ql_OS_TakeMutex(mutex_id_serial);
				APP_DEBUG("Pair device BTHdl: 0x%08x\r\n",pstconBtdev->devHdl);
				APP_DEBUG("Pair device addr: %s\r\n",pstconBtdev->addr);
				APP_DEBUG("Waiting for pair confirm with pinCode...\r\n");
				Ql_OS_GiveMutex(mutex_id_serial);
		  }
		  else if(URC_BT_NO_NEED_PASSKEY == errCode)
		  {
			  	  Ql_OS_TakeMutex(mutex_id_serial);
			  	  APP_DEBUG("CONNECTION ACCEPT--1\n");
				//no need pincode
				pstconBtdev = (ST_BT_BasicInfo*)param1;
//                    Ql_strncpy(pinCode,(char*)param2,BT_PIN_LEN);
				pinCode[BT_PIN_LEN-1] = 0;
				APP_DEBUG("Pair device BTHdl: 0x%08x\r\n",pstconBtdev->devHdl);
				APP_DEBUG("Pair device addr: %s\r\n",pstconBtdev->addr);
				APP_DEBUG("Pair pin code: %s\r\n",pinCode);
				APP_DEBUG("pair confirm automatically\r\n");
				//RIL_BT_PairConfirm(TRUE,pinCode);

				APP_DEBUG("CONNECTION ACCEPT--1\n");
				Ql_OS_GiveMutex(mutex_id_serial);
				memset(btatsend,0,sizeof(btatsend));
				Ql_sprintf(btatsend,"AT+QBTPAIRCNF=1,\"%s\"",pinCode);
				okflag = waitcount = 0;
				Ql_RIL_SendATCmd(btatsend,Ql_strlen(btatsend),ATResponse_Handler,NULL,0);
				wait_ok();
		  }

            break;
         case MSG_BT_PAIR_CNF_IND :
            if(URC_BT_PAIR_CNF_SUCCESS == errCode)
            {
                pstconBtdev = (ST_BT_BasicInfo*)param1;
                Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("Paired successful.\r\n");
                Ql_OS_GiveMutex(mutex_id_serial);
                buzz();
            }
            else
            {
            	 Ql_OS_TakeMutex(mutex_id_serial);
            	APP_DEBUG("Paired failed.\r\n");
            	 Ql_OS_GiveMutex(mutex_id_serial);
            }
            break;

         case MSG_BT_SPP_CONN_IND :

              if(URC_BT_CONN_SUCCESS == errCode )
              {

                Ql_memcpy(&(BTSppDev1.btDevice),(ST_BT_BasicInfo *)param1,sizeof(ST_BT_BasicInfo));

                Ql_OS_TakeMutex(mutex_id_serial);
                APP_DEBUG("Connect successful.\r\n");
                Ql_OS_GiveMutex(mutex_id_serial);
                btconflag=1;
                buzz();
              }
              else
              {
            	  Ql_OS_TakeMutex(mutex_id_serial);
            	  APP_DEBUG("Connect failed.\r\n");
            	  Ql_OS_GiveMutex(mutex_id_serial);
              }

              break;

         case MSG_BT_RECV_IND :

        	 Ql_OS_TakeMutex(mutex_id_serial);
            APP_DEBUG("DATA RECEVIED-1\n");
             connid = *(s32 *)param1;
             pstconBtdev = (ST_BT_BasicInfo *)param2;
            Ql_memcpy(&pSppRecHdl,pstconBtdev,sizeof(pSppRecHdl));
             APP_DEBUG("SPP receive data from BTHdl[0x%08x].\r\n",pSppRecHdl.devHdl);
             Ql_OS_GiveMutex(mutex_id_serial);
             u32 actualReadLen = 0;

             ret = RIL_BT_SPP_Read(pSppRecHdl.devHdl, btbuf, sizeof(btbuf),&actualReadLen);
          if(RIL_AT_SUCCESS != ret)
          {
        	  Ql_OS_TakeMutex(mutex_id_serial);
        	  APP_DEBUG("Read failed.ret=%d\r\n",ret);
        	  Ql_OS_GiveMutex(mutex_id_serial);
             break;
          }
          if(actualReadLen == 0)
          {
        	  Ql_OS_TakeMutex(mutex_id_serial);
        	  APP_DEBUG("No more data.\r\n");
        	  Ql_OS_GiveMutex(mutex_id_serial);
             break;
          }
          Ql_OS_TakeMutex(mutex_id_serial);
          APP_DEBUG("BTHdl[%x][len=%d]:\r\n%s\r\n",pSppRecHdl.devHdl,actualReadLen,btbuf);
          Ql_OS_GiveMutex(mutex_id_serial);
         /* memset(btbuf,0,sizeof(btbuf));
          Ql_sprintf(btbuf,"DATA_rECEVIED\n");
          ret = RIL_BT_SPP_Send(pSppRecHdl.devHdl,btbuf,Ql_strlen(btbuf),NULL);
          if(RIL_AT_SUCCESS == ret)
		   {
			  APP_DEBUG("Send successful.\r\n");
		   }
		   else
		   {
			  APP_DEBUG("Send failed.\r\n");
		   }*/

          btrcvflag = 1;

           break;

         case MSG_BT_PAIR_REQ:

              if(URC_BT_NEED_PASSKEY == errCode)
              {
                   //must ask for pincode;
				  	  Ql_OS_TakeMutex(mutex_id_serial);
				  	  pstconBtdev = (ST_BT_BasicInfo*)param1;
                    APP_DEBUG("Pair device BTHdl: 0x%08x\r\n",pstconBtdev->devHdl);
                    APP_DEBUG("Pair device addr: %s\r\n",pstconBtdev->addr);
                    APP_DEBUG("Waiting for pair confirm with pinCode...\r\n");
                    Ql_OS_GiveMutex(mutex_id_serial);
              }

              if(URC_BT_NO_NEED_PASSKEY == errCode)
              {
					Ql_OS_TakeMutex(mutex_id_serial);
					APP_DEBUG("CONNECTION ACCEPT--2\n");
						//no need pincode
					pstconBtdev = (ST_BT_BasicInfo*)param1;
					Ql_strncpy(pinCode,(char*)param2,BT_PIN_LEN);
					pinCode[BT_PIN_LEN-1] = 0;
					APP_DEBUG("Pair device BTHdl: 0x%08x\r\n",pstconBtdev->devHdl);
					APP_DEBUG("Pair device addr: %s\r\n",pstconBtdev->addr);
					APP_DEBUG("Pair pin code: %s\r\n",pinCode);
					APP_DEBUG("pair confirm automatically\r\n");
					//RIL_BT_PairConfirm(TRUE,pinCode);

					APP_DEBUG("CONNECTION ACCEPT--2\n");
					Ql_OS_GiveMutex(mutex_id_serial);
					memset(btatsend,0,sizeof(btatsend));
				   Ql_sprintf(btatsend,"AT+QBTPAIRCNF=1,\"%s\"",pinCode);
				   okflag = waitcount = 0;
				   Ql_RIL_SendATCmd(btatsend,Ql_strlen(btatsend),ATResponse_Handler,NULL,0);
				   wait_ok();

              }

            break;

        case  MSG_BT_CONN_REQ :

        	 Ql_OS_TakeMutex(mutex_id_serial);
        	pstconBtdev = (ST_BT_BasicInfo*)param1;
            APP_DEBUG("Get a connect req\r\n");
            APP_DEBUG("BTHdl: 0x%08x\r\n",pstconBtdev->devHdl);
            APP_DEBUG("Addr: %s\r\n",pstconBtdev->addr);
            APP_DEBUG("Name: %s\r\n",pstconBtdev->name);

            APP_DEBUG("Waiting connect accept.\r\n");

           APP_DEBUG("CONNECTION ACCEPT\n");
           Ql_OS_GiveMutex(mutex_id_serial);
           okflag = waitcount = 0;
           Ql_RIL_SendATCmd("AT+QBTACPT=1,1",Ql_strlen("AT+QBTACPT=1,1"),ATResponse_Handler,NULL,0);
           wait_ok();
           break;
       case  MSG_BT_DISCONN_IND :

             if(URC_BT_DISCONNECT_PASSIVE == errCode || URC_BT_DISCONNECT_POSITIVE == errCode)
             {
            	 Ql_OS_TakeMutex(mutex_id_serial);
            	 APP_DEBUG("Disconnect ok!\r\n");
            	 Ql_OS_GiveMutex(mutex_id_serial);
            	 btconflag=0;
             }
          break;

        default :
            break;
    }
}
static s32 ATResponse_Handler(char* line, u32 len, void* userData)
{
    Ql_UART_Write(m_myUartPort, (u8*)line, len);
    
    Ql_OS_TakeMutex(mutex_id_serial);
    Ql_Sleep(10);
	APP_DEBUG("\r\n<--AT RESPONSE=%d,%s----->\r\n",len,line);
	Ql_Sleep(10);
	Ql_OS_GiveMutex(mutex_id_serial);
     if(sms_readflag == 1)
     {
    	 Ql_OS_TakeMutex(mutex_id_serial);

    	 APP_DEBUG("\r\n<--command----->\r\n");

    	 Ql_OS_GiveMutex(mutex_id_serial);

    	 Ql_memset(command,0,sizeof(command));
         Ql_sprintf(command," %s",line);
         command[len-1] = 0;
         sms_readflag = 2;
     }
     else if(Ql_strstr(line,"REC UNREAD"))
     {
    	 Ql_OS_TakeMutex(mutex_id_serial);

    	 APP_DEBUG("\r\n<--rec unread----->\r\n");

    	 Ql_OS_GiveMutex(mutex_id_serial);
    	 Ql_memset(msgbuf,0,sizeof(msgbuf));
    	 Ql_memcpy(msgbuf, line, len);
    	 sms_readflag = 1;
     }
    else if(Ql_strstr(line,"+CREG: "))
    {
    	if((Ql_strstr(line,"+CREG: 1,1"))||(Ql_strstr(line,"+CREG: 1,5")))
    	{
    		Ql_OS_TakeMutex(mutex_id_serial);

			APP_DEBUG("\r\n<--network Registered----->\r\n");

			Ql_OS_GiveMutex(mutex_id_serial);
			net_reg=1;
    	}
    	else
    	{
			Ql_OS_TakeMutex(mutex_id_serial);

			APP_DEBUG("\r\n<--network not Registered----->\r\n");

			Ql_OS_GiveMutex(mutex_id_serial);
			net_reg=0;
    	}
    }
    else if(Ql_strstr(line,"+CCLK: "))
    {
    	for(int j=0;j<17;j++)
    	{
    		d_t[j]=line[j+10];
    	}
    	Ql_OS_TakeMutex(mutex_id_serial);

		APP_DEBUG("<-- date=%s-->\r\n",d_t);

		Ql_OS_GiveMutex(mutex_id_serial);
    }
    else if(Ql_strstr(line,"+QSPN: "))
    {
    	if(Ql_strstr(line,"airtel"))
		{

			apn_id=0;
			memset(apn,0,sizeof(apn));
			Ql_sprintf(apn,"airtelgprs.com");

		}
    	else if(Ql_strstr(line,"Vodafone"))
		{

			apn_id=1;
			memset(apn,0,sizeof(apn));
			Ql_sprintf(apn,"iot.com");
		}

    	else if(Ql_strstr(line,"Idea"))
		{

			apn_id=2;
			memset(apn,0,sizeof(apn));
			Ql_sprintf(apn,"internet");
		}

    	else if(Ql_strstr(line,"bsnl"))
		{

			apn_id=3;
			memset(apn,0,sizeof(apn));
			Ql_sprintf(apn,"bsnlnet");
		}
    }
    else if(Ql_strstr(line,"+CSQ: "))
    {
    	if(((int)line[8])>47 && ((int)line[8])<58 &&((int)line[9])>47 && ((int)line[9])<58)
    	{
    		sigstr = (((int)line[8]) - 48) * 10 + ((int)line[9] - 48);
    	}
    	else if(((int)line[8])>47 && ((int)line[8])<58)
    	{
    		sigstr =((int)line[8] - 48);
    	}

    	sigstr = (sigstr / 31.0) * 100.0;
    }
    else if (Ql_RIL_FindLine(line, len, "OK"))
    {  

    	Ql_OS_TakeMutex(mutex_id_serial);

    	APP_DEBUG("<-- AT COMMEND OK_1-->\r\n");

    	Ql_OS_GiveMutex(mutex_id_serial);
    	okflag=1;
    	return  RIL_ATRSP_SUCCESS;
    }
    else if (Ql_RIL_FindLine(line, len, "ERROR"))
    {  
    	Ql_OS_TakeMutex(mutex_id_serial);
    	APP_DEBUG("<-- AT COMMEND ERROR-->\r\n");
    	Ql_OS_GiveMutex(mutex_id_serial);
    	return  RIL_ATRSP_FAILED;
    }
    else if (Ql_RIL_FindString(line, len, "+CME ERROR"))
    {

    	Ql_OS_TakeMutex(mutex_id_serial);
    	APP_DEBUG("<-- AT COMMEND ERROR-->\r\n");
    	Ql_OS_GiveMutex(mutex_id_serial);
    	return  RIL_ATRSP_FAILED;
    }
    else if (Ql_RIL_FindString(line, len, "+CMS ERROR:"))
    {

    	Ql_OS_TakeMutex(mutex_id_serial);
    	APP_DEBUG("<-- AT COMMEND ERROR-->\r\n");
    	Ql_OS_GiveMutex(mutex_id_serial);
    	return  RIL_ATRSP_FAILED;
    }
    else if(atflag == 1)
	{
		 for(int lv1=0;lv1<=14;lv1++)
		 {
			if((line[lv1+2] >= '0') && (line[lv1+2] <= '9'))
			   imei[lv1] = line[lv1+2];
			else
			   imei[0] = 0;
		 }
		 imei[15] = 0;
		 atflag = 0;
		 Ql_OS_TakeMutex(mutex_id_serial);
		 APP_DEBUG("imei-%s\n",imei);
		 Ql_OS_GiveMutex(mutex_id_serial);
	 }
    return RIL_ATRSP_CONTINUE; //continue wait
}
void Timer_handler(u32 timerId, void* param)
{
    *((s32*)param) +=1;
    TIME_TEST++;
    time1++;
    time2++;
    time3++;

    APP_DEBUG("<-- stack Timer_handler, param:%d -->\r\n", time1);


}

#endif // __CUSTOMER_CODE__
