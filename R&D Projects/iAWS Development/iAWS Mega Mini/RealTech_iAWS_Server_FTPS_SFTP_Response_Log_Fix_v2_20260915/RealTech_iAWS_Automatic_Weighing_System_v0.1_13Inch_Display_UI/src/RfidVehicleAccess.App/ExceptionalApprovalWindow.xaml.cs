using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess;

public partial class ExceptionalApprovalWindow : Window
{
    private const int RequiredMobileNumberLength = 10;
    private const string InvalidNameMessage =
        "Approver name can contain alphabets and spaces only.";
    private const string InvalidMobileMessage =
        "Mobile number can contain digits only.";

    public ExceptionalApprovalWindow(ExceptionalApprovalRequest request)
    {
        InitializeComponent();

        MissingTripTitleText.Text = request.MissingTripType == MissingTripType.MissingIn
            ? "MISSING IN TRIP"
            : "MISSING OUT TRIP";
        ExplanationText.Text = request.Explanation;
        LaneText.Text = request.CurrentLane == LaneDirection.In ? "IN" : "OUT";
        VehicleNumberText.Text = request.VehicleNumber;
        RfidNumberText.Text = request.RfidNumber;
        AccessTypeText.Text = request.AccessType;
    }

    public ExceptionalApprovalDetails? Approval { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApproverNameTextBox.Focus();
    }

    private void ApproverNameTextBox_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        if (e.Text.All(IsValidNameCharacter))
        {
            return;
        }

        e.Handled = true;
        ShowValidation(InvalidNameMessage);
    }

    private void ApproverNameTextBox_Pasting(
        object sender,
        DataObjectPastingEventArgs e)
    {
        if (TryGetPastedText(e, out var pastedText) &&
            pastedText.All(IsValidNameCharacter))
        {
            return;
        }

        e.CancelCommand();
        ShowValidation(InvalidNameMessage);
    }

    private void MobileNumberTextBox_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        if (e.Text.All(IsAsciiDigit))
        {
            return;
        }

        e.Handled = true;
        ShowValidation(InvalidMobileMessage);
    }

    private void MobileNumberTextBox_Pasting(
        object sender,
        DataObjectPastingEventArgs e)
    {
        if (!TryGetPastedText(e, out var pastedText) ||
            !pastedText.All(IsAsciiDigit))
        {
            e.CancelCommand();
            ShowValidation(InvalidMobileMessage);
            return;
        }

        if (sender is not TextBox textBox ||
            GetTextLengthAfterPaste(textBox, pastedText) > RequiredMobileNumberLength)
        {
            e.CancelCommand();
            ShowValidation($"Mobile number must contain exactly {RequiredMobileNumberLength} digits.");
        }
    }

    private void ApprovalInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        HideValidation();
    }

    private void ExceptionalApproval_Click(object sender, RoutedEventArgs e)
    {
        var approverName = ApproverNameTextBox.Text.Trim();
        var role = RoleTextBox.Text.Trim();
        var mobileNumber = MobileNumberTextBox.Text.Trim();

        var validationError = Validate(approverName, role, mobileNumber);
        if (validationError is not null)
        {
            ShowValidation(validationError);
            return;
        }

        Approval = new ExceptionalApprovalDetails
        {
            Name = approverName,
            Role = role,
            MobileNumber = mobileNumber,
            ApprovedAt = DateTimeOffset.Now
        };

        DialogResult = true;
    }

    private static string? Validate(
        string approverName,
        string role,
        string mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(approverName))
        {
            return "Enter the approver name.";
        }

        if (approverName.Any(character => !IsValidNameCharacter(character)))
        {
            return InvalidNameMessage;
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            return "Enter the approver role.";
        }

        if (mobileNumber.Any(character => !IsAsciiDigit(character)) ||
            mobileNumber.Length != RequiredMobileNumberLength)
        {
            return $"Enter a valid {RequiredMobileNumberLength}-digit mobile number.";
        }

        return null;
    }

    private static bool TryGetPastedText(
        DataObjectPastingEventArgs e,
        out string pastedText)
    {
        pastedText = e.SourceDataObject.GetData(DataFormats.UnicodeText, true) as string
            ?? e.SourceDataObject.GetData(DataFormats.Text, true) as string
            ?? string.Empty;

        return pastedText.Length > 0;
    }

    private static int GetTextLengthAfterPaste(TextBox textBox, string pastedText)
    {
        return textBox.Text.Length - textBox.SelectionLength + pastedText.Length;
    }

    private static bool IsValidNameCharacter(char character)
    {
        return char.IsLetter(character) || character == ' ';
    }

    private static bool IsAsciiDigit(char character)
    {
        return character is >= '0' and <= '9';
    }

    private void ShowValidation(string message)
    {
        ValidationMessage.Text = message;
        ValidationMessage.Visibility = Visibility.Visible;
    }

    private void HideValidation()
    {
        ValidationMessage.Text = string.Empty;
        ValidationMessage.Visibility = Visibility.Collapsed;
    }
}
