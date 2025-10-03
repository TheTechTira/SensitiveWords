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
    /// Thin wrapper around Dapper so repositories are easy to unit test.
    /// </summary>
    public interface IDapperExecutor
    {
        Task<T?> QuerySingleOrDefaultAsync<T>(IDbConnection conn, CommandDefinition command);
        Task<IEnumerable<T>> QueryAsync<T>(IDbConnection conn, CommandDefinition command);
        Task<int> ExecuteAsync(IDbConnection conn, CommandDefinition command);
        Task<T> ExecuteScalarAsync<T>(IDbConnection conn, CommandDefinition command);

        /// <summary>
        /// Multi-result reader (COUNT + rows etc).
        /// </summary>
        Task<IMultiResult> QueryMultipleAsync(IDbConnection conn, CommandDefinition command);
    }

    /// <summary>
    /// Abstraction over Dapper's GridReader.
    /// </summary>
    public interface IMultiResult : IDisposable
    {
        Task<T> ReadFirstAsync<T>();
        Task<IEnumerable<T>> ReadAsync<T>();
    }
}
