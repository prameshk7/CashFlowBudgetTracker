using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class userService
{

    private string _currentUsername;

    // Set the current username when the user logs in
    public void SetUsername(string username)
    {
        _currentUsername = username;
    }

    // Get the current username
    public string GetUsername()
    {
        return _currentUsername;
    }

    private readonly string FilePath;
    private static int userCounter = 0;
    private static int transactionCounter = 0;


    public userService()
    {
        FilePath = @"D:\AppDEV\CW-Data\users.json";

        var users = LoadUsers();
        if (users.Any())
        {
            var allTransactions = users.SelectMany(u => u.transactions).ToList();

            if (allTransactions.Any())
            {
                transactionCounter = allTransactions.Max(t => int.Parse(t.transactionID));
            }
            else
            {
                transactionCounter = 0; // No transactions exist, so start from 0
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
        return Convert.ToBase64String(hash); // Return hashed password
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
            return false; // Username or email already exists
        }

        userCounter++;


        var newUser = new userModel
        {
            userID = userCounter,
            username = username,
            email = email,
            password = HashPassword(password),
            transactions = new List<TransactionModel>(),
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
        var users = LoadUsers(); // Load all users
        var user = users.FirstOrDefault(u => u.username == username);

        if (user == null)
        {
            Console.WriteLine($"User {username} not found.");
            return null; // Return null if user is not found
        }

        Console.WriteLine($"Currency for {username} is {user.currency}.");
        return user.currency; // Return the user's preferred currency
    }

    public decimal GetCurrentBalance(string username)
    {
        var users = LoadUsers();
        var user = users.FirstOrDefault(u => u.username == username);

        if (user == null)
        {
            Console.WriteLine($"User {username} not found.");
            return 0; // Return 0 if the user is not found
        }

        decimal balance = 0;

        // Calculate balance based on transaction type
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
            return false; // User not found
        }

        transaction.transactionID = (transactionCounter++).ToString(); // Ensure unique ID
        transaction.Currency = user.currency;

        user.transactions.Add(transaction);
        Console.WriteLine($"Adding transaction for {username}: {transaction.title} - {transaction.amount}");

        // Save the updated users list to file
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
            return null; // Return null if user is not found
        }

        Console.WriteLine($"Returning transactions for {username}. Total transactions: {user.transactions.Count}");
        return user.transactions;
    }

    public List<TransactionModel>? FilterTransactions(string username, Func<TransactionModel, bool> predicate)
    {
        var transactions = GetTransactions(username);
        return transactions?.Where(predicate).ToList();
    }
}

