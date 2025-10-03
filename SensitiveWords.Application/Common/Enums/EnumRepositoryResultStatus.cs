namespace SensitiveWords.Application.Common.Enums
{
    /// <summary>
    /// Enumeration of possible result statuses from repository operations.
    /// </summary>
    public enum EnumRepositoryResultStatus
    {
        Ok = 0,
        NotFound = 1,
        Conflict = 2,
        Invalid = 3,
        Error = 4
    }
}
