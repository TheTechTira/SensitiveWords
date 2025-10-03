using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using SensitiveWords.Application.Abstractions.Repositories;
using SensitiveWords.Application.Common.Enums;
using SensitiveWords.Application.Common.Options;
using SensitiveWords.Application.Common.Results;
using SensitiveWords.Application.Services;
using SensitiveWords.Domain.Entities;

namespace SensitiveWords.API.Tests.Services
{
    public class SensitiveWordServiceTests
    {
        private readonly Mock<ISensitiveWordRepository> _repo = new();
        private IOptions<SensitiveWordPolicyOptions> Options(params string[] blocked) =>
            Microsoft.Extensions.Options.Options.Create(new SensitiveWordPolicyOptions { BlockedWords = blocked });

        private static SensitiveWord W(int id = 1, string word = "TEST", bool active = true) => new SensitiveWord
        {
            Id = id,
            Word = word,
            IsActive = active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = null,
            IsDeleted = false,
            DeletedAtUtc = null
        };

        #region ListAsync

        [Fact]
        public async Task ListAsync_MapsPagedResult_AndSetsMessage()
        {
            // Arrange
            var page = 1; var pageSize = 50; var search = "te";
            var domainItems = new[] { W(1, "ONE"), W(2, "TWO"), W(3, "THREE") };
            var paged = new PagedResult<SensitiveWord>
            {
                Items = domainItems,
                Page = page,
                PageSize = pageSize,
                TotalCount = 3
            };
            _repo.Setup(r => r.ListAsync(page, pageSize, search, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<PagedResult<SensitiveWord>>.Ok(paged));

            var svc = new SensitiveWordService(_repo.Object, Options());

            // Act
            var res = await svc.ListAsync(page, pageSize, search, default);

            // Assert
            res.Status.Should().Be(EnumServiceResultStatus.Ok);
            res.Message.Should().Be("Words listed.");
            res.Data!.TotalCount.Should().Be(3);
            res.Data.Items.Should().HaveCount(3);
            _repo.Verify(r => r.ListAsync(page, pageSize, search, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region GetAsync

        [Fact]
        public async Task GetAsync_Found_ReturnsOkAndDto()
        {
            _repo.Setup(r => r.GetAsync(42, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<SensitiveWord>.Ok(W(42, "HELLO")));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.GetAsync(42, default);

            res.Status.Should().Be(EnumServiceResultStatus.Ok);
            res.Message.Should().Be("Word fetched successfully.");
            res.Data!.Id.Should().Be(42);
            res.Data.Word.Should().Be("HELLO");
        }

        [Fact]
        public async Task GetAsync_NotFound_BubblesStatus()
        {
            _repo.Setup(r => r.GetAsync(999, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<SensitiveWord>.NotFound("nope"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.GetAsync(999, default);

            res.Status.Should().Be(EnumServiceResultStatus.NotFound);
            res.Data.Should().BeNull();
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task CreateAsync_BlockedWord_ReturnsInvalid_DoesNotCallRepo()
        {
            var svc = new SensitiveWordService(_repo.Object, Options("forbidden"));

            var res = await svc.CreateAsync("  forbidden  ", isActive: true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Invalid);
            res.ErrorCode.Should().Be("word_blocked");
            _repo.Verify(r => r.CreateOrReviveAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_TrimAndCreateOrRevive_ReturnsOk_AndPassesTrimmed()
        {
            string? capturedWord = null;
            _repo.Setup(r => r.CreateOrReviveAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
                 .Callback<string, bool, CancellationToken>((w, _, __) => capturedWord = w)
                 .ReturnsAsync(RepositoryResult<int>.Ok(101));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.CreateAsync("  Hello  ", isActive: true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Ok);
            res.Message.Should().Be("Word created or revived.");
            res.Data.Should().Be(101);
            capturedWord.Should().Be("Hello");
        }

        [Fact]
        public async Task CreateAsync_Conflict_MapsToServiceConflict()
        {
            _repo.Setup(r => r.CreateOrReviveAsync("Dup", true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<int>.Conflict("Word already exists", "word_conflict"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.CreateAsync("Dup", true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Conflict);
            res.ErrorCode.Should().Be("word_conflict");
            res.Message.Should().Be("Word already exists");
        }

        [Fact]
        public async Task CreateAsync_Error_MapsToServiceError()
        {
            _repo.Setup(r => r.CreateOrReviveAsync("X", true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<int>.Error("db failed", "db_error"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.CreateAsync("X", true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Error);
            res.ErrorCode.Should().Be("db_error");
            res.Message.Should().Be("db failed");
        }

        #endregion

        #region UpdateAsync

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdateAsync_BlankWord_ReturnsInvalid_NoRepoCall(string? bad)
        {
            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.UpdateAsync(1, bad!, true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Invalid);
            res.ErrorCode.Should().Be("word_required");
            _repo.Verify(r => r.UpdateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_Ok_MapsToServiceOk_AndTrims()
        {
            string? capturedWord = null;
            _repo.Setup(r => r.UpdateAsync(7, It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                 .Callback<int, string, bool, CancellationToken>((_, w, __, ___) => capturedWord = w)
                 .ReturnsAsync(RepositoryResult<bool>.Ok(true, msg: "Word updated."));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.UpdateAsync(7, "  New  ", false, default);

            res.Status.Should().Be(EnumServiceResultStatus.Ok);
            res.Message.Should().Be("Word updated.");
            res.Data.Should().BeTrue();
            capturedWord.Should().Be("New");
        }

        [Fact]
        public async Task UpdateAsync_NotFound_MapsToServiceNotFound()
        {
            _repo.Setup(r => r.UpdateAsync(1, "X", true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<bool>.NotFound("nope"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.UpdateAsync(1, "X", true, default);

            res.Status.Should().Be(EnumServiceResultStatus.NotFound);
        }

        [Fact]
        public async Task UpdateAsync_Conflict_MapsToServiceConflict()
        {
            _repo.Setup(r => r.UpdateAsync(1, "dup", true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<bool>.Conflict("exists", "word_conflict"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.UpdateAsync(1, "dup", true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Conflict);
            res.ErrorCode.Should().Be("word_conflict");
            res.Message.Should().Be("exists");
        }

        [Fact]
        public async Task UpdateAsync_Invalid_MapsToServiceInvalid()
        {
            _repo.Setup(r => r.UpdateAsync(1, "bad", true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<bool>.Invalid("nope", "E123"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.UpdateAsync(1, "bad", true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Invalid);
            res.ErrorCode.Should().Be("E123");
            res.Message.Should().Be("nope");
        }

        [Fact]
        public async Task UpdateAsync_Error_MapsToServiceError()
        {
            _repo.Setup(r => r.UpdateAsync(1, "x", true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<bool>.Error("db", "db_error"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.UpdateAsync(1, "x", true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Error);
            res.ErrorCode.Should().Be("db_error");
            res.Message.Should().Be("db");
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_SoftDelete_MapsToOkAndPassesSoftDeleteTrue()
        {
            // soft delete: hardDelete=false -> pass softDelete:true
            _repo.Setup(r => r.DeleteAsync(5, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<bool>.Ok(true));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.DeleteAsync(5, hardDelete: false, default);

            res.Status.Should().Be(EnumServiceResultStatus.Ok);
            res.Message.Should().Be("Word soft-deleted.");
            _repo.Verify(r => r.DeleteAsync(5, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_HardDelete_MapsToOkAndPassesSoftDeleteFalse()
        {
            // hard delete: hardDelete=true -> pass softDelete:false
            _repo.Setup(r => r.DeleteAsync(6, false, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<bool>.Ok(true));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.DeleteAsync(6, hardDelete: true, default);

            res.Status.Should().Be(EnumServiceResultStatus.Ok);
            res.Message.Should().Be("Word deleted.");
            _repo.Verify(r => r.DeleteAsync(6, false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_NotFound_MapsToServiceNotFound()
        {
            _repo.Setup(r => r.DeleteAsync(1, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<bool>.NotFound("nope"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.DeleteAsync(1, hardDelete: false, default);

            res.Status.Should().Be(EnumServiceResultStatus.NotFound);
        }

        [Fact]
        public async Task DeleteAsync_Error_MapsToServiceError()
        {
            _repo.Setup(r => r.DeleteAsync(1, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(RepositoryResult<bool>.Error("db", "db_error"));

            var svc = new SensitiveWordService(_repo.Object, Options());

            var res = await svc.DeleteAsync(1, hardDelete: false, default);

            res.Status.Should().Be(EnumServiceResultStatus.Error);
            res.ErrorCode.Should().Be("db_error");
            res.Message.Should().Be("db");
        }

        #endregion
    }
}
