#include <Wire.h>
#include <LiquidCrystal_I2C.h>
#include <RTClib.h>
#include <EEPROM.h>

#define DEBUG false

#define R1 2
#define R2 3
#define R3 4
#define R4 5
#define R5 6
#define MR1 7
#define MR2 8

#define FLOAT_SENSOR_1 9
#define FLOAT_SENSOR_2 10
#define FLOAT_SENSOR_3 11
#define FLOAT_SENSOR_4 12
#define FLOAT_SENSOR_5 13

LiquidCrystal_I2C lcd(0x27, 16, 2);
RTC_DS3231 rtc;
bool rtcOK = true;

bool dayEnabled[7] = {true, true, true, true, true, true, true};
unsigned long restDelay = 60000UL;

bool function1Running = false;
bool function2Running = false;

bool stopRequestedF1 = false;
bool stopRequestedF2 = false;

bool ForceStopF1 = false;
bool ForceStopF2 = false;

String inputCommand = "";

byte f1Step = 0;
unsigned long f1Timer = 0;

byte f2Step = 0;
unsigned long f2Timer = 0;

String lastLine1 = "";
String lastLine2 = "";


#define ADDR_F1_RUNNING 0
#define ADDR_F1_STEP 1
#define ADDR_F1_REMAIN_TIME 2
#define ADDR_F2_RUNNING 6
#define ADDR_F2_STEP 7
#define ADDR_F2_REMAIN_TIME 8
#define ADDR_FORCESTOPF1 12
#define ADDR_FORCESTOPF2 13

unsigned long lastEEPROMsave = 0;
#define EEPROM_SAVE_INTERVAL 5000
#define ADDR_DAY_ENABLED_START 20  // Using EEPROM addresses 20–26
#define ADDR_REST_DELAY 30 // New EEPROM address for saving restDelay


unsigned long lastLCDToggle = 0;
#define LCD_TOGGLE_INTERVAL 5000
bool showTimeOnLCD = false;

unsigned long lastDayStatusPrint = 0;
#define DAY_STATUS_INTERVAL 5000

String currentMotorLine = "";

// ==== Function Prototypes ====
void allRelaysOff();
void runFunction1();
void runFunction2();
void relaysOffF1();
void relaysOffF2();
void printRelayStatus();
void updateLCD(String l1, String l2);
void handleLCDToggle();
void printCurrentDayStatus();
void saveProcessState();
void loadProcessState();

void setup() {
  Serial.begin(9600);
  Wire.begin();

  rtc.begin();
  if (rtc.lostPower()) {
    rtcOK = false;
    rtc.adjust(DateTime(2025, 8, 7, 12, 30, 0));
    Serial.println("RTC LOST POWER -> SET DEFAULT TIME");
  }

  lcd.init();
  lcd.begin(16, 2);
  lcd.backlight();

  pinMode(R1, OUTPUT);
  pinMode(R2, OUTPUT);
  pinMode(R3, OUTPUT);
  pinMode(R4, OUTPUT);
  pinMode(R5, OUTPUT);
  pinMode(MR1, OUTPUT);
  pinMode(MR2, OUTPUT);

  pinMode(FLOAT_SENSOR_1, INPUT_PULLUP);
  pinMode(FLOAT_SENSOR_2, INPUT_PULLUP);
  pinMode(FLOAT_SENSOR_3, INPUT_PULLUP);
  pinMode(FLOAT_SENSOR_4, INPUT_PULLUP);
  pinMode(FLOAT_SENSOR_5, INPUT_PULLUP);

  allRelaysOff();
  updateLCD(" LLS Test Bench", "");
  if (DEBUG) Serial.println("LLS Test Bench");

  loadProcessState();
}

void loop() {
  readBluetoothCommand();

  DateTime now;
  if (!rtcOK) {
    now = DateTime(2025, 8, 7, 12, 30, 0);
  } else {
    now = rtc.now();
  }

  int dayOfWeek = now.dayOfTheWeek();

  // Always run functions if running, so they can finish stop sequence
  if (function1Running) runFunction1();
  if (function2Running) runFunction2();

  // If day is OFF, request stop gracefully
  if (!dayEnabled[dayOfWeek]) {
    if (function1Running && !stopRequestedF1) {
        stopRequestedF1 = true;
        ForceStopF1 = true;
        EEPROM.update(ADDR_FORCESTOPF1, 1);
        Serial.println("F1 STOP REQUESTED (DAY OFF)");
    }
    if (function2Running && !stopRequestedF2) {
        stopRequestedF2 = true;
        ForceStopF2 = true;
        EEPROM.update(ADDR_FORCESTOPF2, 1);
        Serial.println("F2 STOP REQUESTED (DAY OFF)");
    }

    updateLCD("Today OFF", "");
  }

  if (millis() - lastEEPROMsave >= EEPROM_SAVE_INTERVAL) {
    saveProcessState();
    lastEEPROMsave = millis();
  }

  handleLCDToggle();
  printCurrentDayStatus();

  delay(100);
}

void readBluetoothCommand() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') {
      parseCommand(inputCommand);
      inputCommand = "";
    } else {
      inputCommand += c;
    }
  }
}
void parseCommand(String cmd) {
  cmd.trim();
  cmd.toUpperCase();

  if (cmd == "STARTF1") {
    DateTime now = rtc.now();
    int dayOfWeek = now.dayOfTheWeek();

    if (!dayEnabled[dayOfWeek]) {
        Serial.println("Cannot STARTF1 → Today is OFF");
        updateLCD("Cannot STARTF1", "Today is OFF");
        return;
    }

    function1Running = true;
    f1Step = 0;
    f1Timer = millis();
    stopRequestedF1 = false;
    ForceStopF1 = false;
    EEPROM.update(ADDR_FORCESTOPF1, 0);
    Serial.println("F1 STARTED");

    showTimeOnLCD = false;
    lastLine1 = "";
    lastLine2 = "";

    digitalWrite(R1, HIGH);
    printRelayStatus();
  }

  else if (cmd == "STOPF1") {
    stopRequestedF1 = true;
    ForceStopF1 = true;
    EEPROM.update(ADDR_FORCESTOPF1, 1);
    Serial.println("F1 EMERGENCY STOP REQUESTED");
  } 

  else if (cmd == "STARTF2") {
    DateTime now = rtc.now();
    int dayOfWeek = now.dayOfTheWeek();

    if (!dayEnabled[dayOfWeek]) {
        Serial.println("Cannot STARTF2 → Today is OFF");
        updateLCD("Cannot STARTF2", "Today is OFF");
        return;
    }

    function2Running = true;
    f2Step = 0;
    f2Timer = millis();
    stopRequestedF2 = false;
    ForceStopF2 = false;
    EEPROM.update(ADDR_FORCESTOPF2, 0);
    Serial.println("F2 STARTED");

    showTimeOnLCD = false;
    lastLine1 = "";
    lastLine2 = "";

    digitalWrite(R3, HIGH);
    printRelayStatus();
  }

  else if (cmd == "STOPF2") {
    stopRequestedF2 = true;
    ForceStopF2 = true;
    EEPROM.update(ADDR_FORCESTOPF2, 1);
    Serial.println("F2 EMERGENCY STOP REQUESTED");
  } 

  else if (cmd == "STARTBOTH") {
    DateTime now = rtc.now();
    int dayOfWeek = now.dayOfTheWeek();

    if (!dayEnabled[dayOfWeek]) {
        Serial.println("Cannot STARTBOTH → Today is OFF");
        updateLCD("Cannot STARTBOTH", "Today is OFF");
        return;
    }

    function1Running = true;
    function2Running = true;
    f1Step = 0;
    f2Step = 0;
    f1Timer = millis();
    f2Timer = millis();
    stopRequestedF1 = false;
    ForceStopF1 = false;
    stopRequestedF2 = false;
    ForceStopF2 = false;
    EEPROM.update(ADDR_FORCESTOPF1, 0);
    EEPROM.update(ADDR_FORCESTOPF2, 0);
    Serial.println("BOTH STARTED");

    showTimeOnLCD = false;
    lastLine1 = "";
    lastLine2 = "";

    digitalWrite(R1, HIGH);
    digitalWrite(R3, HIGH);
    printRelayStatus();
  }

  else if (cmd == "STOPBOTH") {
    if (function1Running && !stopRequestedF1) {
        stopRequestedF1 = true;
        ForceStopF1 = true;
        EEPROM.update(ADDR_FORCESTOPF1, 1);
        Serial.println("F1 STOP REQUESTED (STOPBOTH)");
    }
    if (function2Running && !stopRequestedF2) {
        stopRequestedF2 = true;
        ForceStopF2 = true;
        EEPROM.update(ADDR_FORCESTOPF2, 1);
        Serial.println("F2 STOP REQUESTED (STOPBOTH)");
    }

    showTimeOnLCD = false;
    lastLine1 = "";
    lastLine2 = "";
    printRelayStatus();
  }

  else if (cmd == "RESETMEM") {
    EEPROM.update(ADDR_F1_RUNNING, 0);
    EEPROM.update(ADDR_F1_STEP, 0);
    unsigned long zero = 0;
    EEPROM.put(ADDR_F1_REMAIN_TIME, zero);

    EEPROM.update(ADDR_F2_RUNNING, 0);
    EEPROM.update(ADDR_F2_STEP, 0);
    EEPROM.put(ADDR_F2_REMAIN_TIME, zero);

    EEPROM.update(ADDR_FORCESTOPF1, 0);
    EEPROM.update(ADDR_FORCESTOPF2, 0);

    function1Running = false;
    function2Running = false;
    stopRequestedF1 = false;
    stopRequestedF2 = false;
    ForceStopF1 = false;
    ForceStopF2 = false;
    f1Step = 0;
    f2Step = 0;
    f1Timer = millis();
    f2Timer = millis();

    Serial.println("EEPROM RESET COMPLETE");
    updateLCD("EEPROM RESET", "COMPLETE");
  }

  else if (cmd.startsWith("DAY")) {
    int dayNum = cmd.substring(3, 5).toInt();
    int dayIdx = dayNum - 1;

    if (dayIdx < 0 || dayIdx > 6) {
        Serial.println("Invalid DAY number (1-7 only)");
        return;
    }

if (cmd.endsWith("OFF")) {
    dayEnabled[dayIdx] = false;
    EEPROM.update(ADDR_DAY_ENABLED_START + dayIdx, 0);  // Save to EEPROM
    Serial.print("DAY ");
    Serial.print(dayNum);
    Serial.println(" DISABLED");

    updateLCD("DAY " + String(dayNum) + " DISABLED", "");
} else if (cmd.endsWith("ON")) {
    dayEnabled[dayIdx] = true;
    EEPROM.update(ADDR_DAY_ENABLED_START + dayIdx, 1);  // Save to EEPROM
    Serial.print("DAY ");
    Serial.print(dayNum);
    Serial.println(" ENABLED");

    updateLCD("DAY " + String(dayNum) + " ENABLED", "");
}
 else {
        Serial.println("Invalid DAY command. Use DAYxxON or DAYxxOFF");
    }
  }

  else if (cmd.startsWith("T")) {
    int mins = cmd.substring(1).toInt();

    if (mins < 1) mins = 1;
    if (mins > 999) mins = 999;

restDelay = mins * 60000UL;
EEPROM.put(ADDR_REST_DELAY, restDelay);  // Save to EEPROM

Serial.print("Rest Delay set to ");
Serial.print(mins);
Serial.println(" min");

updateLCD("RestDelay set:", String(mins) + " min");

  }

  else if (cmd == "LOG") {
    DateTime now = rtc.now();
    int dayOfWeek = now.dayOfTheWeek();
    String dayName = "";

    switch (dayOfWeek) {
      case 0: dayName = "Sunday"; break;
      case 1: dayName = "Monday"; break;
      case 2: dayName = "Tuesday"; break;
      case 3: dayName = "Wednesday"; break;
      case 4: dayName = "Thursday"; break;
      case 5: dayName = "Friday"; break;
      case 6: dayName = "Saturday"; break;
    }

    Serial.println("===== Device LOG =====");
    Serial.print("Current Day: "); Serial.println(dayName);
    Serial.print("DayEnabled: "); Serial.println(dayEnabled[dayOfWeek] ? "YES" : "NO");

    Serial.print("Function1Running: "); Serial.println(function1Running ? "YES" : "NO");
    Serial.print("ForceStopF1: "); Serial.println(ForceStopF1 ? "YES" : "NO");

    Serial.print("Function2Running: "); Serial.println(function2Running ? "YES" : "NO");
    Serial.print("ForceStopF2: "); Serial.println(ForceStopF2 ? "YES" : "NO");

    Serial.print("Relay R1: "); Serial.println(digitalRead(R1) ? "ON" : "OFF");
    Serial.print("Relay R2: "); Serial.println(digitalRead(R2) ? "ON" : "OFF");
    Serial.print("Relay R3: "); Serial.println(digitalRead(R3) ? "ON" : "OFF");
    Serial.print("Relay R4: "); Serial.println(digitalRead(R4) ? "ON" : "OFF");
    Serial.print("Relay R5: "); Serial.println(digitalRead(R5) ? "ON" : "OFF");

    Serial.print("Motor MR1: "); Serial.println(digitalRead(MR1) ? "ON" : "OFF");
    Serial.print("Motor MR2: "); Serial.println(digitalRead(MR2) ? "ON" : "OFF");

    Serial.print("LCD Motor Line: "); Serial.println(currentMotorLine);

    Serial.println("======================");
  }
}
void runFunction1() {
  if (ForceStopF1 && digitalRead(MR1) == HIGH) {
    digitalWrite(MR1, LOW);
    printRelayStatus();
    f1Timer = millis();
    f1Step = 4;
  }

  switch (f1Step) {
    case 0:
      digitalWrite(R1, HIGH);
      printRelayStatus();
      f1Timer = millis();
      f1Step++;
      break;
    case 1:
      if (millis() - f1Timer >= 300000) {
        digitalWrite(MR1, HIGH);
        printRelayStatus();
        f1Step++;
      }
      break;
    case 2:
      if (!ForceStopF1 && digitalRead(FLOAT_SENSOR_1) == LOW) {
        digitalWrite(MR1, LOW);
        printRelayStatus();
        f1Timer = millis();
        f1Step++;
      }
      break;
    case 3:
      if (millis() - f1Timer >= 300000) {
        f1Timer = millis();
        f1Step++;
      }
      break;
    case 4:
      if (millis() - f1Timer >= 300000) {
        digitalWrite(R1, LOW);
        printRelayStatus();
        f1Timer = millis();
        if (stopRequestedF1) {
          relaysOffF1();
          stopRequestedF1 = false;
          ForceStopF1 = false;
          EEPROM.update(ADDR_FORCESTOPF1, 0);
          function1Running = false;
        } else {
          f1Step++;
        }
      }
      break;
    case 5:
      if (millis() - f1Timer >= restDelay) {
        digitalWrite(R2, HIGH);
        printRelayStatus();
        f1Timer = millis();
        f1Step++;
      }
      break;
    case 6:
      if (millis() - f1Timer >= 300000) {
        digitalWrite(MR1, HIGH);
        printRelayStatus();
        f1Step++;
      }
      break;
    case 7:
      if (!ForceStopF1 && digitalRead(FLOAT_SENSOR_2) == LOW) {
        digitalWrite(MR1, LOW);
        printRelayStatus();
        f1Timer = millis();
        f1Step++;
      }
      break;
    case 8:
      if (millis() - f1Timer >= 300000) {
        f1Timer = millis();
        f1Step++;
      }
      break;
    case 9:
      if (millis() - f1Timer >= 300000) {
        digitalWrite(R2, LOW);
        printRelayStatus();
        f1Timer = millis();
        if (stopRequestedF1) {
          relaysOffF1();
          stopRequestedF1 = false;
          ForceStopF1 = false;
          EEPROM.update(ADDR_FORCESTOPF1, 0);
          function1Running = false;
        } else {
          f1Step++;
        }
      }
      break;
    case 10:
      if (millis() - f1Timer >= restDelay) {
        if (stopRequestedF1) {
          relaysOffF1();
          stopRequestedF1 = false;
          ForceStopF1 = false;
          EEPROM.update(ADDR_FORCESTOPF1, 0);
          function1Running = false;
        } else {
          f1Step = 0;
          f1Timer = millis();
        }
      }
      break;
  }
}

void runFunction2() {
  if (ForceStopF2 && digitalRead(MR2) == HIGH) {
    digitalWrite(MR2, LOW);
    printRelayStatus();
    f2Timer = millis();
    f2Step = 4;
  }

  switch (f2Step) {
    case 0:
      digitalWrite(R3, HIGH);
      printRelayStatus();
      f2Timer = millis();
      f2Step++;
      break;
    case 1:
      if (millis() - f2Timer >= 300000) {
        digitalWrite(MR2, HIGH);
        printRelayStatus();
        f2Step++;
      }
      break;
    case 2:
      if (!ForceStopF2 && digitalRead(FLOAT_SENSOR_3) == LOW) {
        digitalWrite(MR2, LOW);
        printRelayStatus();
        f2Timer = millis();
        f2Step++;
      }
      break;
    case 3:
      if (millis() - f2Timer >= 300000) {
        f2Timer = millis();
        f2Step++;
      }
      break;
    case 4:
      if (millis() - f2Timer >= 300000) {
        digitalWrite(R3, LOW);
        printRelayStatus();
        f2Timer = millis();
        if (stopRequestedF2) {
          relaysOffF2();
          stopRequestedF2 = false;
          ForceStopF2 = false;
          EEPROM.update(ADDR_FORCESTOPF2, 0);
          function2Running = false;
        } else {
          f2Step++;
        }
      }
      break;
    case 5:
      if (millis() - f2Timer >= restDelay) {
        digitalWrite(R4, HIGH);
        printRelayStatus();
        f2Timer = millis();
        f2Step++;
      }
      break;
    case 6:
      if (millis() - f2Timer >= 300000) {
        digitalWrite(MR2, HIGH);
        printRelayStatus();
        f2Step++;
      }
      break;
    case 7:
      if (!ForceStopF2 && digitalRead(FLOAT_SENSOR_4) == LOW) {
        digitalWrite(MR2, LOW);
        printRelayStatus();
        f2Timer = millis();
        f2Step++;
      }
      break;
    case 8:
      if (millis() - f2Timer >= 300000) {
        f2Timer = millis();
        f2Step++;
      }
      break;
    case 9:
      if (millis() - f2Timer >= 300000) {
        digitalWrite(R4, LOW);
        printRelayStatus();
        f2Timer = millis();
        if (stopRequestedF2) {
          relaysOffF2();
          stopRequestedF2 = false;
          ForceStopF2 = false;
          EEPROM.update(ADDR_FORCESTOPF2, 0);
          function2Running = false;
        } else {
          f2Step++;
        }
      }
      break;
    case 10:
      if (millis() - f2Timer >= restDelay) {
        digitalWrite(R5, HIGH);
        printRelayStatus();
        f2Timer = millis();
        f2Step++;
      }
      break;
    case 11:
      if (millis() - f2Timer >= 300000) {
        digitalWrite(MR2, HIGH);
        printRelayStatus();
        f2Step++;
      }
      break;
    case 12:
      if (!ForceStopF2 && digitalRead(FLOAT_SENSOR_5) == LOW) {
        digitalWrite(MR2, LOW);
        printRelayStatus();
        f2Timer = millis();
        f2Step++;
      }
      break;
    case 13:
      if (millis() - f2Timer >= 300000) {
        digitalWrite(R5, LOW);
        printRelayStatus();
        f2Timer = millis();
        if (stopRequestedF2) {
          relaysOffF2();
          stopRequestedF2 = false;
          ForceStopF2 = false;
          EEPROM.update(ADDR_FORCESTOPF2, 0);
          function2Running = false;
        } else {
          f2Step++;
        }
      }
      break;
    case 14:
      if (millis() - f2Timer >= restDelay) {
        if (stopRequestedF2) {
          relaysOffF2();
          stopRequestedF2 = false;
          ForceStopF2 = false;
          EEPROM.update(ADDR_FORCESTOPF2, 0);
          function2Running = false;
        } else {
          f2Step = 0;
          f2Timer = millis();
        }
      }
      break;
  }
}

void relaysOffF1() {
  digitalWrite(R1, LOW);
  digitalWrite(R2, LOW);
  digitalWrite(MR1, LOW);
}

void relaysOffF2() {
  digitalWrite(R3, LOW);
  digitalWrite(R4, LOW);
  digitalWrite(R5, LOW);
  digitalWrite(MR2, LOW);
}

void allRelaysOff() {
  digitalWrite(R1, LOW);
  digitalWrite(R2, LOW);
  digitalWrite(R3, LOW);
  digitalWrite(R4, LOW);
  digitalWrite(R5, LOW);
  digitalWrite(MR1, LOW);
  digitalWrite(MR2, LOW);
}

void printRelayStatus() {
  String line1 = "";
  String line2 = "";

  if (digitalRead(R1)) line1 += "T1F/T2D ";
  if (digitalRead(R2)) line1 += "T2F/T1D ";
  if (digitalRead(R3)) line1 += "T3F/T5D ";
  if (digitalRead(R4)) line1 += "T4F/T3D ";
  if (digitalRead(R5)) line1 += "T5F/T4D ";

  if (digitalRead(MR1) && digitalRead(MR2)) line2 = "Motor 1&2 ON";
  else if (digitalRead(MR1)) line2 = "Motor 1 ON";
  else if (digitalRead(MR2)) line2 = "Motor 2 ON";
  else line2 = "Motor 1&2 OFF";

  updateLCD(line1, line2);
  currentMotorLine = line2;
}

void updateLCD(String l1, String l2) {
  if (l1 != lastLine1) {
    lcd.setCursor(0, 0);
    lcd.print("                ");
    lcd.setCursor(0, 0);
    lcd.print(l1.substring(0, 16));
    lastLine1 = l1;
  }

  if (l2 != lastLine2 && !showTimeOnLCD) {
    lcd.setCursor(0, 1);
    lcd.print("                ");
    lcd.setCursor(0, 1);
    lcd.print(l2.substring(0, 16));
    lastLine2 = l2;
  }
}
void handleLCDToggle() {
  // If any function is active → force relay status view
  if (function1Running || function2Running) {
    showTimeOnLCD = false;

    // Only refresh line 2 if it changed
    if (currentMotorLine != lastLine2) {
      lcd.setCursor(0, 1);
      lcd.print("                ");
      lcd.setCursor(0, 1);
      lcd.print(currentMotorLine.substring(0, 16));
      lastLine2 = currentMotorLine;
    }
  }
  else {
    // Toggle between time and relay view when system is idle
    if (millis() - lastLCDToggle >= LCD_TOGGLE_INTERVAL) {
      lastLCDToggle = millis();
      showTimeOnLCD = !showTimeOnLCD;

      if (showTimeOnLCD) {
        DateTime now = rtc.now();
        String dayName = "";

        switch (now.dayOfTheWeek()) {
          case 0: dayName = "Sun"; break;
          case 1: dayName = "Mon"; break;
          case 2: dayName = "Tue"; break;
          case 3: dayName = "Wed"; break;
          case 4: dayName = "Thu"; break;
          case 5: dayName = "Fri"; break;
          case 6: dayName = "Sat"; break;
        }

        char timeBuffer[17];
        sprintf(timeBuffer, "%s %02d:%02d:%02d", dayName.c_str(), now.hour(), now.minute(), now.second());

        lcd.setCursor(0, 1);
        lcd.print("                ");
        lcd.setCursor(0, 1);
        lcd.print(timeBuffer);

        lastLine2 = "";
      } else {
        lcd.setCursor(0, 1);
        lcd.print("                ");
        lcd.setCursor(0, 1);
        lcd.print(currentMotorLine.substring(0, 16));
        lastLine2 = currentMotorLine;
      }
    }
  }
}

void printCurrentDayStatus() {
  if (millis() - lastDayStatusPrint >= DAY_STATUS_INTERVAL) {
    lastDayStatusPrint = millis();

    DateTime now = rtc.now();
    int dayOfWeek = now.dayOfTheWeek();
    String dayName = "";

    switch (dayOfWeek) {
      case 0: dayName = "Sunday"; break;
      case 1: dayName = "Monday"; break;
      case 2: dayName = "Tuesday"; break;
      case 3: dayName = "Wednesday"; break;
      case 4: dayName = "Thursday"; break;
      case 5: dayName = "Friday"; break;
      case 6: dayName = "Saturday"; break;
    }

    if (DEBUG) {
      Serial.print("Current Day: ");
      Serial.println(dayName);
      Serial.print("DayEnabled: ");
      Serial.println(dayEnabled[dayOfWeek] ? "YES" : "NO");
    }
  }
}

void saveProcessState() {
  EEPROM.update(ADDR_F1_RUNNING, function1Running ? 1 : 0);
  EEPROM.update(ADDR_F1_STEP, f1Step);
  unsigned long f1Remain = function1Running ? (millis() - f1Timer) : 0;
  EEPROM.put(ADDR_F1_REMAIN_TIME, f1Remain);

  EEPROM.update(ADDR_F2_RUNNING, function2Running ? 1 : 0);
  EEPROM.update(ADDR_F2_STEP, f2Step);
  unsigned long f2Remain = function2Running ? (millis() - f2Timer) : 0;
  EEPROM.put(ADDR_F2_REMAIN_TIME, f2Remain);

  EEPROM.update(ADDR_FORCESTOPF1, ForceStopF1 ? 1 : 0);
  EEPROM.update(ADDR_FORCESTOPF2, ForceStopF2 ? 1 : 0);
}

void loadProcessState() {
  function1Running = EEPROM.read(ADDR_F1_RUNNING) ? true : false;
  f1Step = EEPROM.read(ADDR_F1_STEP);
  unsigned long f1Remain;
  EEPROM.get(ADDR_F1_REMAIN_TIME, f1Remain);
  f1Timer = millis() - f1Remain;

  function2Running = EEPROM.read(ADDR_F2_RUNNING) ? true : false;
  f2Step = EEPROM.read(ADDR_F2_STEP);
  unsigned long f2Remain;
  EEPROM.get(ADDR_F2_REMAIN_TIME, f2Remain);
  f2Timer = millis() - f2Remain;

  ForceStopF1 = EEPROM.read(ADDR_FORCESTOPF1) ? true : false;
  ForceStopF2 = EEPROM.read(ADDR_FORCESTOPF2) ? true : false;

  // Load dayEnabled[] state from EEPROM
  for (int i = 0; i < 7; i++) {
    byte val = EEPROM.read(ADDR_DAY_ENABLED_START + i);
    dayEnabled[i] = (val == 1);
  }

    // Load restDelay from EEPROM (if valid)
  unsigned long storedDelay;
  EEPROM.get(ADDR_REST_DELAY, storedDelay);
  if (storedDelay >= 60000UL && storedDelay <= 999UL * 60000UL) {
    restDelay = storedDelay;
  } else {
    restDelay = 60000UL; // fallback default 1 minute
  }

}




