using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Protocol;

namespace XFiler.YandexDisk.Client.Clients;

/// <summary>
/// Commands that wait until an asynchronous operation on Yandex's side is done.
/// </summary>
/// <remarks>
/// The status is polled with a growing interval: 250 ms, then doubled up to
/// <c>pollInterval</c> (3 s by default), so short operations finish fast and long ones
/// do not burn the API quota. A failed operation throws <see cref="YandexOperationFailedException"/>,
/// cancellation throws <see cref="OperationCanceledException"/>.
/// </remarks>
public static class CommandsClientExtensions
{
    private static readonly TimeSpan FirstPollDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Wait until the operation from a 202 Accepted response is done.
    /// </summary>
    public static async Task WaitForOperationAsync(this ICommandsClient client, Link operation,
        CancellationToken cancellationToken = default, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(operation);

        TimeSpan maxDelay = pollInterval ?? DefaultPollInterval;

        if (maxDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "Poll interval must be positive.");

        TimeSpan delay = FirstPollDelay < maxDelay ? FirstPollDelay : maxDelay;

        while (true)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

            Operation status = await client.GetOperationStatusAsync(operation, cancellationToken).ConfigureAwait(false);

            switch (status.Status)
            {
                case OperationStatus.Success:
                    return;
                case OperationStatus.Failure:
                    throw new YandexOperationFailedException(operation);
            }

            delay = delay * 2 < maxDelay ? delay * 2 : maxDelay;
        }
    }

    /// <summary>
    /// Copy file or folder on Disk from one path to another and wait until operation is done
    /// </summary>
    public static async Task CopyAndWaitAsync(this ICommandsClient client, CopyFileRequest request,
        CancellationToken cancellationToken = default, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        Link link = await client.CopyAsync(request, cancellationToken).ConfigureAwait(false);

        await client.WaitIfAcceptedAsync(link, cancellationToken, pollInterval).ConfigureAwait(false);
    }

    /// <summary>
    /// Move file or folder on Disk from one path to another and wait until operation is done
    /// </summary>
    public static async Task MoveAndWaitAsync(this ICommandsClient client, MoveFileRequest request,
        CancellationToken cancellationToken = default, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        Link link = await client.MoveAsync(request, cancellationToken).ConfigureAwait(false);

        await client.WaitIfAcceptedAsync(link, cancellationToken, pollInterval).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete file or folder on Disk and wait until operation is done
    /// </summary>
    public static async Task DeleteAndWaitAsync(this ICommandsClient client, DeleteFileRequest request,
        CancellationToken cancellationToken = default, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        Link? link = await client.DeleteAsync(request, cancellationToken).ConfigureAwait(false);

        await client.WaitIfAcceptedAsync(link, cancellationToken, pollInterval).ConfigureAwait(false);
    }

    /// <summary>
    /// Empty trash (or delete <paramref name="path"/> from it) and wait until operation is done
    /// </summary>
    public static async Task EmptyTrashAndWaitAsync(this ICommandsClient client, string? path = null,
        CancellationToken cancellationToken = default, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        Link? link = await client.EmptyTrashAsync(path, cancellationToken).ConfigureAwait(false);

        await client.WaitIfAcceptedAsync(link, cancellationToken, pollInterval).ConfigureAwait(false);
    }

    /// <summary>
    /// Restore files from trash and wait until operation is done
    /// </summary>
    public static async Task RestoreFromTrashAndWaitAsync(this ICommandsClient client, RestoreFromTrashRequest request,
        CancellationToken cancellationToken = default, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        Link link = await client.RestoreFromTrashAsync(request, cancellationToken).ConfigureAwait(false);

        await client.WaitIfAcceptedAsync(link, cancellationToken, pollInterval).ConfigureAwait(false);
    }

    private static Task WaitIfAcceptedAsync(this ICommandsClient client, Link? link,
        CancellationToken cancellationToken, TimeSpan? pollInterval) =>
        link is { HttpStatusCode: HttpStatusCode.Accepted }
            ? client.WaitForOperationAsync(link, cancellationToken, pollInterval)
            : Task.CompletedTask;
}
