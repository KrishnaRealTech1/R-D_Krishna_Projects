# v0.1.31 Build and Smoke Test

1. Run `build-standalone-exe.bat` or `publish-win-x64.ps1`.
2. Confirm restore and publish complete without `CS1061` or other compiler errors.
3. Start the published `RealTechSTPAutomation.exe`.
4. Confirm the RealTech icon appears in the executable, title bar, Alt+Tab view, and running taskbar button.
5. Open Admin Controls and confirm the four approval switches remain available:
   - IN Exceptional Approval Popup
   - OUT Exceptional Approval Popup
   - IN Auto Approval
   - OUT Auto Approval
6. Verify an enabled auto-approval lane processes a missing counterpart trip without displaying the exceptional approval popup.
