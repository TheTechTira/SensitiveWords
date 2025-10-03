using System.Data;

namespace SensitiveWords.Application.Abstractions.Data
{
    /// <summary>
    /// Factory abstraction for creating SQL database connections.
    /// 
    /// Notes:
    /// - Keeps connection creation consistent across the infrastructure layer.
    /// - Abstracted to allow mocking in tests or swapping out the provider.
    /// - Always returns an *open* connection — caller is responsible for disposing it.
    /// </summary>
    public interface ISqlConnectionFactory
    {
        /// <summary>
        /// Creates and opens a new <see cref="IDbConnection"/>.
        /// Caller must dispose the connection after use.
        /// </summary>
        IDbConnection Create();
    }
}
