# v0.1.36 Test Checklist

## Build

1. Run `build-standalone-exe.bat` or `publish-win-x64.ps1`.
2. Confirm restore and publish complete without C# or XAML errors.
3. Confirm the application About menu displays version `0.1.36`.

## Mobile Number validation

1. Trigger an IN or OUT Exceptional Approval popup.
2. Type digits and confirm only numeric characters are accepted.
3. Try letters, spaces, `+`, `-`, or punctuation and confirm they are rejected.
4. Enter 9 digits and submit; confirm approval is blocked.
5. Enter exactly 10 digits and submit; confirm approval proceeds when the other fields are valid.
6. Try entering an 11th digit and confirm the field remains limited to 10 digits.
7. Paste more than 10 digits and confirm the paste is rejected.
8. Replace a selected portion of a 10-digit value by pasting digits and confirm the resulting 10-digit value is accepted.

## Regression

1. Confirm Approver Name accepts alphabetic characters and spaces only.
2. Confirm Role remains free text.
3. Confirm cancelling the popup still rejects the movement.
4. Confirm valid approval details are stored in reconciliation and trip records.
5. Confirm IN and OUT Auto Approval behavior is unchanged.
