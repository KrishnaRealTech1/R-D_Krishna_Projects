
# 1 "I-Tank_Timer_V3.c"


# 2
#pragma config FOSC = INTOSCIO
#pragma config WDTE = OFF
#pragma config PWRTE = ON
#pragma config MCLRE = OFF
#pragma config CP = OFF
#pragma config CPD = OFF
#pragma config BOREN = ON

# 18 "C:\Program Files\Microchip\xc8\v2.31\pic\include\xc.h"
extern const char __xc8_OPTIM_SPEED;

extern double __fpnormalize(double);


# 13 "C:\Program Files\Microchip\xc8\v2.31\pic\include\c90\xc8debug.h"
#pragma intrinsic(__builtin_software_breakpoint)
extern void __builtin_software_breakpoint(void);

# 13 "C:\Program Files\Microchip\xc8\v2.31\pic\include\c90\stdint.h"
typedef signed char int8_t;

# 20
typedef signed int int16_t;

# 28
typedef __int24 int24_t;

# 36
typedef signed long int int32_t;

# 52
typedef unsigned char uint8_t;

# 58
typedef unsigned int uint16_t;

# 65
typedef __uint24 uint24_t;

# 72
typedef unsigned long int uint32_t;

# 88
typedef signed char int_least8_t;

# 96
typedef signed int int_least16_t;

# 109
typedef __int24 int_least24_t;

# 118
typedef signed long int int_least32_t;

# 136
typedef unsigned char uint_least8_t;

# 143
typedef unsigned int uint_least16_t;

# 154
typedef __uint24 uint_least24_t;

# 162
typedef unsigned long int uint_least32_t;

# 181
typedef signed char int_fast8_t;

# 188
typedef signed int int_fast16_t;

# 200
typedef __int24 int_fast24_t;

# 208
typedef signed long int int_fast32_t;

# 224
typedef unsigned char uint_fast8_t;

# 230
typedef unsigned int uint_fast16_t;

# 240
typedef __uint24 uint_fast24_t;

# 247
typedef unsigned long int uint_fast32_t;

# 268
typedef int32_t intmax_t;

# 282
typedef uint32_t uintmax_t;

# 289
typedef int16_t intptr_t;




typedef uint16_t uintptr_t;


# 7 "C:\Program Files\Microchip\xc8\v2.31\pic\include\builtins.h"
#pragma intrinsic(__nop)
extern void __nop(void);


#pragma intrinsic(_delay)
extern __nonreentrant void _delay(uint32_t);
#pragma intrinsic(_delaywdt)
extern __nonreentrant void _delaywdt(uint32_t);

# 52 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\pic\include\proc\pic12f683.h"
extern volatile unsigned char INDF __at(0x000);

asm("INDF equ 00h");




extern volatile unsigned char TMR0 __at(0x001);

asm("TMR0 equ 01h");




extern volatile unsigned char PCL __at(0x002);

asm("PCL equ 02h");




extern volatile unsigned char STATUS __at(0x003);

asm("STATUS equ 03h");


typedef union {
struct {
unsigned C :1;
unsigned DC :1;
unsigned Z :1;
unsigned nPD :1;
unsigned nTO :1;
unsigned RP :2;
unsigned IRP :1;
};
struct {
unsigned :5;
unsigned RP0 :1;
unsigned RP1 :1;
};
struct {
unsigned CARRY :1;
unsigned :1;
unsigned ZERO :1;
};
} STATUSbits_t;
extern volatile STATUSbits_t STATUSbits __at(0x003);

# 159
extern volatile unsigned char FSR __at(0x004);

asm("FSR equ 04h");




extern volatile unsigned char GPIO __at(0x005);

asm("GPIO equ 05h");


typedef union {
struct {
unsigned GP0 :1;
unsigned GP1 :1;
unsigned GP2 :1;
unsigned GP3 :1;
unsigned GP4 :1;
unsigned GP5 :1;
};
} GPIObits_t;
extern volatile GPIObits_t GPIObits __at(0x005);

# 216
extern volatile unsigned char PCLATH __at(0x00A);

asm("PCLATH equ 0Ah");


typedef union {
struct {
unsigned PCLATH :5;
};
} PCLATHbits_t;
extern volatile PCLATHbits_t PCLATHbits __at(0x00A);

# 236
extern volatile unsigned char INTCON __at(0x00B);

asm("INTCON equ 0Bh");


typedef union {
struct {
unsigned GPIF :1;
unsigned INTF :1;
unsigned T0IF :1;
unsigned GPIE :1;
unsigned INTE :1;
unsigned T0IE :1;
unsigned PEIE :1;
unsigned GIE :1;
};
struct {
unsigned :2;
unsigned TMR0IF :1;
unsigned :2;
unsigned TMR0IE :1;
};
} INTCONbits_t;
extern volatile INTCONbits_t INTCONbits __at(0x00B);

# 314
extern volatile unsigned char PIR1 __at(0x00C);

asm("PIR1 equ 0Ch");


typedef union {
struct {
unsigned TMR1IF :1;
unsigned TMR2IF :1;
unsigned OSFIF :1;
unsigned CMIF :1;
unsigned :1;
unsigned CCP1IF :1;
unsigned ADIF :1;
unsigned EEIF :1;
};
struct {
unsigned T1IF :1;
unsigned T2IF :1;
};
} PIR1bits_t;
extern volatile PIR1bits_t PIR1bits __at(0x00C);

# 385
extern volatile unsigned short TMR1 __at(0x00E);

asm("TMR1 equ 0Eh");




extern volatile unsigned char TMR1L __at(0x00E);

asm("TMR1L equ 0Eh");




extern volatile unsigned char TMR1H __at(0x00F);

asm("TMR1H equ 0Fh");




extern volatile unsigned char T1CON __at(0x010);

asm("T1CON equ 010h");


typedef union {
struct {
unsigned TMR1ON :1;
unsigned TMR1CS :1;
unsigned nT1SYNC :1;
unsigned T1OSCEN :1;
unsigned T1CKPS :2;
unsigned TMR1GE :1;
unsigned T1GINV :1;
};
struct {
unsigned :4;
unsigned T1CKPS0 :1;
unsigned T1CKPS1 :1;
unsigned T1GE :1;
};
} T1CONbits_t;
extern volatile T1CONbits_t T1CONbits __at(0x010);

# 483
extern volatile unsigned char TMR2 __at(0x011);

asm("TMR2 equ 011h");




extern volatile unsigned char T2CON __at(0x012);

asm("T2CON equ 012h");


typedef union {
struct {
unsigned T2CKPS :2;
unsigned TMR2ON :1;
unsigned TOUTPS :4;
};
struct {
unsigned T2CKPS0 :1;
unsigned T2CKPS1 :1;
unsigned :1;
unsigned TOUTPS0 :1;
unsigned TOUTPS1 :1;
unsigned TOUTPS2 :1;
unsigned TOUTPS3 :1;
};
} T2CONbits_t;
extern volatile T2CONbits_t T2CONbits __at(0x012);

# 561
extern volatile unsigned short CCPR1 __at(0x013);

asm("CCPR1 equ 013h");




extern volatile unsigned char CCPR1L __at(0x013);

asm("CCPR1L equ 013h");




extern volatile unsigned char CCPR1H __at(0x014);

asm("CCPR1H equ 014h");




extern volatile unsigned char CCP1CON __at(0x015);

asm("CCP1CON equ 015h");


typedef union {
struct {
unsigned CCP1M :4;
unsigned DC1B :2;
};
struct {
unsigned CCP1M0 :1;
unsigned CCP1M1 :1;
unsigned CCP1M2 :1;
unsigned CCP1M3 :1;
unsigned DC1B0 :1;
unsigned DC1B1 :1;
};
} CCP1CONbits_t;
extern volatile CCP1CONbits_t CCP1CONbits __at(0x015);

# 646
extern volatile unsigned char WDTCON __at(0x018);

asm("WDTCON equ 018h");


typedef union {
struct {
unsigned SWDTEN :1;
unsigned WDTPS :4;
};
struct {
unsigned :1;
unsigned WDTPS0 :1;
unsigned WDTPS1 :1;
unsigned WDTPS2 :1;
unsigned WDTPS3 :1;
};
} WDTCONbits_t;
extern volatile WDTCONbits_t WDTCONbits __at(0x018);

# 699
extern volatile unsigned char CMCON0 __at(0x019);

asm("CMCON0 equ 019h");


typedef union {
struct {
unsigned CM :3;
unsigned CIS :1;
unsigned CINV :1;
unsigned :1;
unsigned COUT :1;
};
struct {
unsigned CM0 :1;
unsigned CM1 :1;
unsigned CM2 :1;
};
} CMCON0bits_t;
extern volatile CMCON0bits_t CMCON0bits __at(0x019);

# 758
extern volatile unsigned char CMCON1 __at(0x01A);

asm("CMCON1 equ 01Ah");


typedef union {
struct {
unsigned CMSYNC :1;
unsigned T1GSS :1;
};
} CMCON1bits_t;
extern volatile CMCON1bits_t CMCON1bits __at(0x01A);

# 784
extern volatile unsigned char ADRESH __at(0x01E);

asm("ADRESH equ 01Eh");




extern volatile unsigned char ADCON0 __at(0x01F);

asm("ADCON0 equ 01Fh");


typedef union {
struct {
unsigned ADON :1;
unsigned GO_nDONE :1;
unsigned CHS :2;
unsigned :2;
unsigned VCFG :1;
unsigned ADFM :1;
};
struct {
unsigned :1;
unsigned GO :1;
unsigned CHS0 :1;
unsigned CHS1 :1;
unsigned CHS2 :1;
};
struct {
unsigned :1;
unsigned nDONE :1;
};
struct {
unsigned :1;
unsigned GO_DONE :1;
};
} ADCON0bits_t;
extern volatile ADCON0bits_t ADCON0bits __at(0x01F);

# 881
extern volatile unsigned char OPTION_REG __at(0x081);

asm("OPTION_REG equ 081h");


typedef union {
struct {
unsigned PS :3;
unsigned PSA :1;
unsigned T0SE :1;
unsigned T0CS :1;
unsigned INTEDG :1;
unsigned nGPPU :1;
};
struct {
unsigned PS0 :1;
unsigned PS1 :1;
unsigned PS2 :1;
};
} OPTION_REGbits_t;
extern volatile OPTION_REGbits_t OPTION_REGbits __at(0x081);

# 951
extern volatile unsigned char TRISIO __at(0x085);

asm("TRISIO equ 085h");


typedef union {
struct {
unsigned TRISIO0 :1;
unsigned TRISIO1 :1;
unsigned TRISIO2 :1;
unsigned TRISIO3 :1;
unsigned TRISIO4 :1;
unsigned TRISIO5 :1;
};
} TRISIObits_t;
extern volatile TRISIObits_t TRISIObits __at(0x085);

# 1001
extern volatile unsigned char PIE1 __at(0x08C);

asm("PIE1 equ 08Ch");


typedef union {
struct {
unsigned TMR1IE :1;
unsigned TMR2IE :1;
unsigned OSFIE :1;
unsigned CMIE :1;
unsigned :1;
unsigned CCP1IE :1;
unsigned ADIE :1;
unsigned EEIE :1;
};
struct {
unsigned T1IE :1;
unsigned T2IE :1;
};
} PIE1bits_t;
extern volatile PIE1bits_t PIE1bits __at(0x08C);

# 1072
extern volatile unsigned char PCON __at(0x08E);

asm("PCON equ 08Eh");


typedef union {
struct {
unsigned nBOD :1;
unsigned nPOR :1;
unsigned :2;
unsigned SBODEN :1;
unsigned ULPWUE :1;
};
} PCONbits_t;
extern volatile PCONbits_t PCONbits __at(0x08E);

# 1111
extern volatile unsigned char OSCCON __at(0x08F);

asm("OSCCON equ 08Fh");


typedef union {
struct {
unsigned SCS :1;
unsigned LTS :1;
unsigned HTS :1;
unsigned OSTS :1;
unsigned IRCF :3;
};
struct {
unsigned :4;
unsigned IRCF0 :1;
unsigned IRCF1 :1;
unsigned IRCF2 :1;
};
} OSCCONbits_t;
extern volatile OSCCONbits_t OSCCONbits __at(0x08F);

# 1176
extern volatile unsigned char OSCTUNE __at(0x090);

asm("OSCTUNE equ 090h");


typedef union {
struct {
unsigned TUN :5;
};
struct {
unsigned TUN0 :1;
unsigned TUN1 :1;
unsigned TUN2 :1;
unsigned TUN3 :1;
unsigned TUN4 :1;
};
} OSCTUNEbits_t;
extern volatile OSCTUNEbits_t OSCTUNEbits __at(0x090);

# 1228
extern volatile unsigned char PR2 __at(0x092);

asm("PR2 equ 092h");




extern volatile unsigned char WPU __at(0x095);

asm("WPU equ 095h");


extern volatile unsigned char WPUA __at(0x095);

asm("WPUA equ 095h");


typedef union {
struct {
unsigned WPU0 :1;
unsigned WPU1 :1;
unsigned WPU2 :1;
unsigned :1;
unsigned WPU4 :1;
unsigned WPU5 :1;
};
struct {
unsigned WPUA0 :1;
unsigned WPUA1 :1;
unsigned WPUA2 :1;
unsigned :1;
unsigned WPUA4 :1;
unsigned WPUA5 :1;
};
} WPUbits_t;
extern volatile WPUbits_t WPUbits __at(0x095);

# 1316
typedef union {
struct {
unsigned WPU0 :1;
unsigned WPU1 :1;
unsigned WPU2 :1;
unsigned :1;
unsigned WPU4 :1;
unsigned WPU5 :1;
};
struct {
unsigned WPUA0 :1;
unsigned WPUA1 :1;
unsigned WPUA2 :1;
unsigned :1;
unsigned WPUA4 :1;
unsigned WPUA5 :1;
};
} WPUAbits_t;
extern volatile WPUAbits_t WPUAbits __at(0x095);

# 1389
extern volatile unsigned char IOC __at(0x096);

asm("IOC equ 096h");


extern volatile unsigned char IOCA __at(0x096);

asm("IOCA equ 096h");


typedef union {
struct {
unsigned IOC0 :1;
unsigned IOC1 :1;
unsigned IOC2 :1;
unsigned IOC3 :1;
unsigned IOC4 :1;
unsigned IOC5 :1;
};
struct {
unsigned IOCA0 :1;
unsigned IOCA1 :1;
unsigned IOCA2 :1;
unsigned IOCA3 :1;
unsigned IOCA4 :1;
unsigned IOCA5 :1;
};
} IOCbits_t;
extern volatile IOCbits_t IOCbits __at(0x096);

# 1480
typedef union {
struct {
unsigned IOC0 :1;
unsigned IOC1 :1;
unsigned IOC2 :1;
unsigned IOC3 :1;
unsigned IOC4 :1;
unsigned IOC5 :1;
};
struct {
unsigned IOCA0 :1;
unsigned IOCA1 :1;
unsigned IOCA2 :1;
unsigned IOCA3 :1;
unsigned IOCA4 :1;
unsigned IOCA5 :1;
};
} IOCAbits_t;
extern volatile IOCAbits_t IOCAbits __at(0x096);

# 1563
extern volatile unsigned char VRCON __at(0x099);

asm("VRCON equ 099h");


typedef union {
struct {
unsigned VR :4;
unsigned :1;
unsigned VRR :1;
unsigned :1;
unsigned VREN :1;
};
struct {
unsigned VR0 :1;
unsigned VR1 :1;
unsigned VR2 :1;
unsigned VR3 :1;
};
} VRCONbits_t;
extern volatile VRCONbits_t VRCONbits __at(0x099);

# 1623
extern volatile unsigned char EEDAT __at(0x09A);

asm("EEDAT equ 09Ah");


extern volatile unsigned char EEDATA __at(0x09A);

asm("EEDATA equ 09Ah");


typedef union {
struct {
unsigned EEDAT :8;
};
} EEDATbits_t;
extern volatile EEDATbits_t EEDATbits __at(0x09A);

# 1646
typedef union {
struct {
unsigned EEDAT :8;
};
} EEDATAbits_t;
extern volatile EEDATAbits_t EEDATAbits __at(0x09A);

# 1661
extern volatile unsigned char EEADR __at(0x09B);

asm("EEADR equ 09Bh");




extern volatile unsigned char EECON1 __at(0x09C);

asm("EECON1 equ 09Ch");


typedef union {
struct {
unsigned RD :1;
unsigned WR :1;
unsigned WREN :1;
unsigned WRERR :1;
};
} EECON1bits_t;
extern volatile EECON1bits_t EECON1bits __at(0x09C);

# 1706
extern volatile unsigned char EECON2 __at(0x09D);

asm("EECON2 equ 09Dh");




extern volatile unsigned char ADRESL __at(0x09E);

asm("ADRESL equ 09Eh");




extern volatile unsigned char ANSEL __at(0x09F);

asm("ANSEL equ 09Fh");


typedef union {
struct {
unsigned ANS :4;
unsigned ADCS :3;
};
struct {
unsigned ANS0 :1;
unsigned ANS1 :1;
unsigned ANS2 :1;
unsigned ANS3 :1;
unsigned ADCS0 :1;
unsigned ADCS1 :1;
unsigned ADCS2 :1;
};
} ANSELbits_t;
extern volatile ANSELbits_t ANSELbits __at(0x09F);

# 1800
extern volatile __bit ADCS0 __at(0x4FC);


extern volatile __bit ADCS1 __at(0x4FD);


extern volatile __bit ADCS2 __at(0x4FE);


extern volatile __bit ADFM __at(0xFF);


extern volatile __bit ADIE __at(0x466);


extern volatile __bit ADIF __at(0x66);


extern volatile __bit ADON __at(0xF8);


extern volatile __bit ANS0 __at(0x4F8);


extern volatile __bit ANS1 __at(0x4F9);


extern volatile __bit ANS2 __at(0x4FA);


extern volatile __bit ANS3 __at(0x4FB);


extern volatile __bit CARRY __at(0x18);


extern volatile __bit CCP1IE __at(0x465);


extern volatile __bit CCP1IF __at(0x65);


extern volatile __bit CCP1M0 __at(0xA8);


extern volatile __bit CCP1M1 __at(0xA9);


extern volatile __bit CCP1M2 __at(0xAA);


extern volatile __bit CCP1M3 __at(0xAB);


extern volatile __bit CHS0 __at(0xFA);


extern volatile __bit CHS1 __at(0xFB);


extern volatile __bit CHS2 __at(0xFC);


extern volatile __bit CINV __at(0xCC);


extern volatile __bit CIS __at(0xCB);


extern volatile __bit CM0 __at(0xC8);


extern volatile __bit CM1 __at(0xC9);


extern volatile __bit CM2 __at(0xCA);


extern volatile __bit CMIE __at(0x463);


extern volatile __bit CMIF __at(0x63);


extern volatile __bit CMSYNC __at(0xD0);


extern volatile __bit COUT __at(0xCE);


extern volatile __bit DC __at(0x19);


extern volatile __bit DC1B0 __at(0xAC);


extern volatile __bit DC1B1 __at(0xAD);


extern volatile __bit EEIE __at(0x467);


extern volatile __bit EEIF __at(0x67);


extern volatile __bit GIE __at(0x5F);


extern volatile __bit GO __at(0xF9);


extern volatile __bit GO_DONE __at(0xF9);


extern volatile __bit GO_nDONE __at(0xF9);


extern volatile __bit GP0 __at(0x28);


extern volatile __bit GP1 __at(0x29);


extern volatile __bit GP2 __at(0x2A);


extern volatile __bit GP3 __at(0x2B);


extern volatile __bit GP4 __at(0x2C);


extern volatile __bit GP5 __at(0x2D);


extern volatile __bit GPIE __at(0x5B);


extern volatile __bit GPIF __at(0x58);


extern volatile __bit HTS __at(0x47A);


extern volatile __bit INTE __at(0x5C);


extern volatile __bit INTEDG __at(0x40E);


extern volatile __bit INTF __at(0x59);


extern volatile __bit IOC0 __at(0x4B0);


extern volatile __bit IOC1 __at(0x4B1);


extern volatile __bit IOC2 __at(0x4B2);


extern volatile __bit IOC3 __at(0x4B3);


extern volatile __bit IOC4 __at(0x4B4);


extern volatile __bit IOC5 __at(0x4B5);


extern volatile __bit IOCA0 __at(0x4B0);


extern volatile __bit IOCA1 __at(0x4B1);


extern volatile __bit IOCA2 __at(0x4B2);


extern volatile __bit IOCA3 __at(0x4B3);


extern volatile __bit IOCA4 __at(0x4B4);


extern volatile __bit IOCA5 __at(0x4B5);


extern volatile __bit IRCF0 __at(0x47C);


extern volatile __bit IRCF1 __at(0x47D);


extern volatile __bit IRCF2 __at(0x47E);


extern volatile __bit IRP __at(0x1F);


extern volatile __bit LTS __at(0x479);


extern volatile __bit OSFIE __at(0x462);


extern volatile __bit OSFIF __at(0x62);


extern volatile __bit OSTS __at(0x47B);


extern volatile __bit PEIE __at(0x5E);


extern volatile __bit PS0 __at(0x408);


extern volatile __bit PS1 __at(0x409);


extern volatile __bit PS2 __at(0x40A);


extern volatile __bit PSA __at(0x40B);


extern volatile __bit RD __at(0x4E0);


extern volatile __bit RP0 __at(0x1D);


extern volatile __bit RP1 __at(0x1E);


extern volatile __bit SBODEN __at(0x474);


extern volatile __bit SCS __at(0x478);


extern volatile __bit SWDTEN __at(0xC0);


extern volatile __bit T0CS __at(0x40D);


extern volatile __bit T0IE __at(0x5D);


extern volatile __bit T0IF __at(0x5A);


extern volatile __bit T0SE __at(0x40C);


extern volatile __bit T1CKPS0 __at(0x84);


extern volatile __bit T1CKPS1 __at(0x85);


extern volatile __bit T1GE __at(0x86);


extern volatile __bit T1GINV __at(0x87);


extern volatile __bit T1GSS __at(0xD1);


extern volatile __bit T1IE __at(0x460);


extern volatile __bit T1IF __at(0x60);


extern volatile __bit T1OSCEN __at(0x83);


extern volatile __bit T2CKPS0 __at(0x90);


extern volatile __bit T2CKPS1 __at(0x91);


extern volatile __bit T2IE __at(0x461);


extern volatile __bit T2IF __at(0x61);


extern volatile __bit TMR0IE __at(0x5D);


extern volatile __bit TMR0IF __at(0x5A);


extern volatile __bit TMR1CS __at(0x81);


extern volatile __bit TMR1GE __at(0x86);


extern volatile __bit TMR1IE __at(0x460);


extern volatile __bit TMR1IF __at(0x60);


extern volatile __bit TMR1ON __at(0x80);


extern volatile __bit TMR2IE __at(0x461);


extern volatile __bit TMR2IF __at(0x61);


extern volatile __bit TMR2ON __at(0x92);


extern volatile __bit TOUTPS0 __at(0x93);


extern volatile __bit TOUTPS1 __at(0x94);


extern volatile __bit TOUTPS2 __at(0x95);


extern volatile __bit TOUTPS3 __at(0x96);


extern volatile __bit TRISIO0 __at(0x428);


extern volatile __bit TRISIO1 __at(0x429);


extern volatile __bit TRISIO2 __at(0x42A);


extern volatile __bit TRISIO3 __at(0x42B);


extern volatile __bit TRISIO4 __at(0x42C);


extern volatile __bit TRISIO5 __at(0x42D);


extern volatile __bit TUN0 __at(0x480);


extern volatile __bit TUN1 __at(0x481);


extern volatile __bit TUN2 __at(0x482);


extern volatile __bit TUN3 __at(0x483);


extern volatile __bit TUN4 __at(0x484);


extern volatile __bit ULPWUE __at(0x475);


extern volatile __bit VCFG __at(0xFE);


extern volatile __bit VR0 __at(0x4C8);


extern volatile __bit VR1 __at(0x4C9);


extern volatile __bit VR2 __at(0x4CA);


extern volatile __bit VR3 __at(0x4CB);


extern volatile __bit VREN __at(0x4CF);


extern volatile __bit VRR __at(0x4CD);


extern volatile __bit WDTPS0 __at(0xC1);


extern volatile __bit WDTPS1 __at(0xC2);


extern volatile __bit WDTPS2 __at(0xC3);


extern volatile __bit WDTPS3 __at(0xC4);


extern volatile __bit WPU0 __at(0x4A8);


extern volatile __bit WPU1 __at(0x4A9);


extern volatile __bit WPU2 __at(0x4AA);


extern volatile __bit WPU4 __at(0x4AC);


extern volatile __bit WPU5 __at(0x4AD);


extern volatile __bit WPUA0 __at(0x4A8);


extern volatile __bit WPUA1 __at(0x4A9);


extern volatile __bit WPUA2 __at(0x4AA);


extern volatile __bit WPUA4 __at(0x4AC);


extern volatile __bit WPUA5 __at(0x4AD);


extern volatile __bit WR __at(0x4E1);


extern volatile __bit WREN __at(0x4E2);


extern volatile __bit WRERR __at(0x4E3);


extern volatile __bit ZERO __at(0x1A);


extern volatile __bit nBOD __at(0x470);


extern volatile __bit nDONE __at(0xF9);


extern volatile __bit nGPPU __at(0x40F);


extern volatile __bit nPD __at(0x1B);


extern volatile __bit nPOR __at(0x471);


extern volatile __bit nT1SYNC __at(0x82);


extern volatile __bit nTO __at(0x1C);

# 76 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\pic\include\pic.h"
__attribute__((__unsupported__("The " "FLASH_READ" " macro function is no longer supported. Please use the MPLAB X MCC."))) unsigned char __flash_read(unsigned short addr);

__attribute__((__unsupported__("The " "FLASH_WRITE" " macro function is no longer supported. Please use the MPLAB X MCC."))) void __flash_write(unsigned short addr, unsigned short data);

__attribute__((__unsupported__("The " "FLASH_ERASE" " macro function is no longer supported. Please use the MPLAB X MCC."))) void __flash_erase(unsigned short addr);

# 114 "C:\Program Files\Microchip\xc8\v2.31\pic\include\eeprom_routines.h"
extern void eeprom_write(unsigned char addr, unsigned char value);
extern unsigned char eeprom_read(unsigned char addr);

# 118 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\pic\include\pic.h"
extern __bank0 unsigned char __resetbits;
extern __bank0 __bit __powerdown;
extern __bank0 __bit __timeout;

# 24 "I-Tank_Timer_V3.c"
static void init_device(void)
{

ANSEL = 0x00;
CMCON0 = 0x07;



OSCCONbits.IRCF = 0b110;
OSCCONbits.SCS = 0;

# 39
TRISIO = 0b11111110;

GPIO = 0x00;


OPTION_REGbits.T0CS = 0;
OPTION_REGbits.PSA = 0;
OPTION_REGbits.PS2 = 1;
OPTION_REGbits.PS1 = 1;
OPTION_REGbits.PS0 = 1;


INTCONbits.T0IE = 0;
INTCONbits.T0IF = 0;
}

# 63
static void delay_1s(void)
{
unsigned char i;

for (i = 0; i < 16; i++)
{
TMR0 = 0;
INTCONbits.T0IF = 0;


while (INTCONbits.T0IF == 0)
{

}
}
}




void Wait_Seconds(unsigned long sec)
{
while (sec--)
{
delay_1s();
}
}

# 101
unsigned long Get_Off_Delay(void)
{
unsigned long delay = 0;

if (GP1) delay += 3600UL;
if (GP2) delay += 10800UL;
if (GP4) delay += 28800UL;
if (GP5) delay += 43200UL;

if (delay == 0UL)
{
delay = 10UL;
}

return delay;
}




void main(void)
{
init_device();

while (1)
{
unsigned long off_delay = Get_Off_Delay();


GP0 = 0;
Wait_Seconds(off_delay);


GP0 = 1;
Wait_Seconds(5UL);
}
}
