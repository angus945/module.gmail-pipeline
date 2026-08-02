using Google.Apis.Gmail.v1;

namespace GmailPipeline.Google.Authentication;

public sealed class GmailServiceAccessor : IGmailServiceAccessor
{
    private readonly IGmailServiceFactory _serviceFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GmailService? _service;
    private bool _disposed;

    public GmailServiceAccessor(IGmailServiceFactory serviceFactory)
    {
        _serviceFactory = serviceFactory;
    }

    public async Task<GmailService> GetAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_service is not null)
        {
            return _service;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _service ??= await _serviceFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
            return _service;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service?.Dispose();
        _gate.Dispose();
    }
}
