using Ago.Faq.Domain;

namespace Ago.Faq.Application.Abstractions;

/// <summary>
/// `22-04`: the write-side (EF) port for <see cref="ModuleSiteRegistration"/> - adr/0004's "EF for
/// writes" half. Also the port <c>HmacModuleCallCredentialValidator</c> reads through on every module
/// call, rather than a second, Dapper-backed read store the way <c>IEnabledModuleReadStore</c> exists
/// beside <c>IEnabledModuleRepository</c> in `ago-chat`: that split earns its keep on a hot path hit by
/// every visitor message on every site with a module enabled, and this lookup runs once per
/// module-task call - `adr/0065`'s own "most steps run at human pace" volume, not that one. A second
/// read path here would be the premature optimisation `caching.md` warns against for a read nothing
/// has measured as slow.
/// </summary>
public interface IModuleSiteRegistrationRepository
{
    Task<ModuleSiteRegistration?> GetBySiteIdAsync(SiteId siteId, CancellationToken cancellationToken);

    Task AddAsync(ModuleSiteRegistration registration, CancellationToken cancellationToken);
}
