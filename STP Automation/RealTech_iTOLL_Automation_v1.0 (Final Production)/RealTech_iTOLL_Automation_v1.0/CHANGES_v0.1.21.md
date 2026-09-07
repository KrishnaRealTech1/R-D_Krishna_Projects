# Changes in v0.1.21

- Fixed the Admin Authentication dialog at Windows display scaling levels where the
  Cancel and Unlock buttons were clipped.
- Reworked the approved IN/OUT barrier sequence around an armed sensor-LOW cycle.
- The LOW event can no longer be lost when it arrives while the barrier OPEN command
  is being sent.
- The configured `Processing.BarrierAndGreenDelaySeconds` timer never starts before
  the barrier has been opened.
- At timer expiry the lane sends, in order:
  - `CLOSE IN/OUT BB`
  - `IN/OUT ALL OFF`
  - `IN/OUT GRN`
- ORG, buzzer, and the open barrier remain active for the full configured delay.
