using System.Buffers;
using HotChocolate.AspNetCore.Subscriptions.Protocols;
using Microsoft.AspNetCore.Http;

namespace HotChocolate.AspNetCore.Subscriptions;

/// <summary>
/// The socket connection represent an accepted connection with a socket
/// where the protocol is already negotiated.
/// </summary>
public interface ISocketConnection : IHasContextData, IDisposable
{
    /// <summary>
    /// Gets access to the HTTP Context.
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// Specifies if the connection is closed.
    /// </summary>
    bool IsClosed { get; }

    /// <summary>
    /// Gets access to the subscription manager of this connection.
    /// </summary>
    ISubscriptionManager Subscriptions { get; }

    /// <summary>
    /// Gets access to the request scoped service provider.
    /// </summary>
    IServiceProvider RequestServices { get; }

    /// <summary>
    /// Get the request cancellation token.
    /// </summary>
    CancellationToken RequestAborted { get; }

    /// <summary>
    /// Tries to accept the connection and returns the accepted protocol handler.
    /// </summary>
    Task<IProtocolHandler?> TryAcceptConnection();

    /// <summary>
    /// Send a message to the client.
    /// </summary>
    /// <param name="message">
    /// The message.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    Task SendAsync(ArraySegment<byte> message, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a message from the client.
    /// </summary>
    /// <param name="writer">
    /// The writer to which the message is written to.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    Task ReceiveAsync(IBufferWriter<byte> writer, CancellationToken cancellationToken);

    /// <summary>
    /// Closes the connection with the client.
    /// </summary>
    /// <param name="message">
    /// A human readable message explaining the close reason.
    /// </param>
    /// <param name="reason">
    /// The message close reason.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    Task CloseAsync(
        string message,
        ConnectionCloseReason reason,
        CancellationToken cancellationToken);
    
    Task CloseAsync(
        string message,
        int reason,
        CancellationToken cancellationToken);
}
