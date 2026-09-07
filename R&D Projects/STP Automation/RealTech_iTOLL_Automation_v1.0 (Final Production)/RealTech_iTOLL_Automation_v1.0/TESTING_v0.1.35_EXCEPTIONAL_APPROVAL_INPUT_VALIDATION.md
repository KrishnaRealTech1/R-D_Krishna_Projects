# v0.1.35 Test Checklist

## Build

1. Run `build-standalone-exe.bat` or `publish-win-x64.ps1`.
2. Confirm restore and publish complete without C# or XAML errors.
3. Confirm the application About menu displays version `0.1.35`.

## Approver Name validation

1. Trigger an IN or OUT Exceptional Approval popup.
2. Enter `Harshini Kumar` and confirm letters and spaces are accepted.
3. Try typing `123`, punctuation, or symbols and confirm they are not inserted.
4. Paste text containing numbers or symbols and confirm the paste is rejected.
5. Leave the name empty and submit; confirm the popup shows `Enter the approver name.`
6. Enter only valid alphabetic text and confirm approval can proceed when the other fields are valid.

## Mobile Number validation

1. Type a numeric mobile number and confirm digits are accepted.
2. Try typing letters, spaces, `+`, `-`, or punctuation and confirm they are not inserted.
3. Paste a value containing any non-digit character and confirm the paste is rejected.
4. Enter fewer than 7 digits and submit; confirm approval is blocked.
5. Enter more than 15 digits and confirm the field does not accept additional digits.
6. Enter 7 to 15 digits and confirm approval can proceed when the other fields are valid.

## Regression

1. Confirm Role still accepts the existing free-text values.
2. Confirm cancelling the popup still rejects the movement.
3. Confirm valid approval details are still stored in the reconciliation and current trip records.
4. Confirm IN and OUT Auto Approval behavior is unchanged.
