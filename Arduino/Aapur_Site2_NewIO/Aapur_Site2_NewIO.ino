/******************************************
 * RS485 DISPLAY INTERFACE – HERCULES COMPATIBLE VERSION
 *
 * Existing features kept:
 * 1) Signal light control
 * 2) RS485 display messaging, IDs 1..9
 * 3) Existing boom barrier relay control
 * 4) Separate SBB open/close relay control
 * 5) PE sensor based SBB logic
 *
 * Added feature:
 * 6) Auto Close SBB when there is no RX, TX, or I/O activity for 5 minutes
 *
 * Serial command compatibility:
 * - Works with Arduino Serial Monitor using CR, LF, or CR+LF
 * - Works with Hercules Serial Terminal even when it sends NO line ending
 * - Use Hercules in ASCII/Text mode, 9600 baud, 8N1, no flow control
 *
 * Supported commands:
 *   GRN
 *   ORG
 *   RED VEHICLE_NO WEIGHT
 *   IN BB
 *   IN BB OPEN
 *   OUT BB
 *   OUT BB OPEN
 *   IN BB CLOSE
 *   OUT BB CLOSE
 *   OPEN SBB
 *   CLOSE SBB
 *
 * Also accepted without spaces:
 *   INBB, INBBOPEN, OUTBB, OUTBBOPEN, INBBCLOSE, OUTBBCLOSE,
 *   OPENSBB, CLOSESBB
 ******************************************/

#include <SoftwareSerial.h>

// =====================================================
// PIN CONFIG
// =====================================================

// ---- Signal Lights ----
#define RED_PIN    A1
#define GREEN_PIN  A0
#define ORG_PIN    A2

// ---- RS485 Pins ----
#define RS485_RE   2
#define RS485_DE   3
#define RS485_RO   10
#define RS485_DI   11

// ---- Existing Boom Barrier Relay Pins ----
#define IN_BB_OPEN_PIN    4
#define OUT_BB_OPEN_PIN   5
#define IN_BB_CLOSE_PIN   6
#define OUT_BB_CLOSE_PIN  7

// ---- Separate SBB Relay Pins ----
#define OPEN_SBB_PIN      8
#define CLOSE_SBB_PIN     9

// ---- PE Sensor Pins ----
#define PE_SENSOR_1_PIN   A3
#define PE_SENSOR_2_PIN   A4

// ---- NEW Out Boom Barrier Control Pins ----
// ORG command -> OPEN output immediately for 3 seconds.
// GRN command -> wait 3 seconds, then CLOSE output for 3 seconds.
#define OUT_BOOM_OPEN_PIN   12
#define OUT_BOOM_CLOSE_PIN  13

// =====================================================
// SERIAL CONFIG
// =====================================================
const unsigned long SERIAL_BAUD_RATE = 9600UL;

// If Hercules sends text without CR/LF, command is processed after this idle time.
// Increase to 500 if you type commands manually character-by-character.
const unsigned long SERIAL_IDLE_TIMEOUT_MS = 250UL;

// Maximum command length accepted from Serial.
const byte SERIAL_BUFFER_MAX_LEN = 96;

char serialBuffer[SERIAL_BUFFER_MAX_LEN];
byte serialBufferIndex = 0;
unsigned long lastSerialByteTime = 0;

// Set true only while debugging. Keep false for normal machine/software control.
const bool SERIAL_DEBUG_REPLY = false;

// =====================================================
// RS485 SERIAL
// =====================================================
SoftwareSerial rs485(RS485_RO, RS485_DI);

// ---- Display Header ----
byte header[] = {0x01, 0x10, 0x00, 0x00, 0x00, 0x20, 0x40};

// =====================================================
// DISPLAY CONFIG
// =====================================================
static const byte DISPLAY_ID_MIN = 0x01;
static const byte DISPLAY_ID_MAX = 0x09;

// Index 0 is unused. Index 1..9 are display IDs.
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
const unsigned long RS485_INTERFRAME_GAP_MS   = 15UL;
const unsigned long RS485_ID1_EXTRA_GAP_MS    = 20UL;
const unsigned int  RS485_TURNAROUND_GUARD_US = 400;
const unsigned int  RS485_PRETX_GUARD_US      = 80;
const byte RS485_ID1_RETRY_COUNT = 1;

// =====================================================
// MESSAGE SYSTEM
// =====================================================
String lastMessage = "";
String lastVehicle = "";
String lastWeight  = "";

unsigned long lastSendTime = 0;
unsigned long redStageStartTime = 0;

const unsigned long SEND_INTERVAL  = 1000UL;
const unsigned long RED_STAGE_TIME = 5000UL;
const unsigned long RED_STAGE_DONE = 4294967295UL;

// =====================================================
// EXISTING OLD BOOM BARRIER TIMING
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

bool relayActive[4] = {false, false, false, false};
unsigned long relayStart[4] = {0, 0, 0, 0};

// =====================================================
// SBB CONFIG
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

// =====================================================
// NEW OUT BOOM BARRIER CONTROL
// =====================================================
const unsigned long OUT_BOOM_PULSE_MS       = 3000UL;
const unsigned long OUT_BOOM_CLOSE_DELAY_MS = 3000UL;

bool outBoomOpenPulseActive  = false;
bool outBoomClosePulseActive = false;
bool outBoomClosePending     = false;

unsigned long outBoomOpenPulseStart  = 0;
unsigned long outBoomClosePulseStart = 0;
unsigned long outBoomCloseRequestTime = 0;

// =====================================================
// AUTO CLOSE SBB CONFIG
// =====================================================
// Auto close after 5 minutes with no RX, TX, or I/O activity.
const bool AUTO_CLOSE_SBB_ENABLED = true;
const unsigned long AUTO_CLOSE_SBB_IDLE_MS = 5UL * 60UL * 1000UL;

// true  = repeated RS485 display sending is treated as controller TX activity.
// false = auto-close timer ignores repeated RS485 display sending.
const bool AUTO_CLOSE_COUNT_RS485_TX = true;

unsigned long lastControllerActivityTime = 0;
bool autoCloseSBBAlreadyTriggered = false;

// Used to avoid repeated auto-close pulses every 5 minutes after auto-close.
bool closeSBBPulseStartedByAutoClose = false;

// =====================================================
// FORWARD DECLARATIONS
// =====================================================
void handleSerialInput();
void processSerialBuffer();
void processSerialCommand(String cmd);
void sendToDisplay(const String &asciiText);
void sendToDisplayID(byte displayID, const String &asciiText);

void markControllerActivity(bool resetAutoCloseTrigger = true);
bool isControllerIOBusy();
void updateAutoCloseSBB();
void startCloseSBBPulse(bool startedByAutoClose = false);

void startOutBoomOpenPulse();
void scheduleOutBoomClosePulse();
void updateOutBoomBarrierControl();

// =====================================================
// HELPER FUNCTIONS
// =====================================================
byte sbbRelayOnLevel() {
    return SBB_RELAYS_ACTIVE_LOW ? LOW : HIGH;
}

byte sbbRelayOffLevel() {
    return SBB_RELAYS_ACTIVE_LOW ? HIGH : LOW;
}

void clearSerialBuffer() {
    serialBufferIndex = 0;
    serialBuffer[0] = '\0';
}

String normalizeSpaces(String text) {
    text.trim();

    while (text.indexOf("  ") >= 0) {
        text.replace("  ", " ");
    }

    return text;
}

// =====================================================
// AUTO CLOSE SBB ACTIVITY HELPER
// =====================================================
void markControllerActivity(bool resetAutoCloseTrigger) {
    lastControllerActivityTime = millis();

    if (resetAutoCloseTrigger) {
        autoCloseSBBAlreadyTriggered = false;
    }
}

void debugSerialReply(const __FlashStringHelper *prefix, const String &message) {
    if (!SERIAL_DEBUG_REPLY) {
        return;
    }

    Serial.print(prefix);
    Serial.println(message);

    // Debug reply is also controller TX activity.
    markControllerActivity();
}

// =====================================================
// EXISTING OLD RELAY HELPER
// =====================================================
void startRelayPulse(byte pin) {
    for (byte i = 0; i < 4; i++) {
        if (RELAY_PINS[i] == pin) {
            digitalWrite(pin, HIGH);
            relayActive[i] = true;
            relayStart[i]  = millis();

            // Relay output activity.
            markControllerActivity();

            break;
        }
    }
}

// =====================================================
// SBB OUTPUT HELPERS
// =====================================================
void startOpenSBBPulse() {
    digitalWrite(OPEN_SBB_PIN, sbbRelayOnLevel());
    openSBBPulseActive = true;
    openSBBPulseStart = millis();

    // SBB output activity.
    markControllerActivity();
}

void stopOpenSBBPulse() {
    digitalWrite(OPEN_SBB_PIN, sbbRelayOffLevel());
    openSBBPulseActive = false;

    // SBB output activity.
    markControllerActivity();
}

void startCloseSBBPulse(bool startedByAutoClose) {
    digitalWrite(CLOSE_SBB_PIN, sbbRelayOnLevel());
    closeSBBPulseActive = true;
    closeSBBPulseStart = millis();
    closeSBBPulseStartedByAutoClose = startedByAutoClose;

    // Normal CLOSE SBB resets auto-close latch.
    // Auto CLOSE SBB does not reset the latch, to avoid repeating every 5 minutes.
    markControllerActivity(!startedByAutoClose);
}

void stopCloseSBBPulse() {
    digitalWrite(CLOSE_SBB_PIN, sbbRelayOffLevel());
    closeSBBPulseActive = false;

    // If this pulse was started by auto-close, keep autoCloseSBBAlreadyTriggered true.
    markControllerActivity(!closeSBBPulseStartedByAutoClose);

    closeSBBPulseStartedByAutoClose = false;
}

// =====================================================
// NEW OUT BOOM BARRIER CONTROL HELPERS
// =====================================================
void startOutBoomOpenPulse() {
    // An OPEN command overrides any pending/active CLOSE on the new outputs.
    outBoomClosePending = false;

    if (outBoomClosePulseActive) {
        digitalWrite(OUT_BOOM_CLOSE_PIN, LOW);
        outBoomClosePulseActive = false;
    }

    digitalWrite(OUT_BOOM_OPEN_PIN, HIGH);
    outBoomOpenPulseActive = true;
    outBoomOpenPulseStart = millis();
}

void scheduleOutBoomClosePulse() {
    // GRN must not trigger CLOSE immediately.
    // It only starts the 3-second delay here.
    if (outBoomClosePulseActive) {
        digitalWrite(OUT_BOOM_CLOSE_PIN, LOW);
        outBoomClosePulseActive = false;
    }

    outBoomCloseRequestTime = millis();
    outBoomClosePending = true;
}

void updateOutBoomBarrierControl() {
    unsigned long now = millis();

    // Finish OPEN pulse after 3 seconds.
    if (outBoomOpenPulseActive &&
        (now - outBoomOpenPulseStart >= OUT_BOOM_PULSE_MS)) {
        digitalWrite(OUT_BOOM_OPEN_PIN, LOW);
        outBoomOpenPulseActive = false;
    }

    // GRN CLOSE sequence:
    // 1) wait 3 seconds
    // 2) turn CLOSE output ON for 3 seconds
    if (outBoomClosePending &&
        (now - outBoomCloseRequestTime >= OUT_BOOM_CLOSE_DELAY_MS)) {

        // Interlock: never keep OPEN and CLOSE outputs ON together.
        if (outBoomOpenPulseActive) {
            digitalWrite(OUT_BOOM_OPEN_PIN, LOW);
            outBoomOpenPulseActive = false;
        }

        digitalWrite(OUT_BOOM_CLOSE_PIN, HIGH);
        outBoomClosePulseActive = true;
        outBoomClosePulseStart = now;
        outBoomClosePending = false;
    }

    // Finish CLOSE pulse after 3 seconds.
    if (outBoomClosePulseActive &&
        (now - outBoomClosePulseStart >= OUT_BOOM_PULSE_MS)) {
        digitalWrite(OUT_BOOM_CLOSE_PIN, LOW);
        outBoomClosePulseActive = false;
    }
}

// =====================================================
// AUTO CLOSE SBB UPDATE
// =====================================================
bool isControllerIOBusy() {
    if (serialBufferIndex > 0) {
        return true;
    }

    for (byte i = 0; i < 4; i++) {
        if (relayActive[i]) {
            return true;
        }
    }

    if (inBBPendingClose || outBBPendingClose) {
        return true;
    }

    if (openSBBPulseActive || closeSBBPulseActive) {
        return true;
    }

    if (pendingOpenAfterPE2Low) {
        return true;
    }

    if (digitalRead(PE_SENSOR_1_PIN) == HIGH) {
        return true;
    }

    if (digitalRead(PE_SENSOR_2_PIN) == HIGH) {
        return true;
    }

    return false;
}

void updateAutoCloseSBB() {
    if (!AUTO_CLOSE_SBB_ENABLED) {
        return;
    }

    if (autoCloseSBBAlreadyTriggered) {
        return;
    }

    // If any I/O operation is active, controller is not idle.
    if (isControllerIOBusy()) {
        markControllerActivity();
        return;
    }

    if (millis() - lastControllerActivityTime >= AUTO_CLOSE_SBB_IDLE_MS) {
        autoCloseSBBAlreadyTriggered = true;

        // Same physical output behavior as receiving "CLOSE SBB".
        startCloseSBBPulse(true);
    }
}

// =====================================================
// PE-BASED OPEN REQUEST HELPER
// =====================================================
void requestOpenSBBFromPE() {
    // Block OPEN while PES2 is HIGH.
    if (digitalRead(PE_SENSOR_2_PIN) == HIGH) {
        pendingOpenAfterPE2Low = true;
        return;
    }

    // Avoid OPEN and CLOSE overlapping.
    if (closeSBBPulseActive) {
        pendingOpenAfterPE2Low = true;
        return;
    }

    startOpenSBBPulse();
    pendingOpenAfterPE2Low = false;
}

// =====================================================
// EXISTING OLD RELAY PULSE UPDATE
// =====================================================
void updateRelayPulses() {
    unsigned long now = millis();

    for (byte i = 0; i < 4; i++) {
        if (relayActive[i] && (now - relayStart[i] >= RELAY_PULSE_MS)) {
            digitalWrite(RELAY_PINS[i], LOW);
            relayActive[i] = false;

            // Relay output activity.
            markControllerActivity();
        }
    }
}

// =====================================================
// SBB PULSE UPDATE
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
// PE SENSOR LOGIC
// PES1 HIGH         -> OPEN SBB for 3 sec
// PES2 HIGH -> LOW  -> CLOSE SBB for 3 sec
// While PES2 HIGH   -> block PE-triggered OPEN SBB
// =====================================================
void updatePESensors() {
    int currentPE1State = digitalRead(PE_SENSOR_1_PIN);
    int currentPE2State = digitalRead(PE_SENSOR_2_PIN);

    // PE input activity/change.
    if (currentPE1State != lastPE1State || currentPE2State != lastPE2State) {
        markControllerActivity();
    }

    bool pe1Rising  = (lastPE1State == LOW  && currentPE1State == HIGH);
    bool pe2Falling = (lastPE2State == HIGH && currentPE2State == LOW);

    // PES1 rising edge -> request OPEN.
    if (pe1Rising) {
        requestOpenSBBFromPE();
    }

    // PES2 falling edge -> CLOSE.
    if (pe2Falling) {
        startCloseSBBPulse();

        // If PES1 is still HIGH, keep OPEN pending until CLOSE finishes.
        if (currentPE1State == HIGH) {
            pendingOpenAfterPE2Low = true;
        }
    }

    // If OPEN is pending and now conditions are safe, run it.
    if (pendingOpenAfterPE2Low &&
        currentPE2State == LOW &&
        currentPE1State == HIGH &&
        !closeSBBPulseActive) {
        startOpenSBBPulse();
        pendingOpenAfterPE2Low = false;
    }

    // If PES1 goes LOW, clear pending OPEN request.
    if (currentPE1State == LOW) {
        pendingOpenAfterPE2Low = false;
    }

    lastPE1State = currentPE1State;
    lastPE2State = currentPE2State;
}

// =====================================================
// ROBUST SERIAL INPUT HANDLER
// =====================================================
void processSerialBuffer() {
    if (serialBufferIndex == 0) {
        return;
    }

    serialBuffer[serialBufferIndex] = '\0';
    String cmd = String(serialBuffer);
    clearSerialBuffer();
    processSerialCommand(cmd);
}

void handleSerialInput() {
    while (Serial.available() > 0) {
        char c = (char)Serial.read();
        lastSerialByteTime = millis();

        // Serial RX activity.
        markControllerActivity();

        // Process command for CR, LF, or CR+LF.
        if (c == '\r' || c == '\n') {
            processSerialBuffer();
            continue;
        }

        // Support backspace/delete while typing manually in terminal.
        if (c == 8 || c == 127) {
            if (serialBufferIndex > 0) {
                serialBufferIndex--;
                serialBuffer[serialBufferIndex] = '\0';
            }
            continue;
        }

        // Ignore NUL characters.
        if (c == '\0') {
            continue;
        }

        // Save character if buffer has space.
        if (serialBufferIndex < SERIAL_BUFFER_MAX_LEN - 1) {
            serialBuffer[serialBufferIndex] = c;
            serialBufferIndex++;
            serialBuffer[serialBufferIndex] = '\0';
        } else {
            // Too long or corrupted command. Clear it safely.
            clearSerialBuffer();
            debugSerialReply(F("ERR: "), F("COMMAND TOO LONG"));
        }
    }

    // Hercules sometimes sends command without CR/LF.
    // When bytes stop arriving for a short time, process the command.
    if (serialBufferIndex > 0 &&
        (millis() - lastSerialByteTime >= SERIAL_IDLE_TIMEOUT_MS)) {
        processSerialBuffer();
    }
}

// =====================================================
// SERIAL COMMAND HANDLER
// =====================================================
void processSerialCommand(String cmd) {
    cmd.trim();
    cmd.toUpperCase();
    cmd = normalizeSpaces(cmd);

    if (cmd.length() == 0) {
        return;
    }

    String compactCmd = cmd;
    compactCmd.replace(" ", "");

    if (cmd == "GRN") {
        digitalWrite(GREEN_PIN, HIGH);
        digitalWrite(RED_PIN, LOW);
        digitalWrite(ORG_PIN, LOW);

        // NEW Out Boom Barrier CLOSE:
        // wait 3 seconds, then pulse OUT_BOOM_CLOSE_PIN for 3 seconds.
        scheduleOutBoomClosePulse();

        // Signal light output activity.
        markControllerActivity();

        lastVehicle = "";
        lastWeight  = "";
        redStageStartTime = 0;

        lastMessage = "<L3><F1><S1><R><Ready...!!! >";
        sendToDisplay(lastMessage);
        lastSendTime = millis();

        debugSerialReply(F("OK: "), cmd);
    }
    else if (cmd == "ORG") {
        digitalWrite(ORG_PIN, HIGH);
        digitalWrite(GREEN_PIN, LOW);
        digitalWrite(RED_PIN, LOW);

        // NEW Out Boom Barrier OPEN:
        // pulse OUT_BOOM_OPEN_PIN immediately for 3 seconds.
        startOutBoomOpenPulse();

        // Signal light output activity.
        markControllerActivity();

        lastVehicle = "";
        lastWeight  = "";
        redStageStartTime = 0;

        lastMessage = "<L3><F1><S1><G><Process Completed, GO...!!! >";
        sendToDisplay(lastMessage);
        lastSendTime = millis();

        debugSerialReply(F("OK: "), cmd);
    }
    else if (cmd == "RED" || cmd.startsWith("RED ")) {
        digitalWrite(RED_PIN, HIGH);
        digitalWrite(GREEN_PIN, LOW);
        digitalWrite(ORG_PIN, LOW);

        // Signal light output activity.
        markControllerActivity();

        String data = "";
        if (cmd.length() > 3) {
            data = cmd.substring(3);
            data.trim();
        }

        int p = data.lastIndexOf(' ');

        if (p > 0) {
            lastWeight  = data.substring(p + 1);
            lastVehicle = data.substring(0, p);
            lastVehicle.trim();
            lastWeight.trim();
        } else {
            lastVehicle = data;
            lastVehicle.trim();
            lastWeight  = "";
        }

        lastMessage  = "<L3><F1><S1><B><Stop in WB, \"";
        lastMessage += lastVehicle;
        lastMessage += "\" >";

        sendToDisplay(lastMessage);
        lastSendTime      = millis();
        redStageStartTime = millis();

        debugSerialReply(F("OK: "), cmd);
    }
    else if (cmd == "IN BB" || cmd == "IN BB OPEN" || compactCmd == "INBB" || compactCmd == "INBBOPEN") {
        startRelayPulse(IN_BB_OPEN_PIN);
        debugSerialReply(F("OK: "), cmd);
    }
    else if (cmd == "OUT BB" || cmd == "OUT BB OPEN" || compactCmd == "OUTBB" || compactCmd == "OUTBBOPEN") {
        startRelayPulse(OUT_BB_OPEN_PIN);
        debugSerialReply(F("OK: "), cmd);
    }
    else if (cmd == "IN BB CLOSE" || compactCmd == "INBBCLOSE") {
        inBBCloseCmdTime = millis();
        inBBPendingClose = true;

        // Pending close operation activity.
        markControllerActivity();

        debugSerialReply(F("OK: "), cmd);
    }
    else if (cmd == "OUT BB CLOSE" || compactCmd == "OUTBBCLOSE") {
        outBBCloseCmdTime = millis();
        outBBPendingClose = true;

        // Pending close operation activity.
        markControllerActivity();

        debugSerialReply(F("OK: "), cmd);
    }
    else if (cmd == "OPEN SBB" || compactCmd == "OPENSBB") {
        startOpenSBBPulse();
        debugSerialReply(F("OK: "), cmd);
    }
    else if (cmd == "CLOSE SBB" || compactCmd == "CLOSESBB") {
        startCloseSBBPulse();
        debugSerialReply(F("OK: "), cmd);
    }
    else {
        debugSerialReply(F("UNKNOWN: "), cmd);
    }
}

// =====================================================
// LOW-LEVEL RS485 FRAME SEND
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

    // RS485 TX activity.
    if (AUTO_CLOSE_COUNT_RS485_TX) {
        markControllerActivity();
    }
}

void sendToDisplayID(byte displayID, const String &asciiText) {
    if (displayID < DISPLAY_ID_MIN || displayID > DISPLAY_ID_MAX) {
        return;
    }

    if (!displayEnabled[displayID]) {
        return;
    }

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
        if (!displayEnabled[id]) {
            continue;
        }

        sendToDisplayID(id, asciiText);
        delay(RS485_INTERFRAME_GAP_MS);

        if (id == 0x01) {
            delay(RS485_ID1_EXTRA_GAP_MS);
        }
    }
}

// =====================================================
// SETUP
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

    pinMode(OUT_BOOM_OPEN_PIN, OUTPUT);
    pinMode(OUT_BOOM_CLOSE_PIN, OUTPUT);

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

    digitalWrite(OUT_BOOM_OPEN_PIN, LOW);
    digitalWrite(OUT_BOOM_CLOSE_PIN, LOW);

    clearSerialBuffer();

    Serial.begin(SERIAL_BAUD_RATE);
    rs485.begin(SERIAL_BAUD_RATE);

    lastPE1State = digitalRead(PE_SENSOR_1_PIN);
    lastPE2State = digitalRead(PE_SENSOR_2_PIN);

    // Start idle timer from controller boot time.
    lastControllerActivityTime = millis();
    autoCloseSBBAlreadyTriggered = false;
}

// =====================================================
// MAIN LOOP
// =====================================================
void loop() {
    handleSerialInput();

    updateRelayPulses();
    updateSBBPulses();
    updatePESensors();
    updateOutBoomBarrierControl();

    if (lastVehicle.length() > 0) {
        if (millis() - redStageStartTime >= RED_STAGE_TIME &&
            redStageStartTime != RED_STAGE_DONE) {

            lastMessage  = "<L3><F1><S1><K><\"";
            lastMessage += lastVehicle;
            lastMessage += "\" & \"";
            lastMessage += lastWeight;
            lastMessage += "\" >";

            redStageStartTime = RED_STAGE_DONE;
        }
    }

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

    updateAutoCloseSBB();
}