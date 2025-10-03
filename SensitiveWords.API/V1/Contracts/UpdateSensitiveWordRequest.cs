using System.ComponentModel.DataAnnotations;

namespace SensitiveWords.API.V1.Contracts
{
    public class UpdateSensitiveWordRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Word { get; init; } = "";

        public bool IsActive { get; init; } = true;
    }
}
