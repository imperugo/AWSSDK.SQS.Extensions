using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SQS.Extensions.Abstractions;
using SQS.Extensions.Configurations;

namespace SQS.Extensions;

/// <summary>
/// The base implementation of SQS Consumer
/// </summary>
public abstract partial class SqsHostedService<T> : BackgroundService
{
    private readonly ISqsMessagePumpFactory messagePumpFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqsHostedService{T}"/> class.
    /// </summary>
    protected SqsHostedService(ILogger logger, ISqsMessagePumpFactory messagePumpFactory)
    {
        Logger = logger;
        this.messagePumpFactory = messagePumpFactory;
    }

    /// <summary>
    /// The function to call when a message has been caught from the queue.
    /// </summary>
    protected abstract Func<T?, MessageContext, CancellationToken, Task> ProcessMessageFunc { get; }

    /// <summary>
    /// The pump configuration
    /// </summary>
    protected abstract MessagePumpConfiguration MessagePumpConfiguration { get; }

    /// <summary>
    /// Gets a value indicating whether the background service is enabled.
    /// </summary>
    /// <value>
    ///   <c>true</c> if enabled; otherwise, <c>false</c>.
    /// </value>
    protected virtual Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogHostServiceStarted(Logger, GetType().Name);
            var pump = await messagePumpFactory.CreateAsync<T>(MessagePumpConfiguration, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (await IsEnabledAsync(cancellationToken))
                {
                    try
                    {
                        await pump.PumpAsync(ProcessMessageFunc, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        LogPumpError(Logger, e);
                    }
                }

                await Task.Delay(MessagePumpConfiguration.BatchDelay, cancellationToken);
            }
        }
        catch (Exception e)
        {
            LogCriticalError(Logger, e.Message, e);
        }
    }

    /// <summary>
    /// Gets the logger used by the current class.
    /// </summary>
    /// <value>
    /// The logger used for logging in the current class.
    /// </value>
    protected ILogger Logger { get ; }

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "Sqs Hosted Service: {HostServiceName} is starting")]
    private static partial void LogHostServiceStarted(ILogger logger, string hostServiceName);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "[MessagePump] There is an error while processing messages")]
    private static partial void LogPumpError(ILogger logger, Exception exc);

    [LoggerMessage(EventId = 103, Level = LogLevel.Critical, Message = "The Sqs Hosted Service is shutting down. Reason: {Message}")]
    private static partial void LogCriticalError(ILogger logger, string message, Exception exc);
}

/// <summary>
/// Represents the context of a message.
/// </summary>
public sealed class MessageContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageContext"/> class.
    /// </summary>
    /// <param name="messageId">The SQS message identifier.</param>
    /// <param name="messageAttributes">The message attributes.</param>
    public MessageContext(string messageId, Dictionary<string, string>? messageAttributes = null)
    {
        MessageId = messageId;
        MessageAttributes = messageAttributes ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Gets the identifier of the message.
    /// </summary>
    /// <value>The message identifier.</value>
    public string MessageId { get; }

    /// <summary>
    /// Gets or sets the retry count for the message.
    /// </summary>
    /// <value>The retry count for the message.</value>
    public int? RetryCount { get; internal set; }

    /// <summary>
    /// Gets the message attributes as key-value pairs.
    /// </summary>
    /// <value>A dictionary containing the message attributes.</value>
    public Dictionary<string, string> MessageAttributes { get; }

    /// <summary>
    /// Get the value of a message attribute.
    /// </summary>
    /// <param name="key">The key of the dictionary.</param>
    public string? GetAttribute(string key)
    {
        if (MessageAttributes.TryGetValue(key, out var value))
            return value;

        return null;
    }

    /// <summary>
    /// Get the value of a message attribute as an integer.
    /// </summary>
    /// <param name="key">The key of the dictionary.</param>
    public int? GetAttributeAsInt(string key)
    {
        if (MessageAttributes.TryGetValue(key, out var value))
            return int.Parse(value);

        return null;
    }
}
