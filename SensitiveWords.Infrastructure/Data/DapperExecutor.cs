using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SensitiveWords.Infrastructure.Data
{
    /// <summary>
    /// Production Dapper executor. Very thin pass-through.
    /// </summary>
    public sealed class DapperExecutor : IDapperExecutor
    {
        public Task<T?> QuerySingleOrDefaultAsync<T>(IDbConnection conn, CommandDefinition command)
            => conn.QuerySingleOrDefaultAsync<T>(command);

        public Task<IEnumerable<T>> QueryAsync<T>(IDbConnection conn, CommandDefinition command)
            => conn.QueryAsync<T>(command);

        public Task<int> ExecuteAsync(IDbConnection conn, CommandDefinition command)
            => conn.ExecuteAsync(command);

        public Task<T> ExecuteScalarAsync<T>(IDbConnection conn, CommandDefinition command)
            => conn.ExecuteScalarAsync<T>(command);

        public async Task<IMultiResult> QueryMultipleAsync(IDbConnection conn, CommandDefinition command)
        {
            var gr = await conn.QueryMultipleAsync(command);
            return new DapperMultiResult(gr);
        }

        private sealed class DapperMultiResult : IMultiResult
        {
            private readonly SqlMapper.GridReader _inner;

            public DapperMultiResult(SqlMapper.GridReader inner) => _inner = inner;

            public Task<T> ReadFirstAsync<T>() => _inner.ReadFirstAsync<T>();
            public Task<IEnumerable<T>> ReadAsync<T>() => _inner.ReadAsync<T>();

            public void Dispose() => _inner.Dispose();
        }
    }
}
