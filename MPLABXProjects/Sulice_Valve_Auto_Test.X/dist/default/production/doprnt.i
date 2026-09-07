# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
# 1 "<built-in>" 1
# 1 "<built-in>" 3
# 285 "<built-in>" 3
# 1 "<command line>" 1
# 1 "<built-in>" 2
# 1 "C:/Program Files/Microchip/MPLABX/v5.50/packs/Microchip/PIC18F-K_DFP/1.4.87/xc8\\pic\\include/language_support.h" 1 3
# 2 "<built-in>" 2
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2


# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/ctype.h" 1 3







# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/features.h" 1 3
# 9 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/ctype.h" 2 3

int isalnum(int);
int isalpha(int);
int isblank(int);
int iscntrl(int);
int isdigit(int);
int isgraph(int);
int islower(int);
int isprint(int);
int ispunct(int);
int isspace(int);
int isupper(int);
int isxdigit(int);
int tolower(int);
int toupper(int);
# 4 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/math.h" 1 3



# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/musl_xc8.h" 1 3
# 5 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/math.h" 2 3





# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdint.h" 1 3
# 26 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdint.h" 3
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 1 3
# 133 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef unsigned __int24 uintptr_t;
# 148 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef __int24 intptr_t;
# 164 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef signed char int8_t;




typedef short int16_t;




typedef __int24 int24_t;




typedef long int32_t;





typedef long long int64_t;
# 194 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef long long intmax_t;





typedef unsigned char uint8_t;




typedef unsigned short uint16_t;




typedef __uint24 uint24_t;




typedef unsigned long uint32_t;





typedef unsigned long long uint64_t;
# 235 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef unsigned long long uintmax_t;
# 27 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdint.h" 2 3

typedef int8_t int_fast8_t;

typedef int64_t int_fast64_t;


typedef int8_t int_least8_t;
typedef int16_t int_least16_t;

typedef int24_t int_least24_t;
typedef int24_t int_fast24_t;

typedef int32_t int_least32_t;

typedef int64_t int_least64_t;


typedef uint8_t uint_fast8_t;

typedef uint64_t uint_fast64_t;


typedef uint8_t uint_least8_t;
typedef uint16_t uint_least16_t;

typedef uint24_t uint_least24_t;
typedef uint24_t uint_fast24_t;

typedef uint32_t uint_least32_t;

typedef uint64_t uint_least64_t;
# 148 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdint.h" 3
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/stdint.h" 1 3
typedef int16_t int_fast16_t;
typedef int32_t int_fast32_t;
typedef uint16_t uint_fast16_t;
typedef uint32_t uint_fast32_t;
# 149 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdint.h" 2 3
# 11 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/math.h" 2 3




# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 1 3
# 39 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef float float_t;




typedef double double_t;
# 16 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/math.h" 2 3
# 42 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/math.h" 3
int __fpclassifyf(float);







int __signbitf(float);
# 59 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/math.h" 3
double acos(double);
float acosf(float);
long double acosl(long double);



double acosh(double);
float acoshf(float);
long double acoshl(long double);



double asin(double);
float asinf(float);
long double asinl(long double);



double asinh(double);
float asinhf(float);
long double asinhl(long double);



double atan(double);
float atanf(float);
long double atanl(long double);



double atan2(double, double);
float atan2f(float, float);
long double atan2l(long double, long double);



double atanh(double);
float atanhf(float);
long double atanhl(long double);



double cbrt(double);
float cbrtf(float);
long double cbrtl(long double);



double ceil(double);
float ceilf(float);
long double ceill(long double);



double copysign(double, double);
float copysignf(float, float);
long double copysignl(long double, long double);



double cos(double);
float cosf(float);
long double cosl(long double);



double cosh(double);
float coshf(float);
long double coshl(long double);



double erf(double);
float erff(float);
long double erfl(long double);



double erfc(double);
float erfcf(float);
long double erfcl(long double);



double exp(double);
float expf(float);
long double expl(long double);



double exp2(double);
float exp2f(float);
long double exp2l(long double);



double expm1(double);
float expm1f(float);
long double expm1l(long double);



double fabs(double);
float fabsf(float);
long double fabsl(long double);



double fdim(double, double);
float fdimf(float, float);
long double fdiml(long double, long double);



double floor(double);
float floorf(float);
long double floorl(long double);



double fma(double, double, double);
float fmaf(float, float, float);
long double fmal(long double, long double, long double);



double fmax(double, double);
float fmaxf(float, float);
long double fmaxl(long double, long double);



double fmin(double, double);
float fminf(float, float);
long double fminl(long double, long double);



double fmod(double, double);
float fmodf(float, float);
long double fmodl(long double, long double);



double frexp(double, int *);
float frexpf(float, int *);
long double frexpl(long double, int *);



double hypot(double, double);
float hypotf(float, float);
long double hypotl(long double, long double);



int ilogb(double);
int ilogbf(float);
int ilogbl(long double);



double ldexp(double, int);
float ldexpf(float, int);
long double ldexpl(long double, int);




double lgamma(double);
float lgammaf(float);
long double lgammal(long double);




long long llrint(double);
long long llrintf(float);
long long llrintl(long double);



long long llround(double);
long long llroundf(float);
long long llroundl(long double);




double log(double);
float logf(float);
long double logl(long double);



double log10(double);
float log10f(float);
long double log10l(long double);



double log1p(double);
float log1pf(float);
long double log1pl(long double);



double log2(double);
float log2f(float);
long double log2l(long double);



double logb(double);
float logbf(float);
long double logbl(long double);



long lrint(double);
long lrintf(float);
long lrintl(long double);



long lround(double);
long lroundf(float);
long lroundl(long double);



double modf(double, double *);
float modff(float, float *);
long double modfl(long double, long double *);



double nan(const char *);
float nanf(const char *);
long double nanl(const char *);



double nearbyint(double);
float nearbyintf(float);
long double nearbyintl(long double);



double nextafter(double, double);
float nextafterf(float, float);
long double nextafterl(long double, long double);



double nexttoward(double, long double);
float nexttowardf(float, long double);
long double nexttowardl(long double, long double);
# 326 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/math.h" 3
double pow(double, double);
__attribute__((nonreentrant)) float powf(float, float);
long double powl(long double, long double);



double remainder(double, double);
float remainderf(float, float);
long double remainderl(long double, long double);



double remquo(double, double, int *);
float remquof(float, float, int *);
long double remquol(long double, long double, int *);



double rint(double);
float rintf(float);
long double rintl(long double);



double round(double);
float roundf(float);
long double roundl(long double);



double scalbln(double, long);
float scalblnf(float, long);
long double scalblnl(long double, long);



double scalbn(double, int);
float scalbnf(float, int);
long double scalbnl(long double, int);



double sin(double);
float sinf(float);
long double sinl(long double);



double sinh(double);
float sinhf(float);
long double sinhl(long double);



double sqrt(double);
float sqrtf(float);
long double sqrtl(long double);



double tan(double);
float tanf(float);
long double tanl(long double);



double tanh(double);
float tanhf(float);
long double tanhl(long double);



double tgamma(double);
float tgammaf(float);
long double tgammal(long double);



double trunc(double);
float truncf(float);
long double truncl(long double);
# 431 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/math.h" 3
extern int signgam;

double j0(double);
double j1(double);
double jn(int, double);

double y0(double);
double y1(double);
double yn(int, double);
# 5 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdarg.h" 1 3







# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 1 3
# 12 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef void * va_list[1];
# 9 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdarg.h" 2 3

#pragma intrinsic(__va_start)
#pragma intrinsic(__va_arg)

extern void * __va_start(void);
extern void * __va_arg(void *, ...);
# 6 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stddef.h" 1 3
# 19 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stddef.h" 3
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 1 3
# 24 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef long int wchar_t;
# 128 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef unsigned size_t;
# 138 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef int ptrdiff_t;
# 20 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stddef.h" 2 3
# 7 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2

# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdio.h" 1 3
# 24 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdio.h" 3
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 1 3
# 17 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef void * __isoc_va_list[1];
# 143 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef __int24 ssize_t;
# 255 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef long long off_t;
# 409 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef struct _IO_FILE FILE;
# 25 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdio.h" 2 3
# 52 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdio.h" 3
typedef union _G_fpos64_t {
 char __opaque[16];
 double __align;
} fpos_t;

extern FILE *const stdin;
extern FILE *const stdout;
extern FILE *const stderr;





FILE *fopen(const char *restrict, const char *restrict);
FILE *freopen(const char *restrict, const char *restrict, FILE *restrict);
int fclose(FILE *);

int remove(const char *);
int rename(const char *, const char *);

int feof(FILE *);
int ferror(FILE *);
int fflush(FILE *);
void clearerr(FILE *);

int fseek(FILE *, long, int);
long ftell(FILE *);
void rewind(FILE *);

int fgetpos(FILE *restrict, fpos_t *restrict);
int fsetpos(FILE *, const fpos_t *);

size_t fread(void *restrict, size_t, size_t, FILE *restrict);
size_t fwrite(const void *restrict, size_t, size_t, FILE *restrict);

int fgetc(FILE *);
int getc(FILE *);
int getchar(void);





int ungetc(int, FILE *);
int getch(void);

int fputc(int, FILE *);
int putc(int, FILE *);
int putchar(int);





void putch(char);

char *fgets(char *restrict, int, FILE *restrict);

char *gets(char *);


int fputs(const char *restrict, FILE *restrict);
int puts(const char *);

__attribute__((__format__(__printf__, 1, 2)))
int printf(const char *restrict, ...);
__attribute__((__format__(__printf__, 2, 3)))
int fprintf(FILE *restrict, const char *restrict, ...);
__attribute__((__format__(__printf__, 2, 3)))
int sprintf(char *restrict, const char *restrict, ...);
__attribute__((__format__(__printf__, 3, 4)))
int snprintf(char *restrict, size_t, const char *restrict, ...);

__attribute__((__format__(__printf__, 1, 0)))
int vprintf(const char *restrict, __isoc_va_list);
int vfprintf(FILE *restrict, const char *restrict, __isoc_va_list);
__attribute__((__format__(__printf__, 2, 0)))
int vsprintf(char *restrict, const char *restrict, __isoc_va_list);
__attribute__((__format__(__printf__, 3, 0)))
int vsnprintf(char *restrict, size_t, const char *restrict, __isoc_va_list);

__attribute__((__format__(__scanf__, 1, 2)))
int scanf(const char *restrict, ...);
__attribute__((__format__(__scanf__, 2, 3)))
int fscanf(FILE *restrict, const char *restrict, ...);
__attribute__((__format__(__scanf__, 2, 3)))
int sscanf(const char *restrict, const char *restrict, ...);

__attribute__((__format__(__scanf__, 1, 0)))
int vscanf(const char *restrict, __isoc_va_list);
int vfscanf(FILE *restrict, const char *restrict, __isoc_va_list);
__attribute__((__format__(__scanf__, 2, 0)))
int vsscanf(const char *restrict, const char *restrict, __isoc_va_list);

void perror(const char *);

int setvbuf(FILE *restrict, char *restrict, int, size_t);
void setbuf(FILE *restrict, char *restrict);

char *tmpnam(char *);
FILE *tmpfile(void);




FILE *fmemopen(void *restrict, size_t, const char *restrict);
FILE *open_memstream(char **, size_t *);
FILE *fdopen(int, const char *);
FILE *popen(const char *, const char *);
int pclose(FILE *);
int fileno(FILE *);
int fseeko(FILE *, off_t, int);
off_t ftello(FILE *);
int dprintf(int, const char *restrict, ...);
int vdprintf(int, const char *restrict, __isoc_va_list);
void flockfile(FILE *);
int ftrylockfile(FILE *);
void funlockfile(FILE *);
int getc_unlocked(FILE *);
int getchar_unlocked(void);
int putc_unlocked(int, FILE *);
int putchar_unlocked(int);
ssize_t getdelim(char **restrict, size_t *restrict, int, FILE *restrict);
ssize_t getline(char **restrict, size_t *restrict, FILE *restrict);
int renameat(int, const char *, int, const char *);
char *ctermid(char *);







char *tempnam(const char *, const char *);
# 9 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdlib.h" 1 3
# 21 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdlib.h" 3
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 1 3
# 22 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdlib.h" 2 3

int atoi (const char *);
long atol (const char *);

long long atoll (const char *);

double atof (const char *);


float strtof (const char *restrict, char **restrict);
double strtod (const char *restrict, char **restrict);
long double strtold (const char *restrict, char **restrict);



long strtol (const char *restrict, char **restrict, int);
unsigned long strtoul (const char *restrict, char **restrict, int);

long long strtoll (const char *restrict, char **restrict, int);
unsigned long long strtoull (const char *restrict, char **restrict, int);


unsigned long __strtoxl(const char * s, char ** endptr, int base, char is_signed);

unsigned long long __strtoxll(const char * s, char ** endptr, int base, char is_signed);
# 55 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdlib.h" 3
int rand (void);
void srand (unsigned);

void *malloc (size_t);
void *calloc (size_t, size_t);
void *realloc (void *, size_t);
void free (void *);

          void abort (void);
int atexit (void (*) (void));
          void exit (int);
          void _Exit (int);

void *bsearch (const void *, const void *, size_t, size_t, int (*)(const void *, const void *));







__attribute__((nonreentrant)) void qsort (void *, size_t, size_t, int (*)(const void *, const void *));

int abs (int);
long labs (long);

long long llabs (long long);


typedef struct { int quot, rem; } div_t;
typedef struct { long quot, rem; } ldiv_t;

typedef struct { long long quot, rem; } lldiv_t;


div_t div (int, int);
ldiv_t ldiv (long, long);

lldiv_t lldiv (long long, long long);


typedef struct { unsigned int quot, rem; } udiv_t;
typedef struct { unsigned long quot, rem; } uldiv_t;
udiv_t udiv (unsigned int, unsigned int);
uldiv_t uldiv (unsigned long, unsigned long);
# 10 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/string.h" 1 3
# 25 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/string.h" 3
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 1 3
# 421 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/bits/alltypes.h" 3
typedef struct __locale_struct * locale_t;
# 26 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/string.h" 2 3

void *memcpy (void *restrict, const void *restrict, size_t);
void *memmove (void *, const void *, size_t);
void *memset (void *, int, size_t);
int memcmp (const void *, const void *, size_t);
void *memchr (const void *, int, size_t);

char *strcpy (char *restrict, const char *restrict);
char *strncpy (char *restrict, const char *restrict, size_t);

char *strcat (char *restrict, const char *restrict);
char *strncat (char *restrict, const char *restrict, size_t);

int strcmp (const char *, const char *);
int strncmp (const char *, const char *, size_t);

int strcoll (const char *, const char *);
size_t strxfrm (char *restrict, const char *restrict, size_t);

char *strchr (const char *, int);
char *strrchr (const char *, int);

size_t strcspn (const char *, const char *);
size_t strspn (const char *, const char *);
char *strpbrk (const char *, const char *);
char *strstr (const char *, const char *);
char *strtok (char *restrict, const char *restrict);

size_t strlen (const char *);

char *strerror (int);




char *strtok_r (char *restrict, const char *restrict, char **restrict);
int strerror_r (int, char *, size_t);
char *stpcpy(char *restrict, const char *restrict);
char *stpncpy(char *restrict, const char *restrict, size_t);
size_t strnlen (const char *, size_t);
char *strdup (const char *);
char *strndup (const char *, size_t);
char *strsignal(int);
char *strerror_l (int, locale_t);
int strcoll_l (const char *, const char *, locale_t);
size_t strxfrm_l (char *restrict, const char *restrict, size_t, locale_t);




void *memccpy (void *restrict, const void *restrict, int, size_t);
# 11 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2
# 1 "C:/Program Files/Microchip/MPLABX/v5.50/packs/Microchip/PIC18F-K_DFP/1.4.87/xc8\\pic\\include/inline.h" 1 3
# 12 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2
# 1 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\include\\c99/stdbool.h" 1 3
# 13 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c" 2
# 97 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
typedef signed long long vfpf_sint_t;
typedef unsigned long long vfpf_uint_t;
# 153 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
static int prec, width;
static char flags;

union flui {
 long double f;



 int32_t u;

};
# 185 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
static char dbuf[80];



static int nout;



static int pad(FILE *fp, char *buf, int p)
{
    int i;



    if (flags & (1 << 0)) {
        fputs((const char *)buf, fp);
    }



 if (p < 0) {
  p = 0;
 }
    i = 0;
    while (i < p) {
        fputc(' ', fp);
        ++i;
    }



    if (!(flags & (1 << 0))) {

        fputs((const char *)buf, fp);

    }



    return (int)(strlen(buf) + (size_t)p);

}




static int atoa(FILE *fp, long double f, char c)





{
    char pp, sign, esign;
    int d, e, i, m, n, ne, p, t, w;
    long double g, h, l, ou, u;



 c = (((unsigned char)((c)-'A')) < 26) ? 1 : 0;


    sign = 0;
    g = f;
    if (g < 0.0) {
        sign = 1;
        g = -g;
    }


    n = 0;
    w = width;
    if (sign

   || (flags & (1 << 2))

   ) {
  sign = sign ? '-' : '+';
    }

 else if (flags & (1 << 3)) {
  sign = ' ';
 }


    if (( __fpclassifyf(g) == 1 )) {
  if (sign) {
   dbuf[0] = sign;
   n = 1;
   w--;
  }

        if (c) {
            strcpy(&dbuf[n], "INF");
        }
  else

  {
            strcpy(&dbuf[n], "inf");
        }
        w -= ((sizeof("inf")/sizeof("inf"[0]))-1);
        return (int) pad(fp, &dbuf[0], w);
    }
    if (( __fpclassifyf(g) == 0 )) {
  if (sign) {
   dbuf[0] = sign;
   n = 1;
   w--;
  }

        if (c) {
            strcpy(&dbuf[n], "NAN");
        }
  else

  {
            strcpy(&dbuf[n], "nan");
        }
        w -= ((sizeof("nan")/sizeof("nan"[0]))-1);
        return (int) pad(fp, &dbuf[0], w);
    }


    u = 1.0;
    e = 0;
    if (!(g == 0.0)) {
        while (!(g < (u*2.0))) {
            u = u*2.0;
            ++e;
        }
        while (g < u) {
            u = u/2.0;
            --e;
        }
    }


    p = (prec < 0) ? (80 -5 -1) : prec;


    m = p + 1;


    i = 0;
    h = g;
    ou = u;
    while (i < m) {
        l = floorf(h/u);
        d = (int)l;
        h -= l*u;
        u = u/16.0;
        ++i;
    }


    l = u*8.0;
    if (h < l) {
        l = 0.0;
    } else {

        if ((h == l) && !(d % 2)) {
            l = 0.0;
        }
    }


    h = g + l;


    u = ou;
    ne = 0;
    pp = 0;
    t = 0;
    i = 0;
    while ((i < m) && (n < (80 - 5))) {
        l = floorf(h/u);
        d = (int)l;
        if (!d && (ne < 0)

    && !(flags & (1 << 4))

    ) {
            ++t;
        } else {
            if (!pp && (ne < 0)) {
                dbuf[n++] = '.';
                --w;
                pp = 1;
            }
            while (t) {
                dbuf[n++] = '0';
                --w;
                --t;
            }
            d = (d < 10) ? (int)'0' + d : (int)'a' + (d - 10);

            if (c && ((((unsigned char)(((d)|32)-'a'))) < 26)) {
                d = ((((unsigned char)((d)-'a')) < 26) ? (d)&0x5f : (d));
            }

            dbuf[n++] = (char)d;
            --w;
        }
        h -= l*u;
        u = u/16.0;
        --ne;
        ++i;
  if (prec < 0 && h == 0) {
   break;
  }
    }



 if (!pp && t != 0 && prec > 0 && (n < (80 - 5))) {
  pp = 1;
  dbuf[n++] = '.';
  --w;
 }

 while ((t != 0) && (n < (80 - 5))) {
  dbuf[n++] = '0';
  --w;
  --t;
 }

    if (!pp && (flags & (1 << 4))) {
        dbuf[n++] = '.';
    }

    dbuf[n] = '\0';


    i = sizeof(dbuf) - 1;
    dbuf[i] = '\0';
    esign = 0;
    if (e < 0) {
        esign = 1;
        e = -e;
    }
    p = 1;
    while (e || (0 < p)) {
        --i;
        dbuf[i] = '0' + (e % 10);
        e = e / 10;
        --p;
        --w;
    }
    --i;
    dbuf[i] = esign ? '-' : '+';
    --w;
    --i;

    dbuf[i] = c ? 'P' : 'p';



    --w;


    memcpy(&dbuf[i-n], &dbuf[0], (size_t)n);
 n = i-n;
 i = sign ? 3 : 2 ;


 if (flags & (1 << 1)) {
  while (w > i && n > i) {
   dbuf[--n] = '0';
   w--;
  }
 }



    dbuf[--n] = c ? 'X' : 'x';



    dbuf[--n] = '0';

 if (sign) {
  dbuf[--n] = sign;
 }
 w -= i;


    return (int) pad(fp, &dbuf[n], w);
}



static int ctoa(FILE *fp, char c)
{
    int l,w;


    w = width ? width - 1 : width;



    if (flags & (1 << 0)) {
        fputc(c, fp);
    }



    w = (w < 0) ? 0 : w;
    l = 0;
    while (l < w) {
        fputc(' ', fp);
        ++l;
    }


    if (!(flags & (1 << 0))) {

        fputc(c, fp);

    }



    return l+1;

}



static int dtoa(FILE *fp, vfpf_sint_t d)
{
 char s;
    int i, w;

 int p;





    s = d < 0 ? 1 : 0;




    if (!(prec < 0)) {
        flags &= ~(1 << 1);
    }

    p = (0 <= prec) ? prec : 1;

    w = width;
    if (s

   || (flags & (1 << 2))

    ) {
        --w;
  s = s ? '-' : '+';
    }

 else if (flags & (1 << 3)) {
        --w;
  s = ' ';
 }



    i = sizeof(dbuf) - 1;
    dbuf[i] = '\0';
    while (!(i < 1) && (d != 0

    || (0 < p)




    || ((0 < w) && (flags & (1 << 1)))

    )) {
        --i;
        dbuf[i] = (char)((int)'0' + abs(d % 10));

        --p;



        --w;
        d = d / 10;
    }


    if (s != 0) {
        --i;
        dbuf[i] = s;
    }


    return (int) pad(fp, &dbuf[i], w);
}



static int efgtoa(FILE *fp, long double f, char c)
{
    char mode, nmode, pp, sign, esign;
    int d, e, i, m, n, ne, p, t, w;
    long double h, l;
 union flui g, u, ou;


    sign = 0;
    g.f = f;
    if (g.f < 0.0) {
        sign = 1;
        g.f = -g.f;
    }


    n = 0;
    w = width;
    if (sign

   || (flags & (1 << 2))

   ) {
  sign = sign ? '-' : '+';
    }

 else if (flags & (1 << 3)) {
  sign = ' ';
 }



    if (( __fpclassifyf(g.f) == 1 )) {
  if (sign) {
   dbuf[0] = sign;
   w--;
   n = 1;
  }

        if ((((unsigned char)(((int)c)-'A')) < 26)) {
            strcpy(&dbuf[n], "INF");
        }
  else

  {
            strcpy(&dbuf[n], "inf");
        }
        w -= ((sizeof("inf")/sizeof("inf"[0]))-1);
        return (int) pad(fp, &dbuf[0], w);
    }
    if (( __fpclassifyf(g.f) == 0 )) {
  if (sign) {
   dbuf[0] = sign;
   w--;
   n = 1;
  }

        if ((((unsigned char)(((int)c)-'A')) < 26)) {
            strcpy(&dbuf[n], "NAN");
        }
  else

  {
            strcpy(&dbuf[n], "nan");
        }
        w -= ((sizeof("nan")/sizeof("nan"[0]))-1);
        return (int) pad(fp, &dbuf[0], w);
    }


    u.f = 1.0;
    e = 0;
 if (!(g.f == 0.0)) {
  while ((labs((g).u-((ou.f = u.f*10.0,ou)).u) <= 1) || g.f > ou.f) {
   u = ou;
   ++e;
  }
  if ((labs((g).u-(u).u) <= 1)) {
   g = u;
  }
  else {
   while (g.f < u.f) {
    u.f = u.f/10.0;
    --e;
    if ((labs((g).u-(u).u) <= 1)) {
     g = u;
     break;
    }
   }
  }
    }



    mode = (char)((((unsigned char)(((int)c)-'A')) < 26) ? ((int)c)|0x20 : ((int)c));



    nmode = mode;

    if (mode == 'g') {
  if (prec == 0) {
   prec = 1;
  }
        p = (0 < prec) ? prec : 6;
    }
 else

 {
        p = (prec < 0) ? 6 : prec;
    }



    if (mode == 'g') {
        if (!(e < -4) && !((p - 1) < e)) {
            nmode = 'f';
        } else {
            nmode = 'e';
        }
    }



    m = p;

    if (!(mode == 'g') || ((nmode == 'f') && (e < 0)))

 {
        ++m;
    }



    if (nmode == 'f') {
        if (e < 0) {
            u.f = 1.0;
            e = 0;
        }
        if (!(mode == 'g')) {
            m += e;
        }
    }



    i = 0;
    h = g.f;
    ou = u;
    while (i < m) {
        l = floorf(h/u.f);
        d = (int)l;
        h -= l*u.f;
        u.f = u.f/10.0;
        ++i;
    }


    l = u.f*5.0;
    if (h < l) {
        l = 0.0;
    } else {

        if ((h == l) && !(d % 2)) {
            l = 0.0;
        }
    }


    h = g.f + l;

 if (h >= (ou.f*10.0)) {
  e++;
  ou.f *= 10.0;

  if (nmode == 'f') {

   m++;
  }

 }


    u = ou;

    ne = (nmode == 'e') ? 0 : e;



    pp = 0;
    t = 0;
    i = 0;
    while ((i < m) && (n < (80 - 5))) {
        l = floorf(h/u.f);
        d = (int)l > 9 ? 9 : (int)l;

        if (!d && (mode == 'g') && (ne < 0)

    && !(flags & (1 << 4))

    ) {
            ++t;
        }
  else

  {
            if (!pp && (ne < 0)) {
                dbuf[n++] = '.';
                --w;
                pp = 1;
            }
            while (t) {
                dbuf[n++] = '0';
                --w;
                --t;
            }
            dbuf[n++] = (char)((int)'0' + d);
            --w;
        }
        h -= l*u.f;
        u.f = u.f/10.0;
        --ne;
        ++i;
    }

    if (!pp && (flags & (1 << 4))) {
        dbuf[n++] = '.';
    }

 i = sizeof(dbuf) - 1;
    dbuf[i] = '\0';



    if (nmode == 'e') {
        esign = 0;
        if (e < 0) {
            esign = 1;
            e = -e;
        }
        p = 2;
        while (e || (0 < p)) {
            --i;
            dbuf[i] = '0' + (e % 10);
            e = e / 10;
            --p;
            --w;
        }
        --i;
        dbuf[i] = esign ? '-' : '+';
        --w;
        --i;

        dbuf[i] = (((unsigned char)(((int)c)-'A')) < 26) ? 'E' : 'e';



        --w;
    }


 memcpy(&dbuf[i-n], &dbuf[0], (size_t)n);
 n = i-n;
 i = sign == 0 ? 0 : 1;


 if (flags & (1 << 1)) {
  while (w > i && n > i) {
   dbuf[--n] = '0';
   w--;
  }
 }


 if (sign) {
  dbuf[--n] = sign;
 }
 w -= i;


    return (int) pad(fp, &dbuf[n], w);
}



static int otoa(FILE *fp, vfpf_uint_t d)
{

 char t;

    int i, w;

 int p;







    if (!(prec < 0)) {
        flags &= ~(1 << 1);
    }

    p = (0 <= prec) ? prec : 1;

    w = width;


    i = sizeof(dbuf) - 1;
    dbuf[i] = '\0';

    t = 0;

    while (!(i < 1) && (d != 0

    || (0 < p)




    || ((0 < w) && (flags & (1 << 1)))

    )) {
        --i;

        t = d & 07;
        dbuf[i] = (char)((int)'0' + t);




        --p;



        --w;
        d = d >> 3;
    }



    if ((t || prec == 0) && (flags & (1 << 4))) {
        --i;
        dbuf[i] = '0';
        --w;
    }



    return (int) pad(fp, &dbuf[i], w);
}



static int stoa(FILE *fp, char *s)
{
    char *cp;
    int l, p;

 int w;



    cp = s;
    if (!cp) {
        cp = "(null)";
    }


 for (l = 0; cp[l] != '\0'; l++) {
  continue;
 }

 if (!(prec < 0) && (prec < l)) {
  l = prec;
 }

    p = l;



    w = width;
    if (!(flags & (1 << 0))) {
        while (l < w) {
            fputc(' ', fp);
            ++l;
        }
    }



    while (p--) {
        fputc(*cp, fp);
        ++cp;
    }



    if (flags & (1 << 0)) {
        while (l < w) {
            fputc(' ', fp);
            ++l;
        }
    }



    return l;

}



static int utoa(FILE *fp, vfpf_uint_t d)
{
    int i, w;

 int p;







    if (!(prec < 0)) {
        flags &= ~(1 << 1);
    }

    p = (0 <= prec) ? prec : 1;

    w = width;


    i = sizeof(dbuf) - 1;
    dbuf[i] = '\0';
    while (i && (d != 0

    || (0 < p)




    || ((0 < w) && (flags & (1 << 1)))

    )) {
        --i;
        dbuf[i] = '0' + (d % 10);

        --p;



        --w;
        d = d / 10;
    }


    return (int) pad(fp, &dbuf[i], w);
}




static int xtoa(FILE *fp, vfpf_uint_t d, char x)





{
 char c;

 char a;

    int i, w;

 int p;




 _Bool d_nonzero = 0;





    if (!(prec < 0)) {
        flags &= ~(1 << 1);
    }

    p = (0 <= prec) ? prec : 1;

    w = width;

 d_nonzero = d != 0;
    if ((flags & (1 << 4)) && d_nonzero) {
        w -= 2;
    }



 a = x == 'x' ? 'a' : 'A';



    i = sizeof(dbuf) - 1;
    dbuf[i] = '\0';
    while (!(i < 2) && (d != 0

    || (0 < p)




    || ((0 < w) && (flags & (1 << 1)))

    )) {
        --i;
        c = d & 0x0f;
  if (c < 10) {
   c += '0';
  }
  else {

   c += a - 10;





  }
  dbuf[i] = c;

        --p;



        --w;
        d = d >> 4;
    }



    if ((flags & (1 << 4)) && d_nonzero

   && prec != 0

    ) {
        --i;

        dbuf[i] = x;





        --i;
        dbuf[i] = '0';
    }



    return (int) pad(fp, &dbuf[i], w);
}



static int
read_prec_or_width (const char **fmt, va_list *ap) {
    int n = 0;
    if ((*fmt)[0] == '*') {
        ++*fmt;
        n = (*(int *)__va_arg(*(int **)*ap, (int)0));
    } else {
        unsigned char c;
        while ((c = ((unsigned)(*fmt)[0]) - '0') < 10) {
            n = n * 10 + c;
            ++*fmt;
        }
    }
    return n;
}




static int
vfpfcnvrt(FILE *fp, char *fmt[], va_list ap)
{
    char c, *cp;
    _Bool done;

 union {

  vfpf_sint_t sint;
  vfpf_uint_t uint;

  long double f;
 } convarg;
# 1201 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
    if ((*fmt)[0] == '%') {
        ++*fmt;

        flags = width = 0;
        prec = -1;



        done = 0;
        while (!done) {
            switch ((*fmt)[0]) {

                case '-' :
                    flags |= (1 << 0);
                    ++*fmt;
                    break;


                case '0' :
                    flags |= (1 << 1);
                    ++*fmt;
                    break;


                case '+' :
                    flags |= (1 << 2);
                    ++*fmt;
                    break;


                case ' ' :
                    flags |= (1 << 3);
                    ++*fmt;
                    break;


                case '#' :
                    flags |= (1 << 4);
                    ++*fmt;
                    break;

                default:
                    done = 1;
                    break;
            }
        }

        if (flags & (1 << 0)) {
            flags &= ~(1 << 1);
        }





  width = read_prec_or_width(fmt, ap);
  if (width < 0) {
   flags |= (1 << 0);
   width = -width;
  }




        if ((*fmt)[0] == '.') {
            ++*fmt;
   prec = read_prec_or_width(fmt, ap);
  }




        if ((*fmt)[0] == 'c') {
            ++*fmt;
            c = (unsigned char)(*(int *)__va_arg(*(int **)ap, (int)0));
# 1284 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
            return (int) ctoa(fp, c);

        }




  cp = *fmt;

  c = *cp;
  switch(c) {


   case 'h':
    cp++;

    if (*cp == 'h') {
     c = 'H';
     cp++;
    }

    break;


   case 'l':
    cp++;

    if (*cp == 'l') {
     c = 'L';
     cp++;
    }

    break;



   case 'j':


   case 't':


   case 'z':

    cp++;
    break;


   case 'o':


   case 'u':


   case 'i':
   case 'd':


   case 'x':


   case 'X':


   case 'n':

    c = 'i';
    break;
   default :
    c = 0;
    break;
  }





  if (*cp == 'd' || *cp == 'i') {

   switch (c) {
    case 'i':
     convarg.sint = (vfpf_sint_t)(int)(*(int *)__va_arg(*(int **)ap, (int)0));
     break;

    case 'h':
     convarg.sint = (vfpf_sint_t)(short)(*(int *)__va_arg(*(int **)ap, (int)0));
     break;


    case 'H':
     convarg.sint = (vfpf_sint_t)(signed char)(*(int *)__va_arg(*(int **)ap, (int)0));
     break;


    case 'l':
     convarg.sint = (vfpf_sint_t)(*(long *)__va_arg(*(long **)ap, (long)0));
     break;


    case 'L':
     convarg.sint = (vfpf_sint_t)(*(long long *)__va_arg(*(long long **)ap, (long long)0));
     break;


    case 't':
     convarg.sint = (vfpf_sint_t)(*(ptrdiff_t *)__va_arg(*(ptrdiff_t **)ap, (ptrdiff_t)0));
     break;


    case 'j':
     convarg.sint = (vfpf_sint_t)(*(intmax_t *)__va_arg(*(intmax_t **)ap, (intmax_t)0));
     break;


    case 'z':
     convarg.sint = (vfpf_sint_t)(*(size_t *)__va_arg(*(size_t **)ap, (size_t)0));
     break;

   }



   *fmt = cp+1;
# 1432 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
   return (int) dtoa(fp, convarg.sint);

  }




  if (0

    || *cp == 'x'


    || *cp == 'X'


    || *cp == 'o'


    || *cp == 'u'

    ) {

   switch (c) {
    case 'i':
     convarg.uint = (vfpf_uint_t)(unsigned int)(*(unsigned int *)__va_arg(*(unsigned int **)ap, (unsigned int)0));
     break;

    case 'h':
     convarg.uint = (vfpf_uint_t)(unsigned short)(*(unsigned int *)__va_arg(*(unsigned int **)ap, (unsigned int)0));
     break;


    case 'H':
     convarg.uint = (vfpf_uint_t)(unsigned char)(*(unsigned int *)__va_arg(*(unsigned int **)ap, (unsigned int)0));
     break;


    case 'l':
     convarg.uint = (vfpf_uint_t)(*(unsigned long *)__va_arg(*(unsigned long **)ap, (unsigned long)0));
     break;


    case 'L':
     convarg.uint = (vfpf_uint_t)(*(unsigned long long *)__va_arg(*(unsigned long long **)ap, (unsigned long long)0));
     break;


    case 't':
     convarg.uint = (vfpf_uint_t)(*(ptrdiff_t *)__va_arg(*(ptrdiff_t **)ap, (ptrdiff_t)0));
     break;


    case 'j':
     convarg.uint = (vfpf_uint_t)(*(uintmax_t *)__va_arg(*(uintmax_t **)ap, (uintmax_t)0));
     break;


    case 'z':
     convarg.uint = (vfpf_uint_t)(*(size_t *)__va_arg(*(size_t **)ap, (size_t)0));
     break;

   }



   *fmt = cp+1;
   switch (*cp) {

    case 'o':
# 1519 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
     return (int) otoa(fp, convarg.uint);



    case 'u':
# 1542 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
     return (int) utoa(fp, convarg.uint);




    case 'X':


    case 'x':
# 1586 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
     return (int) xtoa(fp,convarg.uint,*cp);


   }
  }




  if (*cp == 'n') {

   switch (c) {
    case 'i':
     *(*(int* *)__va_arg(*(int* **)ap, (int*)0)) = (int) nout;
     break;

    case 'h':
     *(*(short* *)__va_arg(*(short* **)ap, (short*)0)) = (short) nout;
     break;


    case 'H':
     *(*(signed char* *)__va_arg(*(signed char* **)ap, (signed char*)0)) = (signed char) nout;
     break;


    case 'l':
     *(*(long* *)__va_arg(*(long* **)ap, (long*)0)) = (long) nout;
     break;


    case 'L':
     *(*(long long* *)__va_arg(*(long long* **)ap, (long long*)0)) = (long long) nout;
     break;


    case 't':
     *(*(ptrdiff_t* *)__va_arg(*(ptrdiff_t* **)ap, (ptrdiff_t*)0)) = (ptrdiff_t) nout;
     break;


    case 'j':
     *(*(intmax_t* *)__va_arg(*(intmax_t* **)ap, (intmax_t*)0)) = (intmax_t) nout;
     break;


    case 'z':
     *(*(size_t* *)__va_arg(*(size_t* **)ap, (size_t*)0)) = (size_t) nout;
     break;

   }



   *fmt = cp+1;
   return (int) 0;
  }





        if ((*fmt)[0] == 'p') {
            ++*fmt;
            return (int) xtoa(fp,(vfpf_uint_t)(uintptr_t)(*(void * *)__va_arg(*(void * **)ap, (void *)0)),'x');
        }




  done = 0;
  if ((*fmt)[0] == 's') {
   ++*fmt;
   done = 1;
  }
  else if ((*fmt)[0] == 'l' && (*fmt)[1] == 'l' && (*fmt)[2] == 's') {
   *fmt += 3;
   done = 1;
  }
  if (done) {
# 1682 "C:\\Program Files\\Microchip\\xc8\\v3.00\\pic\\sources\\c99\\common\\doprnt.c"
   return (int) stoa(fp, (*(char * *)__va_arg(*(char * **)ap, (char *)0)));

  }




  cp = *fmt;
  c = *cp;
  switch(c) {

   case 'l':
   case 'L':
    cp++;
    break;

   case 'a':

   case 'A':



   case 'e':

   case 'E':



   case 'f':

   case 'F':



   case 'g':

   case 'G':


    c = 'l';
    break;
   default:
    c = 0;
    break;
  }

  if (0

    || *cp == 'a'

    || *cp == 'A'



    || *cp == 'e'

    || *cp == 'E'



    || *cp == 'f'

    || *cp == 'F'



    || *cp == 'g'

    || *cp == 'G'


    ) {
   switch (c) {
    case 'l':
     convarg.f = (long double)(*(double *)__va_arg(*(double **)ap, (double)0));
     break;
    case 'L':
     convarg.f = (long double)(*(long double *)__va_arg(*(long double **)ap, (long double)0));
     break;
   }
   *fmt = cp+1;
   switch (*cp) {

    case 'a':

    case 'A':

     return (int) atoa(fp,convarg.f,*cp);



    case 'e':

    case 'E':



    case 'f':

    case 'F':



    case 'g':

    case 'G':


     return (int) efgtoa(fp, convarg.f, *cp);

   }
  }




        if ((*fmt)[0] == '%') {
            ++*fmt;
            fputc((int)'%', fp);
            return (int) 1;
        }



        ++*fmt;
        return (int) 0;
    }


    fputc((int)(*fmt)[0], fp);
    ++*fmt;
    return (int) 1;
}


int vfprintf(FILE *fp, const char *fmt, va_list ap)
{

    char *cfmt;

    cfmt = (char *)fmt;

    nout = 0;

    while (*cfmt) {

        nout +=

   vfpfcnvrt(fp, &cfmt, ap);
    }

    return nout;






}
