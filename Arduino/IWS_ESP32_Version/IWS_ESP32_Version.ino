/*******************************************************
 * IWS ESP32 VERSION (HUB75E + MCP23017 + Lamps)
 *
 * DISPLAY:
 *  - HUB75E 128x64
 *  - Double buffer enabled to remove "dots/dust/tearing"
 *  - GRN: old alignment (single centered line)
 *  - ORG: old alignment (3 lines)
 *  - RED: 2-stage split screen (top/bottom), full text, auto-scroll if needed
 *
 * RELAYS:
 *  - MCP23017 I2C (4 channels)
 *
 * COMMANDS:
 *   GRN
 *   ORG
 *   RED <vehicle> <weight>
 *   IN BB / IN BB OPEN
 *   OUT BB / OUT BB OPEN
 *   IN BB CLOSE
 *   OUT BB CLOSE
 *******************************************************/

#include <Arduino.h>
#include <Wire.h>
#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>
#include <Adafruit_MCP23X17.h>

// =====================================================
// SERIAL
// =====================================================
#define CPU_BAUD 115200
#define DEBUG_ECHO 0
static const uint32_t CMD_IDLE_COMMIT_MS = 60;

// =====================================================
// PANEL CONFIG
// =====================================================
#define PANEL_RES_X 128
#define PANEL_RES_Y 64
#define PANEL_CHAIN 1

/* -------- HUB75 PIN DEFINITIONS (YOUR WORKING PINMAP) -------- */
#define R1_PIN 25
#define G1_PIN 26
#define B1_PIN 27
#define R2_PIN 14
#define G2_PIN 12
#define B2_PIN 13

#define A_PIN 23
#define B_PIN 19
#define C_PIN 5
#define D_PIN 17
#define E_PIN 32

#define LAT_PIN 4
#define OE_PIN 15
#define CLK_PIN 16

#define DISPLAY_ROTATION 0   // change 0..3 if needed

// =====================================================
// LAMPS (change if required)
// =====================================================
#define GREEN_LAMP_PIN  18
#define RED_LAMP_PIN    33
#define ORG_LAMP_PIN    2

// =====================================================
// MCP23017 (I2C) CONFIG
// =====================================================
#define I2C_SDA_PIN     21
#define I2C_SCL_PIN     22
#define MCP23017_ADDR   0x20

#define RELAY_IN_OPEN_CH     0
#define RELAY_OUT_OPEN_CH    1
#define RELAY_IN_CLOSE_CH    2
#define RELAY_OUT_CLOSE_CH   3

// =====================================================
// TIMING
// =====================================================
static const uint32_t RED_STAGE_TIME_MS  = 5000;
static const uint32_t RELAY_PULSE_MS     = 3000;
static const uint32_t CLOSE_DELAY_MS     = 10000;

// Marquee (scroll) tuning
static const uint32_t MARQUEE_STEP_MS    = 35;
static const uint8_t  MARQUEE_GAP_PX     = 24;

// =====================================================
// OBJECTS
// =====================================================
static MatrixPanel_I2S_DMA *dma_display = nullptr;
static Adafruit_MCP23X17 mcp;

// =====================================================
// DISPLAY STATE
// =====================================================
enum DisplayMode : uint8_t {
  MODE_NONE = 0,
  MODE_GRN,
  MODE_ORG,
  MODE_RED_STAGE1,
  MODE_RED_STAGE2
};

static DisplayMode mode = MODE_NONE;

static String lastVehicle = "";
static String lastWeight  = "";

static uint32_t redStageStartMs = 0;
static bool redStage2Locked = false;

// =====================================================
// BOOM BARRIER DELAY STATE
// =====================================================
static bool inBBPendingClose  = false;
static bool outBBPendingClose = false;
static uint32_t inBBCloseCmdMs  = 0;
static uint32_t outBBCloseCmdMs = 0;

// =====================================================
// RELAY PULSE MGMT
// =====================================================
static bool relayActive[4]      = { false, false, false, false };
static uint32_t relayStartMs[4] = { 0, 0, 0, 0 };

// =====================================================
// SERIAL BUFFER
// =====================================================
static String rxLine;
static uint32_t lastRxCharMs = 0;

// =====================================================
// HELPERS
// =====================================================
static inline uint16_t C(uint8_t r, uint8_t g, uint8_t b) {
  return dma_display->color565(r, g, b);
}

static inline void setLamps(bool greenOn, bool redOn, bool orgOn) {
  digitalWrite(GREEN_LAMP_PIN, greenOn ? HIGH : LOW);
  digitalWrite(RED_LAMP_PIN,   redOn   ? HIGH : LOW);
  digitalWrite(ORG_LAMP_PIN,   orgOn   ? HIGH : LOW);
}

static inline void relayWrite(uint8_t ch, bool on) {
  mcp.digitalWrite(ch, on ? HIGH : LOW);
}

static void startRelayPulse(uint8_t ch) {
  if (ch > 3) return;
  relayWrite(ch, true);
  relayActive[ch]  = true;
  relayStartMs[ch] = millis();
}

static void updateRelayPulses() {
  uint32_t now = millis();
  for (uint8_t ch = 0; ch < 4; ch++) {
    if (relayActive[ch] && (now - relayStartMs[ch] >= RELAY_PULSE_MS)) {
      relayWrite(ch, false);
      relayActive[ch] = false;
    }
  }
}

// =====================================================
// SERIAL COMMAND INPUT
// =====================================================
static bool readCommand(String &out) {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    lastRxCharMs = millis();

    if (c == '\r' || c == '\n') {
      out = rxLine;
      rxLine = "";
      out.trim();
      return (out.length() > 0);
    }

    if (rxLine.length() < 220) rxLine += c;
  }

  if (rxLine.length() > 0 && (millis() - lastRxCharMs >= CMD_IDLE_COMMIT_MS)) {
    out = rxLine;
    rxLine = "";
    out.trim();
    return (out.length() > 0);
  }

  return false;
}

// =====================================================
// TEXT / ALIGNMENT
// =====================================================
// Default Adafruit_GFX built-in font:
// - width per character ~6px at size=1, height ~8px at size=1
static int textWidthPx(const String &s, uint8_t size) {
  return (int)s.length() * 6 * (int)size;
}

static int textHeightPx(uint8_t size) {
  return 8 * (int)size;
}

static void drawCenteredLine(const String &s, int y, uint8_t size, uint16_t col) {
  int tw = textWidthPx(s, size);
  int x = (PANEL_RES_X - tw) / 2;
  if (x < 0) x = 0;
  dma_display->setTextSize(size);
  dma_display->setTextColor(col);
  dma_display->setCursor(x, y);
  dma_display->print(s);
}

// =====================================================
// REGION MARQUEE (only for RED vehicle/weight when too long)
// =====================================================
struct RegionMarquee {
  String text;
  uint8_t size = 2;
  uint16_t color = 0xFFFF;

  int x = 0;
  int y = 0;
  int w = PANEL_RES_X;
  int h = 32;

  bool active = false;

  int scrollOffset = 0;
  uint32_t lastStepMs = 0;

  void configure(const String &t, int _x, int _y, int _w, int _h, uint8_t preferredSize, uint16_t col) {
    text = t;
    x = _x; y = _y; w = _w; h = _h;
    color = col;

    size = preferredSize;
    int tw = textWidthPx(text, size);

    if (tw <= w - 2) {
      active = false;
      scrollOffset = 0;
      return;
    }

    size = 1;
    tw = textWidthPx(text, size);

    if (tw <= w - 2) {
      active = false;
      scrollOffset = 0;
      return;
    }

    active = true;
    scrollOffset = 0;
    lastStepMs = millis();
  }

  bool stepIfNeeded() {
    if (!active) return false;
    uint32_t now = millis();
    if (now - lastStepMs < MARQUEE_STEP_MS) return false;
    lastStepMs = now;

    int tw = textWidthPx(text, size);
    int loopLen = tw + MARQUEE_GAP_PX;
    scrollOffset++;
    if (scrollOffset >= loopLen) scrollOffset = 0;
    return true;
  }

  void draw() const {
    // Clear only this region
    dma_display->fillRect(x, y, w, h, 0);

    dma_display->setTextSize(size);
    dma_display->setTextColor(color);

    int tw = textWidthPx(text, size);
    int baselineY = y + (h - textHeightPx(size)) / 2;

    if (!active) {
      int cx = x + (w - tw) / 2;
      if (cx < x) cx = x;
      dma_display->setCursor(cx, baselineY);
      dma_display->print(text);
      return;
    }

    int loopLen = tw + MARQUEE_GAP_PX;
    int startX = x + w - scrollOffset;

    dma_display->setCursor(startX, baselineY);
    dma_display->print(text);

    dma_display->setCursor(startX + loopLen, baselineY);
    dma_display->print(text);
  }
};

// Split halves only for RED
static const int TOP_Y = 0;
static const int TOP_H = 32;
static const int BOT_Y = 32;
static const int BOT_H = 32;

static RegionMarquee topRegion;
static RegionMarquee bottomRegion;

static bool displayDirty = true;

// =====================================================
// DOUBLE BUFFER PRESENT
// =====================================================
static inline void presentFrame() {
  // With double buffering enabled, flip after drawing.
  // This removes tearing/dust/dots.
  dma_display->flipDMABuffer();
}

// =====================================================
// RENDER
// =====================================================
static void renderAll() {
  if (!displayDirty) return;
  displayDirty = false;

  const uint16_t MAGENTA = C(255, 0, 255);
  const uint16_t CYAN    = C(0, 255, 255);
  const uint16_t GREEN   = C(0, 255, 0);
  const uint16_t ORANGE  = C(255, 140, 0);
  const uint16_t RED     = C(255, 0, 0);
  const uint16_t WHITE   = C(255, 255, 255);

  // Clear back buffer fully once per screen change (clean background)
  dma_display->fillScreen(0);

  switch (mode) {

    // ✅ OLD ALIGNMENT: GRN (single centered big line)
    case MODE_GRN: {
      dma_display->setTextWrap(false);
      String line = "Ready...";
      uint8_t size = 2; // matches your photo style (fits width)
      int y = (PANEL_RES_Y - textHeightPx(size)) / 2; // vertical center
      drawCenteredLine(line, y, size, GREEN);
      presentFrame();
    } break;

    // ✅ OLD ALIGNMENT: ORG (3 lines like your photo)
    case MODE_ORG: {
      dma_display->setTextWrap(false);
      uint8_t size = 2;

      // 3 lines with good spacing (old look)
      int lineH = textHeightPx(size);
      int totalH = (lineH * 3) + 4; // slight gap
      int startY = (PANEL_RES_Y - totalH) / 2;

      drawCenteredLine("Process",   startY + 0 * (lineH + 2), size, ORANGE);
      drawCenteredLine("Completed", startY + 1 * (lineH + 2), size, ORANGE);
      drawCenteredLine("GO...!!!",  startY + 2 * (lineH + 2), size, ORANGE);

      presentFrame();
    } break;

    // ✅ RED stage 1 split
    case MODE_RED_STAGE1: {
      dma_display->setTextWrap(false);

      topRegion.configure("Stop in WB", 0, TOP_Y, PANEL_RES_X, TOP_H, 2, RED);
      bottomRegion.configure(lastVehicle, 0, BOT_Y, PANEL_RES_X, BOT_H, 2, MAGENTA);

      topRegion.draw();
      bottomRegion.draw();

      presentFrame();
    } break;

    // ✅ RED stage 2 split with "weight <value>"
    case MODE_RED_STAGE2: {
      dma_display->setTextWrap(false);

      topRegion.configure(lastVehicle, 0, TOP_Y, PANEL_RES_X, TOP_H, 2, MAGENTA);

      String wtLine = "weight " + lastWeight; // ✅ your choice (1)
      bottomRegion.configure(wtLine, 0, BOT_Y, PANEL_RES_X, BOT_H, 2, CYAN);

      topRegion.draw();
      bottomRegion.draw();

      presentFrame();
    } break;

    default: {
      // idle blank
      presentFrame();
    } break;
  }
}

// Only update region if marquee moved (RED only)
static void renderRegionsIfMarqueeMoved(bool topMoved, bool bottomMoved) {
  if (!topMoved && !bottomMoved) return;

  // Important: we must draw on back buffer then flip
  // But we are only drawing small regions; other pixels stay unchanged in the back buffer.
  if (topMoved) topRegion.draw();
  if (bottomMoved) bottomRegion.draw();
  presentFrame();
}

// =====================================================
// SETUP: HUB75
// =====================================================
static void setupHUB75() {
  HUB75_I2S_CFG mxconfig(PANEL_RES_X, PANEL_RES_Y, PANEL_CHAIN);

  mxconfig.gpio.r1 = R1_PIN;
  mxconfig.gpio.g1 = G1_PIN;
  mxconfig.gpio.b1 = B1_PIN;
  mxconfig.gpio.r2 = R2_PIN;
  mxconfig.gpio.g2 = G2_PIN;
  mxconfig.gpio.b2 = B2_PIN;

  mxconfig.gpio.a = A_PIN;
  mxconfig.gpio.b = B_PIN;
  mxconfig.gpio.c = C_PIN;
  mxconfig.gpio.d = D_PIN;
  mxconfig.gpio.e = E_PIN;

  mxconfig.gpio.lat = LAT_PIN;
  mxconfig.gpio.oe  = OE_PIN;
  mxconfig.gpio.clk = CLK_PIN;

  mxconfig.clkphase = false;

  // ✅ IMPORTANT: removes dots/dust/tearing
  mxconfig.double_buff = true;

  dma_display = new MatrixPanel_I2S_DMA(mxconfig);
  dma_display->begin();
  dma_display->setBrightness8(80);
  dma_display->setRotation(DISPLAY_ROTATION);
  dma_display->setTextWrap(false);

  // start clean
  dma_display->fillScreen(0);
  presentFrame();
}

// =====================================================
// SETUP: MCP23017
// =====================================================
static void setupRelaysMCP() {
  Wire.begin(I2C_SDA_PIN, I2C_SCL_PIN);

  bool ok = mcp.begin_I2C(MCP23017_ADDR, &Wire);
  if (!ok) {
    // If MCP not detected, still allow display to work
    return;
  }

  for (uint8_t ch = 0; ch < 4; ch++) {
    mcp.pinMode(ch, OUTPUT);
    relayWrite(ch, false);
  }
}

// =====================================================
// COMMAND HANDLERS
// =====================================================
static void handleGRN() {
  setLamps(true, false, false);

  lastVehicle = "";
  lastWeight  = "";
  redStageStartMs = 0;
  redStage2Locked = false;

  mode = MODE_GRN;
  displayDirty = true;
}

static void handleORG() {
  setLamps(false, false, true);

  lastVehicle = "";
  lastWeight  = "";
  redStageStartMs = 0;
  redStage2Locked = false;

  mode = MODE_ORG;
  displayDirty = true;
}

static void handleRED(const String &cmd) {
  setLamps(false, true, false);

  // Parse "RED <vehicle> <weight>" (vehicle may contain spaces)
  String data = cmd.substring(3);
  data.trim();

  int p = data.lastIndexOf(' ');
  if (p > 0) {
    lastWeight  = data.substring(p + 1);
    lastVehicle = data.substring(0, p);
  } else {
    lastVehicle = data;
    lastWeight  = "";
  }

  mode = MODE_RED_STAGE1;
  displayDirty = true;

  redStageStartMs = millis();
  redStage2Locked = false;
}

static void handleBoomBarrier(const String &cmd) {
  if (cmd == "IN BB" || cmd == "IN BB OPEN") {
    startRelayPulse(RELAY_IN_OPEN_CH);
  } else if (cmd == "OUT BB" || cmd == "OUT BB OPEN") {
    startRelayPulse(RELAY_OUT_OPEN_CH);
  } else if (cmd == "IN BB CLOSE") {
    inBBCloseCmdMs = millis();
    inBBPendingClose = true;
  } else if (cmd == "OUT BB CLOSE") {
    outBBCloseCmdMs = millis();
    outBBPendingClose = true;
  }
}

// =====================================================
// REQUIRED ARDUINO FUNCTIONS
// =====================================================
void setup() {
  delay(2000);
  Serial.begin(CPU_BAUD);

  pinMode(GREEN_LAMP_PIN, OUTPUT);
  pinMode(RED_LAMP_PIN,   OUTPUT);
  pinMode(ORG_LAMP_PIN,   OUTPUT);
  setLamps(false, false, false);

  setupRelaysMCP();
  setupHUB75();

  // Boot screen
  dma_display->fillScreen(0);
  drawCenteredLine("IWS READY", 24, 2, C(255, 0, 255));
  presentFrame();
  delay(600);

  mode = MODE_NONE;
  displayDirty = true;
  renderAll();
}

void loop() {
  // 0) update relay pulses
  updateRelayPulses();

  // 1) read command
  String cmd;
  if (readCommand(cmd)) {
#if DEBUG_ECHO
    Serial.print("CMD: ");
    Serial.println(cmd);
#endif
    if (cmd == "GRN") handleGRN();
    else if (cmd == "ORG") handleORG();
    else if (cmd.startsWith("RED")) handleRED(cmd);
    else handleBoomBarrier(cmd);

    renderAll();
  }

  // 2) RED stage timing
  if (mode == MODE_RED_STAGE1 && !redStage2Locked && redStageStartMs != 0) {
    if (millis() - redStageStartMs >= RED_STAGE_TIME_MS) {
      mode = MODE_RED_STAGE2;
      redStage2Locked = true;
      displayDirty = true;
      renderAll();
    }
  }

  // 3) marquee movement (RED only)
  bool topMoved = topRegion.stepIfNeeded();
  bool botMoved = bottomRegion.stepIfNeeded();
  if (topMoved || botMoved) {
    renderRegionsIfMarqueeMoved(topMoved, botMoved);
  }

  // 4) delayed CLOSE pulses
  if (inBBPendingClose && (millis() - inBBCloseCmdMs >= CLOSE_DELAY_MS)) {
    startRelayPulse(RELAY_IN_CLOSE_CH);
    inBBPendingClose = false;
  }

  if (outBBPendingClose && (millis() - outBBCloseCmdMs >= CLOSE_DELAY_MS)) {
    startRelayPulse(RELAY_OUT_CLOSE_CH);
    outBBPendingClose = false;
  }
}
