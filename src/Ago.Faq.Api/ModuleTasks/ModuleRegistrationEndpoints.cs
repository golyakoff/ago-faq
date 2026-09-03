using Ago.Faq.Api.Http;
using Ago.Faq.Application.Abstractions;
using Ago.Faq.Application.UseCases.ModuleRegistration;

namespace Ago.Faq.Api.ModuleTasks;

/// <summary>
/// `22-11`: the generic provisioning surface `adr/0065`'s registry needed all along - the identical
/// route family and identical mechanism
/// <c>Ago.Calendar.Api.ChatModule.ModuleRegistrationEndpoints</c> gives its sibling. See that class's
/// own remarks for the full argument: server-to-server, outside any CORS policy, authenticated by
/// <c>X-Ago-Module-Provisioning-Secret</c> rather than <c>X-Ago-Module-Credential</c>, `PUT` creates,
/// `POST .../rotate` rotates, `DELETE` revokes, `GET` reports status for reconciliation.
/// </summary>
public static class ModuleRegistrationEndpoints
{
    private const string ProvisioningSecretHeaderName = "X-Ago-Module-Provisioning-Secret";

    public static IEndpointRouteBuilder MapModuleRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/module-registrations").AllowAnonymous();

        group.MapPut("/{siteId:guid}", HandleRegisterAsync).WithName("RegisterModuleSite");
        group.MapPost("/{siteId:guid}/rotate", HandleRotateAsync).WithName("RotateModuleSiteCredential");
        group.MapDelete("/{siteId:guid}", HandleRevokeAsync).WithName("RevokeModuleSiteRegistration");
        group.MapGet("/{siteId:guid}", HandleGetStatusAsync).WithName("GetModuleSiteRegistrationStatus");

        return app;
    }

    private static async Task<IResult> HandleRegisterAsync(
        Guid siteId,
        RegisterModuleSiteRequest request,
        RegisterModuleSiteHandler handler,
        IModuleProvisioningAuthenticator authenticator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest();
        }

        if (!authenticator.Authenticate(httpContext.Request.Headers[ProvisioningSecretHeaderName]))
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new RegisterModuleSite(siteId, request.Credential), cancellationToken);
        return result.IsSuccess ? Results.Ok() : result.Error!.Value.ToProblem(httpContext);
    }

    private static async Task<IResult> HandleRotateAsync(
        Guid siteId,
        RotateModuleSiteCredentialRequest request,
        RotateModuleSiteCredentialHandler handler,
        IModuleProvisioningAuthenticator authenticator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest();
        }

        if (!authenticator.Authenticate(httpContext.Request.Headers[ProvisioningSecretHeaderName]))
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new RotateModuleSiteCredential(siteId, request.NewCredential), cancellationToken);
        return result.IsSuccess ? Results.Ok() : result.Error!.Value.ToProblem(httpContext);
    }

    private static async Task<IResult> HandleRevokeAsync(
        Guid siteId,
        RevokeModuleSiteRegistrationHandler handler,
        IModuleProvisioningAuthenticator authenticator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!authenticator.Authenticate(httpContext.Request.Headers[ProvisioningSecretHeaderName]))
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(new RevokeModuleSiteRegistration(siteId), cancellationToken);
        return result.IsSuccess ? Results.Ok() : result.Error!.Value.ToProblem(httpContext);
    }

    private static async Task<IResult> HandleGetStatusAsync(
        Guid siteId,
        GetModuleSiteRegistrationStatusHandler handler,
        IModuleProvisioningAuthenticator authenticator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!authenticator.Authenticate(httpContext.Request.Headers[ProvisioningSecretHeaderName]))
        {
            return Results.Unauthorized();
        }

        var status = await handler.HandleAsync(new GetModuleSiteRegistrationStatus(siteId), cancellationToken);
        return Results.Ok(new ModuleSiteRegistrationStatusResponse(
            status.Exists, status.Exists ? status.RegisteredAt : null, status.HasCredentialInGracePeriod));
    }

    public sealed record RegisterModuleSiteRequest(string Credential);

    public sealed record RotateModuleSiteCredentialRequest(string NewCredential);

    public sealed record ModuleSiteRegistrationStatusResponse(
        bool Exists, DateTimeOffset? RegisteredAt, bool HasCredentialInGracePeriod);
}
