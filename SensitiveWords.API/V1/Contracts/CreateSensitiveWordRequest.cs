using System.ComponentModel.DataAnnotations;

namespace SensitiveWords.API.V1.Contracts
{
    public class CreateSensitiveWordRequest : IValidatableObject
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Word { get; init; } = "";

        // business toggle
        public bool IsActive { get; init; } = true;

        /// <summary>
        /// To demostrate manually validation at transport level
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
        {
            if (Word?.ToLower().Equals("blacklist") == true)
                yield return new ValidationResult("Word is black listed and not allowed.", [nameof(Word)]);
        }
    }
}
