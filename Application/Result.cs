namespace CodeReview.Application;

public class Result<T>
{
    public bool Success { get; set; }
    public string Msg { get; set; } = "";
    public T? Data { get; set; }

    public static Result<T> Ok(T data, string msg = "操作成功") =>
        new() { Success = true, Data = data, Msg = msg };

    public static Result<T> Fail(string msg) =>
        new() { Success = false, Msg = msg };
}

public class Result
{
    public bool Success { get; set; }
    public string Msg { get; set; } = "";

    public static Result Ok(string msg = "操作成功") =>
        new() { Success = true, Msg = msg };

    public static Result Fail(string msg) =>
        new() { Success = false, Msg = msg };
}

public class PagedResult<T>
{
    public bool Success { get; set; } = true;
    public List<T> Data { get; set; } = new();
    public int Total { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int PageCount => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
}
