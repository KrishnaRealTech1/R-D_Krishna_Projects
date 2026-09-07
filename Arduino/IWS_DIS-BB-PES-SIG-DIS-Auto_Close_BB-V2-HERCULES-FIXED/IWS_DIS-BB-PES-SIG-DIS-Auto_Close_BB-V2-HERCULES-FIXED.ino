/******************************************
 * RS485 DISPLAY INTERFACE – UPDATED WORKING VERSION (IDs 1..9)
 *
 * Existing features kept unchanged:
 * 1) Signal light control
 * 2) RS485 display messaging
 * 3) Old boom barrier relay control
 *
 * NEW separate SBB feature:
 *   Inputs:
 *     PE_SENSOR_1_PIN
 *     PE_SENSOR_2_PIN
 *
 *   Outputs:
 *     OPEN_SBB_PIN
 *     CLOSE_SBB_PIN
 *
 * Required behavior:
 *   - PES1 HIGH : OPEN SBB ON for 3 sec, then OFF
 *   - PES2 HIGH -> LOW : CLOSE SBB ON for 3 sec, then OFF
 *   - While PES2 is HIGH, do NOT trigger OPEN SBB
 *   - After PES2 becomes LOW, if PES1 is HIGH, then trigger OPEN SBB
 *   - "OPEN SBB"  : OPEN SBB ON for 3 sec, then OFF
 *   - "CLOSE SBB" : CLOSE SBB ON for 3 sec, then OFF
 *
 * HERCULES SERIAL FIX:
 *   - Commands work with normal CR/LF termination
 *   - Commands also work from Hercules Send buttons without CR/LF
 *   - A 100 ms serial idle timeout completes unterminated commands
 *
 * NEW Auto Close BB feature:
 *   - Every 5 minutes, check Serial command activity
 *   - If no complete Serial command is received for 5 minutes,
 *     internally run "AUTO CLOSE BB"
 *   - "AUTO CLOSE BB" closes both IN BB and OUT BB
 *   - Existing old outputs are not changed
 ******************************************/

#include <SoftwareSerial.h>

// ---- Signal Lights ----
#define RED_PIN    A1
#define GREEN_PIN  A0
#define ORG_PIN    A2

// ---- RS485 Pins ----
#define RS485_RE   2
#define RS485_DE   3
#define RS485_RO  10
#define RS485_DI  11

// ---- Existing Boom Barrier Relay Pins ----
#define IN_BB_OPEN_PIN    4
#define OUT_BB_OPEN_PIN   5
#define IN_BB_CLOSE_PIN   6
#define OUT_BB_CLOSE_PIN  7

// ---- NEW Separate SBB I/O ----
#define OPEN_SBB_PIN      8
#define CLOSE_SBB_PIN     9

#define PE_SENSOR_1_PIN   A3
#define PE_SENSOR_2_PIN   A4

// ---- RS485 Serial ----
SoftwareSerial rs485(RS485_RO, RS485_DI);

// ---- Display Header ----
byte header[] = {0x01, 0x10, 0x00, 0x00, 0x00, 0x20, 0x40};

// =====================================================
// DISPLAY CONFIG
// =====================================================
static const byte DISPLAY_ID_MIN = 0x01;
static const byte DISPLAY_ID_MAX = 0x09;

bool displayEnabled[10] = {
    true,
    true,
    true,
    true,
    true,
    true,
    true,
    true,
    true,
    true
};

// -----------------------------------------------------
// RS485 timing
// -----------------------------------------------------
const unsigned long RS485_INTERFRAME_GAP_MS   = 15;
const unsigned long RS485_ID1_EXTRA_GAP_MS    = 20;
const unsigned int  RS485_TURNAROUND_GUARD_US = 400;
const unsigned int  RS485_PRETX_GUARD_US      = 80;
const byte RS485_ID1_RETRY_COUNT = 1;

// ---- Message system ----
String lastMessage = "";
String lastVehicle = "";
String lastWeight  = "";

enum DisplayMode {
    DISPLAY_MODE_NONE,
    DISPLAY_MODE_GRN,
    DISPLAY_MODE_RED,
    DISPLAY_MODE_ORG
};

DisplayMode currentDisplayMode = DISPLAY_MODE_NONE;

unsigned long lastSendTime = 0;
unsigned long redStageStartTime = 0;
unsigned long redWeightDisplayStartTime = 4294967295UL;

const unsigned long TIMER_DISABLED = 4294967295UL;

const unsigned long SEND_INTERVAL  = 1000UL;
const unsigned long RED_STAGE_TIME = 5000UL;

// ORG must wait 5 seconds after RED vehicle + weight data display.
const unsigned long ORG_AFTER_RED_DELAY_MS = 5000UL;

bool orgPendingAfterRed = false;
bool orgDelayCounting = false;
unsigned long orgDelayStartTime = 0;

// Show company name only while GRN state stays active.
const unsigned long GRN_BRAND_INTERVAL_MS = 180000UL;  // 3 minutes
const unsigned long GRN_BRAND_SHOW_MS     = 10000UL;   // 10 seconds

bool grnBrandActive = false;
unsigned long grnBrandMarkerTime = 0;
unsigned long grnBrandShowStartTime = 0;

const char READY_DISPLAY_MESSAGE[]     = "<L3><F1><S1><R><Ready...!!! >";
const char ORG_DISPLAY_MESSAGE[]       = "<L3><F1><S1><G><Process Completed, GO...!!! >";
const char GRN_BRAND_DISPLAY_MESSAGE[] = "<L3><F1><S1><R><RealTech Systems >";

// =====================================================
// EXISTING old boom barrier timing
// =====================================================
const unsigned long RELAY_PULSE_MS = 3000UL;
const unsigned long CLOSE_DELAY_MS = 10000UL;

bool inBBPendingClose  = false;
bool outBBPendingClose = false;

unsigned long inBBCloseCmdTime  = 0;
unsigned long outBBCloseCmdTime = 0;

const byte RELAY_PINS[] = {
    IN_BB_OPEN_PIN,
    OUT_BB_OPEN_PIN,
    IN_BB_CLOSE_PIN,
    OUT_BB_CLOSE_PIN
};

bool relayActive[4] = { false, false, false, false };
unsigned long relayStart[4] = { 0, 0, 0, 0 };

// =====================================================
// NEW SBB CONFIG
// =====================================================
const bool SBB_RELAYS_ACTIVE_LOW = false;
const unsigned long SBB_PULSE_MS = 3000UL;

// ---- SBB pulse states ----
bool openSBBPulseActive  = false;
bool closeSBBPulseActive = false;

unsigned long openSBBPulseStart  = 0;
unsigned long closeSBBPulseStart = 0;

// ---- PE sensor state tracking ----
int lastPE1State = LOW;
int lastPE2State = LOW;

// If PE1 requests open while PE2 is HIGH or CLOSE pulse is active,
// keep it pending and trigger later when allowed.
bool pendingOpenAfterPE2Low = false;

// ---- Robust serial parser ----
String serialBuffer = "";

// Hercules "Send" buttons may transmit text without CR/LF.
// If no new serial byte arrives for this long, treat the buffered text
// as one complete command automatically.
const unsigned long SERIAL_COMMAND_TIMEOUT_MS = 100UL;
unsigned long lastSerialByteTime = 0;

// Prevent an accidental endless/oversized serial buffer.
const unsigned int SERIAL_BUFFER_MAX = 120;

// =====================================================
// NEW Auto Close BB config
// =====================================================
// Every 5 minutes, if no complete command was received from Serial,
// internally run "AUTO CLOSE BB" to close both existing boom barriers.
const unsigned long AUTO_CLOSE_BB_INTERVAL_MS = 300000UL;

unsigned long lastSerialCommandActivityTime = 0;
unsigned long lastAutoCloseBBCheckTime = 0;

// =====================================================
// Helpers
// =====================================================
byte sbbRelayOnLevel() {
    return SBB_RELAYS_ACTIVE_LOW ? LOW : HIGH;
}

byte sbbRelayOffLevel() {
    return SBB_RELAYS_ACTIVE_LOW ? HIGH : LOW;
}

// =====================================================
// EXISTING old relay helper
// =====================================================
void startRelayPulse(byte pin) {
    for (byte i = 0; i < 4; i++) {
        if (RELAY_PINS[i] == pin) {
            digitalWrite(pin, HIGH);
            relayActive[i] = true;
            relayStart[i]  = millis();
            break;
        }
    }
}

// =====================================================
// NEW SBB output helpers
// =====================================================
void startOpenSBBPulse() {
    digitalWrite(OPEN_SBB_PIN, sbbRelayOnLevel());
    openSBBPulseActive = true;
    openSBBPulseStart = millis();
}

void stopOpenSBBPulse() {
    digitalWrite(OPEN_SBB_PIN, sbbRelayOffLevel());
    openSBBPulseActive = false;
}

void startCloseSBBPulse() {
    digitalWrite(CLOSE_SBB_PIN, sbbRelayOnLevel());
    closeSBBPulseActive = true;
    closeSBBPulseStart = millis();
}

void stopCloseSBBPulse() {
    digitalWrite(CLOSE_SBB_PIN, sbbRelayOffLevel());
    closeSBBPulseActive = false;
}

// =====================================================
// NEW helper for PE-based open request
// =====================================================
void requestOpenSBBFromPE() {
    // Block OPEN while PES2 is HIGH
    if (digitalRead(PE_SENSOR_2_PIN) == HIGH) {
        pendingOpenAfterPE2Low = true;
        return;
    }

    // Avoid OPEN and CLOSE overlapping
    if (closeSBBPulseActive) {
        pendingOpenAfterPE2Low = true;
        return;
    }

    startOpenSBBPulse();
    pendingOpenAfterPE2Low = false;
}

// =====================================================
// EXISTING old relay pulse update
// =====================================================
void updateRelayPulses() {
    unsigned long now = millis();

    for (byte i = 0; i < 4; i++) {
        if (relayActive[i] && (now - relayStart[i] >= RELAY_PULSE_MS)) {
            digitalWrite(RELAY_PINS[i], LOW);
            relayActive[i] = false;
        }
    }
}

// =====================================================
// NEW SBB pulse update
// =====================================================
void updateSBBPulses() {
    unsigned long now = millis();

    if (openSBBPulseActive && (now - openSBBPulseStart >= SBB_PULSE_MS)) {
        stopOpenSBBPulse();
    }

    if (closeSBBPulseActive && (now - closeSBBPulseStart >= SBB_PULSE_MS)) {
        stopCloseSBBPulse();

        // After CLOSE finishes, if an OPEN was pending and conditions are valid,
        // then trigger OPEN.
        if (pendingOpenAfterPE2Low &&
            digitalRead(PE_SENSOR_2_PIN) == LOW &&
            digitalRead(PE_SENSOR_1_PIN) == HIGH) {
            startOpenSBBPulse();
            pendingOpenAfterPE2Low = false;
        }
    }
}

// =====================================================
// NEW PE sensor logic
// PES1 HIGH        -> OPEN SBB for 3 sec
// PES2 HIGH -> LOW -> CLOSE SBB for 3 sec
// While PES2 HIGH  -> block OPEN SBB
// =====================================================
void updatePESensors() {
    int currentPE1State = digitalRead(PE_SENSOR_1_PIN);
    int currentPE2State = digitalRead(PE_SENSOR_2_PIN);

    bool pe1Rising  = (lastPE1State == LOW  && currentPE1State == HIGH);
    bool pe2Falling = (lastPE2State == HIGH && currentPE2State == LOW);

    // PES1 rising edge -> request OPEN
    if (pe1Rising) {
        requestOpenSBBFromPE();
    }

    // PES2 falling edge -> CLOSE
    if (pe2Falling) {
        startCloseSBBPulse();

        // If PES1 is still HIGH, keep OPEN pending until CLOSE finishes
        if (currentPE1State == HIGH) {
            pendingOpenAfterPE2Low = true;
        }
    }

    // If OPEN is pending and now conditions are safe, run it
    if (pendingOpenAfterPE2Low &&
        currentPE2State == LOW &&
        currentPE1State == HIGH &&
        !closeSBBPulseActive) {
        startOpenSBBPulse();
        pendingOpenAfterPE2Low = false;
    }

    // If PES1 goes LOW, clear pending OPEN request
    if (currentPE1State == LOW) {
        pendingOpenAfterPE2Low = false;
    }

    lastPE1State = currentPE1State;
    lastPE2State = currentPE2State;
}

// =====================================================
// Forward declarations
// =====================================================
void sendToDisplay(const String &asciiText);
void executeORGCommand();

// =====================================================
// Display state helpers
// =====================================================
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

void startORGDelayAfterRedWeightDisplay() {
    orgPendingAfterRed = true;
    orgDelayCounting = true;
    orgDelayStartTime = millis();
}

void requestORGCommand() {
    // If RED is active, ORG must run only after vehicle + weight data
    // has stayed on display for ORG_AFTER_RED_DELAY_MS.
    if (currentDisplayMode == DISPLAY_MODE_RED && lastVehicle.length() > 0) {
        orgPendingAfterRed = true;

        if (redWeightDisplayStartTime != TIMER_DISABLED) {
            startORGDelayAfterRedWeightDisplay();
        } else {
            orgDelayCounting = false;  // Wait until RED second stage starts.
        }

        return;
    }

    executeORGCommand();
}

void executeORGCommand() {
    digitalWrite(ORG_PIN, HIGH);
    digitalWrite(GREEN_PIN, LOW);
    digitalWrite(RED_PIN, LOW);

    currentDisplayMode = DISPLAY_MODE_ORG;
    stopGRNBrandDisplay();
    cancelDelayedORGCommand();

    lastVehicle = "";
    lastWeight  = "";
    redStageStartTime = 0;
    redWeightDisplayStartTime = TIMER_DISABLED;

    lastMessage = ORG_DISPLAY_MESSAGE;
    sendToDisplay(lastMessage);
    lastSendTime = millis();
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

    unsigned long now = millis();

    if (grnBrandActive) {
        if (now - grnBrandShowStartTime >= GRN_BRAND_SHOW_MS) {
            lastMessage = READY_DISPLAY_MESSAGE;
            sendToDisplay(lastMessage);
            lastSendTime = now;

            grnBrandActive = false;
            grnBrandMarkerTime = now;
            grnBrandShowStartTime = 0;
        }

        return;
    }

    if (now - grnBrandMarkerTime >= GRN_BRAND_INTERVAL_MS) {
        lastMessage = GRN_BRAND_DISPLAY_MESSAGE;
        sendToDisplay(lastMessage);
        lastSendTime = now;

        grnBrandActive = true;
        grnBrandShowStartTime = now;
    }
}

// =====================================================
// NEW serial command handler
// =====================================================
void processSerialCommand(String cmd) {
    cmd.trim();
    cmd.toUpperCase();

    if (cmd.length() == 0) {
        return;
    }

    if (cmd == "GRN") {
        digitalWrite(GREEN_PIN, HIGH);
        digitalWrite(RED_PIN, LOW);
        digitalWrite(ORG_PIN, LOW);

        currentDisplayMode = DISPLAY_MODE_GRN;
        cancelDelayedORGCommand();
        resetGRNBrandTimer();

        lastVehicle = "";
        lastWeight  = "";
        redStageStartTime = 0;
        redWeightDisplayStartTime = TIMER_DISABLED;

        lastMessage = READY_DISPLAY_MESSAGE;
        sendToDisplay(lastMessage);
        lastSendTime = millis();
    }
    else if (cmd == "ORG") {
        requestORGCommand();
    }
    else if (cmd.startsWith("RED")) {
        digitalWrite(RED_PIN, HIGH);
        digitalWrite(GREEN_PIN, LOW);
        digitalWrite(ORG_PIN, LOW);

        currentDisplayMode = DISPLAY_MODE_RED;
        stopGRNBrandDisplay();
        cancelDelayedORGCommand();
        redWeightDisplayStartTime = TIMER_DISABLED;

        String data = cmd.substring(4);
        int p = data.lastIndexOf(' ');

        if (p > 0) {
            lastWeight  = data.substring(p + 1);
            lastVehicle = data.substring(0, p);
        } else {
            lastVehicle = data;
            lastWeight  = "";
        }

        lastMessage  = "<L3><F1><S1><B><Stop in WB, \"";
        lastMessage += lastVehicle;
        lastMessage += "\" >";

        sendToDisplay(lastMessage);
        lastSendTime      = millis();
        redStageStartTime = millis();
    }
    else if (cmd == "IN BB" || cmd == "IN BB OPEN") {
        startRelayPulse(IN_BB_OPEN_PIN);
    }
    else if (cmd == "OUT BB" || cmd == "OUT BB OPEN") {
        startRelayPulse(OUT_BB_OPEN_PIN);
    }
    else if (cmd == "IN BB CLOSE") {
        inBBCloseCmdTime = millis();
        inBBPendingClose = true;
    }
    else if (cmd == "OUT BB CLOSE") {
        outBBCloseCmdTime = millis();
        outBBPendingClose = true;
    }
    else if (cmd == "AUTO CLOSE BB") {
        unsigned long now = millis();

        // Same working style as the existing CLOSE commands:
        // after CLOSE_DELAY_MS, both close relay pulses will run for RELAY_PULSE_MS.
        inBBCloseCmdTime = now;
        outBBCloseCmdTime = now;

        inBBPendingClose = true;
        outBBPendingClose = true;
    }
    else if (cmd == "OPEN SBB") {
        startOpenSBBPulse();
    }
    else if (cmd == "CLOSE SBB") {
        startCloseSBBPulse();
    }
}

// =====================================================
// Low-level RS485 frame send
// =====================================================
static void rs485SendFrame(byte displayID, const String &asciiText) {
    digitalWrite(RS485_RE, HIGH);
    digitalWrite(RS485_DE, HIGH);

    delayMicroseconds(RS485_PRETX_GUARD_US);

    for (int i = 0; i < (int)sizeof(header); i++) {
        if (i == 0) {
            rs485.write(displayID);
        } else {
            rs485.write(header[i]);
        }
    }

    for (int i = 0; i < asciiText.length(); i++) {
        rs485.write((byte)asciiText[i]);
    }

    rs485.flush();
    delayMicroseconds(RS485_TURNAROUND_GUARD_US);

    digitalWrite(RS485_RE, LOW);
    digitalWrite(RS485_DE, LOW);
}

void sendToDisplayID(byte displayID, const String &asciiText) {
    if (displayID < DISPLAY_ID_MIN || displayID > DISPLAY_ID_MAX) return;
    if (!displayEnabled[displayID]) return;

    rs485SendFrame(displayID, asciiText);

    if (displayID == 0x01 && RS485_ID1_RETRY_COUNT > 0) {
        for (byte r = 0; r < RS485_ID1_RETRY_COUNT; r++) {
            delay(RS485_INTERFRAME_GAP_MS);
            rs485SendFrame(displayID, asciiText);
        }
    }
}

void sendToDisplay(const String &asciiText) {
    for (byte id = DISPLAY_ID_MIN; id <= DISPLAY_ID_MAX; id++) {
        if (!displayEnabled[id]) continue;

        sendToDisplayID(id, asciiText);
        delay(RS485_INTERFRAME_GAP_MS);

        if (id == 0x01) {
            delay(RS485_ID1_EXTRA_GAP_MS);
        }
    }
}

// =====================================================
// NEW Auto Close BB background task
// =====================================================
void updateAutoCloseBB() {
    unsigned long now = millis();

    // Check only once every 5 minutes.
    if (now - lastAutoCloseBBCheckTime < AUTO_CLOSE_BB_INTERVAL_MS) {
        return;
    }

    lastAutoCloseBBCheckTime = now;

    // If any complete Serial command was received in the last 5 minutes,
    // do not auto close.
    if (now - lastSerialCommandActivityTime < AUTO_CLOSE_BB_INTERVAL_MS) {
        return;
    }

    // No Serial command received for 5 minutes:
    // internally execute the new command without affecting old command outputs.
    processSerialCommand("AUTO CLOSE BB");
}

// =====================================================
// Setup
// =====================================================
void setup() {
    pinMode(RED_PIN, OUTPUT);
    pinMode(GREEN_PIN, OUTPUT);
    pinMode(ORG_PIN, OUTPUT);

    pinMode(RS485_RE, OUTPUT);
    pinMode(RS485_DE, OUTPUT);

    pinMode(IN_BB_OPEN_PIN, OUTPUT);
    pinMode(OUT_BB_OPEN_PIN, OUTPUT);
    pinMode(IN_BB_CLOSE_PIN, OUTPUT);
    pinMode(OUT_BB_CLOSE_PIN, OUTPUT);

    pinMode(PE_SENSOR_1_PIN, INPUT);
    pinMode(PE_SENSOR_2_PIN, INPUT);

    pinMode(OPEN_SBB_PIN, OUTPUT);
    pinMode(CLOSE_SBB_PIN, OUTPUT);

    digitalWrite(RED_PIN, LOW);
    digitalWrite(GREEN_PIN, LOW);
    digitalWrite(ORG_PIN, LOW);

    digitalWrite(RS485_RE, LOW);
    digitalWrite(RS485_DE, LOW);

    digitalWrite(IN_BB_OPEN_PIN, LOW);
    digitalWrite(OUT_BB_OPEN_PIN, LOW);
    digitalWrite(IN_BB_CLOSE_PIN, LOW);
    digitalWrite(OUT_BB_CLOSE_PIN, LOW);

    digitalWrite(OPEN_SBB_PIN, sbbRelayOffLevel());
    digitalWrite(CLOSE_SBB_PIN, sbbRelayOffLevel());

    Serial.begin(9600);
    rs485.begin(9600);

    lastSerialCommandActivityTime = millis();
    lastAutoCloseBBCheckTime = millis();

    lastPE1State = digitalRead(PE_SENSOR_1_PIN);
    lastPE2State = digitalRead(PE_SENSOR_2_PIN);
}

// =====================================================
// Main loop
// =====================================================
void loop() {
    updateRelayPulses();
    updateSBBPulses();
    updatePESensors();
    updateAutoCloseBB();

    // =================================================
    // Serial command receiver
    // Works with:
    //   1) Arduino Serial Monitor using CR/LF
    //   2) Hercules Send buttons with NO CR/LF
    // =================================================
    while (Serial.available()) {
        char c = (char)Serial.read();

        if (c == '\n' || c == '\r') {
            // Normal terminated command
            if (serialBuffer.length() > 0) {
                String receivedCmd = serialBuffer;
                serialBuffer = "";

                receivedCmd.trim();
                if (receivedCmd.length() > 0) {
                    lastSerialCommandActivityTime = millis();
                    lastAutoCloseBBCheckTime = millis();
                    processSerialCommand(receivedCmd);
                }
            }
        } else {
            // Hercules may send only the command characters, with no CR/LF.
            if (serialBuffer.length() < SERIAL_BUFFER_MAX) {
                serialBuffer += c;
            } else {
                // Safety reset if corrupt/oversized data is received.
                serialBuffer = "";
            }

            lastSerialByteTime = millis();
        }
    }

    // Hercules compatibility:
    // if characters were received but no CR/LF followed, automatically
    // execute the command after the serial line has been idle briefly.
    if (serialBuffer.length() > 0 &&
        (millis() - lastSerialByteTime >= SERIAL_COMMAND_TIMEOUT_MS)) {

        String receivedCmd = serialBuffer;
        serialBuffer = "";

        receivedCmd.trim();
        if (receivedCmd.length() > 0) {
            lastSerialCommandActivityTime = millis();
            lastAutoCloseBBCheckTime = millis();
            processSerialCommand(receivedCmd);
        }
    }

    if (currentDisplayMode == DISPLAY_MODE_RED && lastVehicle.length() > 0) {
        if (millis() - redStageStartTime >= RED_STAGE_TIME &&
            redStageStartTime != TIMER_DISABLED) {

            lastMessage  = "<L3><F1><S1><K><\"";
            lastMessage += lastVehicle;
            lastMessage += "\" & \"";
            lastMessage += lastWeight;
            lastMessage += "\" >";

            sendToDisplay(lastMessage);
            lastSendTime = millis();

            redStageStartTime = TIMER_DISABLED;
            redWeightDisplayStartTime = millis();

            if (orgPendingAfterRed && !orgDelayCounting) {
                startORGDelayAfterRedWeightDisplay();
            }
        }
    }

    updateDelayedORGCommand();
    updateGRNBrandDisplay();

    if (lastMessage.length() > 0) {
        if (millis() - lastSendTime >= SEND_INTERVAL) {
            sendToDisplay(lastMessage);
            lastSendTime = millis();
        }
    }

    if (inBBPendingClose) {
        if (millis() - inBBCloseCmdTime >= CLOSE_DELAY_MS) {
            startRelayPulse(IN_BB_CLOSE_PIN);
            inBBPendingClose = false;
        }
    }

    if (outBBPendingClose) {
        if (millis() - outBBCloseCmdTime >= CLOSE_DELAY_MS) {
            startRelayPulse(OUT_BB_CLOSE_PIN);
            outBBPendingClose = false;
        }
    }
}