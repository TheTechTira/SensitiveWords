namespace SensitiveWords.Domain.Entities
{
    /// <summary>
    /// Sensitive word entity from database for internal use
    /// </summary>
    public class SensitiveWord
  {
    public int Id { get; init; }
    public string Word { get; init; } = "";
    public bool IsActive { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
  }
}
