/***************************************************************
 * BOOM BARRIER, SIGNAL LIGHT, BUZZER AND RS485 DISPLAY CONTROL
 * ESP32 DevKit V1 / ESP32-WROOM-32 version - V1.6 WIFI + BLUETOOTH
 *
 * Preserves all V1.5 application logic and adds Wi-Fi TCP control in parallel:
 *   - USB Serial, Bluetooth, and Wi-Fi accept the same commands
 *   - All inputs feed the same FIFO command queue
 *   - Replies, status, sensor notifications, and errors go to all active links
 *   - Bluetooth device name: STP_AUTO_ESP32
 *   - Wi-Fi TCP server port: 23
 *   - ESP32 static IP: 192.168.22.102
 *
 * Existing sensor support:
 *   - Existing IN PE sensor
 *   - Existing OUT PE sensor
 *   - New IN RELEASE PE sensor
 *   - New OUT RELEASE PE sensor
 *
 * PE SENSOR OPERATION MATCHES UNO V1.4:
 *   IN/OUT DETECT PE:
 *     confirmed LOW -> HIGH prints Detected once; LOW only rearms.
 *   IN/OUT RELEASE PE:
 *     confirmed HIGH arms; following confirmed LOW prints Released once.
 *     Release sensors are notification-only and do not change lamps,
 *     buzzers, or boom-barrier relays.
 *
 * IMPORTANT ELECTRICAL NOTES:
 * - ESP32 GPIOs are 3.3 V ONLY and are NOT 5 V tolerant.
 * - Never connect 5 V / 12 V / 24 V sensor outputs directly.
 * - Use proper level shifting / optocouplers / transistor interfaces.
 * - GPIO34/35 are input-only and have no internal pull-ups.
 * - GPIO32/33 are used as the two RELEASE sensor inputs on this board.
 * - Outputs and signal lamps are active HIGH, matching the Uno code.
 * - Release inputs use internal pulldowns to prevent floating at startup.
 * - Serial Monitor: 9600 baud; CR/LF, LF, CR, or no-line-ending supported.
 * - Use relay/transistor/MOSFET/optocoupler drivers for loads.
 * - RS485 uses hardware UART2: RX=GPIO16, TX=GPIO17.
 * - Tie MAX485-style RE and DE together and connect to GPIO15.
 ***************************************************************/

#include <Arduino.h>
#include "BluetoothSerial.h"
#include <WiFi.h>
#include <pgmspace.h>
#include <ctype.h>
#include <stdio.h>
#include <string.h>

// =============================================================
// BLUETOOTH CLASSIC SERIAL (SPP)
// =============================================================

BluetoothSerial SerialBT;
const char BLUETOOTH_DEVICE_NAME[] = "STP_AUTO_ESP32";

// =============================================================
// WI-FI TCP SERIAL INTERFACE
// =============================================================

// Enter the actual Wi-Fi credentials for the 192.168.22.x network.
const char WIFI_SSID[] = "AWS Data Kit";
const char WIFI_PASSWORD[] = "rts@12345";

// User-specified fixed IP. Gateway/subnet below assume a normal /24 LAN.
// Change WIFI_GATEWAY only if your router uses another address.
IPAddress WIFI_LOCAL_IP(192, 168, 22, 102);
IPAddress WIFI_GATEWAY(192, 168, 22, 1);
IPAddress WIFI_SUBNET(255, 255, 255, 0);
IPAddress WIFI_DNS1(192, 168, 22, 1);
IPAddress WIFI_DNS2(8, 8, 8, 8);

const uint16_t WIFI_TCP_PORT = 23;
WiFiServer wifiServer(WIFI_TCP_PORT);
WiFiClient wifiClient;

// Send normal text/status/events to USB Serial, Bluetooth, and the
// currently connected Wi-Fi TCP client. Wi-Fi is optional at runtime;
// Bluetooth and USB remain operational even if Wi-Fi is disconnected.
#define BOTH_PRINT(value) do { \
    Serial.print(value); \
    SerialBT.print(value); \
    if (wifiClient && wifiClient.connected()) wifiClient.print(value); \
} while (0)

#define BOTH_PRINTLN(value) do { \
    Serial.println(value); \
    SerialBT.println(value); \
    if (wifiClient && wifiClient.connected()) wifiClient.println(value); \
} while (0)

// =============================================================
// PIN MAPPING
// =============================================================

// IN lane outputs
#define IN_GREEN_PIN   18
#define IN_RED_PIN     19
#define IN_ORANGE_PIN  21
#define IN_BUZZER_PIN  13

// OUT lane outputs
#define OUT_RED_PIN     23
#define OUT_GREEN_PIN   22
#define OUT_ORANGE_PIN   4
#define OUT_BUZZER_PIN   5

// Existing photoelectric sensors (ADC1, input-only pins)
#define IN_PE_SENSOR_PIN   34
#define OUT_PE_SENSOR_PIN  35

// New photoelectric RELEASE sensors (digital input-only pins)
#define IN_RELEASE_SENSOR_PIN   32
#define OUT_RELEASE_SENSOR_PIN  33

// RS485 / display - ESP32 hardware UART2
#define RS485_RO   16   // RS485 module RO -> ESP32 RX2
#define RS485_DI   17   // ESP32 TX2 -> RS485 module DI
#define RS485_DIR  15   // Tie RS485 RE + DE together -> GPIO15

// Boom barrier relay outputs
#define IN_BB_OPEN_PIN    25
#define OUT_BB_OPEN_PIN   26
#define IN_BB_CLOSE_PIN   27
#define OUT_BB_CLOSE_PIN  14

// =============================================================
// OUTPUT POLARITY
// =============================================================

// Match the Arduino Uno version: all controlled outputs are active HIGH.
// HIGH = ON, LOW = OFF.
const byte OUTPUT_ON_LEVEL  = HIGH;
const byte OUTPUT_OFF_LEVEL = LOW;

// Signal-light polarity is intentionally identical to the Uno version.
const byte SIGNAL_ON_LEVEL  = OUTPUT_ON_LEVEL;
const byte SIGNAL_OFF_LEVEL = OUTPUT_OFF_LEVEL;

// IN/OUT ORG lamps blink while their lane is in ORG state.
// 500 ms ON + 500 ms OFF = one complete blink per second.
const unsigned long ORANGE_BLINK_INTERVAL_MS = 500UL;
bool inOrangeBlinkActive = false;
bool outOrangeBlinkActive = false;
unsigned long inOrangeBlinkTime = 0;
unsigned long outOrangeBlinkTime = 0;

// =============================================================
// RS485 DISPLAY CONFIGURATION
// =============================================================

// Use ESP32 hardware UART2 for RS485. This keeps USB/programming Serial
// independent from the display link and avoids SoftwareSerial timing limits.
HardwareSerial rs485(2);

const byte displayHeader[] PROGMEM = {
    0x01, 0x10, 0x00, 0x00, 0x00, 0x20, 0x40
};

const byte DISPLAY_ID_MIN = 0x01;
const byte DISPLAY_ID_MAX = 0x09;

// Bits 1 through 9 are enabled. Bit 0 is unused.
const uint16_t DISPLAY_ENABLED_MASK = 0x03FEU;

const unsigned long RS485_INTERFRAME_GAP_MS   = 15UL;
const unsigned long RS485_ID1_EXTRA_GAP_MS    = 20UL;
const unsigned int  RS485_TURNAROUND_GUARD_US = 400U;
const unsigned int  RS485_PRETX_GUARD_US      = 80U;
const byte RS485_ID1_RETRY_COUNT = 1;

// =============================================================
// DISPLAY MESSAGE STATE
// =============================================================

enum DisplayMode : byte {
    DISPLAY_MODE_NONE,
    DISPLAY_MODE_GRN,
    DISPLAY_MODE_RED,
    DISPLAY_MODE_ORG,
    DISPLAY_MODE_STATIC
};

DisplayMode currentDisplayMode = DISPLAY_MODE_NONE;

const byte DISPLAY_MESSAGE_BUFFER_SIZE = 96;
const byte VEHICLE_BUFFER_SIZE = 25;

char lastDisplayMessage[DISPLAY_MESSAGE_BUFFER_SIZE] = "";
char lastVehicle[VEHICLE_BUFFER_SIZE] = "";

// The latest IN and OUT lane messages are retained independently.
// Only the compact state and vehicle text are stored; the complete
// RS485 string is built in lastDisplayMessage only when transmitted.
enum LaneDisplayMessageType : byte {
    LANE_DISPLAY_NONE,
    LANE_DISPLAY_RED_STAGE_1,
    LANE_DISPLAY_RED_STAGE_2,
    LANE_DISPLAY_ORG,
    LANE_DISPLAY_GRN
};

enum LastDisplayedLane : byte {
    LAST_DISPLAYED_LANE_NONE,
    LAST_DISPLAYED_LANE_IN,
    LAST_DISPLAYED_LANE_OUT
};

LaneDisplayMessageType inLaneDisplayType = LANE_DISPLAY_NONE;
LaneDisplayMessageType outLaneDisplayType = LANE_DISPLAY_NONE;

char inLaneVehicle[VEHICLE_BUFFER_SIZE] = "";
char outLaneVehicle[VEHICLE_BUFFER_SIZE] = "";

bool laneDisplayCycleActive = false;
LastDisplayedLane lastDisplayedLane = LAST_DISPLAYED_LANE_NONE;
unsigned long laneDisplaySlotStartTime = 0;

const unsigned long TIMER_DISABLED = 0xFFFFFFFFUL;

unsigned long lastDisplaySendTime = 0;
unsigned long redStageStartTime = TIMER_DISABLED;
unsigned long redVehicleDisplayStartTime = TIMER_DISABLED;

const unsigned long DISPLAY_REPEAT_INTERVAL_MS = 1000UL;
const unsigned long LANE_DISPLAY_SLOT_MS         = 10000UL;
const unsigned long RED_FIRST_STAGE_TIME_MS      = 5000UL;
const unsigned long ORG_AFTER_RED_DELAY_MS      = 5000UL;

bool orgPendingAfterRed = false;
bool orgDelayCounting = false;
unsigned long orgDelayStartTime = 0;

const unsigned long GRN_BRAND_INTERVAL_MS = 180000UL;
const unsigned long GRN_BRAND_SHOW_MS     = 10000UL;

bool grnBrandActive = false;
unsigned long grnBrandMarkerTime = 0;
unsigned long grnBrandShowStartTime = 0;

// Keep fixed display text in flash instead of consuming Uno SRAM.
const char READY_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><R><Ready...!!! >";

const char ORG_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><G><Process Completed, GO...!!! >";

const char GRN_BRAND_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><R><RealTech Systems >";

const char IN_RED_STAGE_1_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><B><Vehicle Detected - IN Lane >";

const char OUT_RED_STAGE_1_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><B><Vehicle Detected - OUT Lane >";

const char IN_ORG_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><K><IN Process Completed, GO... >";

const char OUT_ORG_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><K><OUT Process Completed, GO... >";

const char IN_GRN_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><G><IN Lane Ready - WELCOME... >";

const char OUT_GRN_DISPLAY_MESSAGE[] PROGMEM =
    "<L3><F1><S1><G><OUT Lane Ready - THANK YOU... >";

// =============================================================
// BOOM-BARRIER RELAY STATE
// =============================================================

const unsigned long RELAY_PULSE_MS = 3000UL;
const unsigned long CLOSE_DELAY_MS = 1000UL;

const byte RELAY_PINS[] = {
    IN_BB_OPEN_PIN,
    OUT_BB_OPEN_PIN,
    IN_BB_CLOSE_PIN,
    OUT_BB_CLOSE_PIN
};

const byte RELAY_COUNT = sizeof(RELAY_PINS) / sizeof(RELAY_PINS[0]);

bool relayActive[RELAY_COUNT] = {false, false, false, false};
unsigned long relayStartTime[RELAY_COUNT] = {0, 0, 0, 0};

bool inBBPendingClose  = false;
bool outBBPendingClose = false;

unsigned long inBBCloseCommandTime  = 0;
unsigned long outBBCloseCommandTime = 0;

// =============================================================
// AUTOMATIC BOOM-BARRIER CLOSING
// =============================================================

const unsigned long AUTO_CLOSE_BB_INTERVAL_MS = 300000UL;

unsigned long lastSerialCommandActivityTime = 0;
unsigned long lastAutoCloseBBCheckTime = 0;

// =============================================================
// PHOTOELECTRIC SENSOR STATE
// =============================================================

// Existing IN/OUT DETECT sensors use analog hysteresis, exactly like the
// Uno version. Thresholds are scaled for ESP32 12-bit ADC (0..4095).
const int SENSOR_HIGH_THRESHOLD = 2800;
const int SENSOR_LOW_THRESHOLD  = 1200;

const unsigned long SENSOR_DETECT_CONFIRM_MS = 150UL;
const unsigned long SENSOR_LOW_CONFIRM_MS    = 300UL;

enum SensorLane : byte {
    SENSOR_LANE_IN,
    SENSOR_LANE_OUT
};

struct FilteredSensor {
    byte pin;
    SensorLane lane;
    bool filteredState;
    bool candidateState;
    unsigned long candidateStartTime;
    bool armed;
};

FilteredSensor inVehicleSensor = {
    IN_PE_SENSOR_PIN,
    SENSOR_LANE_IN,
    false,
    false,
    0,
    false
};

FilteredSensor outVehicleSensor = {
    OUT_PE_SENSOR_PIN,
    SENSOR_LANE_OUT,
    false,
    false,
    0,
    false
};

// =============================================================
// IN/OUT RELEASE PHOTOELECTRIC SENSOR STATE
// =============================================================

// RELEASE PE sensors are separate digital inputs.
// A confirmed HIGH arms the sensor; the following confirmed LOW reports release.
const unsigned long RELEASE_SENSOR_HIGH_CONFIRM_MS = 150UL;
const unsigned long RELEASE_SENSOR_LOW_CONFIRM_MS  = 300UL;

struct ReleaseSensor {
    byte pin;
    SensorLane lane;
    bool filteredState;
    bool candidateState;
    unsigned long candidateStartTime;
    bool sawConfirmedHigh;
};

ReleaseSensor inReleaseSensor = {
    IN_RELEASE_SENSOR_PIN, SENSOR_LANE_IN, false, false, 0, false
};

ReleaseSensor outReleaseSensor = {
    OUT_RELEASE_SENSOR_PIN, SENSOR_LANE_OUT, false, false, 0, false
};

// =============================================================
// SERIAL COMMAND PARSER AND FIFO
// =============================================================

// 55 visible characters plus the terminating NUL.
const byte SERIAL_COMMAND_BUFFER_SIZE = 56;
const byte SERIAL_COMMAND_QUEUE_CAPACITY = 6;
const unsigned long SERIAL_COMMAND_IDLE_TIMEOUT_MS = 1000UL;

char serialBuffer[SERIAL_COMMAND_BUFFER_SIZE] = "";
byte serialBufferLength = 0;
bool serialBufferOverflow = false;
unsigned long lastSerialByteTime = 0;

char serialCommandQueue[SERIAL_COMMAND_QUEUE_CAPACITY]
                       [SERIAL_COMMAND_BUFFER_SIZE];
byte serialCommandQueueHead = 0;
byte serialCommandQueueTail = 0;
byte serialCommandQueueCount = 0;

// Bluetooth has its own receive buffer, but completed commands are placed
// into the SAME FIFO used by USB Serial.
char bluetoothBuffer[SERIAL_COMMAND_BUFFER_SIZE] = "";
byte bluetoothBufferLength = 0;
bool bluetoothBufferOverflow = false;
unsigned long lastBluetoothByteTime = 0;

// Wi-Fi TCP has its own receive buffer. Completed commands go into the
// same FIFO as USB Serial and Bluetooth commands.
char wifiBuffer[SERIAL_COMMAND_BUFFER_SIZE] = "";
byte wifiBufferLength = 0;
bool wifiBufferOverflow = false;
unsigned long lastWiFiByteTime = 0;
unsigned long lastWiFiReconnectAttemptTime = 0;
bool wifiServerStarted = false;
const unsigned long WIFI_RECONNECT_INTERVAL_MS = 10000UL;

// =============================================================
// FORWARD DECLARATIONS
// =============================================================

void processSerialCommand(char *command);
void processNextQueuedSerialCommand();
void processBluetoothInput();
void processWiFiInput();
void maintainWiFi();
void sendToDisplay(const char *asciiText);
void executeORGCommand();
void updateLaneDisplayCycle();

// =============================================================
// SMALL TEXT HELPERS
// =============================================================

void trimInPlace(char *text) {
    if (text == NULL || text[0] == '\0') {
        return;
    }

    char *start = text;
    while (*start != '\0' && isspace((unsigned char)*start)) {
        start++;
    }

    if (start != text) {
        memmove(text, start, strlen(start) + 1U);
    }

    size_t length = strlen(text);
    while (length > 0U &&
           isspace((unsigned char)text[length - 1U])) {
        text[length - 1U] = '\0';
        length--;
    }
}

void uppercaseInPlace(char *text) {
    while (*text != '\0') {
        *text = (char)toupper((unsigned char)*text);
        text++;
    }
}

bool commandEquals(const char *command, PGM_P expected) {
    return strcmp_P(command, expected) == 0;
}

bool commandStartsWith(const char *command, PGM_P prefix) {
    const size_t prefixLength = strlen_P(prefix);
    return strncmp_P(command, prefix, prefixLength) == 0;
}

void copyText(char *destination,
              size_t destinationSize,
              const char *source) {
    if (destinationSize == 0U) {
        return;
    }

    strncpy(destination, source, destinationSize - 1U);
    destination[destinationSize - 1U] = '\0';
}

// =============================================================
// SERIAL DEBUG / STATUS HELPERS
// =============================================================

void printCommandOK(const __FlashStringHelper *action) {
    BOTH_PRINT(F("OK: "));
    BOTH_PRINTLN(action);
}

void printOutputState(const __FlashStringHelper *name, byte pin) {
    BOTH_PRINT(name);
    BOTH_PRINT(F("=GPIO"));
    BOTH_PRINT(pin);
    BOTH_PRINT(F(":"));
    BOTH_PRINTLN(digitalRead(pin) == OUTPUT_ON_LEVEL ? F("ON") : F("OFF"));
}

void printSignalState(const __FlashStringHelper *name, byte pin) {
    BOTH_PRINT(name);
    BOTH_PRINT(F("=GPIO"));
    BOTH_PRINT(pin);
    BOTH_PRINT(F(":"));
    BOTH_PRINT(digitalRead(pin) == SIGNAL_ON_LEVEL ? F("ON") : F("OFF"));
    BOTH_PRINT(F(" RAW="));
    BOTH_PRINTLN(digitalRead(pin) == HIGH ? F("HIGH") : F("LOW"));
}

void printStatus() {
    BOTH_PRINTLN(F("--- STATUS ---"));
    printSignalState(F("IN RED"), IN_RED_PIN);
    printSignalState(F("IN GRN"), IN_GREEN_PIN);
    printSignalState(F("IN ORG"), IN_ORANGE_PIN);
    printOutputState(F("IN BUZZER"), IN_BUZZER_PIN);
    printSignalState(F("OUT RED"), OUT_RED_PIN);
    printSignalState(F("OUT GRN"), OUT_GREEN_PIN);
    printSignalState(F("OUT ORG"), OUT_ORANGE_PIN);
    printOutputState(F("OUT BUZZER"), OUT_BUZZER_PIN);
    printOutputState(F("IN BB OPEN"), IN_BB_OPEN_PIN);
    printOutputState(F("IN BB CLOSE"), IN_BB_CLOSE_PIN);
    printOutputState(F("OUT BB OPEN"), OUT_BB_OPEN_PIN);
    printOutputState(F("OUT BB CLOSE"), OUT_BB_CLOSE_PIN);
    BOTH_PRINT(F("IN PE ADC=")); BOTH_PRINTLN(analogRead(IN_PE_SENSOR_PIN));
    BOTH_PRINT(F("OUT PE ADC=")); BOTH_PRINTLN(analogRead(OUT_PE_SENSOR_PIN));
    BOTH_PRINT(F("IN RELEASE=")); BOTH_PRINTLN(digitalRead(IN_RELEASE_SENSOR_PIN));
    BOTH_PRINT(F("OUT RELEASE=")); BOTH_PRINTLN(digitalRead(OUT_RELEASE_SENSOR_PIN));
    BOTH_PRINT(F("WIFI=")); BOTH_PRINTLN(WiFi.status() == WL_CONNECTED ? F("CONNECTED") : F("DISCONNECTED"));
    if (WiFi.status() == WL_CONNECTED) {
        BOTH_PRINT(F("WIFI IP=")); BOTH_PRINTLN(WiFi.localIP());
        BOTH_PRINT(F("WIFI TCP PORT=")); BOTH_PRINTLN(WIFI_TCP_PORT);
    }
    BOTH_PRINTLN(F("--------------"));
}

void printHelp() {
    BOTH_PRINTLN(F("Commands:"));
    BOTH_PRINTLN(F("IN GRN | IN ORG | IN RED | IN RED <VEHICLE>"));
    BOTH_PRINTLN(F("OUT GRN | OUT ORG | OUT RED | OUT RED <VEHICLE>"));
    BOTH_PRINTLN(F("IN BUZZER | IN BUZZER OFF | IN ALL OFF"));
    BOTH_PRINTLN(F("OUT BUZZER | OUT BUZZER OFF | OUT ALL OFF"));
    BOTH_PRINTLN(F("OPEN IN BB | CLOSE IN BB | OPEN OUT BB | CLOSE OUT BB"));
    BOTH_PRINTLN(F("AUTO CLOSE BB | GRN | ORG | RED <VEHICLE>"));
    BOTH_PRINTLN(F("STATUS | HELP"));
}

// =============================================================
// SIGNAL LIGHT AND BUZZER HELPERS
// =============================================================

void turnOffInSignalLights() {
    inOrangeBlinkActive = false;
    digitalWrite(IN_RED_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(IN_GREEN_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(IN_ORANGE_PIN, SIGNAL_OFF_LEVEL);
}

void turnOffOutSignalLights() {
    outOrangeBlinkActive = false;
    digitalWrite(OUT_RED_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(OUT_GREEN_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(OUT_ORANGE_PIN, SIGNAL_OFF_LEVEL);
}

void setInRed() {
    inOrangeBlinkActive = false;
    digitalWrite(IN_RED_PIN, SIGNAL_ON_LEVEL);
    digitalWrite(IN_GREEN_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(IN_ORANGE_PIN, SIGNAL_OFF_LEVEL);
}

void setInGreen() {
    inOrangeBlinkActive = false;
    digitalWrite(IN_RED_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(IN_GREEN_PIN, SIGNAL_ON_LEVEL);
    digitalWrite(IN_ORANGE_PIN, SIGNAL_OFF_LEVEL);
}

void setInOrange() {
    digitalWrite(IN_RED_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(IN_GREEN_PIN, SIGNAL_OFF_LEVEL);

    // Start ORG visibly ON, then let loop() blink it without delay().
    digitalWrite(IN_ORANGE_PIN, SIGNAL_ON_LEVEL);
    inOrangeBlinkActive = true;
    inOrangeBlinkTime = millis();
}

void setOutRed() {
    outOrangeBlinkActive = false;
    digitalWrite(OUT_RED_PIN, SIGNAL_ON_LEVEL);
    digitalWrite(OUT_GREEN_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(OUT_ORANGE_PIN, SIGNAL_OFF_LEVEL);
}

void setOutGreen() {
    outOrangeBlinkActive = false;
    digitalWrite(OUT_RED_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(OUT_GREEN_PIN, SIGNAL_ON_LEVEL);
    digitalWrite(OUT_ORANGE_PIN, SIGNAL_OFF_LEVEL);
}

void setOutOrange() {
    digitalWrite(OUT_RED_PIN, SIGNAL_OFF_LEVEL);
    digitalWrite(OUT_GREEN_PIN, SIGNAL_OFF_LEVEL);

    // Start ORG visibly ON, then let loop() blink it without delay().
    digitalWrite(OUT_ORANGE_PIN, SIGNAL_ON_LEVEL);
    outOrangeBlinkActive = true;
    outOrangeBlinkTime = millis();
}

void updateOrangeSignalBlinking() {
    const unsigned long now = millis();

    if (inOrangeBlinkActive &&
        now - inOrangeBlinkTime >= ORANGE_BLINK_INTERVAL_MS) {
        inOrangeBlinkTime = now;
        digitalWrite(IN_ORANGE_PIN,
                     digitalRead(IN_ORANGE_PIN) == SIGNAL_ON_LEVEL
                         ? SIGNAL_OFF_LEVEL
                         : SIGNAL_ON_LEVEL);
    }

    if (outOrangeBlinkActive &&
        now - outOrangeBlinkTime >= ORANGE_BLINK_INTERVAL_MS) {
        outOrangeBlinkTime = now;
        digitalWrite(OUT_ORANGE_PIN,
                     digitalRead(OUT_ORANGE_PIN) == SIGNAL_ON_LEVEL
                         ? SIGNAL_OFF_LEVEL
                         : SIGNAL_ON_LEVEL);
    }
}

void turnOffAllInOutputs() {
    // Match Uno: ALL OFF applies to the lane signal lights.
    turnOffInSignalLights();
}

void turnOffAllOutOutputs() {
    // Match Uno: ALL OFF applies to the lane signal lights.
    turnOffOutSignalLights();
}

// =============================================================
// BOOM-BARRIER HELPERS
// =============================================================

void stopRelayPulse(byte pin) {
    for (byte index = 0; index < RELAY_COUNT; index++) {
        if (RELAY_PINS[index] == pin) {
            digitalWrite(pin, OUTPUT_OFF_LEVEL);
            relayActive[index] = false;
            return;
        }
    }
}

byte oppositeBarrierRelay(byte pin) {
    if (pin == IN_BB_OPEN_PIN) return IN_BB_CLOSE_PIN;
    if (pin == IN_BB_CLOSE_PIN) return IN_BB_OPEN_PIN;
    if (pin == OUT_BB_OPEN_PIN) return OUT_BB_CLOSE_PIN;
    if (pin == OUT_BB_CLOSE_PIN) return OUT_BB_OPEN_PIN;
    return 0xFF;
}

void startRelayPulse(byte pin) {
    // Never allow OPEN and CLOSE relays for the same barrier to be active
    // at the same time. This protects the barrier controller interface.
    const byte oppositePin = oppositeBarrierRelay(pin);
    if (oppositePin != 0xFF) {
        stopRelayPulse(oppositePin);
    }

    // An OPEN command cancels any delayed CLOSE that has not fired yet.
    if (pin == IN_BB_OPEN_PIN) inBBPendingClose = false;
    if (pin == OUT_BB_OPEN_PIN) outBBPendingClose = false;

    for (byte index = 0; index < RELAY_COUNT; index++) {
        if (RELAY_PINS[index] != pin) {
            continue;
        }

        digitalWrite(pin, OUTPUT_ON_LEVEL);
        relayActive[index] = true;
        relayStartTime[index] = millis();
        return;
    }
}

void updateRelayPulses() {
    const unsigned long now = millis();

    for (byte index = 0; index < RELAY_COUNT; index++) {
        if (!relayActive[index]) {
            continue;
        }

        if (now - relayStartTime[index] >= RELAY_PULSE_MS) {
            digitalWrite(RELAY_PINS[index], OUTPUT_OFF_LEVEL);
            relayActive[index] = false;
        }
    }
}

void scheduleInBarrierClose() {
    inBBCloseCommandTime = millis();
    inBBPendingClose = true;
}

void scheduleOutBarrierClose() {
    outBBCloseCommandTime = millis();
    outBBPendingClose = true;
}

void scheduleBothBarriersClose() {
    const unsigned long now = millis();

    inBBCloseCommandTime = now;
    outBBCloseCommandTime = now;
    inBBPendingClose = true;
    outBBPendingClose = true;
}

void updatePendingBarrierCloseCommands() {
    const unsigned long now = millis();

    if (inBBPendingClose &&
        now - inBBCloseCommandTime >= CLOSE_DELAY_MS) {
        startRelayPulse(IN_BB_CLOSE_PIN);
        inBBPendingClose = false;
    }

    if (outBBPendingClose &&
        now - outBBCloseCommandTime >= CLOSE_DELAY_MS) {
        startRelayPulse(OUT_BB_CLOSE_PIN);
        outBBPendingClose = false;
    }
}

// =============================================================
// PHOTOELECTRIC SENSOR HELPERS
// =============================================================

bool readSensorWithHysteresis(FilteredSensor &sensor) {
    const int adcValue = analogRead(sensor.pin);

    if (adcValue >= SENSOR_HIGH_THRESHOLD) {
        return true;
    }

    if (adcValue <= SENSOR_LOW_THRESHOLD) {
        return false;
    }

    return sensor.candidateState;
}

void initialiseSensor(FilteredSensor &sensor) {
    const bool initialState = readSensorWithHysteresis(sensor);

    sensor.filteredState = initialState;
    sensor.candidateState = initialState;
    sensor.candidateStartTime = millis();
    sensor.armed = !initialState;
}

void printSensorDetected(SensorLane lane) {
    if (lane == SENSOR_LANE_IN) {
        BOTH_PRINTLN(F("IN Detected"));
    } else {
        BOTH_PRINTLN(F("OUT Detected"));
    }
}

void updateSensor(FilteredSensor &sensor) {
    const unsigned long now = millis();
    const bool sampledState = readSensorWithHysteresis(sensor);

    if (sampledState != sensor.candidateState) {
        sensor.candidateState = sampledState;
        sensor.candidateStartTime = now;
        return;
    }

    if (sensor.candidateState == sensor.filteredState) {
        return;
    }

    const unsigned long confirmationTime = sensor.candidateState
        ? SENSOR_DETECT_CONFIRM_MS
        : SENSOR_LOW_CONFIRM_MS;

    if (now - sensor.candidateStartTime < confirmationTime) {
        return;
    }

    sensor.filteredState = sensor.candidateState;

    // Same as Uno: DETECT PE is detect-only. LOW only rearms it.
    if (!sensor.filteredState) {
        sensor.armed = true;
        return;
    }

    if (sensor.armed) {
        printSensorDetected(sensor.lane);
        sensor.armed = false;
    }
}

void updateVehicleSensors() {
    updateSensor(inVehicleSensor);
    updateSensor(outVehicleSensor);
}

// =============================================================
// RELEASE PHOTOELECTRIC SENSOR HELPERS
// =============================================================

void initialiseReleaseSensor(ReleaseSensor &sensor) {
    const bool initialState = (digitalRead(sensor.pin) == HIGH);

    sensor.filteredState = initialState;
    sensor.candidateState = initialState;
    sensor.candidateStartTime = millis();

    // Same as Uno: require a fresh confirmed HIGH after power-up.
    sensor.sawConfirmedHigh = false;
}

void executeReleaseAction(SensorLane lane) {
    if (lane == SENSOR_LANE_IN) {
        // Notification-only, exactly like Uno V1.4.
        BOTH_PRINTLN(F("IN Released"));
    } else {
        // Notification-only, exactly like Uno V1.4.
        BOTH_PRINTLN(F("OUT Released"));
    }
}

void updateReleaseSensor(ReleaseSensor &sensor) {
    const unsigned long now = millis();
    const bool sampledState = (digitalRead(sensor.pin) == HIGH);

    if (sampledState != sensor.candidateState) {
        sensor.candidateState = sampledState;
        sensor.candidateStartTime = now;
        return;
    }

    if (sensor.candidateState == sensor.filteredState) {
        return;
    }

    const unsigned long confirmationTime = sensor.candidateState
        ? RELEASE_SENSOR_HIGH_CONFIRM_MS
        : RELEASE_SENSOR_LOW_CONFIRM_MS;

    if (now - sensor.candidateStartTime < confirmationTime) {
        return;
    }

    sensor.filteredState = sensor.candidateState;

    if (sensor.filteredState) {
        // Confirmed HIGH arms the next confirmed HIGH -> LOW release.
        sensor.sawConfirmedHigh = true;
        // Match Uno: do not print anything on HIGH.
        return;
    }

    if (sensor.sawConfirmedHigh) {
        sensor.sawConfirmedHigh = false;
        executeReleaseAction(sensor.lane);
    }
}

void updateReleaseSensors() {
    updateReleaseSensor(inReleaseSensor);
    updateReleaseSensor(outReleaseSensor);
}

// =============================================================
// LOW-LEVEL RS485 DISPLAY TRANSMISSION
// =============================================================

bool isDisplayEnabled(byte displayID) {
    return (DISPLAY_ENABLED_MASK & (1U << displayID)) != 0U;
}

void rs485SendFrame(byte displayID, const char *asciiText) {
    digitalWrite(RS485_DIR, HIGH);

    delayMicroseconds(RS485_PRETX_GUARD_US);

    for (byte index = 0;
         index < sizeof(displayHeader);
         index++) {
        if (index == 0) {
            rs485.write(displayID);
        } else {
            rs485.write(pgm_read_byte(&displayHeader[index]));
        }
    }

    while (*asciiText != '\0') {
        rs485.write((byte)*asciiText);
        asciiText++;
    }

    rs485.flush();
    delayMicroseconds(RS485_TURNAROUND_GUARD_US);

    digitalWrite(RS485_DIR, LOW);
}

void sendToDisplayID(byte displayID, const char *asciiText) {
    if (displayID < DISPLAY_ID_MIN ||
        displayID > DISPLAY_ID_MAX ||
        !isDisplayEnabled(displayID)) {
        return;
    }

    rs485SendFrame(displayID, asciiText);

    if (displayID == 0x01 && RS485_ID1_RETRY_COUNT > 0) {
        for (byte retry = 0;
             retry < RS485_ID1_RETRY_COUNT;
             retry++) {
            delay(RS485_INTERFRAME_GAP_MS);
            rs485SendFrame(displayID, asciiText);
        }
    }
}

void sendToDisplay(const char *asciiText) {
    for (byte displayID = DISPLAY_ID_MIN;
         displayID <= DISPLAY_ID_MAX;
         displayID++) {
        if (!isDisplayEnabled(displayID)) {
            continue;
        }

        sendToDisplayID(displayID, asciiText);
        delay(RS485_INTERFRAME_GAP_MS);

        if (displayID == 0x01) {
            delay(RS485_ID1_EXTRA_GAP_MS);
        }
    }
}

// =============================================================
// DISPLAY STATE HELPERS
// =============================================================

void sendStoredDisplayMessage() {
    if (lastDisplayMessage[0] == '\0') {
        return;
    }

    sendToDisplay(lastDisplayMessage);
    lastDisplaySendTime = millis();
}

void setAndSendDisplayMessageRam(const char *message) {
    if (message != lastDisplayMessage) {
        copyText(lastDisplayMessage,
                 sizeof(lastDisplayMessage),
                 message);
    }

    sendStoredDisplayMessage();
}

void setAndSendDisplayMessageFlash(PGM_P message) {
    strncpy_P(lastDisplayMessage,
              message,
              sizeof(lastDisplayMessage) - 1U);
    lastDisplayMessage[sizeof(lastDisplayMessage) - 1U] = '\0';
    sendStoredDisplayMessage();
}

void cancelDelayedORGCommand() {
    orgPendingAfterRed = false;
    orgDelayCounting = false;
    orgDelayStartTime = 0;
}

void resetGRNBrandTimer() {
    grnBrandActive = false;
    grnBrandMarkerTime = millis();
    grnBrandShowStartTime = 0;
}

void stopGRNBrandDisplay() {
    grnBrandActive = false;
    grnBrandShowStartTime = 0;
}

void startORGDelayAfterRedVehicleDisplay() {
    orgPendingAfterRed = true;
    orgDelayCounting = true;
    orgDelayStartTime = millis();
}

bool laneHasDisplayMessage(bool isInLane) {
    return isInLane
        ? inLaneDisplayType != LANE_DISPLAY_NONE
        : outLaneDisplayType != LANE_DISPLAY_NONE;
}

void disableLaneDisplayCycle() {
    laneDisplayCycleActive = false;
    lastDisplayedLane = LAST_DISPLAYED_LANE_NONE;
    laneDisplaySlotStartTime = 0;
}

void prepareLaneDisplayCycleState() {
    currentDisplayMode = DISPLAY_MODE_STATIC;

    stopGRNBrandDisplay();
    cancelDelayedORGCommand();

    lastVehicle[0] = '\0';
    redStageStartTime = TIMER_DISABLED;
    redVehicleDisplayStartTime = TIMER_DISABLED;

    laneDisplayCycleActive = true;
}

bool buildLaneDisplayMessage(bool isInLane) {
    const LaneDisplayMessageType messageType = isInLane
        ? inLaneDisplayType
        : outLaneDisplayType;

    const char *vehicle = isInLane
        ? inLaneVehicle
        : outLaneVehicle;

    switch (messageType) {
        case LANE_DISPLAY_RED_STAGE_1:
            strncpy_P(
                lastDisplayMessage,
                isInLane
                    ? IN_RED_STAGE_1_DISPLAY_MESSAGE
                    : OUT_RED_STAGE_1_DISPLAY_MESSAGE,
                sizeof(lastDisplayMessage) - 1U);
            lastDisplayMessage[sizeof(lastDisplayMessage) - 1U] = '\0';
            return true;

        case LANE_DISPLAY_RED_STAGE_2:
            snprintf_P(
                lastDisplayMessage,
                sizeof(lastDisplayMessage),
                isInLane
                    ? PSTR("<L3><F1><S1><K><%s - IN >")
                    : PSTR("<L3><F1><S1><K><%s - OUT >"),
                vehicle);
            return true;

        case LANE_DISPLAY_ORG:
            strncpy_P(
                lastDisplayMessage,
                isInLane
                    ? IN_ORG_DISPLAY_MESSAGE
                    : OUT_ORG_DISPLAY_MESSAGE,
                sizeof(lastDisplayMessage) - 1U);
            lastDisplayMessage[sizeof(lastDisplayMessage) - 1U] = '\0';
            return true;

        case LANE_DISPLAY_GRN:
            strncpy_P(
                lastDisplayMessage,
                isInLane
                    ? IN_GRN_DISPLAY_MESSAGE
                    : OUT_GRN_DISPLAY_MESSAGE,
                sizeof(lastDisplayMessage) - 1U);
            lastDisplayMessage[sizeof(lastDisplayMessage) - 1U] = '\0';
            return true;

        default:
            return false;
    }
}

bool sendLaneDisplayMessage(bool isInLane) {
    if (!buildLaneDisplayMessage(isInLane)) {
        return false;
    }

    sendStoredDisplayMessage();
    lastDisplayedLane = isInLane
        ? LAST_DISPLAYED_LANE_IN
        : LAST_DISPLAYED_LANE_OUT;
    laneDisplaySlotStartTime = millis();
    return true;
}

void promoteCompletedRedStage(bool isInLane) {
    LaneDisplayMessageType &messageType = isInLane
        ? inLaneDisplayType
        : outLaneDisplayType;

    if (messageType == LANE_DISPLAY_RED_STAGE_1) {
        messageType = LANE_DISPLAY_RED_STAGE_2;
    }
}

void setLaneDisplayMessage(bool isInLane,
                           LaneDisplayMessageType messageType,
                           const char *vehicle) {
    prepareLaneDisplayCycleState();

    if (isInLane) {
        inLaneDisplayType = messageType;

        if ((messageType == LANE_DISPLAY_RED_STAGE_1 ||
             messageType == LANE_DISPLAY_RED_STAGE_2) &&
            vehicle != NULL) {
            copyText(inLaneVehicle, sizeof(inLaneVehicle), vehicle);
        } else {
            inLaneVehicle[0] = '\0';
        }
    } else {
        outLaneDisplayType = messageType;

        if ((messageType == LANE_DISPLAY_RED_STAGE_1 ||
             messageType == LANE_DISPLAY_RED_STAGE_2) &&
            vehicle != NULL) {
            copyText(outLaneVehicle, sizeof(outLaneVehicle), vehicle);
        } else {
            outLaneVehicle[0] = '\0';
        }
    }

    const LastDisplayedLane updatedLane = isInLane
        ? LAST_DISPLAYED_LANE_IN
        : LAST_DISPLAYED_LANE_OUT;

    // The first command starts immediately. A new command for the lane
    // already on-screen also starts immediately. A command for the other
    // lane waits for the current lane's complete 10-second display slot.
    if (lastDisplayedLane == LAST_DISPLAYED_LANE_NONE ||
        lastDisplayedLane == updatedLane) {
        sendLaneDisplayMessage(isInLane);
    }
}

void updateLaneDisplayCycle() {
    if (!laneDisplayCycleActive) {
        return;
    }

    const bool hasInMessage = laneHasDisplayMessage(true);
    const bool hasOutMessage = laneHasDisplayMessage(false);

    if (!hasInMessage && !hasOutMessage) {
        disableLaneDisplayCycle();
        return;
    }

    if (lastDisplayedLane == LAST_DISPLAYED_LANE_NONE) {
        sendLaneDisplayMessage(hasInMessage);
        return;
    }

    if (millis() - laneDisplaySlotStartTime < LANE_DISPLAY_SLOT_MS) {
        return;
    }

    // A RED stage 1 gets exactly one complete 10-second display slot.
    // After that slot, the lane permanently changes to "<vehicle> - IN"
    // or "<vehicle> - OUT" until another command updates that lane.
    if (lastDisplayedLane == LAST_DISPLAYED_LANE_IN) {
        promoteCompletedRedStage(true);
    } else if (lastDisplayedLane == LAST_DISPLAYED_LANE_OUT) {
        promoteCompletedRedStage(false);
    }

    if (hasInMessage && hasOutMessage) {
        // Alternate forever, with one full 10-second slot per lane.
        sendLaneDisplayMessage(
            lastDisplayedLane != LAST_DISPLAYED_LANE_IN);
        return;
    }

    // Only one lane currently has a message. Start its next 10-second
    // slot. For RED, this sends stage 2 after stage 1 has completed.
    sendLaneDisplayMessage(hasInMessage);
}

void prepareStaticDisplayState() {
    currentDisplayMode = DISPLAY_MODE_STATIC;

    disableLaneDisplayCycle();
    stopGRNBrandDisplay();
    cancelDelayedORGCommand();

    lastVehicle[0] = '\0';
    redStageStartTime = TIMER_DISABLED;
    redVehicleDisplayStartTime = TIMER_DISABLED;
}

void executeStaticDisplayMessageFlash(PGM_P message) {
    prepareStaticDisplayState();
    setAndSendDisplayMessageFlash(message);
}

void executePreparedStaticDisplayMessage() {
    prepareStaticDisplayState();
    sendStoredDisplayMessage();
}

void executeGRNCommand() {
    currentDisplayMode = DISPLAY_MODE_GRN;

    disableLaneDisplayCycle();
    cancelDelayedORGCommand();
    resetGRNBrandTimer();

    lastVehicle[0] = '\0';
    redStageStartTime = TIMER_DISABLED;
    redVehicleDisplayStartTime = TIMER_DISABLED;

    setAndSendDisplayMessageFlash(READY_DISPLAY_MESSAGE);
}

void executeREDCommand(const char *vehicle) {
    if (vehicle[0] == '\0') {
        return;
    }

    currentDisplayMode = DISPLAY_MODE_RED;

    disableLaneDisplayCycle();
    stopGRNBrandDisplay();
    cancelDelayedORGCommand();

    copyText(lastVehicle, sizeof(lastVehicle), vehicle);
    redVehicleDisplayStartTime = TIMER_DISABLED;

    snprintf_P(lastDisplayMessage,
               sizeof(lastDisplayMessage),
               PSTR("<L3><F1><S1><B><Stop in WB, \"%s\" >"),
               lastVehicle);

    sendStoredDisplayMessage();
    redStageStartTime = millis();
}

void executeORGCommand() {
    currentDisplayMode = DISPLAY_MODE_ORG;

    disableLaneDisplayCycle();
    stopGRNBrandDisplay();
    cancelDelayedORGCommand();

    lastVehicle[0] = '\0';
    redStageStartTime = TIMER_DISABLED;
    redVehicleDisplayStartTime = TIMER_DISABLED;

    setAndSendDisplayMessageFlash(ORG_DISPLAY_MESSAGE);
}

void executeVehicleDetectedDisplay(bool isInLane,
                                   const char *vehicle) {
    if (vehicle == NULL || vehicle[0] == '\0') {
        return;
    }

    setLaneDisplayMessage(
        isInLane,
        LANE_DISPLAY_RED_STAGE_1,
        vehicle);
}

void requestORGCommand() {
    if (currentDisplayMode == DISPLAY_MODE_RED &&
        lastVehicle[0] != '\0') {
        orgPendingAfterRed = true;

        if (redVehicleDisplayStartTime != TIMER_DISABLED) {
            startORGDelayAfterRedVehicleDisplay();
        } else {
            orgDelayCounting = false;
        }

        return;
    }

    executeORGCommand();
}

void updateRedDisplayStage() {
    if (currentDisplayMode != DISPLAY_MODE_RED ||
        lastVehicle[0] == '\0' ||
        redStageStartTime == TIMER_DISABLED) {
        return;
    }

    if (millis() - redStageStartTime < RED_FIRST_STAGE_TIME_MS) {
        return;
    }

    snprintf_P(lastDisplayMessage,
               sizeof(lastDisplayMessage),
               PSTR("<L3><F1><S1><K><\"%s\" >"),
               lastVehicle);

    sendStoredDisplayMessage();

    redStageStartTime = TIMER_DISABLED;
    redVehicleDisplayStartTime = millis();

    if (orgPendingAfterRed && !orgDelayCounting) {
        startORGDelayAfterRedVehicleDisplay();
    }
}

void updateDelayedORGCommand() {
    if (!orgPendingAfterRed || !orgDelayCounting) {
        return;
    }

    if (millis() - orgDelayStartTime >= ORG_AFTER_RED_DELAY_MS) {
        executeORGCommand();
    }
}

void updateGRNBrandDisplay() {
    if (currentDisplayMode != DISPLAY_MODE_GRN) {
        return;
    }

    const unsigned long now = millis();

    if (grnBrandActive) {
        if (now - grnBrandShowStartTime >= GRN_BRAND_SHOW_MS) {
            setAndSendDisplayMessageFlash(READY_DISPLAY_MESSAGE);
            grnBrandActive = false;
            grnBrandMarkerTime = now;
            grnBrandShowStartTime = 0;
        }

        return;
    }

    if (now - grnBrandMarkerTime >= GRN_BRAND_INTERVAL_MS) {
        setAndSendDisplayMessageFlash(GRN_BRAND_DISPLAY_MESSAGE);
        grnBrandActive = true;
        grnBrandShowStartTime = now;
    }
}

void repeatCurrentDisplayMessage() {
    if (laneDisplayCycleActive) {
        updateLaneDisplayCycle();
        return;
    }

    if (lastDisplayMessage[0] == '\0') {
        return;
    }

    if (millis() - lastDisplaySendTime >=
        DISPLAY_REPEAT_INTERVAL_MS) {
        sendStoredDisplayMessage();
    }
}

// =============================================================
// SERIAL COMMAND HANDLER
// =============================================================

void processSerialCommand(char *command) {
    trimInPlace(command);

    if (command[0] == '\0') {
        return;
    }

    uppercaseInPlace(command);
    BOTH_PRINT(F("CMD: "));
    BOTH_PRINTLN(command);

    if (commandEquals(command, PSTR("HELP"))) {
        printHelp();
        return;
    }

    if (commandEquals(command, PSTR("STATUS"))) {
        printStatus();
        return;
    }

    // Prefix checks must be before exact IN RED / OUT RED checks.
    if (commandStartsWith(command, PSTR("IN RED "))) {
        char *vehicle = command + 7;
        trimInPlace(vehicle);
        if (vehicle[0] == '\0') {
            BOTH_PRINTLN(F("ERR: vehicle number missing"));
            return;
        }
        setInRed();
        executeVehicleDetectedDisplay(true, vehicle);
        printCommandOK(F("IN RED + DISPLAY"));
        return;
    }

    if (commandStartsWith(command, PSTR("OUT RED "))) {
        char *vehicle = command + 8;
        trimInPlace(vehicle);
        if (vehicle[0] == '\0') {
            BOTH_PRINTLN(F("ERR: vehicle number missing"));
            return;
        }
        setOutRed();
        executeVehicleDetectedDisplay(false, vehicle);
        printCommandOK(F("OUT RED + DISPLAY"));
        return;
    }

    if (commandEquals(command, PSTR("IN ORG"))) {
        setInOrange();
        setLaneDisplayMessage(true, LANE_DISPLAY_ORG, NULL);
        printCommandOK(F("IN ORG"));
        return;
    }
    if (commandEquals(command, PSTR("OUT ORG"))) {
        setOutOrange();
        setLaneDisplayMessage(false, LANE_DISPLAY_ORG, NULL);
        printCommandOK(F("OUT ORG"));
        return;
    }
    if (commandEquals(command, PSTR("IN GRN"))) {
        setInGreen();
        setLaneDisplayMessage(true, LANE_DISPLAY_GRN, NULL);
        printCommandOK(F("IN GRN"));
        return;
    }
    if (commandEquals(command, PSTR("OUT GRN"))) {
        setOutGreen();
        setLaneDisplayMessage(false, LANE_DISPLAY_GRN, NULL);
        printCommandOK(F("OUT GRN"));
        return;
    }

    // Legacy display-only commands.
    if (commandEquals(command, PSTR("GRN"))) {
        executeGRNCommand();
        printCommandOK(F("DISPLAY GRN"));
        return;
    }
    if (commandEquals(command, PSTR("ORG"))) {
        requestORGCommand();
        printCommandOK(F("DISPLAY ORG"));
        return;
    }
    if (commandStartsWith(command, PSTR("RED "))) {
        char *vehicle = command + 4;
        trimInPlace(vehicle);
        if (vehicle[0] == '\0') {
            BOTH_PRINTLN(F("ERR: vehicle number missing"));
            return;
        }
        executeREDCommand(vehicle);
        printCommandOK(F("DISPLAY RED"));
        return;
    }

    // Boom-barrier commands.
    if (commandEquals(command, PSTR("OPEN IN BB"))) {
        startRelayPulse(IN_BB_OPEN_PIN);
        printCommandOK(F("OPEN IN BB"));
        return;
    }
    if (commandEquals(command, PSTR("CLOSE IN BB"))) {
        scheduleInBarrierClose();
        printCommandOK(F("CLOSE IN BB SCHEDULED"));
        return;
    }
    if (commandEquals(command, PSTR("OPEN OUT BB"))) {
        startRelayPulse(OUT_BB_OPEN_PIN);
        printCommandOK(F("OPEN OUT BB"));
        return;
    }
    if (commandEquals(command, PSTR("CLOSE OUT BB"))) {
        scheduleOutBarrierClose();
        printCommandOK(F("CLOSE OUT BB SCHEDULED"));
        return;
    }
    if (commandEquals(command, PSTR("AUTO CLOSE BB"))) {
        scheduleBothBarriersClose();
        printCommandOK(F("AUTO CLOSE BB SCHEDULED"));
        return;
    }

    // IN signal and buzzer commands.
    if (commandEquals(command, PSTR("IN RED"))) {
        setInRed();
        printCommandOK(F("IN RED"));
        return;
    }
    if (commandEquals(command, PSTR("IN BUZZER"))) {
        digitalWrite(IN_BUZZER_PIN, OUTPUT_ON_LEVEL);
        printCommandOK(F("IN BUZZER ON"));
        return;
    }
    if (commandEquals(command, PSTR("IN BUZZER OFF"))) {
        digitalWrite(IN_BUZZER_PIN, OUTPUT_OFF_LEVEL);
        printCommandOK(F("IN BUZZER OFF"));
        return;
    }
    if (commandEquals(command, PSTR("IN ALL OFF"))) {
        turnOffAllInOutputs();
        printCommandOK(F("IN ALL OFF"));
        return;
    }

    // OUT signal and buzzer commands.
    if (commandEquals(command, PSTR("OUT RED"))) {
        setOutRed();
        printCommandOK(F("OUT RED"));
        return;
    }
    if (commandEquals(command, PSTR("OUT BUZZER"))) {
        digitalWrite(OUT_BUZZER_PIN, OUTPUT_ON_LEVEL);
        printCommandOK(F("OUT BUZZER ON"));
        return;
    }
    if (commandEquals(command, PSTR("OUT BUZZER OFF"))) {
        digitalWrite(OUT_BUZZER_PIN, OUTPUT_OFF_LEVEL);
        printCommandOK(F("OUT BUZZER OFF"));
        return;
    }
    if (commandEquals(command, PSTR("OUT ALL OFF"))) {
        turnOffAllOutOutputs();
        printCommandOK(F("OUT ALL OFF"));
        return;
    }

    BOTH_PRINTLN(F("ERR: unknown command. Send HELP"));
}

// =============================================================
// SERIAL INPUT PARSER
// =============================================================

bool enqueueSerialCommand(const char *command) {
    if (serialCommandQueueCount >= SERIAL_COMMAND_QUEUE_CAPACITY) {
        return false;
    }

    copyText(serialCommandQueue[serialCommandQueueTail],
             SERIAL_COMMAND_BUFFER_SIZE,
             command);

    serialCommandQueueTail =
        (serialCommandQueueTail + 1U) % SERIAL_COMMAND_QUEUE_CAPACITY;
    serialCommandQueueCount++;
    return true;
}

void finishCurrentSerialCommand() {
    if (serialBufferOverflow) {
        BOTH_PRINTLN(F("ERR: serial command too long"));
        serialBufferLength = 0;
        serialBuffer[0] = '\0';
        serialBufferOverflow = false;
        return;
    }

    serialBuffer[serialBufferLength] = '\0';
    trimInPlace(serialBuffer);

    if (serialBuffer[0] != '\0') {
        lastSerialCommandActivityTime = millis();
        lastAutoCloseBBCheckTime = millis();
        if (!enqueueSerialCommand(serialBuffer)) {
            BOTH_PRINTLN(F("ERR: command queue full"));
        }
    }

    serialBufferLength = 0;
    serialBuffer[0] = '\0';
}

void processNextQueuedSerialCommand() {
    if (serialCommandQueueCount == 0) {
        return;
    }

    processSerialCommand(serialCommandQueue[serialCommandQueueHead]);

    serialCommandQueueHead =
        (serialCommandQueueHead + 1U) % SERIAL_COMMAND_QUEUE_CAPACITY;
    serialCommandQueueCount--;
}

void processSerialInput() {
    while (Serial.available() > 0) {
        const char receivedCharacter = (char)Serial.read();
        lastSerialByteTime = millis();

        if (receivedCharacter == '\n' ||
            receivedCharacter == '\r') {
            if (serialBufferLength > 0 || serialBufferOverflow) {
                finishCurrentSerialCommand();
            }
            continue;
        }

        if (serialBufferOverflow) {
            continue;
        }

        if (serialBufferLength <
            SERIAL_COMMAND_BUFFER_SIZE - 1U) {
            serialBuffer[serialBufferLength] = receivedCharacter;
            serialBufferLength++;
            serialBuffer[serialBufferLength] = '\0';
        } else {
            serialBufferOverflow = true;
        }
    }

    // Also accept Serial.print("IN GRN") without CR/LF. The longer
    // idle timeout prevents a spaced vehicle ID from being split.
    if ((serialBufferLength > 0 || serialBufferOverflow) &&
        millis() - lastSerialByteTime >=
            SERIAL_COMMAND_IDLE_TIMEOUT_MS) {
        finishCurrentSerialCommand();
    }
}

// =============================================================
// BLUETOOTH INPUT PARSER
// =============================================================

void finishCurrentBluetoothCommand() {
    if (bluetoothBufferOverflow) {
        BOTH_PRINTLN(F("ERR: Bluetooth command too long"));
        bluetoothBufferLength = 0;
        bluetoothBuffer[0] = '\0';
        bluetoothBufferOverflow = false;
        return;
    }

    bluetoothBuffer[bluetoothBufferLength] = '\0';
    trimInPlace(bluetoothBuffer);

    if (bluetoothBuffer[0] != '\0') {
        lastSerialCommandActivityTime = millis();
        lastAutoCloseBBCheckTime = millis();

        if (!enqueueSerialCommand(bluetoothBuffer)) {
            BOTH_PRINTLN(F("ERR: command queue full"));
        }
    }

    bluetoothBufferLength = 0;
    bluetoothBuffer[0] = '\0';
}

void processBluetoothInput() {
    while (SerialBT.available() > 0) {
        const char receivedCharacter = (char)SerialBT.read();
        lastBluetoothByteTime = millis();

        if (receivedCharacter == '\n' ||
            receivedCharacter == '\r') {
            if (bluetoothBufferLength > 0 || bluetoothBufferOverflow) {
                finishCurrentBluetoothCommand();
            }
            continue;
        }

        if (bluetoothBufferOverflow) {
            continue;
        }

        if (bluetoothBufferLength <
            SERIAL_COMMAND_BUFFER_SIZE - 1U) {
            bluetoothBuffer[bluetoothBufferLength] = receivedCharacter;
            bluetoothBufferLength++;
            bluetoothBuffer[bluetoothBufferLength] = '\0';
        } else {
            bluetoothBufferOverflow = true;
        }
    }

    // Also accept a Bluetooth command sent without CR/LF.
    if ((bluetoothBufferLength > 0 || bluetoothBufferOverflow) &&
        millis() - lastBluetoothByteTime >=
            SERIAL_COMMAND_IDLE_TIMEOUT_MS) {
        finishCurrentBluetoothCommand();
    }
}

// =============================================================
// WI-FI TCP INPUT PARSER / CONNECTION MANAGEMENT
// =============================================================

void finishCurrentWiFiCommand() {
    if (wifiBufferOverflow) {
        BOTH_PRINTLN(F("ERR: WiFi command too long"));
        wifiBufferLength = 0;
        wifiBuffer[0] = '\0';
        wifiBufferOverflow = false;
        return;
    }

    wifiBuffer[wifiBufferLength] = '\0';
    trimInPlace(wifiBuffer);

    if (wifiBuffer[0] != '\0') {
        lastSerialCommandActivityTime = millis();
        lastAutoCloseBBCheckTime = millis();

        if (!enqueueSerialCommand(wifiBuffer)) {
            BOTH_PRINTLN(F("ERR: command queue full"));
        }
    }

    wifiBufferLength = 0;
    wifiBuffer[0] = '\0';
}

void maintainWiFi() {
    const unsigned long now = millis();

    if (WiFi.status() != WL_CONNECTED) {
        wifiServerStarted = false;
        if (now - lastWiFiReconnectAttemptTime >= WIFI_RECONNECT_INTERVAL_MS) {
            lastWiFiReconnectAttemptTime = now;
            WiFi.disconnect();
            WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
        }
        return;
    }

    if (!wifiServerStarted) {
        wifiServer.begin();
        wifiServer.setNoDelay(true);
        wifiServerStarted = true;
    }

    // Accept one TCP command client at a time. A new client replaces a
    // disconnected/old one; Bluetooth continues independently in parallel.
    if (!wifiClient || !wifiClient.connected()) {
        WiFiClient candidate = wifiServer.available();
        if (candidate) {
            wifiClient.stop();
            wifiClient = candidate;
            wifiClient.setNoDelay(true);
            wifiBufferLength = 0;
            wifiBuffer[0] = '\0';
            wifiBufferOverflow = false;
            lastWiFiByteTime = now;

            wifiClient.println(F("STP AUTO ESP32 WIFI CONNECTED"));
            wifiClient.print(F("IP: "));
            wifiClient.println(WiFi.localIP());
            wifiClient.print(F("TCP PORT: "));
            wifiClient.println(WIFI_TCP_PORT);
            wifiClient.println(F("Send HELP or STATUS."));
        }
    }
}

void processWiFiInput() {
    if (!wifiClient || !wifiClient.connected()) {
        return;
    }

    while (wifiClient.available() > 0) {
        const char receivedCharacter = (char)wifiClient.read();
        lastWiFiByteTime = millis();

        if (receivedCharacter == '\n' ||
            receivedCharacter == '\r') {
            if (wifiBufferLength > 0 || wifiBufferOverflow) {
                finishCurrentWiFiCommand();
            }
            continue;
        }

        if (wifiBufferOverflow) {
            continue;
        }

        if (wifiBufferLength < SERIAL_COMMAND_BUFFER_SIZE - 1U) {
            wifiBuffer[wifiBufferLength] = receivedCharacter;
            wifiBufferLength++;
            wifiBuffer[wifiBufferLength] = '\0';
        } else {
            wifiBufferOverflow = true;
        }
    }

    // Also accept a TCP command sent without CR/LF.
    if ((wifiBufferLength > 0 || wifiBufferOverflow) &&
        millis() - lastWiFiByteTime >= SERIAL_COMMAND_IDLE_TIMEOUT_MS) {
        finishCurrentWiFiCommand();
    }
}

// =============================================================
// AUTOMATIC CLOSE BACKGROUND TASK
// =============================================================

void updateAutoCloseBB() {
    const unsigned long now = millis();

    if (now - lastAutoCloseBBCheckTime <
        AUTO_CLOSE_BB_INTERVAL_MS) {
        return;
    }

    lastAutoCloseBBCheckTime = now;

    if (now - lastSerialCommandActivityTime <
        AUTO_CLOSE_BB_INTERVAL_MS) {
        return;
    }

    scheduleBothBarriersClose();
}

// =============================================================
// SETUP
// =============================================================

void setup() {
    pinMode(IN_RED_PIN, OUTPUT);
    pinMode(IN_GREEN_PIN, OUTPUT);
    pinMode(IN_ORANGE_PIN, OUTPUT);
    pinMode(IN_BUZZER_PIN, OUTPUT);

    pinMode(OUT_RED_PIN, OUTPUT);
    pinMode(OUT_GREEN_PIN, OUTPUT);
    pinMode(OUT_ORANGE_PIN, OUTPUT);
    pinMode(OUT_BUZZER_PIN, OUTPUT);

    pinMode(IN_BB_OPEN_PIN, OUTPUT);
    pinMode(OUT_BB_OPEN_PIN, OUTPUT);
    pinMode(IN_BB_CLOSE_PIN, OUTPUT);
    pinMode(OUT_BB_CLOSE_PIN, OUTPUT);

    pinMode(RS485_DIR, OUTPUT);

    pinMode(IN_PE_SENSOR_PIN, INPUT);
    pinMode(OUT_PE_SENSOR_PIN, INPUT);
    // Release sensors are defined as active-HIGH 0-3.3 V inputs.
    // Internal pulldowns prevent floating inputs from causing false releases.
    pinMode(IN_RELEASE_SENSOR_PIN, INPUT_PULLDOWN);
    pinMode(OUT_RELEASE_SENSOR_PIN, INPUT_PULLDOWN);

    turnOffAllInOutputs();
    turnOffAllOutOutputs();

    digitalWrite(IN_BB_OPEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_BB_OPEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(IN_BB_CLOSE_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_BB_CLOSE_PIN, OUTPUT_OFF_LEVEL);

    digitalWrite(RS485_DIR, LOW);

    Serial.begin(9600);

    // Bluetooth Classic SPP. The phone/PC sees this device name.
    const bool bluetoothStarted = SerialBT.begin(BLUETOOTH_DEVICE_NAME);

    // Wi-Fi station mode runs in parallel with Bluetooth Classic.
    WiFi.mode(WIFI_STA);
    WiFi.setAutoReconnect(true);
    WiFi.persistent(false);
    if (!WiFi.config(WIFI_LOCAL_IP, WIFI_GATEWAY, WIFI_SUBNET, WIFI_DNS1, WIFI_DNS2)) {
        Serial.println(F("ERR: WiFi static IP configuration failed"));
        SerialBT.println(F("ERR: WiFi static IP configuration failed"));
    }
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    // Do not block startup waiting for Wi-Fi. Bluetooth, USB, sensors, RS485,
    // lights, buzzers, and barriers start immediately and Wi-Fi reconnects
    // automatically in loop().
    const unsigned long wifiConnectStart = millis();
    while (WiFi.status() != WL_CONNECTED && millis() - wifiConnectStart < 3000UL) {
        delay(50);
    }
    if (WiFi.status() == WL_CONNECTED) {
        wifiServer.begin();
        wifiServer.setNoDelay(true);
        wifiServerStarted = true;
    }

    // Existing PE sensor logic uses analogRead(). ESP32 ADC is 12-bit here.
    analogReadResolution(12);
    analogSetPinAttenuation(IN_PE_SENSOR_PIN, ADC_11db);
    analogSetPinAttenuation(OUT_PE_SENSOR_PIN, ADC_11db);

    // Hardware UART2 for RS485 display communication.
    rs485.begin(9600, SERIAL_8N1, RS485_RO, RS485_DI);

    const unsigned long now = millis();
    lastSerialCommandActivityTime = now;
    lastAutoCloseBBCheckTime = now;
    lastSerialByteTime = now;
    lastBluetoothByteTime = now;
    lastWiFiByteTime = now;
    lastWiFiReconnectAttemptTime = now;

    initialiseSensor(inVehicleSensor);
    initialiseSensor(outVehicleSensor);
    initialiseReleaseSensor(inReleaseSensor);
    initialiseReleaseSensor(outReleaseSensor);

    Serial.println();
    SerialBT.println();
    BOTH_PRINTLN(F("STP AUTO ESP32 V1.6 WIFI + BLUETOOTH READY"));
    BOTH_PRINTLN(F("USB Serial: 9600 baud. Send HELP or STATUS."));
    if (bluetoothStarted) {
        BOTH_PRINT(F("Bluetooth Classic SPP: "));
        BOTH_PRINTLN(BLUETOOTH_DEVICE_NAME);
    } else {
        BOTH_PRINTLN(F("ERR: Bluetooth failed to start"));
    }
    if (WiFi.status() == WL_CONNECTED) {
        BOTH_PRINT(F("WiFi connected, static IP: "));
        BOTH_PRINTLN(WiFi.localIP());
        BOTH_PRINT(F("WiFi TCP command port: "));
        BOTH_PRINTLN(WIFI_TCP_PORT);
    } else {
        BOTH_PRINTLN(F("WiFi not connected yet; Bluetooth remains active."));
    }
    printStatus();
}

// =============================================================
// MAIN LOOP
// =============================================================

void loop() {
    // Read USB and Bluetooth input first. Then execute only one queued
    // command, preserving FIFO order across both communication links.
    maintainWiFi();
    processSerialInput();
    processBluetoothInput();
    processWiFiInput();
    processNextQueuedSerialCommand();

    updateRelayPulses();
    updatePendingBarrierCloseCommands();
    updateAutoCloseBB();

    updateVehicleSensors();
    updateReleaseSensors();

    // Non-blocking IN/OUT ORG lamp blinking.
    updateOrangeSignalBlinking();

    updateRedDisplayStage();
    updateDelayedORGCommand();
    updateGRNBrandDisplay();
    repeatCurrentDisplayMessage();
}
