using FluentAssertions;
using Moq;
using SensitiveWords.Application.Abstractions.Repositories;
using SensitiveWords.Infrastructure.Seed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SensitiveWords.API.Tests.Seed
{
    public class WordSeederTests
    {
        [Fact]
        public async Task SeedFromFileAsync_FileMissing_ThrowsFileNotFound()
        {
            // Arrange
            var repo = new Mock<ISensitiveWordRepository>();
            var missing = Guid.NewGuid().ToString("N") + ".txt";

            // Act
            var act = async () => await WordSeeder.SeedFromFileAsync(missing, repo.Object, default);

            // Assert
            await act.Should().ThrowAsync<FileNotFoundException>();
            repo.Verify(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SeedFromFileAsync_PlainText_TrimsAndSkipsEmptyLines_PassesToRepo()
        {
            // Arrange
            var repo = new Mock<ISensitiveWordRepository>();
            var path = Path.GetTempFileName();
            try
            {
                // Plain text: trims & drops empty lines
                await File.WriteAllLinesAsync(path, new[]
                {
                "  one  ",
                "",
                "two",
                "   ",
                "three ",
            });

                List<string>? captured = null;
                CancellationToken capturedCt = default;

                repo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback<IEnumerable<string>, CancellationToken>((w, ct) =>
                    {
                        captured = w.ToList();
                        capturedCt = ct;
                    });

                var cts = new CancellationTokenSource();

                // Act
                await WordSeeder.SeedFromFileAsync(path, repo.Object, cts.Token);

                // Assert
                captured.Should().NotBeNull();
                captured!.Should().BeEquivalentTo(new[] { "one", "two", "three" }, opts => opts.WithoutStrictOrdering());
                capturedCt.Should().Be(cts.Token);

                repo.Verify(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task SeedFromFileAsync_JsonArray_PassesDeserializedStrings_AsIs()
        {
            // Arrange
            var repo = new Mock<ISensitiveWordRepository>();
            var path = Path.GetTempFileName();
            try
            {
                var payload = new List<string> { "Hello", "world", "  test  ", "" };
                // Add leading whitespace/newline to ensure TrimStart + first-char detection works
                var json = "\n   " + JsonSerializer.Serialize(payload);
                await File.WriteAllTextAsync(path, json);

                List<string>? captured = null;

                repo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback<IEnumerable<string>, CancellationToken>((w, _) => captured = w.ToList());

                var ct = new CancellationTokenSource().Token;

                // Act
                await WordSeeder.SeedFromFileAsync(path, repo.Object, ct);

                // Assert
                // JSON branch does not trim entries; it forwards them as in the file
                captured.Should().NotBeNull();
                captured!.Should().Equal(payload);
                repo.Verify(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<string>>(), ct), Times.Once);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task SeedFromFileAsync_JsonArray_EmptyList_CallsRepoWithEmpty()
        {
            // Arrange
            var repo = new Mock<ISensitiveWordRepository>();
            var path = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(path, "[]");

                IEnumerable<string>? captured = null;

                repo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback<IEnumerable<string>, CancellationToken>((w, _) => captured = w);

                // Act
                await WordSeeder.SeedFromFileAsync(path, repo.Object, default);

                // Assert
                captured.Should().NotBeNull();
                captured!.Should().BeEmpty();
                repo.Verify(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
