
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class userService
{
    private string _currentUsername;
    private readonly string FilePath;
    private static int userCounter = 0;
    private static int transactionCounter = 0;
    private static int debtCounter = 0; 
    private readonly currencyService _currencyService;

    public void SetUsername(string username)
    {
        _currentUsername = username;
    }

    public string GetUsername()
    {
        return _currentUsername;
    }

    public userService()
    {
        _currencyService = new currencyService();
        FilePath = @"D:\AppDEV\CW-Data\users.json";

        var users = LoadUsers();
        if (users.Any())
        {
            var allTransactions = users.SelectMany(u => u.transactions).ToList();
            var allDebts = users.SelectMany(u => u.debts).ToList();

            if (allTransactions.Any())
            {
                transactionCounter = allTransactions.Max(t => int.Parse(t.transactionID));
            }

            if (allDebts.Any())
            {
                debtCounter = allDebts.Count;
            }

            userCounter = users.Max(u => u.userID);
        }
    }

    public List<userModel> LoadUsers()
    {
        if (!File.Exists(FilePath))
        {
            Console.WriteLine("File not found, returning empty user list.");
            return new List<userModel>();
        }

        var json = File.ReadAllText(FilePath);
        var users = JsonSerializer.Deserialize<List<userModel>>(json) ?? new List<userModel>();
        Console.WriteLine($"Loaded {users.Count} users.");
        return users;
    }

    public void SaveUsers(List<userModel> users)
    {
        var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
        Console.WriteLine($"Saved {users.Count} users to file.");
    }

    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public bool ValidatePassword(string inputPassword, string storedPassword)
    {
        var hashedInputPassword = HashPassword(inputPassword);
        return hashedInputPassword == storedPassword;
    }

    public bool RegisterUser(string username, string email, string password, string currency)
    {
        var users = LoadUsers();

        if (users.Any(u => u.username == username || u.email == email))
        {
            return false;
        }

        userCounter++;

        var newUser = new userModel
        {
            userID = userCounter,
            username = username,
            email = email,
            password = HashPassword(password),
            transactions = new List<TransactionModel>(),
            debts = new List<DebtModel>(),
            currency = currency
        };

        users.Add(newUser);
        SaveUsers(users);
        return true;
    }

    public userModel? AuthenticateUser(string username, string password)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        if (user != null && ValidatePassword(password, user.password))
        {
            return user;
        }
        return null;
    }

    public string? GetUserCurrency(string username)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);

        if (user == null)
        {
            Console.WriteLine($"User {username} not found.");
            return null;
        }

        Console.WriteLine($"Currency for {username} is {user.currency}.");
        return user.currency;
    }

    public decimal GetCurrentBalance(string username)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);

        if (user == null)
        {
            Console.WriteLine($"User {username} not found.");
            return 0;
        }

        decimal balance = 0;

        foreach (var transaction in user.transactions.OrderBy(t => t.date))
        {
            switch (transaction.transactionType)
            {
                case "Income":
                    balance += transaction.amount;
                    break;
                case "Debt":  // Treat debt as cash inflow
                    balance += transaction.amount;
                    break;
                case "Expense":  // Only expenses are cash outflow
                    balance -= transaction.amount;
                    break;
            }
        }

        decimal convertedBalance = _currencyService.ConvertFromNPR(balance, user.currency);

        if (convertedBalance < 0)
        {
            Console.WriteLine($"Warning: Negative balance detected for {username}. This should not happen with proper debt handling.");
        }

        Console.WriteLine($"Current balance for {username} is {_currencyService.FormatCurrency(convertedBalance, user.currency)}");
        return convertedBalance;
    }

    public bool AddTransaction(string username, TransactionModel transaction)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        if (user == null)
        {
            Console.WriteLine($"User {username} not found.");
            return false;
        }

        if (transaction.Currency != "NPR")
        {
            decimal amountInNPR = _currencyService.ConvertToNPR(transaction.amount, transaction.Currency);
            transaction.amount = amountInNPR;
            transaction.Currency = "NPR"; //database stores in NPR
        }

        if (transaction.title.StartsWith("Debt Repayment:"))
        {
            transaction.transactionType = "Expense";
        }
        // For new debts, create an income transaction
        else if (transaction.transactionType == "Debt")
        {
            transaction.transactionType = "Debt";
        }

        transaction.transactionID = (transactionCounter++).ToString();

        user.transactions.Add(transaction);
        Console.WriteLine($"Adding transaction for {username}: {transaction.title} - {_currencyService.FormatCurrency(transaction.amount, "NPR")} (NPR)");

        SaveUsers(users);
        return true;
    }

    public List<TransactionModel>? GetTransactions(string username)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        if (user == null)
        {
            Console.WriteLine($"User {username} not found.");
            return null;
        }

        var convertedTransactions = user.transactions.Select(t => new TransactionModel
        {
            transactionID = t.transactionID,
            title = t.title,
            amount = _currencyService.ConvertFromNPR(t.amount, user.currency),
            transactionType = t.transactionType,
            date = t.date,
            notes = t.notes,
            tags = t.tags,
            Currency = user.currency
        }).ToList();


        Console.WriteLine($"Returning transactions for {username}. Total transactions: {convertedTransactions.Count}");
        return convertedTransactions;
    }

    public List<TransactionModel>? FilterTransactions(string username, Func<TransactionModel, bool> predicate)
    {
        var transactions = GetTransactions(username);
        return transactions?.Where(predicate).ToList();
    }

    // for debts
    public List<DebtModel> GetDebts(string username)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        if (user == null) return new List<DebtModel>();

        // Convert debt amounts from NPR to user's preferred currency
        var convertedDebts = user.debts.Select(d => new DebtModel
        {
            DebtID = d.DebtID,
            Title = d.Title,
            OriginalAmount = _currencyService.ConvertFromNPR(d.OriginalAmount, user.currency),
            RemainingAmount = _currencyService.ConvertFromNPR(d.RemainingAmount, user.currency),
            DueDate = d.DueDate,
            ClearedDate = d.ClearedDate,
            Notes = d.Notes,
            Tags = d.Tags,
            IsCleared = d.IsCleared,
            Currency = user.currency
        }).ToList();

        return convertedDebts;
    }

    public bool AddDebt(string username, DebtModel debt)
    {
        try
        {
            var users = LoadUsers();
            var user = users.FirstOrDefault(u => u.username == username);
            if (user == null)
            {
                Console.WriteLine($"AddDebt failed: User {username} not found");
                return false;
            }

            if (string.IsNullOrEmpty(debt.Currency))
            {
                debt.Currency = user.currency;
            }

            debtCounter++;
            debt.DebtID = $"DEBT_{debtCounter}";

            if (debt.Currency != "NPR")
            {
                try
                {
                    debt.OriginalAmount = _currencyService.ConvertToNPR(debt.OriginalAmount, debt.Currency);
                    debt.Currency = "NPR"; // default is NPR so used this
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"AddDebt failed: Currency conversion error - {ex.Message}");
                    return false;
                }
            }

            debt.RemainingAmount = debt.OriginalAmount;
            debt.IsCleared = false;



            user.debts.Add(debt);
            SaveUsers(users);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error Adding debt:{e.Message}");
            return false;
        }
    }

    public bool ClearDebt(string username, string debtId, decimal amountToRepay)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        if (user == null) return false;

        var debt = user.debts.FirstOrDefault(d => d.DebtID == debtId);
        if (debt == null) return false;

        if (user.currency != "NPR")
        {
            amountToRepay = _currencyService.ConvertToNPR(amountToRepay, user.currency);
        }


        if (amountToRepay > debt.RemainingAmount)
        {
            return false;
        }

        decimal currentBalance = GetCurrentBalance(username);
        if (currentBalance < amountToRepay)
        {
            return false;
        }

        // Update debt
        debt.RemainingAmount -= amountToRepay;

        // Create repayment transaction
        var repaymentTransaction = new TransactionModel
        {
            title = $"Debt Repayment: {debt.Title}",
            amount = amountToRepay,
            transactionType = "Expense",
            date = DateTime.Now,
            notes = $"Repayment for debt: {debt.Title}",
            tags = new List<string> { "Debt Repayment" },
            Currency = "NPR",
        };

        AddTransaction(username, repaymentTransaction);

        if (debt.RemainingAmount <= 0)
        {
            debt.IsCleared = true;
            debt.RemainingAmount = 0;
            debt.ClearedDate = DateTime.Now;
        }

        SaveUsers(users);
        return true;
    }

    public decimal GetTotalDebt(string username)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        if (user == null) return 0;

        decimal totalDebtInNPR = user.debts
            .Where(d => !d.IsCleared)
            .Sum(d => d.RemainingAmount);

        // Convert total debt to user's preferred currency
        return _currencyService.ConvertFromNPR(totalDebtInNPR, user.currency);
    }

    public List<DebtModel> GetActiveDebts(string username)
    {
        return GetDebts(username).Where(d => !d.IsCleared).ToList();
    }

    public List<DebtModel> GetClearedDebts(string username)
    {
        return GetDebts(username).Where(d => d.IsCleared).ToList();
    }

    public bool UpdateUserCurrency(string username, string newCurrency)
    {
        try
        {
            var users = LoadUsers();
            var user = users.FirstOrDefault(u => u.username == username);

            if (user == null)
            {
                Console.WriteLine($"User {username} not found.");
                return false;
            }

            // Validate currency
            if (newCurrency != "NPR" && newCurrency != "USD" && newCurrency != "GBP")
            {
                Console.WriteLine($"Invalid currency: {newCurrency}");
                return false;
            }

            // Update the user's preferred currency
            user.currency = newCurrency;

            // Save the updated user list
            SaveUsers(users);
            Console.WriteLine($"Updated currency for {username} to {newCurrency}.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating currency: {ex.Message}");
            return false;
        }
    }
}
