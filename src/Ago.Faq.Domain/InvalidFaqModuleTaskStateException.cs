namespace Ago.Faq.Domain;

/// <summary>
/// A reply (or a completion) arrived for a task that is not (or no longer) waiting on it - the same
/// class of invariant failure <c>Ago.Calendar.Domain.InvalidChatBookingTaskStateException</c> already
/// names for <c>ChatBookingTask</c>. A bug in the caller, never an expected outcome, so it is an
/// exception rather than a <c>Result</c> (coding-style.md, ago-root).
/// </summary>
public sealed class InvalidFaqModuleTaskStateException(string message) : InvalidOperationException(message);
