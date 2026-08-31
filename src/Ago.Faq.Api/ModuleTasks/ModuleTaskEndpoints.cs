using Ago.Faq.Api.Http;
using Ago.Faq.Application.UseCases.FaqModuleTask;
using Ago.Faq.Contracts;

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
/// <c>ChatModuleTaskEndpoints</c>'s own remarks give. <b>No service-to-service authentication exists
/// yet</b> - adr/0077 already names this as a real, accepted gap for Calendar's identical surface, and
/// this module extends it here without re-litigating it (`19-03`'s own "Decided" section, ago-root).</para>
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

    private static async Task<IResult> HandleStartAsync(
        ModuleTaskStartRequest request,
        StartFaqModuleTaskHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest();
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
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest();
        }

        var result = await handler.HandleAsync(
            new ReplyToFaqModuleTask(externalTaskId, request.ChatTaskId, request.Kind, request.Value),
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
