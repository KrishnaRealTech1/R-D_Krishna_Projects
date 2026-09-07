/***************************************************************
 * BOOM BARRIER, SIGNAL LIGHT, PE SENSOR AND RS485 DISPLAY CONTROL
 * Low-SRAM version for Arduino Uno / Nano (ATmega328P)
 *
 * CONTROLLER COMMANDS (case-insensitive):
 *
 * Boom barriers:
 *   OPEN IN BB
 *   CLOSE IN BB
 *   OPEN OUT BB
 *   CLOSE OUT BB
 *   AUTO CLOSE BB
 *
 * IN lane:
 *   IN RED <VEHICLE NUMBER>
 *   IN RED
 *   IN GRN
 *   IN ORG
 *   IN ALL OFF
 *
 * OUT lane:
 *   OUT RED <VEHICLE NUMBER>
 *   OUT RED
 *   OUT GRN
 *   OUT ORG
 *   OUT ALL OFF
 *
 * Legacy display-only commands:
 *   GRN
 *   RED <VEHICLE NUMBER>
 *   ORG
 *
 * New lane display behavior:
 *   IN RED <NO>:
 *     Stage 1 (10 s) -> Vehicle Detected - IN Lane
 *     Stage 2        -> <NO> - IN
 *   OUT RED <NO>:
 *     Stage 1 (10 s) -> Vehicle Detected - OUT Lane
 *     Stage 2        -> <NO> - OUT
 *   IN ORG       -> IN Process Completed, GO...
 *   OUT ORG      -> OUT Process Completed, GO...
 *   IN GRN       -> IN Lane Ready - WELCOME...
 *   OUT GRN      -> OUT Lane Ready - THANK YOU...
 *
 * IN and OUT lane messages are stored independently. Each active
 * lane owns the display for a full 10-second slot. When both lanes
 * are active, the display alternates IN -> OUT -> IN -> OUT. A RED
 * stage 1 is shown for one complete slot; later slots show stage 2.
 * Updating one lane does not erase the other lane.
 * Complete commands are queued and processed FIFO, one command per
 * pass through loop(). CR/LF-terminated commands are processed
 * immediately. A 1000 ms receive-idle timeout also accepts commands
 * sent without a line ending while preserving spaces in vehicle IDs.
 *
 * IMPORTANT:
 * - Outputs are active HIGH.
 * - Use relay/transistor/MOSFET/optocoupler drivers for loads.
 * - Detect sensor inputs A3 and A4 require external 10 kOhm pull-downs.
 * - IN RELEASE PE sensor uses pin 8.
 * - OUT RELEASE PE sensor uses A5.
 * - RELEASE action is triggered only after confirmed HIGH -> confirmed LOW.
 ***************************************************************/

#include <Arduino.h>
#include <SoftwareSerial.h>
#include <avr/pgmspace.h>
#include <ctype.h>
#include <stdio.h>
#include <string.h>

// =============================================================
// PIN MAPPING
// =============================================================

#define IN_GREEN_PIN   A0
#define IN_RED_PIN     A1
#define IN_ORANGE_PIN  A2

#define OUT_RED_PIN     9
#define OUT_GREEN_PIN  12
#define OUT_ORANGE_PIN 13

#define IN_PE_SENSOR_PIN       A3
#define OUT_PE_SENSOR_PIN      A4
#define IN_RELEASE_SENSOR_PIN   8
#define OUT_RELEASE_SENSOR_PIN A5

#define RS485_RE  2
#define RS485_DE  3
#define RS485_RO 10
#define RS485_DI 11

#define IN_BB_OPEN_PIN    4
#define OUT_BB_OPEN_PIN   5
#define IN_BB_CLOSE_PIN   6
#define OUT_BB_CLOSE_PIN  7

// =============================================================
// GENERAL OUTPUT LEVELS
// =============================================================

const byte OUTPUT_ON_LEVEL  = HIGH;
const byte OUTPUT_OFF_LEVEL = LOW;

// =============================================================
// RS485 DISPLAY CONFIGURATION
// =============================================================

SoftwareSerial rs485(RS485_RO, RS485_DI);

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

// Existing IN/OUT DETECT sensors use analog hysteresis.
const int SENSOR_HIGH_THRESHOLD = 700;
const int SENSOR_LOW_THRESHOLD  = 300;

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

// The RELEASE PE sensors are separate digital inputs.
// A confirmed HIGH arms the sensor. The following confirmed LOW
// triggers the lane release action once, exactly as in Code 2.
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

// =============================================================
// FORWARD DECLARATIONS
// =============================================================

void processSerialCommand(char *command);
void processNextQueuedSerialCommand();
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
// SIGNAL LIGHT HELPERS
// =============================================================

void turnOffInSignalLights() {
    digitalWrite(IN_RED_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(IN_GREEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(IN_ORANGE_PIN, OUTPUT_OFF_LEVEL);
}

void turnOffOutSignalLights() {
    digitalWrite(OUT_RED_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_GREEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_ORANGE_PIN, OUTPUT_OFF_LEVEL);
}

void setInRed() {
    digitalWrite(IN_RED_PIN, OUTPUT_ON_LEVEL);
    digitalWrite(IN_GREEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(IN_ORANGE_PIN, OUTPUT_OFF_LEVEL);
}

void setInGreen() {
    digitalWrite(IN_RED_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(IN_GREEN_PIN, OUTPUT_ON_LEVEL);
    digitalWrite(IN_ORANGE_PIN, OUTPUT_OFF_LEVEL);
}

void setInOrange() {
    digitalWrite(IN_RED_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(IN_GREEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(IN_ORANGE_PIN, OUTPUT_ON_LEVEL);
}

void setOutRed() {
    digitalWrite(OUT_RED_PIN, OUTPUT_ON_LEVEL);
    digitalWrite(OUT_GREEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_ORANGE_PIN, OUTPUT_OFF_LEVEL);
}

void setOutGreen() {
    digitalWrite(OUT_RED_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_GREEN_PIN, OUTPUT_ON_LEVEL);
    digitalWrite(OUT_ORANGE_PIN, OUTPUT_OFF_LEVEL);
}

void setOutOrange() {
    digitalWrite(OUT_RED_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_GREEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_ORANGE_PIN, OUTPUT_ON_LEVEL);
}

void turnOffAllInOutputs() {
    turnOffInSignalLights();
}

void turnOffAllOutOutputs() {
    turnOffOutSignalLights();
}

// =============================================================
// BOOM-BARRIER HELPERS
// =============================================================

void startRelayPulse(byte pin) {
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
        Serial.println(F("IN Detected"));
    } else {
        Serial.println(F("OUT Detected"));
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

    // DETECT PE is detect-only. LOW only rearms it for the next vehicle.
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

    // Require a fresh confirmed HIGH after power-up before release.
    // This prevents startup LOW from closing a barrier.
    sensor.sawConfirmedHigh = false;
}

void executeReleaseAction(SensorLane lane) {
    if (lane == SENSOR_LANE_IN) {
        // IN RELEASE is notification-only.
        // Keep the current signal light unchanged and do not operate the barrier.
        // The IN barrier closes only when the corresponding close command is received.
        Serial.println(F("IN Released"));
    } else {
        // OUT RELEASE is notification-only.
        // Keep the current signal light unchanged and do not operate the barrier.
        // The OUT barrier closes only when the corresponding close command is received.
        Serial.println(F("OUT Released"));
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

        // HIGH only arms the release edge. Do not send any Serial message.
        return;
    }

    // Confirmed HIGH -> LOW: execute once, then require another HIGH.
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
    digitalWrite(RS485_RE, HIGH);
    digitalWrite(RS485_DE, HIGH);

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

    digitalWrite(RS485_RE, LOW);
    digitalWrite(RS485_DE, LOW);
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

    // Prefix checks must be before exact IN RED / OUT RED checks.
    if (commandStartsWith(command, PSTR("IN RED "))) {
        char *vehicle = command + 7;
        trimInPlace(vehicle);

        if (vehicle[0] != '\0') {
            setInRed();
            executeVehicleDetectedDisplay(true, vehicle);
        }
        return;
    }

    if (commandStartsWith(command, PSTR("OUT RED "))) {
        char *vehicle = command + 8;
        trimInPlace(vehicle);

        if (vehicle[0] != '\0') {
            setOutRed();
            executeVehicleDetectedDisplay(false, vehicle);
        }
        return;
    }

    if (commandEquals(command, PSTR("IN ORG"))) {
        setInOrange();
        setLaneDisplayMessage(true, LANE_DISPLAY_ORG, NULL);
        return;
    }

    if (commandEquals(command, PSTR("OUT ORG"))) {
        setOutOrange();
        setLaneDisplayMessage(false, LANE_DISPLAY_ORG, NULL);
        return;
    }

    if (commandEquals(command, PSTR("IN GRN"))) {
        setInGreen();
        setLaneDisplayMessage(true, LANE_DISPLAY_GRN, NULL);
        return;
    }

    if (commandEquals(command, PSTR("OUT GRN"))) {
        setOutGreen();
        setLaneDisplayMessage(false, LANE_DISPLAY_GRN, NULL);
        return;
    }

    // Legacy display-only commands.
    if (commandEquals(command, PSTR("GRN"))) {
        executeGRNCommand();
        return;
    }

    if (commandEquals(command, PSTR("ORG"))) {
        requestORGCommand();
        return;
    }

    if (commandStartsWith(command, PSTR("RED "))) {
        char *vehicle = command + 4;
        trimInPlace(vehicle);
        executeREDCommand(vehicle);
        return;
    }

    // Boom-barrier commands.
    if (commandEquals(command, PSTR("OPEN IN BB"))) {
        startRelayPulse(IN_BB_OPEN_PIN);
        return;
    }

    if (commandEquals(command, PSTR("CLOSE IN BB"))) {
        scheduleInBarrierClose();
        return;
    }

    if (commandEquals(command, PSTR("OPEN OUT BB"))) {
        startRelayPulse(OUT_BB_OPEN_PIN);
        return;
    }

    if (commandEquals(command, PSTR("CLOSE OUT BB"))) {
        scheduleOutBarrierClose();
        return;
    }

    if (commandEquals(command, PSTR("AUTO CLOSE BB"))) {
        scheduleBothBarriersClose();
        return;
    }

    // IN signal commands.
    if (commandEquals(command, PSTR("IN RED"))) {
        setInRed();
        return;
    }

    if (commandEquals(command, PSTR("IN ALL OFF"))) {
        turnOffAllInOutputs();
        return;
    }

    // OUT signal commands.
    if (commandEquals(command, PSTR("OUT RED"))) {
        setOutRed();
        return;
    }

    if (commandEquals(command, PSTR("OUT ALL OFF"))) {
        turnOffAllOutOutputs();
    }
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
        enqueueSerialCommand(serialBuffer);
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

    pinMode(OUT_RED_PIN, OUTPUT);
    pinMode(OUT_GREEN_PIN, OUTPUT);
    pinMode(OUT_ORANGE_PIN, OUTPUT);

    pinMode(IN_BB_OPEN_PIN, OUTPUT);
    pinMode(OUT_BB_OPEN_PIN, OUTPUT);
    pinMode(IN_BB_CLOSE_PIN, OUTPUT);
    pinMode(OUT_BB_CLOSE_PIN, OUTPUT);

    pinMode(RS485_RE, OUTPUT);
    pinMode(RS485_DE, OUTPUT);

    pinMode(IN_PE_SENSOR_PIN, INPUT);
    pinMode(OUT_PE_SENSOR_PIN, INPUT);
    pinMode(IN_RELEASE_SENSOR_PIN, INPUT);
    pinMode(OUT_RELEASE_SENSOR_PIN, INPUT);

    turnOffAllInOutputs();
    turnOffAllOutOutputs();

    digitalWrite(IN_BB_OPEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_BB_OPEN_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(IN_BB_CLOSE_PIN, OUTPUT_OFF_LEVEL);
    digitalWrite(OUT_BB_CLOSE_PIN, OUTPUT_OFF_LEVEL);

    digitalWrite(RS485_RE, LOW);
    digitalWrite(RS485_DE, LOW);

    Serial.begin(9600);
    rs485.begin(9600);

    const unsigned long now = millis();
    lastSerialCommandActivityTime = now;
    lastAutoCloseBBCheckTime = now;
    lastSerialByteTime = now;

    initialiseSensor(inVehicleSensor);
    initialiseSensor(outVehicleSensor);
    initialiseReleaseSensor(inReleaseSensor);
    initialiseReleaseSensor(outReleaseSensor);
}

// =============================================================
// MAIN LOOP
// =============================================================

void loop() {
    // Read every currently available byte first. Then execute only one
    // queued command, preserving FIFO order for command bursts.
    processSerialInput();
    processNextQueuedSerialCommand();

    updateRelayPulses();
    updatePendingBarrierCloseCommands();
    updateAutoCloseBB();

    updateVehicleSensors();
    updateReleaseSensors();

    updateRedDisplayStage();
    updateDelayedORGCommand();
    updateGRNBrandDisplay();
    repeatCurrentDisplayMessage();
}
