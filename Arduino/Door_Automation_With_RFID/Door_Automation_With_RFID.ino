#include <Wire.h>
#include <LiquidCrystal_I2C.h>
#include <RTClib.h>
#include <EEPROM.h>

#include <SoftwareSerial.h>

// ------------------- PIN CONFIG -------------------
#define RFID_RX 10
#define RFID_TX 11
#define RELAY_PIN 8
#define LED_PIN 7
#define BTN_REG 6 
#define BTN_DEL 5
#define BTN_IMMEDIATE 4

#define UID_LEN 12
#define UID_HEX_LEN 24
#define MAX_CARDS 10
#define EEPROM_START 0

SoftwareSerial rfidSerial(RFID_RX, RFID_TX);
LiquidCrystal_I2C lcd(0x27, 16, 2);
RTC_DS3231 rtc;

char lastTagStr[UID_HEX_LEN + 1] = "";
unsigned long lastRFIDTime = 0;
bool tagDetected = false;

// ------------------- FUNCTION DECLARATIONS -------------------
bool readTag(byte* tag);
bool isRegistered(byte* uid);
void registerUID(byte* uid);
void deleteUID(byte* uid);
void readUID(int index, byte* buffer);
void writeUID(int index, byte* uid);
void clearUID(int index);
bool compareUID(byte* a, byte* b);
void showIdleScreen();
void openDoor(const char* msg);
void waitForTagRemoval();

// ------------------- SETUP -------------------
void setup() {
  Serial.begin(9600);
  rfidSerial.begin(9600);
  lcd.init();
  lcd.backlight();
  rtc.begin();

  pinMode(RELAY_PIN, OUTPUT);
  pinMode(LED_PIN, OUTPUT);
  pinMode(BTN_REG, INPUT_PULLUP);
  pinMode(BTN_DEL, INPUT_PULLUP);
  pinMode(BTN_IMMEDIATE, INPUT_PULLUP);

  digitalWrite(RELAY_PIN, LOW);
  digitalWrite(LED_PIN, LOW);
}

// ------------------- MAIN LOOP -------------------
void loop() {
  byte tag[UID_LEN];

  if (digitalRead(BTN_IMMEDIATE) == LOW) {
    openDoor("Immediate Access");
    return;
  }

  if (readTag(tag)) {
    tagDetected = true;
    lastRFIDTime = millis();

    lcd.clear();
    lcd.setCursor(0, 0); lcd.print("Tag Detected:");
    lcd.setCursor(0, 1); lcd.print(lastTagStr);
    delay(2000);

    String tagString = String(lastTagStr);
    lcd.clear();
    lcd.setCursor(0, 0); lcd.print(tagString.substring(0, 16));
    lcd.setCursor(0, 1); lcd.print(tagString.substring(16));

    while (millis() - lastRFIDTime < 5000) {
      if (digitalRead(BTN_REG) == LOW) {
        registerUID(tag);
        lcd.clear(); lcd.setCursor(0, 0); lcd.print("Tag Registered");
        delay(2000);
        waitForTagRemoval();
        return;
      }
      if (digitalRead(BTN_DEL) == LOW) {
        deleteUID(tag);
        lcd.clear(); lcd.setCursor(0, 0); lcd.print("Tag Deleted");
        delay(2000);
        waitForTagRemoval();
        return;
      }
    }

    if (isRegistered(tag)) {
      openDoor("Access Granted");
    } else {
      lcd.clear(); lcd.setCursor(0, 0); lcd.print("Access Denied");
      delay(2000);
    }

    waitForTagRemoval();  // Prevent repeated access
    return;
  }

  if (!tagDetected || millis() - lastRFIDTime > 5000) {
    tagDetected = false;
    showIdleScreen();
  }
}

// ------------------- FUNCTION: READ RFID TAG -------------------
bool readTag(byte* tag) {
  static byte buffer[64];
  static int bufIndex = 0;

  while (rfidSerial.available()) {
    byte b = rfidSerial.read();

    if (b == 0x0C || b == 0xE2) {
      bufIndex = 0;
    }

    if (bufIndex < 64) {
      buffer[bufIndex++] = b;
    }

    for (int i = 0; i < bufIndex - UID_LEN; i++) {
      if (buffer[i] == 0xE2) {
        memcpy(tag, &buffer[i], UID_LEN);

        bool valid = true;
        for (int j = 0; j < UID_LEN; j++) {
          if (tag[j] == 0x00 && j < 4) valid = false;
        }
        if (!valid) continue;

        lastTagStr[0] = '\0';
        for (int j = 0; j < UID_LEN; j++) {
          char hexByte[3];
          sprintf(hexByte, "%02X", tag[j]);
          strcat(lastTagStr, hexByte);
        }

        Serial.print("VALID TAG: ");
        Serial.println(lastTagStr);

        bufIndex = 0;
        return true;
      }
    }
  }

  return false;
}

// ------------------- FUNCTION: UID CHECK -------------------
bool compareUID(byte* a, byte* b) {
  for (int i = 0; i < UID_LEN; i++) {
    if (a[i] != b[i]) return false;
  }
  return true;
}

// ------------------- FUNCTION: REGISTRATION -------------------
bool isRegistered(byte* uid) {
  for (int i = 0; i < MAX_CARDS; i++) {
    byte stored[UID_LEN];
    readUID(i, stored);
    if (stored[0] != 0xFF && compareUID(stored, uid)) return true;
  }
  return false;
}

void registerUID(byte* uid) {
  if (isRegistered(uid)) return;
  for (int i = 0; i < MAX_CARDS; i++) {
    byte stored[UID_LEN];
    readUID(i, stored);
    if (stored[0] == 0xFF) {
      writeUID(i, uid);
      return;
    }
  }
}

void deleteUID(byte* uid) {
  for (int i = 0; i < MAX_CARDS; i++) {
    byte stored[UID_LEN];
    readUID(i, stored);
    if (compareUID(stored, uid)) {
      clearUID(i);
      return;
    }
  }
}

// ------------------- FUNCTION: EEPROM -------------------
void readUID(int index, byte* buffer) {
  int addr = EEPROM_START + index * UID_LEN;
  for (int i = 0; i < UID_LEN; i++) {
    buffer[i] = EEPROM.read(addr + i);
  }
}

void writeUID(int index, byte* uid) {
  int addr = EEPROM_START + index * UID_LEN;
  for (int i = 0; i < UID_LEN; i++) {
    if (EEPROM.read(addr + i) != uid[i]) {
      EEPROM.write(addr + i, uid[i]);
    }
  }
}

void clearUID(int index) {
  int addr = EEPROM_START + index * UID_LEN;
  for (int i = 0; i < UID_LEN; i++) {
    EEPROM.write(addr + i, 0xFF);
  }
}

// ------------------- FUNCTION: OPEN DOOR -------------------
void openDoor(const char* msg) {
  lcd.clear(); lcd.setCursor(0, 0); lcd.print(msg);
  digitalWrite(RELAY_PIN, HIGH);
  digitalWrite(LED_PIN, HIGH);
  delay(1000);
  digitalWrite(RELAY_PIN, LOW);
  digitalWrite(LED_PIN, LOW);
}

// ------------------- FUNCTION: IDLE DISPLAY -------------------
void showIdleScreen() {
  lcd.setCursor(0, 0);
  lcd.print("RealTech Systems");

  DateTime now = rtc.now();
  const char* daysOfWeek[] = {"SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"};

  lcd.setCursor(0, 1);
  lcd.print(now.day()); lcd.print('/');
  lcd.print(now.month()); lcd.print(' ');
  lcd.print(daysOfWeek[now.dayOfTheWeek()]);
  lcd.print(' ');
  lcd.print(now.hour()); lcd.print(':');
  if (now.minute() < 10) lcd.print('0');
  lcd.print(now.minute());

  delay(500);
}

// ------------------- FUNCTION: WAIT FOR TAG REMOVAL -------------------
void waitForTagRemoval() {
  unsigned long start = millis();
  while (millis() - start < 3000) {
    byte temp[UID_LEN];
    if (!readTag(temp)) {
      return;
    }
    delay(100);
  }
}
