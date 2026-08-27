# Changes in v0.1.19

- Replaced the independent signal and barrier timers with one configurable
  `Processing.BarrierAndGreenDelaySeconds` value.
- Approved lane sequence now requires the vehicle sensor to be HIGH before RFID
  processing and waits for the sensor to change to LOW after the barrier opens.
- When the sensor becomes LOW, the orange signal and buzzer turn off immediately.
- The green signal and boom-barrier close command are issued after the same
  configurable delay.
- Added configurable control-unit sensor messages under `Hardware.SensorMessages`.
- Added separate Sensor HIGH and Sensor LOW controls to the Admin Controls window
  for testing the complete sequence.
