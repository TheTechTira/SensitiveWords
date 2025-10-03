using Dapper;
using Microsoft.Data.SqlClient;
using SensitiveWords.Application.Abstractions.Data;
using SensitiveWords.Application.Abstractions.Repositories;
using SensitiveWords.Application.Common.Results;
using SensitiveWords.Domain.Entities;
using SensitiveWords.Infrastructure.Data;
using System.Data;

namespace SensitiveWords.Infrastructure.Repositories
{
    /// <summary>
    /// Dapper-based repository for CRUD operations on SensitiveWord rows.
    /// 
    /// Notes for future devs:
    /// - We use soft-deletes (IsDeleted flag) for most actions so we can revive words later.
    /// - After *any* data mutation we bump a "WordsVersion" counter in dbo.Metadata.
    ///   This can be used by caches or clients to invalidate/refresh.
    /// - Dapper is used with raw SQL for speed and explicit control.
    /// - All methods accept a <see cref="CancellationToken"/> and pass it to Dapper.
    /// </summary>
    public class SensitiveWordRepository : ISensitiveWordRepository
    {
        private readonly ISqlConnectionFactory _factory;
        private readonly IDapperExecutor _exec;

        public SensitiveWordRepository(ISqlConnectionFactory factory, IDapperExecutor exec)
        {
            _factory = factory;
            _exec = exec;
        }

        /// <summary>
        /// Fetch a single non-deleted sensitive word by Id.
        /// </summary>
        public async Task<RepositoryResult<SensitiveWord>> GetAsync(int id, CancellationToken ct)
        {
            const string sql = """
            SELECT Id, Word, IsActive, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc
            FROM dbo.SensitiveWord WHERE Id=@Id AND IsDeleted=0;
            """;

            using var con = _factory.Create();

            // QuerySingleOrDefaultAsync returns null if not found (no exception).
            var row = await _exec.QuerySingleOrDefaultAsync<SensitiveWord>(
                 con, new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

            return row is null
                ? RepositoryResult<SensitiveWord>.NotFound("Word not found.")
                : RepositoryResult<SensitiveWord>.Ok(row);
        }

        /// <summary>
        /// Paged list of non-deleted words, optional "search" (LIKE) on Word field.
        /// </summary>
        public async Task<RepositoryResult<PagedResult<SensitiveWord>>> ListAsync(
            int page, int pageSize, string? search, CancellationToken ct)
        {
            // Safety clamps for paging.
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);

            // Build WHERE with optional filter (kept simple since inputs are parameterized).
            var where = "WHERE IsDeleted = 0";
            if (!string.IsNullOrWhiteSpace(search)) where += " AND Word LIKE @Like";

            // Two-result set query:
            // 1) COUNT for total rows (for pagination UI)
            // 2) Page slice ordered by Id (stable ordering)
            var sql = $@"
        SELECT COUNT(1)
        FROM dbo.SensitiveWord
        {where};

        SELECT Id, Word, IsActive, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc
        FROM dbo.SensitiveWord
        {where}
        ORDER BY Id
        OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

            using var con = _factory.Create();

            // QueryMultipleAsync allows reading multiple result sets in one round-trip.
            using var grid = await _exec.QueryMultipleAsync(con, new CommandDefinition(sql, new
            {
                Like = $"%{search}%",
                Skip = (page - 1) * pageSize,
                Take = pageSize
            }, cancellationToken: ct));

            var total = await grid.ReadFirstAsync<int>();
            var items = (await grid.ReadAsync<SensitiveWord>()).AsList();

            var payload = new PagedResult<SensitiveWord>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            };
            return RepositoryResult<PagedResult<SensitiveWord>>.Ok(payload);
        }

        /// <summary>
        /// Returns all active (IsActive=1) and non-deleted words.
        /// </summary>
        public async Task<RepositoryResult<IReadOnlyList<SensitiveWord>>> ListActiveAsync(CancellationToken ct)
        {
            const string sql = """
    SELECT Id, Word, IsActive, CreatedAtUtc, UpdatedAtUtc, IsDeleted, DeletedAtUtc
      FROM dbo.SensitiveWord
     WHERE IsDeleted=0 AND IsActive=1;
    """;

            using var con = _factory.Create();
            try
            {
                var rows = (await _exec.QueryAsync<SensitiveWord>(
                    con, new CommandDefinition(sql, cancellationToken: ct))).ToList();

                return RepositoryResult<IReadOnlyList<SensitiveWord>>.Ok(
                    rows,
                    affected: rows.Count,
                    msg: rows.Count > 0 ? "Active words retrieved." : "No active words found.");
            }
            catch (Exception ex)
            {
                // Keep error human-readable; include a machine-readable code where helpful.
                return RepositoryResult<IReadOnlyList<SensitiveWord>>
                    .Error($"Failed to fetch active words: {ex.Message}", "db_error");
            }
        }

        /// <summary>
        /// Inserts a new word. If you want "revive if previously soft-deleted",
        /// use <see cref="CreateOrReviveAsync"/> instead.
        /// </summary>
        public async Task<RepositoryResult<int>> CreateAsync(string word, bool isActive, CancellationToken ct)
        {
            const string sql = """
            INSERT INTO dbo.SensitiveWord(Word, IsActive) VALUES(@Word,@IsActive);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

            using var con = _factory.Create();

            // ExecuteScalar returns the new identity (Id).
            var id = await _exec.ExecuteScalarAsync<int>(
                con, new CommandDefinition(sql, new { Word = word, IsActive = isActive }, cancellationToken: ct));

            // Any data-changing operation should bump WordsVersion for cache invalidation.
            await IncrementWordsVersionAsync(ct);

            return RepositoryResult<int>.Ok(id, affected: 1, msg: "Word created.");
        }

        /// <summary>
        /// Creates a word if it doesn't exist; otherwise "revives" a matching (case-insensitive)
        /// existing/soft-deleted row and updates IsActive. Returns the Id in both cases.
        /// </summary>
        public async Task<RepositoryResult<int>> CreateOrReviveAsync(string word, bool isActive, CancellationToken ct)
        {
            // We first try to UPDATE (revive) by case-insensitive match on Word.
            // If no rows affected, we INSERT a new one.
            // NOTE: Keep UPPER() logic in sync with any DB collation expectations.
            const string upsert = """
                SET NOCOUNT ON;
                DECLARE @Id INT;

                -- Try revive a soft-deleted or existing row
                UPDATE dbo.SensitiveWord
                   SET IsDeleted = 0,
                       IsActive = @IsActive,
                       UpdatedAtUtc = SYSUTCDATETIME(),
                       DeletedAtUtc = NULL
                 WHERE UPPER(Word) = UPPER(@Word);

                IF @@ROWCOUNT > 0
                BEGIN
                    SELECT @Id = Id FROM dbo.SensitiveWord WHERE UPPER(Word) = UPPER(@Word);
                    SELECT @Id;
                    RETURN;
                END

                -- Insert new (unique index ignores deleted rows)
                INSERT INTO dbo.SensitiveWord(Word, IsActive) VALUES(@Word, @IsActive);

                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;

            using var con = _factory.Create();
            var id = await _exec.ExecuteScalarAsync<int>(
                con, new CommandDefinition(upsert, new { Word = word, IsActive = isActive }, cancellationToken: ct));

            await IncrementWordsVersionAsync(ct);

            return RepositoryResult<int>.Ok(id, affected: 1, msg: "Word created or revived.");
        }

        /// <summary>
        /// Update a word's value and active flag.
        /// Throws a conflict (handled in catch) if attempting to rename to an existing non-deleted word.
        /// </summary>
        public async Task<RepositoryResult<bool>> UpdateAsync(int id, string word, bool isActive, CancellationToken ct)
        {
            // Custom error code for "word exists" conflict thrown by T-SQL THROW.
            const int ERR_CONFLICT = 50001;

            const string sql = """
    -- Prevent renaming to an existing non-deleted word
    IF EXISTS (SELECT 1 FROM dbo.SensitiveWord WHERE UPPER(Word)=UPPER(@Word) AND IsDeleted=0 AND Id<>@Id)
        THROW 50001, 'Word already exists', 1;

    UPDATE dbo.SensitiveWord
       SET Word = @Word,
           IsActive = @IsActive,
           UpdatedAtUtc = SYSUTCDATETIME()
     WHERE Id = @Id AND IsDeleted = 0;
    """;

            using var con = _factory.Create();
            try
            {
                var n = await _exec.ExecuteAsync(
                    con, new(sql, new { Id = id, Word = word, IsActive = isActive }, cancellationToken: ct));

                if (n == 0) return RepositoryResult<bool>.NotFound("Word not found.");

                await IncrementWordsVersionAsync(ct);
                return RepositoryResult<bool>.Ok(true, affected: n, msg: "Word updated.");
            }
            catch (SqlException ex) when (ex.Number == ERR_CONFLICT)
            {
                // Surface a domain-specific conflict result to the caller.
                return RepositoryResult<bool>.Conflict("Another word with the same value already exists.", "word_conflict");
            }
        }

        /// <summary>
        /// Delete a word. If <paramref name="softDelete"/> is true, marks IsDeleted=1 (preferred);
        /// otherwise hard-deletes the row.
        /// </summary>
        public async Task<RepositoryResult<bool>> DeleteAsync(int id, bool softDelete, CancellationToken ct)
        {
            // Soft delete keeps history/ability to revive; hard delete removes permanently.
            string sql = softDelete
                ? """
          UPDATE dbo.SensitiveWord
             SET IsDeleted = 1,
                 IsActive = 0,
                 DeletedAtUtc = SYSUTCDATETIME(),
                 UpdatedAtUtc = SYSUTCDATETIME()
           WHERE Id = @Id AND IsDeleted = 0;
          """
                : "DELETE FROM dbo.SensitiveWord WHERE Id=@Id;";

            using var con = _factory.Create();

            var n = await _exec.ExecuteAsync(con, new(sql, new { Id = id }, cancellationToken: ct));
            if (n == 0) return RepositoryResult<bool>.NotFound("Word not found.");

            await IncrementWordsVersionAsync(ct);

            return RepositoryResult<bool>.Ok(
                true,
                affected: n,
                msg: softDelete ? "Word soft-deleted." : "Word deleted.");
        }

        /// <summary>
        /// Bumps the integer "WordsVersion" value in dbo.Metadata. Creates it if missing.
        /// Consumers can use this to invalidate caches/subscriptions.
        /// </summary>
        public async Task IncrementWordsVersionAsync(CancellationToken ct)
        {
            // MERGE is used as an UPSERT: update existing row or insert a new one.
            const string sql = """
            MERGE dbo.Metadata AS t
            USING (SELECT 'WordsVersion' AS [Key]) AS s
            ON t.[Key] = s.[Key]
            WHEN MATCHED THEN
                UPDATE SET [Value] = CAST(TRY_CAST([Value] AS INT) + 1 AS NVARCHAR(10)), UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT ([Key],[Value]) VALUES ('WordsVersion','1');
        """;

            using var con = _factory.Create();
            await _exec.ExecuteAsync(con,new CommandDefinition(sql, cancellationToken: ct));
        }

        /// <summary>
        /// Bulk upsert (create or activate) a list of words.
        /// - Normalizes to UPPER for a consistent match.
        /// - Uses a TVP (table-valued parameter) + MERGE for efficiency.
        /// - Only sets IsActive=1 for matches currently inactive; does not touch deleted rows unless
        ///   you add logic—this method treats deleted rows as distinct from "inactive".
        /// - Bumps WordsVersion at the end.
        /// </summary>
        public async Task BulkUpsertAsync(IEnumerable<string> words, CancellationToken ct)
        {
            // 1) Normalize and dedupe.
            var list = words
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();

            if (list.Length == 0) return;

            // Ensure the TVP type exists. (Idempotent; harmless if already created.)
            const string createType = """
        IF TYPE_ID(N'dbo.ListOfWords') IS NULL
            CREATE TYPE dbo.ListOfWords AS TABLE(Word NVARCHAR(255) NOT NULL);
    """;

            // MERGE logic:
            // - Insert new words (default IsActive=1).
            // - If the word exists and IsActive=0, flip it to active.
            const string mergeSql = """
        MERGE dbo.SensitiveWord AS tgt
        USING @tvp AS src
           ON tgt.Word = src.Word
        WHEN NOT MATCHED THEN 
            INSERT(Word, IsActive) VALUES(src.Word, 1)
        WHEN MATCHED AND tgt.IsActive = 0 THEN
            UPDATE SET IsActive = 1, UpdatedAtUtc = SYSUTCDATETIME();
    """;

            // Bump the version after the MERGE.
            const string bump = """
        MERGE dbo.Metadata AS t
        USING (SELECT 'WordsVersion' AS [Key]) AS s
        ON t.[Key] = s.[Key]
        WHEN MATCHED THEN
          UPDATE SET [Value] = CAST(TRY_CAST([Value] AS INT) + 1 AS NVARCHAR(10)), UpdatedAtUtc = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN
          INSERT ([Key],[Value]) VALUES ('WordsVersion','1');
    """;

            using var con = _factory.Create();

            await _exec.ExecuteAsync(con, new CommandDefinition(createType, cancellationToken: ct));

            // Build TVP payload.
            var tvp = new DataTable();
            tvp.Columns.Add("Word", typeof(string));
            foreach (var w in list) tvp.Rows.Add(w);

            var p = new DynamicParameters();
            p.Add("@tvp", tvp.AsTableValuedParameter("dbo.ListOfWords"));

            await _exec.ExecuteAsync(con, new(mergeSql, p, cancellationToken: ct));
            await _exec.ExecuteAsync(con, new(bump, cancellationToken: ct));
        }

        /// <summary>
        /// Returns the current WordsVersion (0 if not set).
        /// </summary>
        public async Task<int> GetWordsVersionAsync(CancellationToken ct)
        {
            const string sql = "SELECT TRY_CAST([Value] AS INT) FROM dbo.Metadata WHERE [Key]='WordsVersion';";
            using var con = _factory.Create();

            // If null or non-int, default to 0 (unknown / not set).
            return await _exec.ExecuteScalarAsync<int?>(con, new(sql, cancellationToken: ct)) ?? 0;
        }
    }
}
