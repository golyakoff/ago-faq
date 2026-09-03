using Ago.Faq.Api.Http;
using Ago.Faq.Application.Abstractions;
using Ago.Faq.Application.UseCases.FaqModuleTask;
using Ago.Faq.Contracts;
using Ago.Platform.Kernel;

namespace Ago.Faq.Api.ModuleTasks;

/// <summary>
/// This module's wire contract with `Ago.Chat.*` - the two endpoints a chat conversation drives this
/// module through, hand-synchronized with the `ago-chat` worker building the other side
/// (<c>Ago.Chat.Infrastructure.Modules.ModuleWireContract</c>; see <c>Ago.Faq.Contracts.ModuleTaskContracts</c>
/// for the shared field-level detail). The identical route shape
/// <c>Ago.Calendar.Api.ChatModule.ChatModuleTaskEndpoints</c> already established for Calendar - this
/// module is a second, independent implementer of the same contract, not a fork of Calendar's code.
///
/// <para><b>Server-to-server, not widget-facing, and outside any CORS policy.</b> Nothing here checks
/// an <c>Origin</c> header - a server calling another server does not send one, the same reasoning
/// <c>ChatModuleTaskEndpoints</c>'s own remarks give.</para>
///
/// <para><b>`22-02`: every request now carries a signed <c>X-Ago-Module-Credential</c> header</b>,
/// checked by <see cref="IModuleCallCredentialValidator"/> before either handler ever runs - the gap
/// adr/0077 named as accepted for Calendar's identical surface, closed identically for both products
/// by the same contract. A missing-or-wrong credential is refused with <c>401</c>.
/// <see cref="HandleStartAsync"/> cross-checks the credential's own site id against
/// <see cref="ModuleTaskStartRequest.SiteId"/>. <see cref="HandleReplyAsync"/> threads the credential's
/// site id into <see cref="ReplyToFaqModuleTask.CredentialSiteId"/> instead of cross-checking it here -
/// unlike Calendar's identical route, this module's own <c>Domain.FaqModuleTask</c> already carries a
/// site id (<c>ModuleTaskContracts.ModuleTaskStartRequest.SiteId</c>'s own remarks), so the real check
/// (against the task actually being replied to, not merely the request shape) belongs in the handler
/// that loads that task - see <see cref="ReplyToFaqModuleTaskHandler"/>'s own remarks.</para>
///
/// <para><b>`22-04`: the credential is now checked against a per-site secret</b>
/// (<c>HmacModuleCallCredentialValidator</c>'s own remarks) rather than one shared across this whole
/// deployment - <see cref="IModuleCallCredentialValidator.ValidateAsync"/> reads a database row to do
/// it, which is why the call below is awaited rather than a synchronous method call.</para>
///
/// <para><b>200, not 201, on the <c>POST</c> that starts a task.</b> api-design.md's default is
/// <c>201</c> with a <c>Location</c> for a creating <c>POST</c>; this route deviates for the identical
/// reason <c>ChatModuleTaskEndpoints</c>'s own remarks give: the wire contract is fixed by
/// hand-agreement with another repository's own HTTP client, which reads
/// <c>{ externalTaskId, step, complete }</c> from a <c>200</c> body, and there is no <c>GET</c> this
/// module serves for a task that a <c>Location</c> header could honestly point at.</para>
/// </summary>
public static class ModuleTaskEndpoints
{
    public static IEndpointRouteBuilder MapModuleTaskEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/module-tasks").AllowAnonymous();

        group.MapPost("/", HandleStartAsync).WithName("StartFaqModuleTask");
        group.MapPost("/{externalTaskId}/replies", HandleReplyAsync).WithName("ReplyToFaqModuleTask");

        return app;
    }

    private const string CredentialHeaderName = "X-Ago-Module-Credential";

    private static async Task<IResult> HandleStartAsync(
        ModuleTaskStartRequest request,
        StartFaqModuleTaskHandler handler,
        IModuleCallCredentialValidator credentialValidator,
        IClock clock,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest();
        }

        var auth = await credentialValidator.ValidateAsync(
            httpContext.Request.Headers[CredentialHeaderName], clock.UtcNow, cancellationToken);
        if (!auth.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        // `22-02`'s own sharpest claim: a credential valid for one site cannot name another in the
        // body. `22-04` removed the one case that used to leave auth.SiteId null (the
        // accepting-but-warning rollout window) - IsAuthenticated true now always carries a real site
        // id - but the null-conditional stays rather than an assumed-non-null read, so this check
        // degrades safely rather than throwing if that ever stops being true again.
        if (auth.SiteId is { } authenticatedSiteId && authenticatedSiteId != request.SiteId)
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(
            new StartFaqModuleTask(request.ChatTaskId, request.SiteId, request.ConversationId, request.TriggerText),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error!.Value.ToProblem(httpContext);
        }

        var started = result.Value;
        return Results.Ok(new ModuleTaskStartResponse(started.ExternalTaskId, ToStepDto(started.Step), started.Complete));
    }

    private static async Task<IResult> HandleReplyAsync(
        string externalTaskId,
        ModuleTaskReplyRequest request,
        ReplyToFaqModuleTaskHandler handler,
        IModuleCallCredentialValidator credentialValidator,
        IClock clock,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest();
        }

        var auth = await credentialValidator.ValidateAsync(
            httpContext.Request.Headers[CredentialHeaderName], clock.UtcNow, cancellationToken);
        if (!auth.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        // Unlike Start, the wire body carries no site id to cross-check here - the credential's own
        // site id is threaded into the command instead, and ReplyToFaqModuleTaskHandler checks it
        // against the task actually being replied to. See this class's own remarks.
        var result = await handler.HandleAsync(
            new ReplyToFaqModuleTask(externalTaskId, request.ChatTaskId, request.Kind, request.Value, auth.SiteId),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Error!.Value.ToProblem(httpContext);
        }

        var replied = result.Value;
        return Results.Ok(new ModuleTaskReplyResponse(ToStepDto(replied.Step), replied.Complete));
    }

    /// <summary>Application's <see cref="FaqModuleStep"/> - a plain result, ignorant of wire casing
    /// and of <c>System.Text.Json</c> - becomes the wire's own per-kind payload shape. See
    /// <see cref="FaqModuleStep"/>'s own remarks for why this translation lives here and not in the
    /// handler that built the step.</summary>
    private static StepDto ToStepDto(FaqModuleStep step) => step switch
    {
        FaqModuleStep.Form form => new StepDto(
            FaqStepKinds.Form, new FormPayload(form.Prompt, form.FieldId, form.FieldLabel), []),

        FaqModuleStep.Answer answer => new StepDto(
            FaqStepKinds.Answer, new AnswerPayload(answer.Prompt), []),

        // adr/0081: the payload is omitted entirely (null) when this module has no reason text of its
        // own - Chat substitutes its own generic fallback prompt in that case. In practice
        // AnswerFaqQuestionHandler always supplies one, so this module never actually sends a bare
        // escalate with no payload today, but the wire shape - and this translation - supports it.
        FaqModuleStep.Escalate escalate => new StepDto(
            FaqStepKinds.Escalate,
            escalate.Prompt is null ? null : new EscalatePayload(escalate.Prompt),
            []),

        _ => throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown FAQ module step kind."),
    };
}
