// Define the pin where the LED is connected
int ledPin = 13;  // You can change this to the pin your LED is connected to

void setup() {
  // Initialize the digital pin as an output
  pinMode(ledPin, OUTPUT);
}

void loop() {
  // Fast blink for 5 seconds
  for (int i = 0; i < (5 * 10); i++) {  // 5 seconds, 100ms cycle (50 fast blinks)
    digitalWrite(ledPin, HIGH);  // Turn the LED on
    delay(50);                   // Wait for 50ms
    digitalWrite(ledPin, LOW);   // Turn the LED off
    delay(50);                   // Wait for 50ms
  }

  // Slow blink for 3 seconds
  for (int i = 0; i < (3 * 2); i++) {  // 3 seconds, 1 second cycle (3 slow blinks)
    digitalWrite(ledPin, HIGH);  // Turn the LED on
    delay(500);                  // Wait for 500ms
    digitalWrite(ledPin, LOW);   // Turn the LED off
    delay(500);                  // Wait for 500ms
  }
}
