#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <EEPROM.h> // Include EEPROM library

// TEA5767 FM module address
#define FM_ADDRESS 0x60

// OLED display settings
#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
#define OLED_RESET -1 // This display does not have a reset pin

Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

// Frequency control buttons
#define BUTTON_INC 2   // Increase frequency button pin
#define BUTTON_DEC 3   // Decrease frequency button pin

// Define EEPROM address to store frequency
#define EEPROM_ADDR 0  // We'll use the first 4 bytes of EEPROM

float currentFrequency = 88.8; // Default frequency in MHz
const float stepSize = 0.1;    // Frequency change step size

void setup() {
  Wire.begin();
  Wire.setClock(100000);
  Serial.begin(9600);

  if (!display.begin(SSD1306_SWITCHCAPVCC, 0x3C)) {
    Serial.println(F("SSD1306 allocation failed"));
    for (;;);
  }

  pinMode(BUTTON_INC, INPUT_PULLUP);
  pinMode(BUTTON_DEC, INPUT_PULLUP);

  // Load the last frequency from EEPROM
  loadFrequency();

  // Clear display and show welcome message
  display.clearDisplay();
  display.setTextSize(2);
  display.setTextColor(SSD1306_WHITE);
  
  String welcomeText1 = "RTS";
  String welcomeText2 = "Welcome!";
  
  // Center the welcome message
  int16_t x1 = (SCREEN_WIDTH - (welcomeText1.length() * 12)) / 2; // Approx. width of each character
  int16_t x2 = (SCREEN_WIDTH - (welcomeText2.length() * 12)) / 2; // Approx. width of each character
  
  display.setCursor(x1, 16);
  display.println(welcomeText1);
  display.setCursor(x2, 32);
  display.println(welcomeText2);

  display.display();
  delay(2000);
  
  // Clear display and set up frequency display
  display.clearDisplay();
  display.setTextSize(1);
  display.setCursor(0, 0);
  display.println("RTS FM Receiver");

  setFrequency(currentFrequency);
  display.display();
}

void loop() {
  static float previousFrequency = -1;  // Track the last frequency

  // Check for button presses
  if (digitalRead(BUTTON_INC) == LOW) {
    currentFrequency += stepSize;
    if (currentFrequency > 108.0) { // Upper FM limit
      currentFrequency = 108.0;
    }
    setFrequency(currentFrequency);
    EEPROM.put(EEPROM_ADDR, currentFrequency); // Save updated frequency to EEPROM
    delay(200);  // Debounce delay
  }

  if (digitalRead(BUTTON_DEC) == LOW) {
    currentFrequency -= stepSize;
    if (currentFrequency < 76.0) { // Lower FM limit
      currentFrequency = 76.0;
    }
    setFrequency(currentFrequency);
    EEPROM.put(EEPROM_ADDR, currentFrequency); // Save updated frequency to EEPROM
    delay(200);  // Debounce delay
  }

  // Display the current frequency only if it has changed
  if (currentFrequency != previousFrequency) {
    display.clearDisplay();
    display.setTextSize(2);
    
    String frequencyText = "Freq: " + String(currentFrequency, 1) + " MHz";
    
    // Center the frequency display
    int16_t xFreq = (SCREEN_WIDTH - (frequencyText.length() * 12)) / 2; // Approx. width of each character
    display.setCursor(xFreq, 0);
    display.print(frequencyText);

    display.display();
    previousFrequency = currentFrequency;  // Update the tracked frequency
  }
  delay(100);  // Small delay to avoid too frequent updates
}

// Function to set the frequency on TEA5767 with mute during frequency change
void setFrequency(float frequency) {
  uint8_t frequencyH = 0;
  uint8_t frequencyL = 0;
  uint16_t frequencyB;

  // Mute the TEA5767
  Wire.beginTransmission(FM_ADDRESS);
  Wire.write(0x80); // Mute on
  Wire.write(0x00);
  Wire.endTransmission();

  delay(100);  // Short delay to avoid noise during frequency change

  // Calculate the PLL word
  frequencyB = 4 * (frequency * 1000000 + 225000) / 32768;
  frequencyH = frequencyB >> 8;
  frequencyL = frequencyB & 0xFF;

  // Send the frequency to the TEA5767
  Wire.beginTransmission(FM_ADDRESS);
  Wire.write(frequencyH);
  Wire.write(frequencyL);
  Wire.write(0xB0); // High side LO injection and search mode disabled
  Wire.write(0x10); // Mute off after frequency set
  Wire.write(0x00);
  Wire.endTransmission();
}

// Function to read the current frequency from TEA5767
float readFrequency() {
  uint8_t frequencyH = 0;
  uint8_t frequencyL = 0;
  uint16_t frequencyB;
  float frequency;

  Wire.requestFrom(FM_ADDRESS, 5);
  frequencyH = Wire.read();
  frequencyL = Wire.read();

  frequencyB = ((frequencyH & 0x3F) << 8) + frequencyL;
  frequency = frequencyB * 32768 / 4 - 225000;
  frequency = frequency / 1000000;

  return frequency;
}

// Load frequency from EEPROM
void loadFrequency() {
  EEPROM.get(EEPROM_ADDR, currentFrequency);
  if (isnan(currentFrequency) || currentFrequency < 76.0 || currentFrequency > 108.0) {
    currentFrequency = 88.8; // Default frequency if invalid
  }
}
