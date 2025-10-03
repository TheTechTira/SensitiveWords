using SensitiveWords.Application.Abstractions.Repositories;
using System.Text.Json;

namespace SensitiveWords.Infrastructure.Seed
{
    /// <summary>
    /// Utility class for seeding sensitive words into the repository from a file.
    /// 
    /// Supported formats:
    /// - Plain text file (one word per line).
    /// - JSON array of strings (["word1", "word2", ...]).
    /// 
    /// Behavior:
    /// - Detects format by inspecting the first non-whitespace character of the file.
    ///   '[' → JSON array, otherwise → plain text.
    /// - Trims whitespace and ignores empty lines.
    /// - Delegates to <see cref="ISensitiveWordRepository.BulkUpsertAsync"/> 
    ///   so duplicates are handled consistently (normalized to UPPER, distinct, reactivated if inactive).
    /// - Throws <see cref="FileNotFoundException"/> if the file is missing.
    /// 
    /// Usage:
    ///   await WordSeeder.SeedFromFileAsync("sensitive-words.txt", repo, ct);
    /// </summary>
    public static class WordSeeder
    {
        /// <summary>
        /// Reads words from the given file and bulk-upserts them into the repository.
        /// </summary>
        /// <param name="filePath">Path to a JSON or plain text file containing words.</param>
        /// <param name="repo">Repository used to upsert the words.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <exception cref="FileNotFoundException">Thrown if the file does not exist.</exception>
        public static async Task SeedFromFileAsync(
            string filePath,
            ISensitiveWordRepository repo,
            CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Sensitive words file not found", filePath);

            IEnumerable<string> words;

            // Detect JSON array vs plain text.
            // NOTE: This reads the whole file once just to check the first char.
            var firstChar = (await File.ReadAllTextAsync(filePath, ct))
                                .TrimStart()[0];

            if (firstChar == '[')
            {
                // JSON array of strings
                words = JsonSerializer.Deserialize<List<string>>(
                    await File.ReadAllTextAsync(filePath, ct),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            else
            {
                // Plain text: one word per line
                words = File.ReadAllLines(filePath)
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .Select(l => l.Trim());
            }

            // Insert/update words in bulk
            await repo.BulkUpsertAsync(words, ct);
        }
    }
}
