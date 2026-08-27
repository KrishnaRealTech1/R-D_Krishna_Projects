#include <Arduino.h>

// Define the button pins
const int buttonPins[] = {
  PC14, PC15,
  PA0, PA1, PA4, PA5, PA6, PA7,
  PB0, PB1, PB2, PB10, PB12, PB13, PB14, PB15,
  PA8, PA9, PA10, PA11, PA12, PA15, PB3, PB4
};

const int numberOfButtons = sizeof(buttonPins) / sizeof(buttonPins[0]);

// Debounce delay in milliseconds
const unsigned long debounceDelay = 50;

// Cooldown delay in milliseconds
const unsigned long cooldownDelay = 10000;  // 10 seconds

// Enum to define button states
enum ButtonState {
  Idle,
  Pressed,
  Debouncing
};

ButtonState buttonStates[numberOfButtons];

// Store last button debounce time
unsigned long lastButtonTime[numberOfButtons];

// Store last successful send time for each button
unsigned long lastSendTime[numberOfButtons];

// Track first send for each button
bool hasSentBefore[numberOfButtons];

void setup() {
  // Initialize Serial2 at 9600 baud rate for UART communication
  Serial2.begin(9600);

  // Initialize button pins as input with internal pull-up resistor
  for (int i = 0; i < numberOfButtons; i++) {
    pinMode(buttonPins[i], INPUT_PULLUP);

    buttonStates[i] = Idle;
    lastButtonTime[i] = 0;
    lastSendTime[i] = 0;
    hasSentBefore[i] = false;
  }
}

void loop() {
  unsigned long currentTime = millis();

  for (int i = 0; i < numberOfButtons; i++) {
    switch (buttonStates[i]) {
      case Idle:
        if (digitalRead(buttonPins[i]) == LOW) {
          buttonStates[i] = Pressed;
          lastButtonTime[i] = currentTime;
        }
        break;

      case Pressed:
        if (currentTime - lastButtonTime[i] >= debounceDelay) {
          if (digitalRead(buttonPins[i]) == LOW) {

            int buttonNumber = i + 1;

            // Cooldown only for buttons 1 to 20
            bool cooldownRequired = buttonNumber <= 20;
            bool canSend = false;

            if (!cooldownRequired) {
              // Buttons 21, 22, 23, 24 have no cooldown
              canSend = true;
            } else if (!hasSentBefore[i]) {
              // First press of buttons 1 to 20 is allowed
              canSend = true;
            } else if (currentTime - lastSendTime[i] >= cooldownDelay) {
              // After first press, buttons 1 to 20 need 10 seconds cooldown
              canSend = true;
            }

            if (canSend) {
              Serial2.println(buttonNumber);

              lastSendTime[i] = currentTime;
              hasSentBefore[i] = true;
            }

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