using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;

namespace CatsCafeWpfApp;

public partial class MainWindow : Window
{
    private const int min_desc_len = 3;
    private const int max_desc_len = 20;
    private const int max_items = 5;
    private const int min_filename_len = 1;
    private const int max_filename_len = 10;
    private const decimal gst_rate = 0.05m;

    private const int tip_none = 0;
    private const int tip_percent = 1;
    private const int tip_amount = 2;

    private List<string> descriptions = new List<string>();
    private List<decimal> prices = new List<decimal>();

    private int tipMethod = tip_none;
    private decimal tipValue = 0m;
    private bool tipWasSet = false;

    private static readonly SolidColorBrush normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAEBD7"));
    private static readonly SolidColorBrush errorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFBDC5"));

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (descriptions.Count >= max_items)
        {
            ShowError(null, "Замовлення вже містить максимум " + max_items + " позицій. Додати більше не можна.");
            return;
        }

        if (!IsValidDescription(TxtDescription.Text))
        {
            ShowError(TxtDescription, "Опис страви має бути від " + min_desc_len + " до " + max_desc_len + " символів.");
            return;
        }

        if (!TryParsePrice(TxtPrice.Text, out decimal price))
        {
            ShowError(TxtPrice, "Ціна має бути додатнім числом (напр. 4.50).");
            return;
        }

        descriptions.Add(TxtDescription.Text.Trim());
        prices.Add(price);

        TxtDescription.Clear();
        TxtPrice.Clear();

        RefreshItemsList();
        RefreshBillSummary();
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        int selectedIndex = ItemsListBox.SelectedIndex;

        if (selectedIndex < 0)
        {
            ShowError(null, "Спочатку виберіть страву у списку.");
            return;
        }

        descriptions.RemoveAt(selectedIndex);
        prices.RemoveAt(selectedIndex);

        RefreshItemsList();
        RefreshBillSummary();
    }

    private void BtnApplyTip_Click(object sender, RoutedEventArgs e)
    {
        if (descriptions.Count == 0)
        {
            ShowError(null, "У замовленні немає страв, для яких можна додати чайові.");
            return;
        }

        if (RbTipPercent.IsChecked == true)
        {
            if (!TryParseNonNegative(TxtTipValue.Text, out decimal percent))
            {
                ShowError(TxtTipValue, "Відсоток чайових має бути невід'ємним числом.");
                return;
            }
            tipMethod = tip_percent;
            tipValue = percent;
        }
        else if (RbTipAmount.IsChecked == true)
        {
            if (!TryParseNonNegative(TxtTipValue.Text, out decimal amount))
            {
                ShowError(TxtTipValue, "Сума чайових має бути невід'ємним числом.");
                return;
            }
            tipMethod = tip_amount;
            tipValue = amount;
        }
        else
        {
            tipMethod = tip_none;
            tipValue = 0m;
        }

        tipWasSet = true;

        RefreshBillSummary();
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        ClearAll();
    }

    private void MenuNew_Click(object sender, RoutedEventArgs e)
    {
        ClearAll();
    }

    private void ClearAll()
    {
        descriptions.Clear();
        prices.Clear();
        tipMethod = tip_none;
        tipValue = 0m;
        tipWasSet = false;
        TxtTipValue.Clear();
        TxtDescription.Clear();
        TxtPrice.Clear();

        RefreshItemsList();
        RefreshBillSummary();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveOrderToFile();
    }

    private void MenuSave_Click(object sender, RoutedEventArgs e)
    {
        SaveOrderToFile();
    }

    private void SaveOrderToFile()
    {
        if (descriptions.Count == 0)
        {
            ShowError(null, "У замовленні немає страв для збереження.");
            return;
        }

        SaveFileDialog dialog = new SaveFileDialog();
        dialog.Filter = "CSV файли (*.csv)|*.csv|Усі файли (*.*)|*.*";
        dialog.FileName = "bill.csv";

        if (dialog.ShowDialog() != true)
            return;

        string chosenName = System.IO.Path.GetFileName(dialog.FileName);

        if (!IsValidFileName(chosenName))
        {
            ShowError(null, "Ім'я файлу має бути від " + min_filename_len + " до " + max_filename_len + " символів.");
            return;
        }

        if (TrySaveToFile(dialog.FileName, out string errorMessage))
            MessageBox.Show("Замовлення збережено у файл " + chosenName + ".", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            ShowError(null, errorMessage);
    }

    private bool TrySaveToFile(string filePath, out string errorMessage)
    {
        try
        {
            using StreamWriter writer = new StreamWriter(filePath, false);

            for (int i = 0; i < descriptions.Count; i++)
            {
                string safeDescription = descriptions[i].Replace(",", " ");
                writer.WriteLine(safeDescription + "," + prices[i].ToString(CultureInfo.InvariantCulture));
            }

            errorMessage = "";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "Не вдалося зберегти файл: " + ex.Message;
            return false;
        }
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        LoadOrderFromFile();
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        LoadOrderFromFile();
    }

    private void LoadOrderFromFile()
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Filter = "CSV файли (*.csv)|*.csv|Усі файли (*.*)|*.*";

        if (dialog.ShowDialog() != true)
            return;

        string chosenName = System.IO.Path.GetFileName(dialog.FileName);

        if (!IsValidFileName(chosenName))
        {
            ShowError(null, "Ім'я файлу має бути від " + min_filename_len + " до " + max_filename_len + " символів.");
            return;
        }

        if (TryLoadFromFile(dialog.FileName, out string errorMessage))
        {
            RefreshItemsList();
            RefreshBillSummary();
            MessageBox.Show("Замовлення завантажено з файлу " + dialog.SafeFileName + ".", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ShowError(null, errorMessage);
        }
    }

    private bool TryLoadFromFile(string filePath, out string errorMessage)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                errorMessage = "Файл не знайдено.";
                return false;
            }

            List<string> loadedDescriptions = new List<string>();
            List<decimal> loadedPrices = new List<decimal>();

            string[] lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int commaIndex = line.LastIndexOf(',');
                if (commaIndex <= 0 || commaIndex == line.Length - 1)
                    continue;

                string description = line.Substring(0, commaIndex).Trim();
                string priceText = line.Substring(commaIndex + 1).Trim();

                bool priceOk = decimal.TryParse(priceText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out decimal price);

                if (priceOk && IsValidDescription(description) && price > 0 && loadedDescriptions.Count < max_items)
                {
                    loadedDescriptions.Add(description);
                    loadedPrices.Add(price);
                }
            }

            descriptions = loadedDescriptions;
            prices = loadedPrices;

            errorMessage = "";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "Не вдалося завантажити файл: " + ex.Message;
            return false;
        }
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Cat's Cafe\n\n" +
            "Застосунок для оформлення замовлень у кафе: додавання та видалення страв, " +
            "чайові (відсоток / фіксована сума / без чайових), розрахунок рахунку з GST, " +
            "збереження та завантаження замовлення у файл.",
            "Про програму", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void TxtDescription_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        System.Windows.Input.Key key = e.Key;

        if (key == System.Windows.Input.Key.Back ||
            key == System.Windows.Input.Key.Delete ||
            key == System.Windows.Input.Key.Left ||
            key == System.Windows.Input.Key.Right ||
            key == System.Windows.Input.Key.Tab)
            return;

        if (TxtDescription.Text.Length >= max_desc_len)
            e.Handled = true;
    }

    private void TxtDescription_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (TxtDescription.Text.Length > max_desc_len)
        {
            TxtDescription.Text = TxtDescription.Text.Substring(0, max_desc_len);
            TxtDescription.CaretIndex = TxtDescription.Text.Length;
        }

        TxtDescription.Background = normalBrush;
    }

    private void TxtPrice_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        TxtPrice.Background = normalBrush;
    }

    private bool IsValidDescription(string? description)
    {
        if (description == null)
            return false;

        int len = description.Trim().Length;
        return len >= min_desc_len && len <= max_desc_len;
    }

    private bool TryParsePrice(string text, out decimal price)
    {
        bool ok = decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out price);
        return ok && price > 0;
    }

    private bool TryParseNonNegative(string text, out decimal value)
    {
        bool ok = decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out value);
        return ok && value >= 0;
    }

    private bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        string baseName = System.IO.Path.GetFileNameWithoutExtension(fileName.Trim());
        return baseName.Length >= min_filename_len && baseName.Length <= max_filename_len;
    }

    private void RefreshItemsList()
    {
        ItemsListBox.Items.Clear();

        for (int i = 0; i < descriptions.Count; i++)
        {
            string line = (i + 1) + ". " + descriptions[i] + Spaces(22 - descriptions[i].Length) + prices[i].ToString("$0.00");
            ItemsListBox.Items.Add(line);
        }
    }

    private void RefreshBillSummary()
    {
        decimal netTotal = 0m;
        for (int i = 0; i < prices.Count; i++)
            netTotal += prices[i];

        decimal tipAmount = 0m;
        if (tipMethod == tip_percent)
            tipAmount = Math.Round(netTotal * tipValue / 100m, 2);
        else if (tipMethod == tip_amount)
            tipAmount = Math.Round(tipValue, 2);

        decimal gstAmount = Math.Round(netTotal * gst_rate, 2);
        decimal totalAmount = netTotal + tipAmount + gstAmount;

        TxtNetTotal.Text = netTotal.ToString("$0.00");
        TxtTipAmount.Text = tipAmount.ToString("$0.00");
        TxtGstAmount.Text = gstAmount.ToString("$0.00");
        TxtTotalAmount.Text = totalAmount.ToString("$0.00");

        TxtGstLabel.Text = tipWasSet ? "GST Amount" : "Total GST";
    }

    private void ShowError(System.Windows.Controls.TextBox? textBox, string message)
    {
        if (textBox != null)
            textBox.Background = errorBrush;

        MessageBox.Show(message, "Помилка введення", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private string Spaces(int count)
    {
        string result = "";

        for (int i = 0; i < count; i++)
            result = result + " ";

        return result;
    }
}