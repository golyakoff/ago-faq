using Ago.Platform.Kernel;

namespace Ago.Faq.Api.Http;

/// <summary>
/// api-design.md (ago-root): "Errors are RFC 7807 problem details with a stable machine-readable
/// <c>type</c>... clients branch on <c>type</c>, never on the message." This module's error codes
/// (<c>FaqModuleTaskErrors</c>, <c>KnowledgeBaseErrors</c>) are that vocabulary; this is the one place
/// they become a status code - the same split <c>Ago.Calendar.Api.Http.ErrorExtensions</c> and
/// `ago-chat`'s own <c>ErrorExtensions</c> both make: an HTTP status is a statement about a protocol,
/// and Application must not know there is one.
/// </summary>
public static class ErrorExtensions
{
    public static IResult ToProblem(this Error error, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var statusCode = error.Code switch
        {
            "faq_module_task.not_found" => StatusCodes.Status404NotFound,
            // 409, not 404: the task exists and the request was well-formed - it simply already
            // answered its one question. The same reasoning Ago.Calendar.Api's own
            // chat_module_task.already_complete comment gives.
            "faq_module_task.already_complete" => StatusCodes.Status409Conflict,
            "faq_module_task.kind_mismatch" => StatusCodes.Status400BadRequest,
            "knowledge_base.text_too_long" => StatusCodes.Status400BadRequest,
            // Anything unmapped is a bug in this switch, not a client error - a 500 says so honestly
            // instead of inventing a 400 that would make a caller retry something that cannot work.
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: statusCode,
            type: error.Code,
            extensions: new Dictionary<string, object?> { ["traceId"] = httpContext.TraceIdentifier });
    }
}
