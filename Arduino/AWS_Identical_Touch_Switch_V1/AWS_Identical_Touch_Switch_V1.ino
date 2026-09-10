#include <Arduino.h>

// Define the button pins
const int buttonPins[] = {
    PC_14, PC_15, PA_0, PA_1, PA_4, PA_5, PA_6, PA_7, 
    PB_0, PB_1, PB_2, PB_10, PB_12, PB_13, PB_14, PB_15, 
    PA_8, PA_9, PA_10, PA_11, PA_12, PA_15, PB_3, PB_4
};

const int numberOfButtons = sizeof(buttonPins) / sizeof(buttonPins[0]);

// Enum to define button states
enum ButtonState {
  Idle,
  Pressed,
  Debouncing
};

ButtonState buttonStates[numberOfButtons];

void setup() {
  // Initialize Serial2 at 9600 baud rate for UART communication
  Serial.begin(9600);

  // Initialize button pins as input with internal pull-up resistor
  for (int i = 0; i < numberOfButtons; i++) {
    pinMode(buttonPins[i], INPUT_PULLUP);
    buttonStates[i] = Idle; // Initialize button states
  }
}

void loop() {
  static unsigned long lastButtonTime[numberOfButtons] = {0}; // Array to store last button press time
  const unsigned long debounceDelay = 50; // Debounce delay in milliseconds

  for (int i = 0; i < numberOfButtons; i++) {
    switch(buttonStates[i]) {
      case Idle:
        if (digitalRead(buttonPins[i]) == LOW) {
          buttonStates[i] = Pressed;
          lastButtonTime[i] = millis();
        }
        break;
      case Pressed:
        if (millis() - lastButtonTime[i] >= debounceDelay) {
          if (digitalRead(buttonPins[i]) == LOW) {
            Serial.println(i + 1);  // Send the button number over UART
            buttonStates[i] = Debouncing;
          } else {
            buttonStates[i] = Idle;
          }
        }
        break;
      case Debouncing:
        if (digitalRead(buttonPins[i]) == HIGH) {
          buttonStates[i] = Idle;
        }
        break;
    }
  }
}
