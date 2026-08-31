using Ago.Faq.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ago.Faq.Infrastructure.Postgres.Persistence;

/// <summary>
/// One converter per strongly-typed id - explicit and compiled, rather than one reflection-based
/// generic converter that would run per row materialized (the same call
/// <c>Ago.Calendar.Infrastructure.Postgres.Persistence.IdConverters</c> makes).
/// </summary>
internal static class IdConverters
{
    public static readonly ValueConverter<FaqModuleTaskId, Guid> FaqModuleTask =
        new(id => id.Value, value => new FaqModuleTaskId(value));

    public static readonly ValueConverter<SiteId, Guid> Site = new(id => id.Value, value => new SiteId(value));
}
