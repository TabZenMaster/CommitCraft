namespace CodeReview.Infrastructure;

public class AppDbContext
{
    public static string ConnectionString { get; set; } = "Server=127.0.0.1;Port=3306;Uid=root;Pwd=123456;Database=code_review_oc;AllowLoadLocalInFile=true;CharSet=utf8mb4;";
}
