/******************************************
 * RS485 DISPLAY INTERFACE – FINAL UPDATED VERSION
 *
 * Behavior:
 *   - "GRN" always turns GREEN light ON immediately + Ready message immediately.
 *   - If "GRN" comes while ORG is active:
 *        only NEW I/O pulse (GRN_IMM_OPEN_PIN) is delayed by ORG_TO_GRN_DELAY_MS.
 *   - Otherwise, NEW I/O pulse happens immediately with GRN.
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

// ---- Boom Barrier Relay Pins ----
#define IN_BB_OPEN_PIN    4
#define OUT_BB_OPEN_PIN   5
#define IN_BB_CLOSE_PIN   6
#define OUT_BB_CLOSE_PIN  7

// ---- NEW: I/O pulse when GRN happens ----
#define GRN_IMM_OPEN_PIN  8

SoftwareSerial rs485(RS485_RO, RS485_DI);  // RX, TX

byte header[] = {0x01, 0x10, 0x00, 0x00, 0x00, 0x20, 0x40};

String lastMessage = "";
String lastVehicle = "";
String lastWeight  = "";

unsigned long lastSendTime = 0;
unsigned long redStageStartTime = 0;

const unsigned long SEND_INTERVAL  = 1000;
const unsigned long RED_STAGE_TIME = 5000;

// ---- Delay for NEW I/O pulse only (when GRN comes after ORG) ----
const unsigned long ORG_TO_GRN_DELAY_MS = 10000UL;
bool grnPulsePendingAfterOrg = false;
unsigned long grnPulseCmdTime = 0;

enum SignalMode : byte { MODE_NONE, MODE_GRN, MODE_ORG, MODE_RED };
SignalMode currentMode = MODE_NONE;

const unsigned long RELAY_PULSE_MS  = 3000UL;
const unsigned long CLOSE_DELAY_MS  = 10000UL;

bool inBBPendingClose  = false;
bool outBBPendingClose = false;

unsigned long inBBCloseCmdTime  = 0;
unsigned long outBBCloseCmdTime = 0;

// ---- Relay Pulse Management ----
const byte RELAY_PINS[] = {
    IN_BB_OPEN_PIN,
    OUT_BB_OPEN_PIN,
    IN_BB_CLOSE_PIN,
    OUT_BB_CLOSE_PIN,
    GRN_IMM_OPEN_PIN
};

const byte NUM_RELAYS = sizeof(RELAY_PINS) / sizeof(RELAY_PINS[0]);

bool relayActive[NUM_RELAYS]         = { false };
unsigned long relayStart[NUM_RELAYS] = { 0 };

void startRelayPulse(byte pin) {
    for (byte i = 0; i < NUM_RELAYS; i++) {
        if (RELAY_PINS[i] == pin) {
            digitalWrite(pin, HIGH);
            relayActive[i] = true;
            relayStart[i]  = millis();
            break;
        }
    }
}

void updateRelayPulses() {
    unsigned long now = millis();
    for (byte i = 0; i < NUM_RELAYS; i++) {
        if (relayActive[i] && (now - relayStart[i] >= RELAY_PULSE_MS)) {
            digitalWrite(RELAY_PINS[i], LOW);
            relayActive[i] = false;
        }
    }
}

void sendToDisplayID(byte displayID, const String &asciiText) {
    digitalWrite(RS485_RE, HIGH);
    digitalWrite(RS485_DE, HIGH);

    for (int i = 0; i < (int)sizeof(header); i++) {
        if (i == 0) rs485.write(displayID);
        else        rs485.write(header[i]);
    }

    for (int i = 0; i < asciiText.length(); i++) {
        rs485.write((byte)asciiText[i]);
    }

    digitalWrite(RS485_RE, LOW);
    digitalWrite(RS485_DE, LOW);
}

void sendToDisplay(const String &asciiText) {
    sendToDisplayID(0x01, asciiText);
    sendToDisplayID(0x02, asciiText);
}

// GREEN lights + message (no NEW I/O pulse here)
void applyGreenLightsAndMessage() {
    currentMode = MODE_GRN;

    digitalWrite(GREEN_PIN, HIGH);
    digitalWrite(RED_PIN, LOW);
    digitalWrite(ORG_PIN, LOW);

    lastVehicle = "";
    lastWeight  = "";
    redStageStartTime = 0;

    lastMessage = "<L3><F1><S1><W><Ready...!!! >";
    sendToDisplay(lastMessage);
    lastSendTime = millis();
}

void pulseGrnNewIO() {
    startRelayPulse(GRN_IMM_OPEN_PIN);
}

void applyOrangeState() {
    currentMode = MODE_ORG;

    digitalWrite(ORG_PIN, HIGH);
    digitalWrite(GREEN_PIN, LOW);
    digitalWrite(RED_PIN, LOW);

    lastVehicle = "";
    lastWeight  = "";
    redStageStartTime = 0;

    lastMessage = "<L3><F1><S1><G><Process Completed, GO...!!! >";
    sendToDisplay(lastMessage);
    lastSendTime = millis();
}

void setup() {
    pinMode(RED_PIN, OUTPUT);
    pinMode(GREEN_PIN, OUTPUT);
    pinMode(ORG_PIN, OUTPUT);

    pinMode(RS485_RE, OUTPUT);
    pinMode(RS485_DE, OUTPUT);

    pinMode(IN_BB_OPEN_PIN,  OUTPUT);
    pinMode(OUT_BB_OPEN_PIN, OUTPUT);
    pinMode(IN_BB_CLOSE_PIN, OUTPUT);
    pinMode(OUT_BB_CLOSE_PIN, OUTPUT);

    pinMode(GRN_IMM_OPEN_PIN, OUTPUT);

    digitalWrite(RED_PIN, LOW);
    digitalWrite(GREEN_PIN, LOW);
    digitalWrite(ORG_PIN, LOW);

    digitalWrite(RS485_RE, LOW);
    digitalWrite(RS485_DE, LOW);

    digitalWrite(IN_BB_OPEN_PIN,  LOW);
    digitalWrite(OUT_BB_OPEN_PIN, LOW);
    digitalWrite(IN_BB_CLOSE_PIN, LOW);
    digitalWrite(OUT_BB_CLOSE_PIN, LOW);
    digitalWrite(GRN_IMM_OPEN_PIN, LOW);

    Serial.begin(9600);
    rs485.begin(9600);
}

void loop() {
    updateRelayPulses();

    // 1) Read CPU command
    if (Serial.available()) {
        String cmd = Serial.readStringUntil('\n');
        cmd.trim();

        // Cancel pending delayed NEW-I/O pulse if any other command comes
        if (cmd != "GRN") {
            grnPulsePendingAfterOrg = false;
        }

        // Capture mode BEFORE any changes from this command
        SignalMode prevMode = currentMode;

        if (cmd == "GRN") {
            // GREEN light + message is immediate always
            applyGreenLightsAndMessage();

            // NEW I/O pulse: delayed only if GRN came while ORG was active
            if (prevMode == MODE_ORG) {
                grnPulseCmdTime = millis();
                grnPulsePendingAfterOrg = true;
            } else {
                pulseGrnNewIO();
            }
        }
        else if (cmd == "ORG") {
            applyOrangeState();
        }
        else if (cmd.startsWith("RED")) {

            currentMode = MODE_RED;

            digitalWrite(RED_PIN, HIGH);
            digitalWrite(GREEN_PIN, LOW);
            digitalWrite(ORG_PIN, LOW);

            String data = cmd.substring(4);  // after "RED "
            int p = data.lastIndexOf(' ');

            if (p > 0) {
                lastWeight  = data.substring(p + 1);
                lastVehicle = data.substring(0, p);
            } else {
                lastVehicle = data;
                lastWeight  = "";
            }

            lastMessage  = "<L3><F1><S1><R><Stop in WB, \"";
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
    }

    // 2) Delayed NEW I/O pulse after ORG->GRN (non-blocking)
    if (grnPulsePendingAfterOrg) {
        if (millis() - grnPulseCmdTime >= ORG_TO_GRN_DELAY_MS) {
            pulseGrnNewIO();
            grnPulsePendingAfterOrg = false;
        }
    }

    // 3) RED Stage-1 to Stage-2 after timeout
    if (lastVehicle.length() > 0) {
        if (millis() - redStageStartTime >= RED_STAGE_TIME &&
            redStageStartTime != 4294967295UL) {

            lastMessage  = "<L3><F1><S1><K><\"";
            lastMessage += lastVehicle;
            lastMessage += "\" & \"";
            lastMessage += lastWeight;
            lastMessage += "\">";

            redStageStartTime = 4294967295UL;
        }
    }

    // 4) Repeat current message every 1 second
    if (lastMessage.length() > 0) {
        if (millis() - lastSendTime >= SEND_INTERVAL) {
            sendToDisplay(lastMessage);
            lastSendTime = millis();
        }
    }

    // 5) Handle delayed CLOSE for Boom Barriers
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
