/*
 * Behavior:
 * - ORG: turns ORG on solid immediately.
 * - GRN:
 *    - If currently in ORG: start a 30s lockout; after 30s auto-switch to GRN and unlock.
 *    - If not in ORG: switch to GRN immediately (no delay, no lock).
 * - RED:
 *    - If currently in ORG: start a 30s lockout; after 30s auto-switch to RED and unlock.
 *    - If not in ORG: switch to RED immediately (no delay, no lock).
 * - During lockout: all commands except STAT are ignored.
 * - STAT: always prints current state; during lock shows remaining seconds.
 *
 * Hercules-friendly input:
 * - Executes 3-char commands immediately even without newline (ORG/GRN/RED/OFF).
 * - Commits a command after 100ms of no input (inter-char timeout).
 * - Still accepts \r, \n, or \r\n.
 */

#include <Arduino.h>

const uint8_t RED_PIN   = A1;  // PC1
const uint8_t GREEN_PIN = A0;  // PC0
const uint8_t ORG_PIN   = A2;  // PC2 (steady "orange")

const uint32_t ORG_TO_COLOR_DELAY_MS = 30000UL;  // 30 seconds

// Operation modes
enum Mode : uint8_t {
  MODE_OFF = 0,
  MODE_RED,
  MODE_GRN,
  MODE_ORG
};

static Mode mode = MODE_OFF;

// Lockout state: active when GRN/RED is requested from ORG
static bool lockoutActive = false;
static unsigned long lockoutDueMs = 0;
enum PendingTarget : uint8_t { PENDING_NONE=0, PENDING_TO_GRN, PENDING_TO_RED };
static PendingTarget pendingTarget = PENDING_NONE;

// Simple serial line buffer
static char cmdBuf[16];
static uint8_t cmdLen = 0;

// Hercules-friendly additions
static unsigned long lastCharMs = 0;
const unsigned long INTER_CHAR_TIMEOUT_MS = 100;  // commit if idle ≥ 100ms

void setup() {
  Serial.begin(9600);

  pinMode(RED_PIN,   OUTPUT);
  pinMode(GREEN_PIN, OUTPUT);
  pinMode(ORG_PIN,   OUTPUT);

  digitalWrite(RED_PIN,   LOW);
  digitalWrite(GREEN_PIN, LOW);
  digitalWrite(ORG_PIN,   LOW);

  Serial.println(F("Ready. Commands: ORG, GRN, RED, OFF, STAT"));
}

void loop() {
  readSerialLine();   // non-blocking
  handleLockout();    // non-blocking timed transition
}

/* ---------------- Serial input ---------------- */

void commitCommandIfAny() {
  if (cmdLen == 0) return;

  // Trim trailing whitespace
  uint8_t n = cmdLen;
  while (n > 0 && (cmdBuf[n - 1] == ' ' || cmdBuf[n - 1] == '\t')) n--;

  if (n == 0) { cmdLen = 0; return; }

  cmdBuf[n] = '\0';
  handleCommand(cmdBuf, n);
  cmdLen = 0;
}

bool isImmediate3Token() {
  if (cmdLen != 3) return false;
  // ORG/RED/GRN/OFF
  if ( (cmdBuf[0]=='O' && cmdBuf[1]=='R' && cmdBuf[2]=='G') ||
       (cmdBuf[0]=='R' && cmdBuf[1]=='E' && cmdBuf[2]=='D') ||
       (cmdBuf[0]=='G' && cmdBuf[1]=='R' && cmdBuf[2]=='N') ||
       (cmdBuf[0]=='O' && cmdBuf[1]=='F' && cmdBuf[2]=='F') ) {
    return true;
  }
  return false;
}

void readSerialLine() {
  bool gotChar = false;

  while (Serial.available() > 0) {
    char c = Serial.read();
    gotChar = true;
    lastCharMs = millis();

    if (c == '\n' || c == '\r') {
      commitCommandIfAny();
      continue;
    }

    // Accept printable ASCII + tab
    if ((c < 32 || c > 126) && c != '\t') continue;

    if (cmdLen < sizeof(cmdBuf) - 1) {
      // Uppercase
      if (c >= 'a' && c <= 'z') c = char(c - 'a' + 'A');

      // Skip leading spaces/tabs
      if (!(cmdLen == 0 && (c == ' ' || c == '\t'))) {
        cmdBuf[cmdLen++] = c;

        // Immediate execution for 3-char tokens
        if (isImmediate3Token()) {
          commitCommandIfAny();
        }
      }
    }
  }

  // Inter-char timeout commit (for Hercules without newline)
  if (!gotChar && cmdLen > 0) {
    unsigned long now = millis();
    if ((long)(now - lastCharMs) >= (long)INTER_CHAR_TIMEOUT_MS) {
      commitCommandIfAny();
    }
  }
}

void handleCommand(const char* cmd, uint8_t n) {
  // Always allow STAT during lockout; everything else ignored
  bool isSTAT = ( (n == 4 && strncmp(cmd, "STAT", 4) == 0) ||
                  (n == 6 && strncmp(cmd, "STATUS", 6) == 0) );

  if (lockoutActive && !isSTAT) {
    Serial.print(F("BUSY: ORG->"));
    Serial.print(pendingTarget == PENDING_TO_GRN ? F("GRN") : F("RED"));
    Serial.print(F(" in "));
    long remaining = (long)(lockoutDueMs - millis());
    if (remaining < 0) remaining = 0;
    Serial.print(remaining / 1000);
    Serial.println(F("s (command ignored)"));
    return;
  }

  if (n == 3 && strncmp(cmd, "ORG", 3) == 0) {
    setMode(MODE_ORG);
    Serial.println(F("ACK ORG"));
  } else if (n == 3 && strncmp(cmd, "GRN", 3) == 0) {
    requestGreen();
  } else if (n == 3 && strncmp(cmd, "RED", 3) == 0) {
    requestRed();
  } else if (n == 3 && strncmp(cmd, "OFF", 3) == 0) {
    setMode(MODE_OFF);
    Serial.println(F("ACK OFF"));
  } else if (isSTAT) {
    reportStatus();
  } else {
    Serial.print(F("ERR Unknown: "));
    Serial.write(cmd, n);
    Serial.println();
  }
}

/* --------------- Modes & outputs --------------- */

void setMode(Mode m) {
  mode = m;

  // all off by default
  digitalWrite(RED_PIN,   LOW);
  digitalWrite(GREEN_PIN, LOW);
  digitalWrite(ORG_PIN,   LOW);

  switch (mode) {
    case MODE_OFF: break;
    case MODE_RED: digitalWrite(RED_PIN,   HIGH); break;
    case MODE_GRN: digitalWrite(GREEN_PIN, HIGH); break;
    case MODE_ORG: digitalWrite(ORG_PIN,   HIGH); break;
  }
}

void requestGreen() {
  if (mode == MODE_ORG) {
    lockoutActive = true;
    pendingTarget = PENDING_TO_GRN;
    lockoutDueMs = millis() + ORG_TO_COLOR_DELAY_MS;
    Serial.println(F("ACK GRN: scheduled after 30s (inputs locked)"));
  } else {
    setMode(MODE_GRN);
    Serial.println(F("ACK GRN"));
  }
}

void requestRed() {
  if (mode == MODE_ORG) {
    lockoutActive = true;
    pendingTarget = PENDING_TO_RED;
    lockoutDueMs = millis() + ORG_TO_COLOR_DELAY_MS;
    Serial.println(F("ACK RED: scheduled after 30s (inputs locked)"));
  } else {
    setMode(MODE_RED);
    Serial.println(F("ACK RED"));
  }
}

void handleLockout() {
  if (!lockoutActive) return;

  unsigned long now = millis();
  if ((long)(now - lockoutDueMs) >= 0) {
    // 30s elapsed: perform the scheduled switch and unlock
    if (pendingTarget == PENDING_TO_GRN) {
      setMode(MODE_GRN);
    } else if (pendingTarget == PENDING_TO_RED) {
      setMode(MODE_RED);
    }
    lockoutActive = false;
    pendingTarget = PENDING_NONE;
    Serial.println(F("INFO: ORG->target delayed transition executed; inputs unlocked"));
  }
}

void reportStatus() {
  Serial.print(F("MODE="));
  switch (mode) {
    case MODE_OFF: Serial.print(F("OFF")); break;
    case MODE_RED: Serial.print(F("RED")); break;
    case MODE_GRN: Serial.print(F("GRN")); break;
    case MODE_ORG: Serial.print(F("ORG")); break;
  }

  Serial.print(F(" | RED="));   Serial.print(digitalRead(RED_PIN));
  Serial.print(F(" GRN="));     Serial.print(digitalRead(GREEN_PIN));
  Serial.print(F(" ORG="));     Serial.print(digitalRead(ORG_PIN));

  if (lockoutActive) {
    long remaining = (long)(lockoutDueMs - millis());
    if (remaining < 0) remaining = 0;
    Serial.print(F(" | LOCKOUT: ORG->"));
    Serial.print(pendingTarget == PENDING_TO_GRN ? F("GRN") : F("RED"));
    Serial.print(F(" in "));
    Serial.print(remaining / 1000);
    Serial.print(F("s"));
  }
  Serial.println();
}
