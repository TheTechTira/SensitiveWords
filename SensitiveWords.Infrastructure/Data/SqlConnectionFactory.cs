using Microsoft.Data.SqlClient;
using SensitiveWords.Application.Abstractions.Data;
using System.Data;

namespace SensitiveWords.Infrastructure.Data
{
    /// <summary>
    /// Default SQL Server implementation of <see cref="ISqlConnectionFactory"/>.
    /// Wraps the connection string and returns a ready-to-use, open <see cref="SqlConnection"/>.
    /// </summary>
    public class SqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes the factory with a SQL Server connection string.
        /// </summary>
        public SqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <inheritdoc/>
        public IDbConnection Create()
        {
            var conn = new SqlConnection(_connectionString);
            conn.Open(); // we return an already open connection for immediate use
            return conn;
        }
    }
}
