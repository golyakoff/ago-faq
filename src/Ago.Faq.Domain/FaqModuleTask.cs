namespace Ago.Faq.Domain;

/// <summary>
/// One visitor's walk through the FAQ module's chat entry point - the module contract's own state,
/// keyed by its own id, exactly the role <c>Ago.Calendar.Domain.ChatBookingTask</c> plays for
/// Calendar's booking flow.
///
/// <para><b>Why this exists at all, given the wire contract's reply request carries no
/// <c>siteId</c>.</b> <c>SubmitReplyWireRequest</c> (<c>Ago.Chat.Infrastructure.Modules.ModuleWireContract</c>,
/// mirrored by this module's own <c>ModuleTaskReplyRequest</c>) only ever carries
/// <c>chatTaskId</c>/<c>kind</c>/<c>value</c> - Chat never repeats a <c>siteId</c> it already told this
/// module once. Without a row here, a reply arriving later would have no way to know which tenant's
/// <see cref="KnowledgeBase"/> to answer the question from. This is not optional statelessness; it is
/// the same correlation problem <c>ChatBookingTask</c>'s own remarks describe for the identical reason.</para>
///
/// <para><b>Opaque to <c>Ago.Chat.*</c> on purpose, and opaque the other way too.</b>
/// <c>externalTaskId</c> on the wire is this aggregate's own <see cref="Id"/> in string form; the
/// <c>chatTaskId</c>/<c>conversationId</c> Chat's own requests carry are accepted by the Application
/// handlers only long enough to be echoed back unread - never stored here, matching adr/0065 decision 1
/// applied to this module's own state.</para>
/// </summary>
public sealed class FaqModuleTask
{
    public FaqModuleTaskId Id { get; }

    public SiteId SiteId { get; }

    public FaqModuleTaskState State { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private FaqModuleTask(
        FaqModuleTaskId id, SiteId siteId, FaqModuleTaskState state, DateTimeOffset now)
    {
        Id = id;
        SiteId = siteId;
        State = state;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // EF Core materialization only - never called by domain code.
    private FaqModuleTask()
    {
    }

    /// <summary>A task begins waiting for a question - the bare-trigger case
    /// (<c>StartFaqModuleTaskHandler</c>) persists it in this state and sends the <c>form</c> step
    /// asking for one. The inline-question case calls <see cref="Complete"/> in the same request,
    /// immediately after this factory - see that handler's own remarks.</summary>
    public static FaqModuleTask Start(FaqModuleTaskId id, SiteId siteId, DateTimeOffset now) =>
        new(id, siteId, FaqModuleTaskState.AwaitingQuestion, now);

    /// <summary>An answer or an escalation has been produced for this task's one question. Terminal -
    /// see <see cref="FaqModuleTaskState.Completed"/>. Unlike <c>ChatBookingTask</c>'s four-step walk,
    /// there is no re-open path here: this module never re-asks a follow-up, so once a task has its
    /// one answer there is nothing left for a reply to advance.</summary>
    public void Complete(DateTimeOffset now)
    {
        RequireState(FaqModuleTaskState.AwaitingQuestion);
        State = FaqModuleTaskState.Completed;
        UpdatedAt = now;
    }

    private void RequireState(FaqModuleTaskState expected)
    {
        if (State != expected)
        {
            throw new InvalidFaqModuleTaskStateException(
                $"Cannot advance task {Id.Value} from state {State}; only a reply while it is " +
                $"{expected} may do this.");
        }
    }
}
