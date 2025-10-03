namespace SensitiveWords.Domain.Dtos
{
    /// <summary>
    /// Sensitive word DTO for transferring data
    /// </summary>
    public class SensitiveWordDto
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id"></param>
        /// <param name="word"></param>
        /// <param name="isActive"></param>
        public SensitiveWordDto(int id, string word, bool isActive)
        {
            Id = id;
            Word = word;
            IsActive = isActive;
        }

        /// <summary>
        /// Record ID for word in DB
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Actual word in string like "FOO"
        /// </summary>
        public string Word { get; set; } = null!;

        /// <summary>
        /// Active status of the word
        /// </summary>
        public bool IsActive { get; set; }
    }
}
