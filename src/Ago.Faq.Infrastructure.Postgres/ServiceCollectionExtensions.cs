using Ago.Faq.Application.Abstractions;
using Ago.Faq.Infrastructure.Postgres.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ago.Faq.Infrastructure.Postgres;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The one place a concrete adapter is named - clean-architecture.md puts DI wiring in the host
    /// (here, <c>Ago.Faq.Module</c>), and this method is how that stays true without the module
    /// learning two class names.
    ///
    /// <para>Scoped, not singleton: <c>DbContext</c> is not thread-safe and a unit of work is
    /// per-request by construction. Both repositories are scoped for the same reason - they hold the
    /// context, so their lifetime is its lifetime.</para>
    /// </summary>
    public static IServiceCollection AddFaqPostgresPersistence(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AgoFaqDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IFaqModuleTaskStore, FaqModuleTaskStore>();
        services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();

        // `22-04`: adr/0065's registry, this module's own consuming half.
        services.AddScoped<IModuleSiteRegistrationRepository, ModuleSiteRegistrationRepository>();

        return services;
    }
}
