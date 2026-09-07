# v0.1.55 - Separate buzzer ON/OFF admin controls

- Replaced each Admin Controls single Buzzer button with separate Buzzer ON and Buzzer OFF buttons.
- Buzzer ON preserves the existing commands: `IN Buzzer` and `OUT Buzzer`.
- Buzzer OFF sends `IN Buzzer OFF` and `OUT Buzzer OFF` so the buzzer can be disabled without using lane `ALL OFF`.
- Dashboard buzzer state is updated for the new OFF commands.
- Silent wired-COM to Bluetooth failover remains unchanged.
