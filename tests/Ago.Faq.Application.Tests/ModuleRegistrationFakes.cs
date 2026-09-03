using Ago.Faq.Application.Abstractions;
using Ago.Faq.Domain;

namespace Ago.Faq.Application.Tests;

/// <summary>Plain in-memory store for <see cref="ModuleSiteRegistration"/> - the same
/// hand-written-fake shape every other fake in this project follows (testing.md).</summary>
internal sealed class FakeModuleSiteRegistrationRepository : IModuleSiteRegistrationRepository
{
    private readonly Dictionary<SiteId, ModuleSiteRegistration> _rows = [];

    public Task<ModuleSiteRegistration?> GetBySiteIdAsync(SiteId siteId, CancellationToken cancellationToken) =>
        Task.FromResult(_rows.GetValueOrDefault(siteId));

    public Task AddAsync(ModuleSiteRegistration registration, CancellationToken cancellationToken)
    {
        _rows[registration.SiteId] = registration;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ModuleSiteRegistration registration, CancellationToken cancellationToken)
    {
        _rows[registration.SiteId] = registration;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SiteId siteId, CancellationToken cancellationToken)
    {
        _rows.Remove(siteId);
        return Task.CompletedTask;
    }
}
