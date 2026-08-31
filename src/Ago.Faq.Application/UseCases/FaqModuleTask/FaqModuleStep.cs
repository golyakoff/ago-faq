namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <summary>
/// Application's own result for "what does the visitor see next" - a plain, technology-ignorant
/// shape, the same role <c>Ago.Calendar.Application.UseCases.ChatModuleTask.ModuleStep</c> plays for
/// Calendar. Translating this into <c>Ago.Faq.Contracts.StepDto</c> is <c>Ago.Faq.Api</c>'s own job
/// (see <c>ModuleTaskEndpoints.ToStepDto</c>), not this project's - Application must not know there is
/// a wire, camelCase JSON, or a <c>System.Text.Json</c> <c>object</c> payload trick underneath it.
///
/// <para>A closed set of exactly the three outcomes this module ever produces (the "Decided,
/// 2026-08-31" section of `19-03`'s own backlog file, ago-root) - unlike Calendar's four-kind
/// <c>ModuleStep</c>, there is no case here that needs optional fields for kinds it is not, so a
/// discriminated union of sealed records is both simpler and exhaustive-checkable at the one switch
/// that consumes it.</para>
/// </summary>
public abstract record FaqModuleStep
{
    /// <summary>The bare-trigger case: no question was supplied yet, so ask for one.</summary>
    public sealed record Form(string Prompt, string FieldId, string FieldLabel) : FaqModuleStep;

    /// <summary>A grounded, successful answer.</summary>
    public sealed record Answer(string Prompt) : FaqModuleStep;

    /// <summary>The low-confidence escape - <paramref name="Prompt"/> is optional; the wire endpoint
    /// omits the payload entirely when it is null, and Chat substitutes its own generic fallback text
    /// (adr/0081).</summary>
    public sealed record Escalate(string? Prompt) : FaqModuleStep;
}
