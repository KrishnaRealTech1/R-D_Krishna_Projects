const int buttonPin = 2;        // Pin connected to the button
const int rxLedPin = 9;         // Pin connected to RX LED
const int buttonLedPin = 8;     // Pin connected to the new push button indicator LED
const int switchPin = 3;        // Pin connected to the slide switch

bool isGeneratingWeight = false;  // Track the toggle state
bool lastButtonState = HIGH;       // Store the previous button state
long currentWeight = 0;            // Store the current weight (0 by default)
unsigned long startTime = 0;       // Store the time when weight generation started
unsigned long buttonPressTime = 0; // Track button press time for long press detection
int baudRate;                       // Variable to store the selected baud rate

void setup() {
    pinMode(switchPin, INPUT_PULLUP);  // Use internal pull-up resistor for the switch
    
    // Read the switch state to determine the baud rate
    if (digitalRead(switchPin) == LOW) {
        baudRate = 2400;  // Set baud rate to 2400 if switch is in one position
    } else {
        baudRate = 9600;  // Set baud rate to 9600 if switch is in the other position
    }

    Serial.begin(baudRate);  // Initialize Serial Monitor at selected baud rate
    pinMode(buttonPin, INPUT_PULLUP);  // Use internal pull-up resistor for button
    pinMode(rxLedPin, OUTPUT);  // Set RX LED pin as output
    pinMode(buttonLedPin, OUTPUT);  // Set button indicator LED pin as output

    randomSeed(analogRead(A0));  // Seed the random generator
    delay(2000);  // Wait for the serial connection to stabilize
}

void loop() {
    bool buttonState = digitalRead(buttonPin);  // Read the button state

    // Control button indicator LED based on button press
    if (buttonState == LOW) {
        digitalWrite(buttonLedPin, HIGH);  // Turn LED on when button is pressed
    } else {
        digitalWrite(buttonLedPin, LOW);  // Turn LED off when button is not pressed
    }

    // If the button is pressed (LOW) and the state changed, toggle the mode
    if (buttonState == LOW && lastButtonState == HIGH) {
        isGeneratingWeight = !isGeneratingWeight;  // Toggle between states

        if (isGeneratingWeight) {
            currentWeight = random(0, 10000);  // Generate a new random weight
        } else {
            currentWeight = 0;  // Reset to 0 when toggled off
        }

        delay(50);  // Short delay to avoid immediate toggling on a single press
    }

    // Save the current button state for the next iteration
    lastButtonState = buttonState;

    // Print the current weight continuously
    Serial.print("wn");
    printWithLeadingZeros(currentWeight);
    Serial.println(" kg");

    // Blink the RX LED with reduced brightness
    blinkRxLedWithDimming();

    delay(200);  // Control print speed for readability
}

// Function to print weight with leading zeros (6 digits)
void printWithLeadingZeros(long weight) {
    if (weight < 10) Serial.print("00000");
    else if (weight < 100) Serial.print("0000");
    else if (weight < 1000) Serial.print("000");
    else if (weight < 10000) Serial.print("00");
    else if (weight < 100000) Serial.print("0");

    Serial.print(weight);
}

// Function to blink RX LED with reduced brightness using PWM
void blinkRxLedWithDimming() {
    analogWrite(rxLedPin, 10);  // Set LED brightness (10 out of 255)
    delay(50);  // Keep it on for 50ms
    analogWrite(rxLedPin, 0);   // Turn LED off (0 brightness)
}
