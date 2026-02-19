namespace TGit;

public class FileEditInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsStaged { get; set; }
}
