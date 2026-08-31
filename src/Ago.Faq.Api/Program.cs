using Ago.Faq.Api.Auth;
using Ago.Faq.Api.Cors;
using Ago.Faq.Api.KnowledgeBase;
using Ago.Faq.Api.ModuleTasks;
using Ago.Faq.Module;
using Ago.Platform.Hosting;

var builder = WebApplication.CreateBuilder(args);

// The composition root, and the only place that knows a concrete implementation
// (clean-architecture.md, ago-root). AddPlatformKernel brings IClock/IIdGenerator in from the
// published package; the module registers this product's own adapters, handlers and options.
builder.Services.AddPlatformKernel();

IProductModule module = new FaqModule();
module.ConfigureServices(builder.Services, builder.Configuration);

// The knowledge-base console endpoint's own auth - adr/0022's OIDC scheme, this module's own copy
// (see FaqAuthenticationSetup's own remarks for how this differs from Ago.Calendar.Api's).
builder.Services.AddFaqOperatorAuthentication(builder.Configuration);

// A plain static allow-list, not a per-tenant dynamic policy provider - see ConsoleOriginOptions' own
// remarks for why this endpoint does not need Ago.Calendar.Api's TenantOriginCorsPolicyProvider shape.
builder.Services
    .AddOptions<ConsoleOriginOptions>()
    .Bind(builder.Configuration.GetSection(ConsoleOriginOptions.SectionName));
var consoleOrigins =
    builder.Configuration.GetSection(ConsoleOriginOptions.SectionName).Get<ConsoleOriginOptions>()
    ?? new ConsoleOriginOptions();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(consoleOrigins.AllowedOrigins)
    .WithMethods("GET", "PUT", "OPTIONS")
    .WithHeaders("Content-Type", "Authorization")));

var app = builder.Build();

// Before authentication, and that ordering is load-bearing: a CORS preflight is an unauthenticated
// OPTIONS request that carries no token, so CORS middleware sitting behind authentication would never
// answer one - the same ordering Ago.Calendar.Api's own Program.cs states explicitly.
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// The wire contract Ago.Chat.* drives this module through - server-to-server, outside any CORS
// policy. See ModuleTaskEndpoints's own remarks.
app.MapModuleTaskEndpoints();

// This item's own new surface: the console-facing knowledge-base configuration screen.
app.MapKnowledgeBaseEndpoints();

// Answers with the loaded module's name rather than a constant, so "the host composed the module" is
// something the running process can be asked - the same call Ago.Calendar.Api's own Program.cs makes.
app.MapGet("/", () => module.Name);

app.Run();

/// <summary>
/// Named so that <c>Ago.Faq.Integration.Tests</c> can point a <c>WebApplicationFactory</c> at this
/// host. A top-level-statements program's generated entry class is <c>internal</c>, and the factory
/// needs a public type argument; this partial declaration is the documented way to widen it without
/// turning the file into a conventional <c>Main</c>.
/// </summary>
public partial class Program;
