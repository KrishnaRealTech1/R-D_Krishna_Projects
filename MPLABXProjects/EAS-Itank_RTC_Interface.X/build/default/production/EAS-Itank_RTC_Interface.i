# 1 "EAS-Itank_RTC_Interface.c"
# 1 "<built-in>" 1
# 1 "<built-in>" 3
# 288 "<built-in>" 3
# 1 "<command line>" 1
# 1 "<built-in>" 2
# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\language_support.h" 1 3
# 2 "<built-in>" 2
# 1 "EAS-Itank_RTC_Interface.c" 2



#pragma config FOSC = INTOSCIO
#pragma config WDTE = OFF
#pragma config PWRTE = ON
#pragma config MCLRE = OFF
#pragma config CP = OFF
#pragma config CPD = OFF
#pragma config BOREN = ON


# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\xc.h" 1 3
# 18 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\xc.h" 3
extern const char __xc8_OPTIM_SPEED;

extern double __fpnormalize(double);



# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\xc8debug.h" 1 3
# 13 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\xc8debug.h" 3
#pragma intrinsic(__builtin_software_breakpoint)
extern void __builtin_software_breakpoint(void);
# 23 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\xc.h" 2 3

# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\builtins.h" 1 3



# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 1 3
# 13 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef signed char int8_t;






typedef signed int int16_t;







typedef __int24 int24_t;







typedef signed long int int32_t;
# 52 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef unsigned char uint8_t;





typedef unsigned int uint16_t;






typedef __uint24 uint24_t;






typedef unsigned long int uint32_t;
# 88 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef signed char int_least8_t;







typedef signed int int_least16_t;
# 109 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef __int24 int_least24_t;
# 118 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef signed long int int_least32_t;
# 136 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef unsigned char uint_least8_t;






typedef unsigned int uint_least16_t;
# 154 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef __uint24 uint_least24_t;







typedef unsigned long int uint_least32_t;
# 181 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef signed char int_fast8_t;






typedef signed int int_fast16_t;
# 200 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef __int24 int_fast24_t;







typedef signed long int int_fast32_t;
# 224 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef unsigned char uint_fast8_t;





typedef unsigned int uint_fast16_t;
# 240 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef __uint24 uint_fast24_t;






typedef unsigned long int uint_fast32_t;
# 268 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef int32_t intmax_t;
# 282 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 3
typedef uint32_t uintmax_t;






typedef int16_t intptr_t;




typedef uint16_t uintptr_t;
# 4 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\builtins.h" 2 3



#pragma intrinsic(__nop)
extern void __nop(void);


#pragma intrinsic(_delay)
extern __attribute__((nonreentrant)) void _delay(uint32_t);
#pragma intrinsic(_delaywdt)
extern __attribute__((nonreentrant)) void _delaywdt(uint32_t);
# 24 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\xc.h" 2 3




# 1 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic.h" 1 3




# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\htc.h" 1 3



# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\xc.h" 1 3
# 4 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\htc.h" 2 3
# 6 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic.h" 2 3







# 1 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic_chip_select.h" 1 3
# 233 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic_chip_select.h" 3
# 1 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 1 3
# 44 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\__at.h" 1 3
# 45 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 2 3







extern volatile unsigned char INDF __attribute__((address(0x000)));

__asm("INDF equ 00h");




extern volatile unsigned char TMR0 __attribute__((address(0x001)));

__asm("TMR0 equ 01h");




extern volatile unsigned char PCL __attribute__((address(0x002)));

__asm("PCL equ 02h");




extern volatile unsigned char STATUS __attribute__((address(0x003)));

__asm("STATUS equ 03h");


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
extern volatile STATUSbits_t STATUSbits __attribute__((address(0x003)));
# 159 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char FSR __attribute__((address(0x004)));

__asm("FSR equ 04h");




extern volatile unsigned char GPIO __attribute__((address(0x005)));

__asm("GPIO equ 05h");


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
extern volatile GPIObits_t GPIObits __attribute__((address(0x005)));
# 216 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char PCLATH __attribute__((address(0x00A)));

__asm("PCLATH equ 0Ah");


typedef union {
    struct {
        unsigned PCLATH :5;
    };
} PCLATHbits_t;
extern volatile PCLATHbits_t PCLATHbits __attribute__((address(0x00A)));
# 236 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char INTCON __attribute__((address(0x00B)));

__asm("INTCON equ 0Bh");


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
extern volatile INTCONbits_t INTCONbits __attribute__((address(0x00B)));
# 314 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char PIR1 __attribute__((address(0x00C)));

__asm("PIR1 equ 0Ch");


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
extern volatile PIR1bits_t PIR1bits __attribute__((address(0x00C)));
# 385 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned short TMR1 __attribute__((address(0x00E)));

__asm("TMR1 equ 0Eh");




extern volatile unsigned char TMR1L __attribute__((address(0x00E)));

__asm("TMR1L equ 0Eh");




extern volatile unsigned char TMR1H __attribute__((address(0x00F)));

__asm("TMR1H equ 0Fh");




extern volatile unsigned char T1CON __attribute__((address(0x010)));

__asm("T1CON equ 010h");


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
extern volatile T1CONbits_t T1CONbits __attribute__((address(0x010)));
# 483 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char TMR2 __attribute__((address(0x011)));

__asm("TMR2 equ 011h");




extern volatile unsigned char T2CON __attribute__((address(0x012)));

__asm("T2CON equ 012h");


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
extern volatile T2CONbits_t T2CONbits __attribute__((address(0x012)));
# 561 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned short CCPR1 __attribute__((address(0x013)));

__asm("CCPR1 equ 013h");




extern volatile unsigned char CCPR1L __attribute__((address(0x013)));

__asm("CCPR1L equ 013h");




extern volatile unsigned char CCPR1H __attribute__((address(0x014)));

__asm("CCPR1H equ 014h");




extern volatile unsigned char CCP1CON __attribute__((address(0x015)));

__asm("CCP1CON equ 015h");


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
extern volatile CCP1CONbits_t CCP1CONbits __attribute__((address(0x015)));
# 646 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char WDTCON __attribute__((address(0x018)));

__asm("WDTCON equ 018h");


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
extern volatile WDTCONbits_t WDTCONbits __attribute__((address(0x018)));
# 699 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char CMCON0 __attribute__((address(0x019)));

__asm("CMCON0 equ 019h");


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
extern volatile CMCON0bits_t CMCON0bits __attribute__((address(0x019)));
# 758 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char CMCON1 __attribute__((address(0x01A)));

__asm("CMCON1 equ 01Ah");


typedef union {
    struct {
        unsigned CMSYNC :1;
        unsigned T1GSS :1;
    };
} CMCON1bits_t;
extern volatile CMCON1bits_t CMCON1bits __attribute__((address(0x01A)));
# 784 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char ADRESH __attribute__((address(0x01E)));

__asm("ADRESH equ 01Eh");




extern volatile unsigned char ADCON0 __attribute__((address(0x01F)));

__asm("ADCON0 equ 01Fh");


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
extern volatile ADCON0bits_t ADCON0bits __attribute__((address(0x01F)));
# 881 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char OPTION_REG __attribute__((address(0x081)));

__asm("OPTION_REG equ 081h");


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
extern volatile OPTION_REGbits_t OPTION_REGbits __attribute__((address(0x081)));
# 951 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char TRISIO __attribute__((address(0x085)));

__asm("TRISIO equ 085h");


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
extern volatile TRISIObits_t TRISIObits __attribute__((address(0x085)));
# 1001 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char PIE1 __attribute__((address(0x08C)));

__asm("PIE1 equ 08Ch");


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
extern volatile PIE1bits_t PIE1bits __attribute__((address(0x08C)));
# 1072 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char PCON __attribute__((address(0x08E)));

__asm("PCON equ 08Eh");


typedef union {
    struct {
        unsigned nBOD :1;
        unsigned nPOR :1;
        unsigned :2;
        unsigned SBODEN :1;
        unsigned ULPWUE :1;
    };
} PCONbits_t;
extern volatile PCONbits_t PCONbits __attribute__((address(0x08E)));
# 1111 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char OSCCON __attribute__((address(0x08F)));

__asm("OSCCON equ 08Fh");


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
extern volatile OSCCONbits_t OSCCONbits __attribute__((address(0x08F)));
# 1176 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char OSCTUNE __attribute__((address(0x090)));

__asm("OSCTUNE equ 090h");


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
extern volatile OSCTUNEbits_t OSCTUNEbits __attribute__((address(0x090)));
# 1228 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char PR2 __attribute__((address(0x092)));

__asm("PR2 equ 092h");




extern volatile unsigned char WPU __attribute__((address(0x095)));

__asm("WPU equ 095h");


extern volatile unsigned char WPUA __attribute__((address(0x095)));

__asm("WPUA equ 095h");


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
extern volatile WPUbits_t WPUbits __attribute__((address(0x095)));
# 1316 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
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
extern volatile WPUAbits_t WPUAbits __attribute__((address(0x095)));
# 1389 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char IOC __attribute__((address(0x096)));

__asm("IOC equ 096h");


extern volatile unsigned char IOCA __attribute__((address(0x096)));

__asm("IOCA equ 096h");


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
extern volatile IOCbits_t IOCbits __attribute__((address(0x096)));
# 1480 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
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
extern volatile IOCAbits_t IOCAbits __attribute__((address(0x096)));
# 1563 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char VRCON __attribute__((address(0x099)));

__asm("VRCON equ 099h");


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
extern volatile VRCONbits_t VRCONbits __attribute__((address(0x099)));
# 1623 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char EEDAT __attribute__((address(0x09A)));

__asm("EEDAT equ 09Ah");


extern volatile unsigned char EEDATA __attribute__((address(0x09A)));

__asm("EEDATA equ 09Ah");


typedef union {
    struct {
        unsigned EEDAT :8;
    };
} EEDATbits_t;
extern volatile EEDATbits_t EEDATbits __attribute__((address(0x09A)));







typedef union {
    struct {
        unsigned EEDAT :8;
    };
} EEDATAbits_t;
extern volatile EEDATAbits_t EEDATAbits __attribute__((address(0x09A)));
# 1661 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char EEADR __attribute__((address(0x09B)));

__asm("EEADR equ 09Bh");




extern volatile unsigned char EECON1 __attribute__((address(0x09C)));

__asm("EECON1 equ 09Ch");


typedef union {
    struct {
        unsigned RD :1;
        unsigned WR :1;
        unsigned WREN :1;
        unsigned WRERR :1;
    };
} EECON1bits_t;
extern volatile EECON1bits_t EECON1bits __attribute__((address(0x09C)));
# 1706 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile unsigned char EECON2 __attribute__((address(0x09D)));

__asm("EECON2 equ 09Dh");




extern volatile unsigned char ADRESL __attribute__((address(0x09E)));

__asm("ADRESL equ 09Eh");




extern volatile unsigned char ANSEL __attribute__((address(0x09F)));

__asm("ANSEL equ 09Fh");


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
extern volatile ANSELbits_t ANSELbits __attribute__((address(0x09F)));
# 1800 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\proc\\pic12f683.h" 3
extern volatile __bit ADCS0 __attribute__((address(0x4FC)));


extern volatile __bit ADCS1 __attribute__((address(0x4FD)));


extern volatile __bit ADCS2 __attribute__((address(0x4FE)));


extern volatile __bit ADFM __attribute__((address(0xFF)));


extern volatile __bit ADIE __attribute__((address(0x466)));


extern volatile __bit ADIF __attribute__((address(0x66)));


extern volatile __bit ADON __attribute__((address(0xF8)));


extern volatile __bit ANS0 __attribute__((address(0x4F8)));


extern volatile __bit ANS1 __attribute__((address(0x4F9)));


extern volatile __bit ANS2 __attribute__((address(0x4FA)));


extern volatile __bit ANS3 __attribute__((address(0x4FB)));


extern volatile __bit CARRY __attribute__((address(0x18)));


extern volatile __bit CCP1IE __attribute__((address(0x465)));


extern volatile __bit CCP1IF __attribute__((address(0x65)));


extern volatile __bit CCP1M0 __attribute__((address(0xA8)));


extern volatile __bit CCP1M1 __attribute__((address(0xA9)));


extern volatile __bit CCP1M2 __attribute__((address(0xAA)));


extern volatile __bit CCP1M3 __attribute__((address(0xAB)));


extern volatile __bit CHS0 __attribute__((address(0xFA)));


extern volatile __bit CHS1 __attribute__((address(0xFB)));


extern volatile __bit CHS2 __attribute__((address(0xFC)));


extern volatile __bit CINV __attribute__((address(0xCC)));


extern volatile __bit CIS __attribute__((address(0xCB)));


extern volatile __bit CM0 __attribute__((address(0xC8)));


extern volatile __bit CM1 __attribute__((address(0xC9)));


extern volatile __bit CM2 __attribute__((address(0xCA)));


extern volatile __bit CMIE __attribute__((address(0x463)));


extern volatile __bit CMIF __attribute__((address(0x63)));


extern volatile __bit CMSYNC __attribute__((address(0xD0)));


extern volatile __bit COUT __attribute__((address(0xCE)));


extern volatile __bit DC __attribute__((address(0x19)));


extern volatile __bit DC1B0 __attribute__((address(0xAC)));


extern volatile __bit DC1B1 __attribute__((address(0xAD)));


extern volatile __bit EEIE __attribute__((address(0x467)));


extern volatile __bit EEIF __attribute__((address(0x67)));


extern volatile __bit GIE __attribute__((address(0x5F)));


extern volatile __bit GO __attribute__((address(0xF9)));


extern volatile __bit GO_DONE __attribute__((address(0xF9)));


extern volatile __bit GO_nDONE __attribute__((address(0xF9)));


extern volatile __bit GP0 __attribute__((address(0x28)));


extern volatile __bit GP1 __attribute__((address(0x29)));


extern volatile __bit GP2 __attribute__((address(0x2A)));


extern volatile __bit GP3 __attribute__((address(0x2B)));


extern volatile __bit GP4 __attribute__((address(0x2C)));


extern volatile __bit GP5 __attribute__((address(0x2D)));


extern volatile __bit GPIE __attribute__((address(0x5B)));


extern volatile __bit GPIF __attribute__((address(0x58)));


extern volatile __bit HTS __attribute__((address(0x47A)));


extern volatile __bit INTE __attribute__((address(0x5C)));


extern volatile __bit INTEDG __attribute__((address(0x40E)));


extern volatile __bit INTF __attribute__((address(0x59)));


extern volatile __bit IOC0 __attribute__((address(0x4B0)));


extern volatile __bit IOC1 __attribute__((address(0x4B1)));


extern volatile __bit IOC2 __attribute__((address(0x4B2)));


extern volatile __bit IOC3 __attribute__((address(0x4B3)));


extern volatile __bit IOC4 __attribute__((address(0x4B4)));


extern volatile __bit IOC5 __attribute__((address(0x4B5)));


extern volatile __bit IOCA0 __attribute__((address(0x4B0)));


extern volatile __bit IOCA1 __attribute__((address(0x4B1)));


extern volatile __bit IOCA2 __attribute__((address(0x4B2)));


extern volatile __bit IOCA3 __attribute__((address(0x4B3)));


extern volatile __bit IOCA4 __attribute__((address(0x4B4)));


extern volatile __bit IOCA5 __attribute__((address(0x4B5)));


extern volatile __bit IRCF0 __attribute__((address(0x47C)));


extern volatile __bit IRCF1 __attribute__((address(0x47D)));


extern volatile __bit IRCF2 __attribute__((address(0x47E)));


extern volatile __bit IRP __attribute__((address(0x1F)));


extern volatile __bit LTS __attribute__((address(0x479)));


extern volatile __bit OSFIE __attribute__((address(0x462)));


extern volatile __bit OSFIF __attribute__((address(0x62)));


extern volatile __bit OSTS __attribute__((address(0x47B)));


extern volatile __bit PEIE __attribute__((address(0x5E)));


extern volatile __bit PS0 __attribute__((address(0x408)));


extern volatile __bit PS1 __attribute__((address(0x409)));


extern volatile __bit PS2 __attribute__((address(0x40A)));


extern volatile __bit PSA __attribute__((address(0x40B)));


extern volatile __bit RD __attribute__((address(0x4E0)));


extern volatile __bit RP0 __attribute__((address(0x1D)));


extern volatile __bit RP1 __attribute__((address(0x1E)));


extern volatile __bit SBODEN __attribute__((address(0x474)));


extern volatile __bit SCS __attribute__((address(0x478)));


extern volatile __bit SWDTEN __attribute__((address(0xC0)));


extern volatile __bit T0CS __attribute__((address(0x40D)));


extern volatile __bit T0IE __attribute__((address(0x5D)));


extern volatile __bit T0IF __attribute__((address(0x5A)));


extern volatile __bit T0SE __attribute__((address(0x40C)));


extern volatile __bit T1CKPS0 __attribute__((address(0x84)));


extern volatile __bit T1CKPS1 __attribute__((address(0x85)));


extern volatile __bit T1GE __attribute__((address(0x86)));


extern volatile __bit T1GINV __attribute__((address(0x87)));


extern volatile __bit T1GSS __attribute__((address(0xD1)));


extern volatile __bit T1IE __attribute__((address(0x460)));


extern volatile __bit T1IF __attribute__((address(0x60)));


extern volatile __bit T1OSCEN __attribute__((address(0x83)));


extern volatile __bit T2CKPS0 __attribute__((address(0x90)));


extern volatile __bit T2CKPS1 __attribute__((address(0x91)));


extern volatile __bit T2IE __attribute__((address(0x461)));


extern volatile __bit T2IF __attribute__((address(0x61)));


extern volatile __bit TMR0IE __attribute__((address(0x5D)));


extern volatile __bit TMR0IF __attribute__((address(0x5A)));


extern volatile __bit TMR1CS __attribute__((address(0x81)));


extern volatile __bit TMR1GE __attribute__((address(0x86)));


extern volatile __bit TMR1IE __attribute__((address(0x460)));


extern volatile __bit TMR1IF __attribute__((address(0x60)));


extern volatile __bit TMR1ON __attribute__((address(0x80)));


extern volatile __bit TMR2IE __attribute__((address(0x461)));


extern volatile __bit TMR2IF __attribute__((address(0x61)));


extern volatile __bit TMR2ON __attribute__((address(0x92)));


extern volatile __bit TOUTPS0 __attribute__((address(0x93)));


extern volatile __bit TOUTPS1 __attribute__((address(0x94)));


extern volatile __bit TOUTPS2 __attribute__((address(0x95)));


extern volatile __bit TOUTPS3 __attribute__((address(0x96)));


extern volatile __bit TRISIO0 __attribute__((address(0x428)));


extern volatile __bit TRISIO1 __attribute__((address(0x429)));


extern volatile __bit TRISIO2 __attribute__((address(0x42A)));


extern volatile __bit TRISIO3 __attribute__((address(0x42B)));


extern volatile __bit TRISIO4 __attribute__((address(0x42C)));


extern volatile __bit TRISIO5 __attribute__((address(0x42D)));


extern volatile __bit TUN0 __attribute__((address(0x480)));


extern volatile __bit TUN1 __attribute__((address(0x481)));


extern volatile __bit TUN2 __attribute__((address(0x482)));


extern volatile __bit TUN3 __attribute__((address(0x483)));


extern volatile __bit TUN4 __attribute__((address(0x484)));


extern volatile __bit ULPWUE __attribute__((address(0x475)));


extern volatile __bit VCFG __attribute__((address(0xFE)));


extern volatile __bit VR0 __attribute__((address(0x4C8)));


extern volatile __bit VR1 __attribute__((address(0x4C9)));


extern volatile __bit VR2 __attribute__((address(0x4CA)));


extern volatile __bit VR3 __attribute__((address(0x4CB)));


extern volatile __bit VREN __attribute__((address(0x4CF)));


extern volatile __bit VRR __attribute__((address(0x4CD)));


extern volatile __bit WDTPS0 __attribute__((address(0xC1)));


extern volatile __bit WDTPS1 __attribute__((address(0xC2)));


extern volatile __bit WDTPS2 __attribute__((address(0xC3)));


extern volatile __bit WDTPS3 __attribute__((address(0xC4)));


extern volatile __bit WPU0 __attribute__((address(0x4A8)));


extern volatile __bit WPU1 __attribute__((address(0x4A9)));


extern volatile __bit WPU2 __attribute__((address(0x4AA)));


extern volatile __bit WPU4 __attribute__((address(0x4AC)));


extern volatile __bit WPU5 __attribute__((address(0x4AD)));


extern volatile __bit WPUA0 __attribute__((address(0x4A8)));


extern volatile __bit WPUA1 __attribute__((address(0x4A9)));


extern volatile __bit WPUA2 __attribute__((address(0x4AA)));


extern volatile __bit WPUA4 __attribute__((address(0x4AC)));


extern volatile __bit WPUA5 __attribute__((address(0x4AD)));


extern volatile __bit WR __attribute__((address(0x4E1)));


extern volatile __bit WREN __attribute__((address(0x4E2)));


extern volatile __bit WRERR __attribute__((address(0x4E3)));


extern volatile __bit ZERO __attribute__((address(0x1A)));


extern volatile __bit nBOD __attribute__((address(0x470)));


extern volatile __bit nDONE __attribute__((address(0xF9)));


extern volatile __bit nGPPU __attribute__((address(0x40F)));


extern volatile __bit nPD __attribute__((address(0x1B)));


extern volatile __bit nPOR __attribute__((address(0x471)));


extern volatile __bit nT1SYNC __attribute__((address(0x82)));


extern volatile __bit nTO __attribute__((address(0x1C)));
# 234 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic_chip_select.h" 2 3
# 14 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic.h" 2 3
# 76 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic.h" 3
__attribute__((__unsupported__("The " "FLASH_READ" " macro function is no longer supported. Please use the MPLAB X MCC."))) unsigned char __flash_read(unsigned short addr);

__attribute__((__unsupported__("The " "FLASH_WRITE" " macro function is no longer supported. Please use the MPLAB X MCC."))) void __flash_write(unsigned short addr, unsigned short data);

__attribute__((__unsupported__("The " "FLASH_ERASE" " macro function is no longer supported. Please use the MPLAB X MCC."))) void __flash_erase(unsigned short addr);



# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\eeprom_routines.h" 1 3
# 114 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\eeprom_routines.h" 3
extern void eeprom_write(unsigned char addr, unsigned char value);
extern unsigned char eeprom_read(unsigned char addr);
# 84 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic.h" 2 3
# 118 "C:/Users/RealTech/.mchp_packs/Microchip/PIC10-12Fxxx_DFP/1.7.178/xc8\\pic\\include\\pic.h" 3
extern __bank0 unsigned char __resetbits;
extern __bank0 __bit __powerdown;
extern __bank0 __bit __timeout;
# 28 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\xc.h" 2 3
# 12 "EAS-Itank_RTC_Interface.c" 2

# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdint.h" 1 3
# 13 "EAS-Itank_RTC_Interface.c" 2

# 1 "C:\\Program Files\\Microchip\\xc8\\v2.31\\pic\\include\\c90\\stdbool.h" 1 3
# 14 "EAS-Itank_RTC_Interface.c" 2
# 129 "EAS-Itank_RTC_Interface.c"
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




static void Delay_Approx_65ms(void);
static void Delay_Approx_250ms(void);
static void Delay_Approx_1s(void);
static void Delay_Seconds(uint8_t seconds);

static void Relay_On(void);
static void Relay_Off(void);
static void Relay_Reset_Pulse(void);
static void Relay_Test_Cycle(void);
static _Bool Test_Delay_Seconds_Abortable(uint8_t seconds);

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
static _Bool I2C_WriteByte(uint8_t data);
static uint8_t I2C_ReadByte(_Bool send_ack);

static uint8_t BCD_To_Dec(uint8_t bcd);





static uint8_t EEPROM_ReadByte(uint8_t address);
static void EEPROM_WriteByte(uint8_t address, uint8_t value);
static uint8_t Trigger_Checksum(uint8_t hour,
                                uint8_t minute,
                                uint8_t date,
                                uint8_t month,
                                uint8_t year);
static _Bool EEPROM_Load_Last_Trigger(uint8_t *last_hour,
                                      uint8_t *last_minute,
                                      uint8_t *last_date,
                                      uint8_t *last_month,
                                      uint8_t *last_year);
static void EEPROM_Save_Last_Trigger(uint8_t hour,
                                      uint8_t minute,
                                      uint8_t date,
                                      uint8_t month,
                                      uint8_t year);

static _Bool DS3231_ReadTime(RTC_Time *time);
static _Bool RTC_Time_Is_Valid(const RTC_Time *time);
static _Bool DS3231_ReadTime_With_Retry(RTC_Time *time);





static _Bool Is_Sunday_Enabled(void);
static _Bool Is_Hour_Window_Enabled(uint8_t hour);
static _Bool Is_Test_Mode_Enabled(void);
static _Bool Is_Allowed_Day_And_Hour(const RTC_Time *now);
static _Bool Is_Scheduled_Relay_Time(const RTC_Time *now);
static _Bool Already_Triggered_This_Minute(const RTC_Time *now,
                                          uint8_t last_hour,
                                          uint8_t last_minute,
                                          uint8_t last_date,
                                          uint8_t last_month,
                                          uint8_t last_year);

static void Init_Device(void);




static void Relay_On(void)
{
    GPIObits.GP0 = 1;
}

static void Relay_Off(void)
{
    GPIObits.GP0 = 0;
}

static void Relay_Reset_Pulse(void)
{
    Relay_On();
    Delay_Seconds(10);
    Relay_Off();
}







static _Bool Test_Delay_Seconds_Abortable(uint8_t seconds)
{
    uint8_t sec;
    uint8_t slice;

    for (sec = 0; sec < seconds; sec++)
    {


        for (slice = 0; slice < 16; slice++)
        {
            if (!Is_Test_Mode_Enabled())
            {
                Relay_Off();
                return 0;
            }

            Delay_Approx_65ms();
        }
    }

    return 1;
}

static void Relay_Test_Cycle(void)
{
    _Bool dip1_on;
    _Bool dip2_on;
    uint8_t off_seconds;
    uint8_t on_seconds;


    if (!Is_Test_Mode_Enabled())
    {
        Relay_Off();
        return;
    }

    dip1_on = (GPIObits.GP4 == 1);
    dip2_on = (GPIObits.GP5 == 1);


    off_seconds = 0;
    on_seconds = 0;

    if ((!dip1_on) && (!dip2_on))
    {
        off_seconds = 30;
        on_seconds = 10;
    }
    else if ((!dip1_on) && dip2_on)
    {
        off_seconds = 10;
        on_seconds = 5;
    }
    else if (dip1_on && (!dip2_on))
    {
        off_seconds = 60;
        on_seconds = 15;
    }
    else
    {

        Relay_Off();
        Delay_Approx_250ms();
        return;
    }


    Relay_Off();
    if (!Test_Delay_Seconds_Abortable(off_seconds))
    {
        return;
    }


    Relay_On();
    if (!Test_Delay_Seconds_Abortable(on_seconds))
    {
        Relay_Off();
        return;
    }

    Relay_Off();
}




static void Timer0_Init_For_Delay(void)
{
    OPTION_REGbits.T0CS = 0;
    OPTION_REGbits.PSA = 0;

    OPTION_REGbits.PS2 = 1;
    OPTION_REGbits.PS1 = 1;
    OPTION_REGbits.PS0 = 1;

    INTCONbits.T0IE = 0;
    INTCONbits.T0IF = 0;
}

static void Delay_Approx_65ms(void)
{
    TMR0 = 0;
    INTCONbits.T0IF = 0;

    while (INTCONbits.T0IF == 0)
    {

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




static void Safe_Fault_Idle_Forever(void)
{
    while (1)
    {
        Relay_Off();
        I2C_Init();
        Delay_Seconds(1);
    }
}




static void I2C_Delay(void)
{
    volatile uint8_t i;

    for (i = 0; i < 50; i++)
    {

    }
}




static void SDA_Low(void)
{
    GPIObits.GP1 = 0;
    TRISIObits.TRISIO1 = 0;
}

static void SDA_Release(void)
{
    TRISIObits.TRISIO1 = 1;
}

static void SCL_Low(void)
{
    GPIObits.GP2 = 0;
    TRISIObits.TRISIO2 = 0;
}

static void SCL_Release(void)
{
    TRISIObits.TRISIO2 = 1;
}

static uint8_t SDA_Read(void)
{
    return GPIObits.GP1;
}







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

static _Bool I2C_WriteByte(uint8_t data)
{
    uint8_t i;
    _Bool ack;

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

static uint8_t I2C_ReadByte(_Bool send_ack)
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




static uint8_t BCD_To_Dec(uint8_t bcd)
{
    uint8_t tens;
    uint8_t ones;

    tens = (uint8_t)((bcd >> 4) & 0x0F);
    ones = (uint8_t)(bcd & 0x0F);

    return (uint8_t)((tens * 10) + ones);
}
# 595 "EAS-Itank_RTC_Interface.c"
static uint8_t EEPROM_ReadByte(uint8_t address)
{
    while (EECON1bits.WR != 0)
    {

    }

    EEADR = address;
    EECON1bits.RD = 1;

    return EEDAT;
}

static void EEPROM_WriteByte(uint8_t address, uint8_t value)
{
    _Bool gie_was_enabled;

    if (EEPROM_ReadByte(address) == value)
    {
        return;
    }

    while (EECON1bits.WR != 0)
    {

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

static _Bool EEPROM_Load_Last_Trigger(uint8_t *last_hour,
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

    if (EEPROM_ReadByte(0) != 0xA5)
    {
        return 0;
    }

    hour = EEPROM_ReadByte(1);
    minute = EEPROM_ReadByte(2);
    date = EEPROM_ReadByte(3);
    month = EEPROM_ReadByte(4);
    year = EEPROM_ReadByte(5);
    checksum = EEPROM_ReadByte(6);

    if (checksum != Trigger_Checksum(hour, minute, date, month, year))
    {
        return 0;
    }

    if (hour > 23)
    {
        return 0;
    }

    if (minute > 59)
    {
        return 0;
    }

    if ((date < 1) || (date > 31))
    {
        return 0;
    }

    if ((month < 1) || (month > 12))
    {
        return 0;
    }

    if (year > 99)
    {
        return 0;
    }

    *last_hour = hour;
    *last_minute = minute;
    *last_date = date;
    *last_month = month;
    *last_year = year;

    return 1;
}

static void EEPROM_Save_Last_Trigger(uint8_t hour,
                                      uint8_t minute,
                                      uint8_t date,
                                      uint8_t month,
                                      uint8_t year)
{
    uint8_t checksum;

    checksum = Trigger_Checksum(hour, minute, date, month, year);



    EEPROM_WriteByte(0, 0x00);

    EEPROM_WriteByte(1, hour);
    EEPROM_WriteByte(2, minute);
    EEPROM_WriteByte(3, date);
    EEPROM_WriteByte(4, month);
    EEPROM_WriteByte(5, year);
    EEPROM_WriteByte(6, checksum);


    EEPROM_WriteByte(0, 0xA5);
}




static _Bool DS3231_ReadTime(RTC_Time *time)
{
    uint8_t raw_second;
    uint8_t raw_minute;
    uint8_t raw_hour;
    uint8_t raw_day;
    uint8_t raw_date;
    uint8_t raw_month;
    uint8_t raw_year;

    I2C_Start();

    if (!I2C_WriteByte(((0x68 << 1) | 0)))
    {
        I2C_Stop();
        return 0;
    }

    if (!I2C_WriteByte(0x00))
    {
        I2C_Stop();
        return 0;
    }

    I2C_Start();

    if (!I2C_WriteByte(((0x68 << 1) | 1)))
    {
        I2C_Stop();
        return 0;
    }

    raw_second = I2C_ReadByte(1);
    raw_minute = I2C_ReadByte(1);
    raw_hour = I2C_ReadByte(1);
    raw_day = I2C_ReadByte(1);
    raw_date = I2C_ReadByte(1);
    raw_month = I2C_ReadByte(1);
    raw_year = I2C_ReadByte(0);

    I2C_Stop();

    time->second = BCD_To_Dec((uint8_t)(raw_second & 0x7F));
    time->minute = BCD_To_Dec((uint8_t)(raw_minute & 0x7F));

    if ((raw_hour & 0x40) != 0)
    {
        uint8_t hour_12;
        _Bool is_pm;

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
    time->date = BCD_To_Dec((uint8_t)(raw_date & 0x3F));
    time->month = BCD_To_Dec((uint8_t)(raw_month & 0x1F));
    time->year = BCD_To_Dec(raw_year);

    return 1;
}

static _Bool RTC_Time_Is_Valid(const RTC_Time *time)
{
    if (time->second > 59)
    {
        return 0;
    }

    if (time->minute > 59)
    {
        return 0;
    }

    if (time->hour > 23)
    {
        return 0;
    }

    if ((time->day_of_week < 1) || (time->day_of_week > 7))
    {
        return 0;
    }

    if ((time->date < 1) || (time->date > 31))
    {
        return 0;
    }

    if ((time->month < 1) || (time->month > 12))
    {
        return 0;
    }

    if (time->year > 99)
    {
        return 0;
    }

    return 1;
}

static _Bool DS3231_ReadTime_With_Retry(RTC_Time *time)
{
    uint8_t attempt;

    for (attempt = 0; attempt < 3; attempt++)
    {
        I2C_Init();

        if (DS3231_ReadTime(time))
        {
            if (RTC_Time_Is_Valid(time))
            {
                return 1;
            }
        }

        Relay_Off();
        I2C_Init();
        Delay_Approx_250ms();
    }

    return 0;
}
# 966 "EAS-Itank_RTC_Interface.c"
static _Bool Is_Sunday_Enabled(void)
{
    return (GPIObits.GP4 == 1);
}

static _Bool Is_Hour_Window_Enabled(uint8_t hour)
{
    _Bool dip2_enabled;

    dip2_enabled = (GPIObits.GP5 == 1);


    if (!dip2_enabled)
    {
        return 1;
    }


    if ((hour >= 8) && (hour <= 20))
    {
        return 1;
    }

    return 0;
}

static _Bool Is_Test_Mode_Enabled(void)
{
    return (GPIObits.GP3 == 0);
}

static _Bool Is_Allowed_Day_And_Hour(const RTC_Time *now)
{

    if ((now->day_of_week == 1) && !Is_Sunday_Enabled())
    {
        return 0;
    }


    if (!Is_Hour_Window_Enabled(now->hour))
    {
        return 0;
    }

    return 1;
}

static _Bool Is_Scheduled_Relay_Time(const RTC_Time *now)
{
    if (!Is_Allowed_Day_And_Hour(now))
    {
        return 0;
    }


    return (now->minute == 0);
}

static _Bool Already_Triggered_This_Minute(const RTC_Time *now,
                                          uint8_t last_hour,
                                          uint8_t last_minute,
                                          uint8_t last_date,
                                          uint8_t last_month,
                                          uint8_t last_year)
{
    if (now->hour != last_hour)
    {
        return 0;
    }

    if (now->minute != last_minute)
    {
        return 0;
    }

    if (now->date != last_date)
    {
        return 0;
    }

    if (now->month != last_month)
    {
        return 0;
    }

    if (now->year != last_year)
    {
        return 0;
    }

    return 1;
}




static void Init_Device(void)
{
    ANSEL = 0x00;
    ADCON0 = 0x00;
    CMCON0 = 0x07;

    OSCCONbits.IRCF = 0b110;
    OSCCONbits.SCS = 1;

    GPIO = 0x00;


    TRISIObits.TRISIO0 = 0;
    Relay_Off();


    SDA_Release();


    SCL_Release();


    TRISIObits.TRISIO3 = 1;


    TRISIObits.TRISIO4 = 1;


    TRISIObits.TRISIO5 = 1;

    Timer0_Init_For_Delay();
}




void main(void)
{
    RTC_Time now;

    uint8_t last_trigger_hour = 0xFF;
    uint8_t last_trigger_minute = 0xFF;
    uint8_t last_trigger_date = 0xFF;
    uint8_t last_trigger_month = 0xFF;
    uint8_t last_trigger_year = 0xFF;

    Init_Device();
    I2C_Init();


    (void)EEPROM_Load_Last_Trigger(&last_trigger_hour,
                                   &last_trigger_minute,
                                   &last_trigger_date,
                                   &last_trigger_month,
                                   &last_trigger_year);
# 1141 "EAS-Itank_RTC_Interface.c"
    Delay_Seconds(1);

    while (!DS3231_ReadTime_With_Retry(&now))
    {
        Relay_Off();
        I2C_Init();
        Delay_Seconds(1);
    }

    while (1)
    {



        if (Is_Test_Mode_Enabled())
        {
            Relay_Test_Cycle();
            continue;
        }


        if (DS3231_ReadTime_With_Retry(&now))
        {
            if (Is_Scheduled_Relay_Time(&now))
            {
                _Bool already_triggered;

                already_triggered = Already_Triggered_This_Minute(&now,
                                                                   last_trigger_hour,
                                                                   last_trigger_minute,
                                                                   last_trigger_date,
                                                                   last_trigger_month,
                                                                   last_trigger_year);

                if (!already_triggered)
                {


                    EEPROM_Save_Last_Trigger(now.hour,
                                             now.minute,
                                             now.date,
                                             now.month,
                                             now.year);

                    last_trigger_hour = now.hour;
                    last_trigger_minute = now.minute;
                    last_trigger_date = now.date;
                    last_trigger_month = now.month;
                    last_trigger_year = now.year;

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
