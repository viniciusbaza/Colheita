using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Pests;
using FarmAndFriends.Api.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Authorize]
[Route("farms/{farmId:guid}/plots/{plotId:guid}/pest")]
public sealed class PestController : ControllerBase
{
    private readonly PestService _pestService;

    public PestController(PestService pestService)
    {
        _pestService = pestService;
    }

    [HttpPost("remove")]
    public async Task<IActionResult> Remove(
        Guid farmId,
        Guid plotId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        RemovePestRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyValue,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        if (!Guid.TryParse(idempotencyKeyValue, out var idempotencyKey))
        {
            return PestProblem(
                StatusCodes.Status400BadRequest,
                PestErrorCodes.IdempotencyKeyRequired,
                "Chave idempotente inválida",
                "Envie um Idempotency-Key UUID válido.");
        }

        if (request == null || request.PestOccurrenceId == Guid.Empty)
        {
            return PestProblem(
                StatusCodes.Status400BadRequest,
                PestErrorCodes.OccurrenceIdRequired,
                "Identidade da lagarta inválida",
                "Atualize o lote e envie um pestOccurrenceId UUID válido.");
        }

        var attempt = await _pestService.RemoveAsync(
            userId,
            farmId,
            plotId,
            request.PestOccurrenceId,
            idempotencyKey,
            cancellationToken);
        return Map(attempt.Failure, attempt.Response);
    }

    [HttpPost("protection")]
    public async Task<IActionResult> ApplyProtection(
        Guid farmId,
        Guid plotId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var attempt = await _pestService.ApplyProtectionAsync(
            userId,
            farmId,
            plotId,
            cancellationToken);
        return Map(attempt.Failure, attempt.Response);
    }

    private IActionResult Map(PestActionFailure failure, object? response)
    {
        return failure switch
        {
            PestActionFailure.None => Ok(response),
            PestActionFailure.FeatureDisabled => PestProblem(
                StatusCodes.Status409Conflict,
                PestErrorCodes.FeatureDisabled,
                "Sistema de pragas desativado",
                "A proteção contra pragas está temporariamente indisponível."),
            PestActionFailure.FarmNotFound => PestProblem(
                StatusCodes.Status404NotFound,
                PestErrorCodes.FarmNotFound,
                "Fazenda não encontrada",
                "A fazenda informada não existe."),
            PestActionFailure.PlotNotFound => PestProblem(
                StatusCodes.Status404NotFound,
                PestErrorCodes.PlotNotFound,
                "Lote não encontrado",
                "O lote informado não existe ou está bloqueado."),
            PestActionFailure.NotFriends => PestProblem(
                StatusCodes.Status403Forbidden,
                PestErrorCodes.Forbidden,
                "Ação não permitida",
                "Somente o dono ou um amigo aceito pode interagir com a praga."),
            PestActionFailure.PestNotActive =>
                PestProblem(
                    StatusCodes.Status409Conflict,
                    PestErrorCodes.NotActive,
                    "Praga não está ativa",
                    "A lagarta já foi removida ou não está mais ativa neste lote."),
            PestActionFailure.PestOccurrenceMismatch =>
                PestProblem(
                    StatusCodes.Status409Conflict,
                    PestErrorCodes.OccurrenceMismatch,
                    "Lagarta desatualizada",
                    "Esta lagarta já mudou ou foi resolvida. Atualize o lote e tente novamente."),
            PestActionFailure.AlreadyProtected =>
                PestProblem(
                    StatusCodes.Status409Conflict,
                    PestErrorCodes.AlreadyProtected,
                    "Lote já protegido",
                    "Este lote já está protegido contra pragas."),
            PestActionFailure.InventoryNotFound =>
                PestProblem(
                    StatusCodes.Status400BadRequest,
                    PestErrorCodes.InventoryNotFound,
                    "Inventário não encontrado",
                    "O inventário do jogador não foi encontrado."),
            PestActionFailure.ItemUnavailable =>
                PestProblem(
                    StatusCodes.Status409Conflict,
                    PestErrorCodes.ItemUnavailable,
                    "Repelente indisponível",
                    "Você não possui Repelente Natural."),
            PestActionFailure.IdempotencyKeyReused =>
                PestProblem(
                    StatusCodes.Status409Conflict,
                    PestErrorCodes.IdempotencyKeyReused,
                    "Chave idempotente reutilizada",
                    "Este Idempotency-Key já foi usado em outra remoção de praga."),
            _ => Problem(statusCode: 500)
        };
    }

    private ObjectResult PestProblem(
        int status,
        string code,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
        problem.Extensions["code"] = code;
        return StatusCode(status, problem);
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
