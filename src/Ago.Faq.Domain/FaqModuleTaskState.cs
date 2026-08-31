namespace Ago.Faq.Domain;

/// <summary>
/// Which step a <see cref="FaqModuleTask"/> is waiting a reply for. Two states only - this module's
/// entire flow is one question and one answer, unlike <c>Ago.Calendar.Domain.ChatBookingTaskState</c>'s
/// four-step walk through a booking - so there is exactly one "awaiting" state and one terminal state,
/// named for what is being awaited rather than for what was just sent
/// (<c>ChatBookingTaskState</c>'s own remarks explain why that framing is the one
/// <c>ReplyToFaqModuleTaskHandler</c> actually branches on).
/// </summary>
public enum FaqModuleTaskState
{
    /// <summary>The task exists and the <c>form</c> step asking for a question has been sent. Set
    /// only for the bare-trigger case (<c>/faq</c> with no question text after it) - a task started
    /// with an inline question is answered and completed in the same call, never passing through
    /// this state.</summary>
    AwaitingQuestion,

    /// <summary>An answer (or an escalation) has been produced and sent. Terminal - no further reply
    /// is expected, and <see cref="FaqModuleTask"/> refuses one.</summary>
    Completed,
}
