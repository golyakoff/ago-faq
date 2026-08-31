using Ago.Platform.Kernel;

namespace Ago.Faq.Application.UseCases.FaqModuleTask;

/// <summary>
/// This surface's own expected failures, in the same <c>&lt;area&gt;.&lt;reason&gt;</c> vocabulary
/// <c>Ago.Calendar.Application.UseCases.ChatModuleTask.ChatModuleTaskErrors</c> establishes. This
/// endpoint's only intended caller is `Ago.Chat.*`'s own server, over a link this module's own report
/// names as carrying no service-to-service auth yet (adr/0077) - so there is no enumeration concern
/// driving these messages to be deliberately vague.
/// </summary>
public static class FaqModuleTaskErrors
{
    public static Error TaskNotFound() => new(
        "faq_module_task.not_found",
        "No FAQ module task answers to that id.");

    public static Error AlreadyComplete() => new(
        "faq_module_task.already_complete",
        "This task already answered its one question; start a new one to ask again.");

    /// <summary>The reply's own <c>kind</c> is not <c>form</c> - the only kind this module's tasks
    /// ever wait on a reply for.</summary>
    public static Error KindMismatch() => new(
        "faq_module_task.kind_mismatch",
        "That reply's kind does not match the step this task is currently waiting on.");
}
