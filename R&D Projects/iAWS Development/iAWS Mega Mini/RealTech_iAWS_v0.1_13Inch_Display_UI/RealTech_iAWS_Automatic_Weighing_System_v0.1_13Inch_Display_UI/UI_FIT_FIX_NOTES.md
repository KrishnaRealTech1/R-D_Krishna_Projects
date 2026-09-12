# iAWS v0.1 UI Fit Fix

This update changes only the dashboard presentation for the IN/OUT lane hardware area and window fit behavior.

- Signal, buzzer and boom barrier controls are compacted to match the supplied reference.
- IN/OUT hardware graphics are wrapped in a down-only Viewbox so they remain fully visible at smaller screen/work-area sizes instead of being clipped.
- Window minimum size is reduced from 1280x800 to 960x620 so the right-side iAWS information panel remains accessible on lower-resolution displays.
- Main dashboard row ratios are rebalanced slightly to give the IN/OUT cards more usable vertical space.
- Existing bindings and logic for RFID, signal state, buzzer state, barrier open/closed state, cameras, weighbridge, server, and first-RFID-wins processing are unchanged.
