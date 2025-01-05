
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class userService
{
    private string _currentUsername;
    private readonly string FilePath;
    private static int userCounter = 0;
    private static int transactionCounter = 0;
    private static int debtCounter = 0;  // Added for debt tracking

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

        foreach (var transaction in user.transactions)
        {
            if (transaction.transactionType == "Income")
            {
                balance += transaction.amount;
            }
            else if (transaction.transactionType == "Expense" || transaction.transactionType == "Debt")
            {
                balance -= transaction.amount;
            }
        }

        Console.WriteLine($"Current balance for {username} is {balance:C}.");
        return balance;
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

        transaction.transactionID = (transactionCounter++).ToString();
        transaction.Currency = user.currency;

        user.transactions.Add(transaction);
        Console.WriteLine($"Adding transaction for {username}: {transaction.title} - {transaction.amount}");

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

        Console.WriteLine($"Returning transactions for {username}. Total transactions: {user.transactions.Count}");
        return user.transactions;
    }

    public List<TransactionModel>? FilterTransactions(string username, Func<TransactionModel, bool> predicate)
    {
        var transactions = GetTransactions(username);
        return transactions?.Where(predicate).ToList();
    }

    // New Debt Management Methods
    public List<DebtModel> GetDebts(string username)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        return user?.debts ?? new List<DebtModel>();
    }

    public bool AddDebt(string username, DebtModel debt)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        if (user == null) return false;

        debtCounter++;
        debt.DebtID = $"DEBT_{debtCounter}";
        debt.Currency = user.currency;
        user.debts.Add(debt);

        // Create corresponding transaction
        var debtTransaction = new TransactionModel
        {
            title = $"Debt: {debt.Title}",
            amount = debt.OriginalAmount,
            transactionType = "Debt",
            date = DateTime.Now,
            notes = debt.Notes,
            tags = debt.Tags,
            Currency = debt.Currency,
        };

        AddTransaction(username, debtTransaction);
        SaveUsers(users);
        return true;
    }

    public bool ClearDebt(string username, string debtId, decimal amountToRepay)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);
        if (user == null) return false;

        var debt = user.debts.FirstOrDefault(d => d.DebtID == debtId);
        if (debt == null) return false;

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
            Currency = debt.Currency,
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
        var debts = GetDebts(username);
        return debts.Where(d => !d.IsCleared).Sum(d => d.RemainingAmount);
    }

    public List<DebtModel> GetActiveDebts(string username)
    {
        return GetDebts(username).Where(d => !d.IsCleared).ToList();
    }

    public List<DebtModel> GetClearedDebts(string username)
    {
        return GetDebts(username).Where(d => d.IsCleared).ToList();
    }
}








