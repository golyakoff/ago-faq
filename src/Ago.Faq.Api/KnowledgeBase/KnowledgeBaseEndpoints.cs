using Ago.Faq.Api.Http;
using Ago.Faq.Application.UseCases.KnowledgeBase;
using Ago.Faq.Contracts;

namespace Ago.Faq.Api.KnowledgeBase;

/// <summary>
/// The console-facing knowledge-base configuration surface - this item's own new endpoint, not part
/// of the module-task wire contract Chat drives.
///
/// <para><b>Auth: <c>RequireAuthenticatedUser()</c> only, deliberately not a
/// <c>Permission.SiteConfigure</c> check.</b> This validates the same shared Keycloak-issued operator
/// JWT `ago-chat`'s own console already obtains - no second Keycloak client, because bearer validation
/// needs only the issuer's public signing keys. What it does <b>not</b> yet do is re-verify that the
/// authenticated operator specifically holds a permission over the <c>siteId</c> named in the route:
/// this module has no concept of a site's operators at all, so closing that gap would need either a
/// reversed wire call into `ago-chat` or a locally duplicated permission table - both real, larger
/// changes this item's scope did not build. A real, bounded, named gap
/// (`19-03`'s own "Decided" section, ago-root), the same shape adr/0077 already accepted for the
/// module-task endpoints (authenticity checked, cross-tenant authorization not, yet) - not silently
/// shipped.</para>
/// </summary>
public static class KnowledgeBaseEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeBaseEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/sites/{siteId:guid}/knowledge-base").RequireAuthorization();

        group.MapGet("/", HandleGetAsync).WithName("GetKnowledgeBase");
        group.MapPut("/", HandlePutAsync).WithName("PutKnowledgeBase");

        return app;
    }

    private static async Task<IResult> HandleGetAsync(
        Guid siteId,
        GetKnowledgeBaseHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetKnowledgeBase(siteId), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error!.Value.ToProblem(httpContext);
        }

        return Results.Ok(new KnowledgeBaseResponse(result.Value.Text, result.Value.UpdatedAt));
    }

    private static async Task<IResult> HandlePutAsync(
        Guid siteId,
        PutKnowledgeBaseRequest request,
        PutKnowledgeBaseHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest();
        }

        var result = await handler.HandleAsync(new PutKnowledgeBase(siteId, request.Text), cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error!.Value.ToProblem(httpContext);
        }

        return Results.Ok(new KnowledgeBaseResponse(result.Value.Text, result.Value.UpdatedAt));
    }
}
