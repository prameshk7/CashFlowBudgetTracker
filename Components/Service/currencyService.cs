public class currencyService
{
    private readonly Dictionary<string, decimal> _exchangeRates = new()
    {
        { "NPR", 1m },
        { "USD", 135m },  // 1 USD = 135 NPR
        { "GBP", 168m }   // 1 GBP = 168 NPR
    };

    public decimal ConvertToNPR(decimal amount, string fromCurrency)
    {
        if (fromCurrency == "NPR") return Math.Round(amount, 2);
        return Math.Round(amount * _exchangeRates[fromCurrency], 2);
    }

    public decimal ConvertFromNPR(decimal amountInNPR, string toCurrency)
    {
        if (toCurrency == "NPR") return Math.Round(amountInNPR, 2);
        return Math.Round(amountInNPR / _exchangeRates[toCurrency], 2);
    }

    public string FormatCurrency(decimal amount, string currency)
    {
        amount = Math.Round(amount, 2);

        return currency switch
        {
            "USD" => $"$ {amount:N2}",
            "GBP" => $"£ {amount:N2}",
            "NPR" => $"NPR {amount:N2}",
            _ => $"{amount:N2} {currency}"
        };
    }

    public List<string> GetAvailableCurrencies()
    {
        return _exchangeRates.Keys.ToList();
    }
}