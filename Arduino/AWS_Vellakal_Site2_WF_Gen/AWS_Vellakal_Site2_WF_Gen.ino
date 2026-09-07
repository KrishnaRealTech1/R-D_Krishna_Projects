/*
  Arduino Uno + Bare MAX3232 IC
  Continuous Weight Data Generator using D3 TX pin

  Requirement:
  - Send weight continuously
  - Change weight only once every 5 seconds
  - Until 5 seconds, same weight is repeatedly sent

  Hercules / site setting:
  Baud      : 2400
  Data bits : 8
  Parity    : None
  Stop bits : 1
  Handshake : OFF
  Mode      : Free

  Output frame:
  <STX> + 8-character right-aligned weight + <CR> + <LF> + <ETX>

  Example for weight 00:
  HEX:
  02 20 20 20 20 20 20 30 30 0D 0A 03

  Visible:
        00

  Arduino to MAX3232:
  Arduino D3      -> MAX3232 Pin 11 T1IN
  Arduino 5V      -> MAX3232 Pin 16 VCC
  Arduino GND     -> MAX3232 Pin 15 GND

  MAX3232 to DB9:
  MAX3232 Pin 14 T1OUT -> DB9 Pin 2 or Pin 3
  MAX3232 Pin 15 GND   -> DB9 Pin 5

  Required capacitors for bare MAX3232:
  0.1uF between Pin 1 and Pin 3
  0.1uF between Pin 4 and Pin 5
  0.1uF between Pin 2 and GND
  0.1uF between Pin 6 and GND
  0.1uF between Pin 16 and GND
*/

#include <SoftwareSerial.h>

// SoftwareSerial RX pin is unused, but required by library
const int SOFT_RX_PIN = 2;
const int SOFT_TX_PIN = 3;

SoftwareSerial weightSerial(SOFT_RX_PIN, SOFT_TX_PIN);

const unsigned long BAUD_RATE = 2400;

// Weight changes only every 5 seconds
const unsigned long WEIGHT_CHANGE_INTERVAL_MS = 5000;

// Same weight is sent repeatedly.
// 200ms means same weight frame is sent 5 times per second.
// Change this to 100, 250, 500, etc. if needed.
const unsigned long CONTINUOUS_SEND_INTERVAL_MS = 200;

// Random weight range
const int MIN_WEIGHT = 0;
const int MAX_WEIGHT = 9999;

// Control characters
const byte STX = 0x02;
const byte ETX = 0x03;
const byte CR  = 0x0D;
const byte LF  = 0x0A;

const int LED_PIN = 13;

unsigned long previousWeightChangeTime = 0;
unsigned long previousSendTime = 0;

int currentWeight = 0;

void setup() {
  pinMode(LED_PIN, OUTPUT);

  /*
    D3 output:
    2400 baud, 8 data bits, no parity, 1 stop bit
  */
  weightSerial.begin(BAUD_RATE);

  /*
    USB serial is only for debugging.
    RS232 output is from D3.
  */
  Serial.begin(9600);
  Serial.println("Continuous weight generator started on D3");

  randomSeed(analogRead(A0));

  currentWeight = generateRandomWeight();

  previousWeightChangeTime = millis();
  previousSendTime = millis();

  sendWeightFrame(currentWeight);
  blinkLed();

  Serial.print("Initial weight: ");
  Serial.println(currentWeight);
}

void loop() {
  unsigned long currentTime = millis();

  /*
    Change weight only once every 5 seconds.
  */
  if (currentTime - previousWeightChangeTime >= WEIGHT_CHANGE_INTERVAL_MS) {
    previousWeightChangeTime = currentTime;

    currentWeight = generateRandomWeight();

    Serial.print("New weight: ");
    Serial.println(currentWeight);
  }

  /*
    Send the current same weight continuously.
    The value does not change here.
  */
  if (currentTime - previousSendTime >= CONTINUOUS_SEND_INTERVAL_MS) {
    previousSendTime = currentTime;

    sendWeightFrame(currentWeight);
    blinkLed();
  }
}

int generateRandomWeight() {
  return random(MIN_WEIGHT, MAX_WEIGHT + 1);
}

void sendWeightFrame(int weight) {
  char weightNumber[10];
  char weightField[9];

  /*
    Convert weight to minimum 2 digits.

    Examples:
    0    -> 00
    5    -> 05
    25   -> 25
    1234 -> 1234
  */
  snprintf(weightNumber, sizeof(weightNumber), "%02d", weight);

  /*
    Make final field exactly 8 characters, right aligned.

    Examples:
    00   -> "      00"
    05   -> "      05"
    250  -> "     250"
    1234 -> "    1234"
  */
  snprintf(weightField, sizeof(weightField), "%8s", weightNumber);

  /*
    Final frame:
    STX + weightField + CR + LF + ETX
  */
  weightSerial.write(STX);
  weightSerial.print(weightField);
  weightSerial.write(CR);
  weightSerial.write(LF);
  weightSerial.write(ETX);
}

void blinkLed() {
  digitalWrite(LED_PIN, HIGH);
  delay(20);
  digitalWrite(LED_PIN, LOW);
}