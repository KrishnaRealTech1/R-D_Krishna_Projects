using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using RfidVehicleAccess.Services;

namespace RfidVehicleAccess;

public partial class PaymentConfigurationWindow : Window, INotifyPropertyChanged
{
    private readonly AppOptions _options;
    private readonly AppConfigurationService _configurationService;
    private PaymentCategoryEditorRow? _selectedDefaultCategory;

    public PaymentConfigurationWindow(
        AppOptions options,
        AppConfigurationService configurationService)
    {
        InitializeComponent();
        _options = options;
        _configurationService = configurationService;

        Categories = new ObservableCollection<PaymentCategoryEditorRow>(
            options.Payment.Categories.Select(category =>
                new PaymentCategoryEditorRow(
                    category.Name,
                    FormatDecimal(category.Price))));

        if (Categories.Count == 0)
        {
            Categories.Add(new PaymentCategoryEditorRow(
                "Default",
                FormatDecimal(Math.Max(0m, options.Processing.EntryFee))));
        }

        SelectedDefaultCategory = Categories.FirstOrDefault(category =>
                                      string.Equals(
                                          category.Name,
                                          options.Payment.DefaultCategory,
                                          StringComparison.OrdinalIgnoreCase))
                                  ?? Categories[0];

        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PaymentCategoryEditorRow> Categories { get; }

    public PaymentCategoryEditorRow? SelectedDefaultCategory
    {
        get => _selectedDefaultCategory;
        set
        {
            if (ReferenceEquals(_selectedDefaultCategory, value))
            {
                return;
            }

            _selectedDefaultCategory = value;
            OnPropertyChanged();
        }
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var row = new PaymentCategoryEditorRow(
            CreateUniqueCategoryName(),
            FormatDecimal(0m));
        Categories.Add(row);
        CategoryDataGrid.SelectedItem = row;
        CategoryDataGrid.ScrollIntoView(row);
        CategoryDataGrid.Focus();
        ValidationTextBlock.Text = string.Empty;
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryDataGrid.SelectedItem is not PaymentCategoryEditorRow selected)
        {
            ValidationTextBlock.Text = "Select a category to delete.";
            return;
        }

        if (Categories.Count == 1)
        {
            ValidationTextBlock.Text = "At least one payment category is required.";
            return;
        }

        var removedDefault = ReferenceEquals(SelectedDefaultCategory, selected);
        Categories.Remove(selected);
        if (removedDefault)
        {
            SelectedDefaultCategory = Categories[0];
        }

        ValidationTextBlock.Text = string.Empty;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        CategoryDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        CategoryDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

        try
        {
            var validatedCategories = ValidateCategories();
            var selectedDefaultName = SelectedDefaultCategory?.Name.Trim();
            if (string.IsNullOrWhiteSpace(selectedDefaultName))
            {
                throw new InvalidOperationException("Select a default category.");
            }

            var defaultCategory = validatedCategories.FirstOrDefault(category =>
                string.Equals(
                    category.Name,
                    selectedDefaultName,
                    StringComparison.OrdinalIgnoreCase));
            if (defaultCategory is null)
            {
                throw new InvalidOperationException(
                    "The selected default category is not present in the price list.");
            }

            var previousPayment = _options.Payment;
            var previousEntryFee = _options.Processing.EntryFee;
            try
            {
                _options.Payment = new PaymentOptions
                {
                    DefaultCategory = defaultCategory.Name,
                    Categories = validatedCategories
                };
                _options.Processing.EntryFee = defaultCategory.Price;

                await _configurationService.SaveAsync();
                DialogResult = true;
            }
            catch
            {
                _options.Payment = previousPayment;
                _options.Processing.EntryFee = previousEntryFee;
                throw;
            }
        }
        catch (Exception ex)
        {
            ValidationTextBlock.Text = ex.Message;
        }
    }

    private List<VehiclePaymentCategoryOptions> ValidateCategories()
    {
        if (Categories.Count == 0)
        {
            throw new InvalidOperationException("Add at least one payment category.");
        }

        var validated = new List<VehiclePaymentCategoryOptions>(Categories.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in Categories)
        {
            var name = string.Join(' ', row.Name.Trim().Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Every category must have a name.");
            }

            if (!names.Add(name))
            {
                throw new InvalidOperationException(
                    $"Category '{name}' is duplicated. Category names must be unique.");
            }

            if (!TryParseDecimal(row.PriceText, out var price) || price < 0m)
            {
                throw new InvalidOperationException(
                    $"Enter a valid zero or positive price for category '{name}'.");
            }

            row.Name = name;
            row.PriceText = FormatDecimal(price);
            validated.Add(new VehiclePaymentCategoryOptions
            {
                Name = name,
                Price = price
            });
        }

        return validated;
    }

    private string CreateUniqueCategoryName()
    {
        const string baseName = "New Category";
        var candidate = baseName;
        var suffix = 2;

        while (Categories.Any(category =>
                   string.Equals(category.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseName} {suffix++}";
        }

        return candidate;
    }

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result) ||
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public sealed class PaymentCategoryEditorRow : INotifyPropertyChanged
    {
        private string _name;
        private string _priceText;

        public PaymentCategoryEditorRow(string name, string priceText)
        {
            _name = name;
            _priceText = priceText;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set
            {
                if (string.Equals(_name, value, StringComparison.Ordinal))
                {
                    return;
                }

                _name = value;
                OnPropertyChanged();
            }
        }

        public string PriceText
        {
            get => _priceText;
            set
            {
                if (string.Equals(_priceText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _priceText = value;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
