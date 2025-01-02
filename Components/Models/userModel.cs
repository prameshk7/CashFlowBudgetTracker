public class userModel
{
    public int userID { get; set; }
    public string username { get; set; }
    public string password { get; set; }
    public string email { get; set; }
    public string currency { get; set; } = "NPR";
    public List<TransactionModel> transactions { get; set; } = new List<TransactionModel>();
}

public class TransactionModel
{
    public string transactionID { get; set; }
    public string title { get; set; }
    public decimal amount { get; set; }
    public string transactionType { get; set; } = "Income";
    public DateTime date { get; set; }
    public string notes { get; set; }
    public List<string> tags { get; set; } = new List<string>();
    public bool isDebtCleared { get; set; }
    public string Currency { get; set; }
}
