#include <Wire.h>
#include <RTClib.h>

// Use RTC_DS3231 for DS3231 module
// Use RTC_DS1307 for DS1307 module
RTC_DS3231 rtc;

void setup() {
  Serial.begin(9600);
  Wire.begin();

  if (!rtc.begin()) {
    Serial.println("ERROR: RTC module not found! Check wiring.");
    while (1); // Halt
  }

  // -------------------------------------------------------
  // SET TIME: This line reads your PC's compile time and
  // writes it to the RTC. It runs ONCE on upload.
  // After setting, comment this line out and re-upload
  // so it doesn't reset on every power cycle.
  // -------------------------------------------------------
  rtc.adjust(DateTime(F(__DATE__), F(__TIME__)));

  Serial.println("✓ RTC time has been set successfully!");
  Serial.println("--------------------------------------");
}

void loop() {
  DateTime now = rtc.now();

  // Print Date
  Serial.print("Date: ");
  Serial.print(now.year());   Serial.print("/");
  Serial.print(now.month());  Serial.print("/");
  Serial.print(now.day());

  // Print Day of Week
  String days[] = {"Sun","Mon","Tue","Wed","Thu","Fri","Sat"};
  Serial.print("  Day: ");
  Serial.print(days[now.dayOfTheWeek()]);

  // Print Time
  Serial.print("  Time: ");
  if (now.hour() < 10)   Serial.print("0");
  Serial.print(now.hour()); Serial.print(":");
  if (now.minute() < 10) Serial.print("0");
  Serial.print(now.minute()); Serial.print(":");
  if (now.second() < 10) Serial.print("0");
  Serial.println(now.second());

  delay(1000); // Update every second
}