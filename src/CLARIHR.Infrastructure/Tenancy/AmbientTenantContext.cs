namespace CLARIHR.Infrastructure.Tenancy;

internal sealed class AmbientTenantContext
{
    private readonly AsyncLocal<Guid?> _currentTenantId = new();

    public Guid? TenantId => _currentTenantId.Value;

    public IDisposable Push(Guid tenantId)
    {
        var previousTenantId = _currentTenantId.Value;
        _currentTenantId.Value = tenantId;
        return new ResetScope(this, previousTenantId);
    }

    private sealed class ResetScope(AmbientTenantContext context, Guid? previousTenantId) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            context._currentTenantId.Value = previousTenantId;
            _disposed = true;
        }
    }
}
