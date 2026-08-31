namespace Ago.Faq.Contracts;

/// <summary>
/// The wire shapes of this module's chat-module surface - hand-synchronized with
/// <c>Ago.Chat.Infrastructure.Modules.ModuleWireContract</c> in `ago-chat`, over plain HTTP+JSON
/// (System.Text.Json camelCase, the ASP.NET Core Minimal API default). There is no shared package
/// between the two products (adr/0027, adr/0012 sets no precedent for one), and no
/// <c>[JsonPropertyName]</c> attribute anywhere below - `Ago.Calendar.Contracts.ModuleTaskContracts`
/// (this module's own structural template) carries none either and relies on the framework's default
/// camelCase naming policy, so this file matches what the real sibling repository does rather than a
/// paraphrase that suggested attributes it does not use.
///
/// <para><b>Field names and casing matter more here than they would inside one product</b>: a typo on
/// either side is a silent contract break with no compiler to catch it - the same warning
/// `Ago.Calendar.Contracts.ModuleTaskContracts`'s own remarks give.</para>
/// </summary>
public static class FaqStepKinds
{
    /// <summary>The bare-trigger step, asking the visitor what they want to know.</summary>
    public const string Form = "form";

    /// <summary>A successful, grounded answer. Deliberately Chat's own <c>choice_list</c> kind with
    /// zero actions - "a payload with no actions is a card nobody has to answer"
    /// (<c>Ago.Chat.Domain.MessageContent</c>'s own doc comment) - not a sixth primitive invented for
    /// a plain answer.</summary>
    public const string Answer = "choice_list";

    /// <summary>The low-confidence escape - Chat's fifth closed-vocabulary primitive
    /// (adr/0081, ago-root). Force-closes the task on Chat's side regardless of this module's own
    /// <c>complete</c> flag.</summary>
    public const string Escalate = "escalate";
}

/// <summary>Chat's own <c>MessageAction</c> shape (adr/0061, ago-root) - always empty for every step
/// this module ever produces: this module only ever answers a question or hands off to a human, never
/// offers a choice to pick from.</summary>
public sealed record ModuleActionDto(string Label, string Value);

/// <summary><paramref name="Payload"/> is deliberately <c>object?</c>, not a closed union type - the
/// wire shape genuinely varies by <see cref="Kind"/>, and <c>System.Text.Json</c> serializes a
/// property declared as <c>object</c> using the value's own runtime type. Nullable because an
/// <c>escalate</c> step may omit its payload entirely (adr/0081) - Chat substitutes a generic fallback
/// prompt when it is absent.</summary>
public sealed record StepDto(string Kind, object? Payload, IReadOnlyList<ModuleActionDto> Actions);

public sealed record FormPayload(string Prompt, string FieldId, string FieldLabel);

public sealed record AnswerPayload(string Prompt);

public sealed record EscalatePayload(string Prompt);

/// <param name="ChatTaskId">Chat's own id for this task. Opaque to this module - accepted and never
/// stored.</param>
/// <param name="SiteId">This module's own correlation key - stored on <c>Ago.Faq.Domain.FaqModuleTask</c>
/// so a later reply (which carries no <c>siteId</c> of its own) can still resolve the right
/// knowledge base.</param>
/// <param name="ConversationId">Opaque, same reason as <paramref name="ChatTaskId"/>.</param>
/// <param name="TriggerText">The full visitor message that matched this module's trigger word,
/// leading word included - e.g. <c>"/faq what is your return policy"</c> or a bare <c>"/faq"</c>.</param>
public sealed record ModuleTaskStartRequest(Guid ChatTaskId, Guid SiteId, Guid ConversationId, string TriggerText);

public sealed record ModuleTaskStartResponse(string ExternalTaskId, StepDto Step, bool Complete);

/// <param name="ChatTaskId">Opaque, accepted and never stored - see <see cref="ModuleTaskStartRequest"/>.</param>
/// <param name="Kind">Echoes the step's own <see cref="StepDto.Kind"/> - for this module, always
/// <see cref="FaqStepKinds.Form"/>, since a <c>form</c> is the only step kind this module ever sends
/// that expects a reply.</param>
/// <param name="Value">The visitor's raw typed text - their question, unvalidated by Chat.</param>
public sealed record ModuleTaskReplyRequest(Guid ChatTaskId, string Kind, string Value);

/// <param name="Step">Null only when there is genuinely nothing further to say - not the case this
/// module ever hits, since every reply it accepts (the visitor's question) produces exactly one more
/// step, the answer or the escalation, in the same response that completes the task. Nullable purely
/// to match the wire contract's own general shape (<c>SubmitReplyWireResponse</c> in `ago-chat`),
/// which other modules with a genuine "nothing more to say" terminal state may use.</param>
public sealed record ModuleTaskReplyResponse(StepDto? Step, bool Complete);
