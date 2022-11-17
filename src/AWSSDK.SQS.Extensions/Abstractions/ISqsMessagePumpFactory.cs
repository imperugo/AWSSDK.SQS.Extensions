using AWSSDK.SQS.Extensions.Configurations;

namespace AWSSDK.SQS.Extensions.Abstractions;

public interface ISqsMessagePumpFactory
{
    /// <summary>
    /// Create a new instance of message pump with automatic deserialization
    /// </summary>
    /// <param name="configuration">The pump configuration</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <typeparam name="T">The type to use during the message deserialization</typeparam>
    Task<ISqsMessagePump<T>> CreateAsync<T>(MessagePumpConfiguration configuration, CancellationToken cancellationToken);
}
