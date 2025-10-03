using FluentAssertions;
using Moq;
using SensitiveWords.Application.Abstractions.Caching;
using SensitiveWords.Application.Services;
using SensitiveWords.Domain.Dtos;
using System.Text.RegularExpressions;

namespace SensitiveWords.API.Tests.Services
{
    public class BloopServiceTests
    {
        private readonly Mock<IWordsCache> _cache = new();

        private BloopService CreateSut() => new BloopService(_cache.Object);

        private static Regex Rx(string pattern) =>
            new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        [Fact]
        public async Task BloopAsync_WholeWordTrue_MasksWholeWordsOnly()
        {
            // Arrange
            // Matches "cat" as a whole word, but not inside "catalog".
            _cache.Setup(c => c.GetRegexAsync(true, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Rx(@"(?<!\w)cat(?!\w)"));

            var sut = CreateSut();
            var msg = "the catalog has cat";
            var req = new BloopRequestDto(message: msg, wholeWord: true);
            using var cts = new CancellationTokenSource();

            // Act
            var res = await sut.BloopAsync(req, cts.Token);

            // Assert
            res.Original.Should().Be(msg);
            res.Blooped.Should().Be("the catalog has ***");
            res.Matches.Should().Be(1);
            res.ElapsedMs.Should().BeGreaterThanOrEqualTo(0);

            _cache.Verify(c => c.GetRegexAsync(true, cts.Token), Times.Once);
        }

        [Fact]
        public async Task BloopAsync_WholeWordFalse_MasksSubstringsToo()
        {
            // Arrange
            // Matches "cat" anywhere (so "catalog" and "cat").
            _cache.Setup(c => c.GetRegexAsync(false, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Rx(@"cat"));

            var sut = CreateSut();
            var msg = "the catalog has cat";
            var req = new BloopRequestDto(message: msg, wholeWord: false);
            using var cts = new CancellationTokenSource();

            // Act
            var res = await sut.BloopAsync(req, cts.Token);

            // Assert
            res.Blooped.Should().Be("the ***alog has ***");
            res.Matches.Should().Be(2);
            _cache.Verify(c => c.GetRegexAsync(false, cts.Token), Times.Once);
        }

        [Fact]
        public async Task BloopAsync_NoMatches_ReturnsOriginalAndZeroCount()
        {
            // Arrange
            _cache.Setup(c => c.GetRegexAsync(true, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Rx(@"xyz"));

            var sut = CreateSut();
            var msg = "hello world";
            var req = new BloopRequestDto(message: msg, wholeWord: true);

            // Act
            var res = await sut.BloopAsync(req, CancellationToken.None);

            // Assert
            res.Blooped.Should().Be(msg);
            res.Matches.Should().Be(0);
        }

        [Fact]
        public async Task BloopAsync_MultipleTerms_MasksEachWithSameLengthAsterisks()
        {
            // Arrange
            // Whole-word match for "drop" or "table".
            _cache.Setup(c => c.GetRegexAsync(true, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Rx(@"(?<!\w)(?:drop|table)(?!\w)"));

            var sut = CreateSut();
            var msg = "DROP TABLE users";
            var req = new BloopRequestDto(message: msg, wholeWord: true);

            // Act
            var res = await sut.BloopAsync(req, CancellationToken.None);

            // Assert
            res.Blooped.Should().Be("**** ***** users"); // 4 stars for DROP, 5 for TABLE
            res.Matches.Should().Be(2);
            res.ElapsedMs.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task BloopAsync_PropagatesCancellationFromCache()
        {
            // Arrange
            _cache.Setup(c => c.GetRegexAsync(true, It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new OperationCanceledException());

            var sut = CreateSut();
            var req = new BloopRequestDto(message: "anything", wholeWord: true);

            // Act
            var act = async () => await sut.BloopAsync(req, new CancellationToken(canceled: true));

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task BloopAsync_PassesExactCancellationTokenToCache()
        {
            // Arrange
            _cache.Setup(c => c.GetRegexAsync(true, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Rx(@"cat"));

            var sut = CreateSut();
            using var cts = new CancellationTokenSource();
            var req = new BloopRequestDto(message: "cat", wholeWord: true);

            // Act
            _ = await sut.BloopAsync(req, cts.Token);

            // Assert
            _cache.Verify(c => c.GetRegexAsync(true,
                It.Is<CancellationToken>(t => t == cts.Token)), Times.Once);
        }
    }
}
