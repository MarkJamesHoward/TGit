namespace TGitApi.Data.Entities;

public class FileEditEntity
{
    public int Id { get; set; }
    public int RepoActivityId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsStaged { get; set; }
    public RepoActivityEntity RepoActivity { get; set; } = null!;
}
