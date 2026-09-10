
const int RED_PIN = A1;  // PC1
const int GREEN_PIN = A0;  // PC0
const int BLINK_PIN = A2;  // PC2

bool keepBlinking = false;

void setup() {
 
  Serial.begin(9600);

  pinMode(RED_PIN, OUTPUT);
  pinMode(GREEN_PIN, OUTPUT);
  pinMode(BLINK_PIN, OUTPUT);
}

void loop() {
  
  if (Serial.available() > 0) {
   
    String command = Serial.readStringUntil('\n');

    if (command == "ORG") {
    
      keepBlinking = true;
      blinkPin(BLINK_PIN);
      digitalWrite(RED_PIN, LOW);
      digitalWrite(GREEN_PIN, LOW);
    } else if (command == "GRN") {
    
      digitalWrite(GREEN_PIN, HIGH);
      digitalWrite(RED_PIN, LOW);
      keepBlinking = false;
    } else if (command == "RED") {
      
      digitalWrite(RED_PIN, HIGH);
      digitalWrite(GREEN_PIN, LOW);
      keepBlinking = false;
    } else if (command == "OFF") {
      
      digitalWrite(GREEN_PIN, LOW);
      digitalWrite(RED_PIN, LOW);
      keepBlinking = false;
    }
  }

  if (keepBlinking) {
    blinkPin(BLINK_PIN);
  }
}

void blinkPin(int pin) {
  digitalWrite(pin, HIGH);
  delay(500);
  digitalWrite(pin, LOW);
  delay(200);
}
