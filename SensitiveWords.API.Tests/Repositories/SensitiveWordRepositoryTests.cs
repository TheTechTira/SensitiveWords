using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Moq;
using SensitiveWords.Application.Abstractions.Data;
using SensitiveWords.Application.Common.Enums;
using SensitiveWords.Domain.Entities;
using SensitiveWords.Infrastructure.Data;
using SensitiveWords.Infrastructure.Repositories;
using System.Data;
using System.Reflection;

namespace SensitiveWords.API.Tests.Repositories
{
    public class SensitiveWordRepositoryTests
    {
        private readonly Mock<ISqlConnectionFactory> _factory = new();
        private readonly Mock<IDbConnection> _conn = new();
        private readonly Mock<IDapperExecutor> _exec = new();

        public SensitiveWordRepositoryTests()
        {
            _factory.Setup(f => f.Create()).Returns(_conn.Object);
        }

        private static SensitiveWord MakeWord(int id = 1, string word = "TEST", bool active = true) => new()
        {
            Id = id,
            Word = word,
            IsActive = active,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = null,
            IsDeleted = false,
            DeletedAtUtc = null
        };

        #region GetAsync

        [Fact]
        public async Task GetAsync_Found_ReturnsOkWithEntity()
        {
            var entity = MakeWord(42, "HELLO");

            _exec.Setup(x => x.QuerySingleOrDefaultAsync<SensitiveWord>(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(entity);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.GetAsync(42, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data.Should().BeEquivalentTo(entity);

            _exec.Verify(x => x.QuerySingleOrDefaultAsync<SensitiveWord>(_conn.Object, It.IsAny<CommandDefinition>()), Times.Once);
        }

        [Fact]
        public async Task GetAsync_NotFound_ReturnsNotFound()
        {
            _exec.Setup(x => x.QuerySingleOrDefaultAsync<SensitiveWord>(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync((SensitiveWord?)null);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.GetAsync(999, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.NotFound);
            result.Data.Should().BeNull();
        }

        #endregion

        #region ListAsync

        [Fact]
        public async Task ListAsync_WithSearch_ReturnsPagedResult()
        {
            var totalCount = 3;
            var items = new[] { MakeWord(1), MakeWord(2), MakeWord(3) };

            var multi = new Mock<IMultiResult>();
            multi.Setup(m => m.ReadFirstAsync<int>()).ReturnsAsync(totalCount);
            multi.Setup(m => m.ReadAsync<SensitiveWord>()).ReturnsAsync(items);

            _exec.Setup(x => x.QueryMultipleAsync(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(multi.Object);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.ListAsync(page: 1, pageSize: 50, search: "TE", ct: default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data.Should().NotBeNull();
            result.Data!.Items.Should().HaveCount(3);
            result.Data.TotalCount.Should().Be(3);
            result.Data.Page.Should().Be(1);
            result.Data.PageSize.Should().Be(50);

            multi.Verify(m => m.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ListAsync_ClampsPageAndPageSize()
        {
            var multi = new Mock<IMultiResult>();
            multi.Setup(m => m.ReadFirstAsync<int>()).ReturnsAsync(0);
            multi.Setup(m => m.ReadAsync<SensitiveWord>()).ReturnsAsync(Array.Empty<SensitiveWord>());

            _exec.Setup(x => x.QueryMultipleAsync(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(multi.Object);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.ListAsync(page: 0, pageSize: -10, search: null, ct: default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data!.Page.Should().Be(1);
            result.Data!.PageSize.Should().Be(50);
            multi.Verify(m => m.Dispose(), Times.Once);
        }

        #endregion

        #region ListActiveAsync

        [Fact]
        public async Task ListActiveAsync_Found_ReturnsOkWithItems()
        {
            _exec.Setup(x => x.QueryAsync<SensitiveWord>(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(new[] { MakeWord(1), MakeWord(2) });

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.ListActiveAsync(default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data.Should().NotBeNull();
            result.Data!.Should().HaveCount(2);
            result.Affected.Should().Be(2);
            result.Message.Should().Be("Active words retrieved.");
        }

        [Fact]
        public async Task ListActiveAsync_OnException_ReturnsError()
        {
            _exec.Setup(x => x.QueryAsync<SensitiveWord>(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ThrowsAsync(new Exception("boom"));

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.ListActiveAsync(default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Error);
            result.ErrorCode.Should().Be("db_error");
            result.Message.Should().Contain("Failed to fetch active words");
        }

        #endregion

        #region CreateAsync

        [Fact]
        public async Task CreateAsync_InsertsAndBumpsVersion_ReturnsOkWithId()
        {
            _exec.Setup(x => x.ExecuteScalarAsync<int>(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("INSERT INTO dbo.SensitiveWord"))))
                 .ReturnsAsync(101);

            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("MERGE dbo.Metadata"))))
                 .ReturnsAsync(1);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.CreateAsync("NewWord", true, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data.Should().Be(101);
            result.Message.Should().Be("Word created.");

            _exec.Verify(x => x.ExecuteScalarAsync<int>(_conn.Object, It.IsAny<CommandDefinition>()), Times.Once);
            _exec.Verify(x => x.ExecuteAsync(_conn.Object, It.IsAny<CommandDefinition>()), Times.Once);
        }

        #endregion

        #region CreateOrReviveAsync

        [Fact]
        public async Task CreateOrReviveAsync_Upsert_ReturnsIdAndBumpsVersion()
        {
            _exec.Setup(x => x.ExecuteScalarAsync<int>(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(7);

            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("MERGE dbo.Metadata"))))
                 .ReturnsAsync(1);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.CreateOrReviveAsync("hello", true, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data.Should().Be(7);
            result.Message.Should().Be("Word created or revived.");
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UpdateAsync_Success_ReturnsOkAndBumpsVersion()
        {
            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("UPDATE dbo.SensitiveWord"))))
                 .ReturnsAsync(1);

            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("MERGE dbo.Metadata"))))
                 .ReturnsAsync(1);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.UpdateAsync(3, "RENAMED", true, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateAsync_NoRows_ReturnsNotFound()
        {
            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(0);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.UpdateAsync(999, "X", true, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.NotFound);
        }

        [Fact]
        public async Task UpdateAsync_Conflict_HandlesSqlException_ReturnsConflict()
        {
            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ThrowsAsync(CreateSqlException(50001)); // matches your THROW 50001

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.UpdateAsync(10, "DUPLICATE", true, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Conflict);
            result.ErrorCode.Should().Be("word_conflict");
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_SoftDelete_BumpsVersion_ReturnsOk()
        {
            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("UPDATE dbo.SensitiveWord") &&
                     cd.CommandText.Contains("IsDeleted = 1"))))
                 .ReturnsAsync(1);

            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("MERGE dbo.Metadata"))))
                 .ReturnsAsync(1);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.DeleteAsync(5, softDelete: true, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteAsync_HardDelete_BumpsVersion_ReturnsOk()
        {
            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("DELETE FROM dbo.SensitiveWord"))))
                 .ReturnsAsync(1);

            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("MERGE dbo.Metadata"))))
                 .ReturnsAsync(1);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.DeleteAsync(6, softDelete: false, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.Ok);
            result.Data.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteAsync_NoRows_ReturnsNotFound()
        {
            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(0);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var result = await sut.DeleteAsync(123, softDelete: true, default);

            result.Status.Should().Be(EnumRepositoryResultStatus.NotFound);
        }

        #endregion

        #region IncrementWordsVersionAsync

        [Fact]
        public async Task IncrementWordsVersionAsync_ExecutesMerge()
        {
            _exec.Setup(x => x.ExecuteAsync(_conn.Object, It.Is<CommandDefinition>(cd =>
                     cd.CommandText.Contains("MERGE dbo.Metadata"))))
                 .ReturnsAsync(1);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            await sut.IncrementWordsVersionAsync(default);

            _exec.Verify(x => x.ExecuteAsync(_conn.Object, It.IsAny<CommandDefinition>()), Times.Once);
        }

        #endregion

        #region BulkUpsertAsync

        [Fact]
        public async Task BulkUpsertAsync_NormalizesAndMergesAndBumps()
        {
            _exec.SetupSequence(x => x.ExecuteAsync(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(0)  // create type (idempotent)
                 .ReturnsAsync(5)  // merge
                 .ReturnsAsync(1); // bump

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            await sut.BulkUpsertAsync(new[] { "  hello ", "HELLO", "world" }, default);

            _exec.Verify(x => x.ExecuteAsync(_conn.Object, It.IsAny<CommandDefinition>()), Times.Exactly(3));
        }

        [Fact]
        public async Task BulkUpsertAsync_EmptyInput_DoesNothing()
        {
            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            await sut.BulkUpsertAsync(Array.Empty<string>(), default);

            _exec.Verify(x => x.ExecuteAsync(_conn.Object, It.IsAny<CommandDefinition>()), Times.Never);
        }

        #endregion

        #region GetWordsVersionAsync

        [Fact]
        public async Task GetWordsVersionAsync_ReturnsValue()
        {
            _exec.Setup(x => x.ExecuteScalarAsync<int?>(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync(12);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var v = await sut.GetWordsVersionAsync(default);

            v.Should().Be(12);
        }

        [Fact]
        public async Task GetWordsVersionAsync_Null_ReturnsZero()
        {
            _exec.Setup(x => x.ExecuteScalarAsync<int?>(_conn.Object, It.IsAny<CommandDefinition>()))
                 .ReturnsAsync((int?)null);

            var sut = new SensitiveWordRepository(_factory.Object, _exec.Object);

            var v = await sut.GetWordsVersionAsync(default);

            v.Should().Be(0);
        }

        #endregion

        #region Error

        private static SqlException CreateSqlException(int number, string message = "boom")
        {
            var sqlError = CreateSqlError(number, state: 1, errorClass: 16,
                                          server: "server", msg: message, proc: "proc", line: 42);

            // Build SqlErrorCollection and add our SqlError
            var errorCollection = (SqlErrorCollection)Activator.CreateInstance(
                typeof(SqlErrorCollection), nonPublic: true)!;

            typeof(SqlErrorCollection)
                .GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(errorCollection, new object[] { sqlError });

            // Try internal ctor: SqlException(string, SqlErrorCollection, Exception, Guid)
            var exCtor = typeof(SqlException)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(c =>
                {
                    var ps = c.GetParameters();
                    return ps.Length == 4
                           && ps[0].ParameterType == typeof(string)
                           && ps[1].ParameterType == typeof(SqlErrorCollection);
                });

            if (exCtor != null)
            {
                return (SqlException)exCtor.Invoke(new object?[] { message, errorCollection, null, Guid.NewGuid() });
            }

            // Fallback: internal static CreateException(SqlErrorCollection, string serverVersion)
            var factory = typeof(SqlException).GetMethod(
                "CreateException",
                BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(SqlErrorCollection), typeof(string) },
                modifiers: null);

            if (factory != null)
            {
                return (SqlException)factory.Invoke(null, new object[] { errorCollection, "11.0.0" })!;
            }

            throw new InvalidOperationException("Could not create SqlException via reflection for this client version.");
        }

        private static SqlError CreateSqlError(int number, byte state, byte errorClass,
                                               string server, string msg, string proc, int line)
        {
            // Look for a ctor whose FIRST 7 params match (int, byte, byte, string, string, string, int)
            var targetTypes = new[] { typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int) };

            var ctor = typeof(SqlError)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(c =>
                {
                    var ps = c.GetParameters();
                    if (ps.Length < 7) return false;
                    for (int i = 0; i < 7; i++)
                        if (ps[i].ParameterType != targetTypes[i]) return false;
                    return true;
                });

            if (ctor == null)
                throw new InvalidOperationException("No compatible SqlError constructor found (signature changed).");

            var paramCount = ctor.GetParameters().Length;
            var args = new object?[paramCount];

            // Fill the first 7 required args
            args[0] = number;   // infoNumber
            args[1] = state;    // errorState
            args[2] = errorClass; // errorClass
            args[3] = server;   // server
            args[4] = msg;      // message
            args[5] = proc;     // procedure
            args[6] = line;     // lineNumber

            // Any extra ctor params → fill with defaults
            for (int i = 7; i < paramCount; i++)
            {
                var t = ctor.GetParameters()[i].ParameterType;
                args[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
            }

            return (SqlError)ctor.Invoke(args);
        }

        #endregion
    }
}