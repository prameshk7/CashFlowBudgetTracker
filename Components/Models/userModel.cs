public class userModel
{
    public int userID { get; set; }
    public string username { get; set; }
    public string password { get; set; }
    public string email { get; set; }
    public string currency { get; set; } = "NPR";
    public List<TransactionModel> transactions { get; set; } = new List<TransactionModel>();
    public List<DebtModel> debts { get; set; } = new List<DebtModel>();
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
    public string Currency { get; set; }
}


   public class DebtModel
{
    public string DebtID { get; set; } = Guid.NewGuid().ToString(); // Unique identifier
    public string Title { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ClearedDate { get; set; }
    public string Notes { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsCleared { get; set; } = false;
    public string Currency { get; set; }
}