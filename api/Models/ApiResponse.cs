namespace TGitApi.Models;

public class ApiResponse
{
    public bool Success { get; set; }
}

public class ApiErrorResponse
{
    public string Error { get; set; } = string.Empty;
}
