using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Care;
using FarmAndFriends.Api.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Authorize]
[Route("farms/{farmId:guid}/plots/{plotId:guid}/care")]
public sealed class CropCareController : ControllerBase
{
    private readonly CropCareService _cropCareService;

    public CropCareController(CropCareService cropCareService)
    {
        _cropCareService = cropCareService;
    }

    [HttpPost]
    public async Task<IActionResult> CareForPlot(
        Guid farmId,
        Guid plotId,
        [FromBody] CareForPlotRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyValue,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var visitorUserId))
        {
            return Unauthorized();
        }

        if (request.CareOpportunityId == Guid.Empty)
        {
            return CareProblem(
                StatusCodes.Status400BadRequest,
                "INVALID_CARE_OPPORTUNITY",
                "O cuidado informado é inválido.");
        }

        if (!Guid.TryParse(idempotencyKeyValue, out var idempotencyKey)
            || idempotencyKey == Guid.Empty)
        {
            return CareProblem(
                StatusCodes.Status400BadRequest,
                "INVALID_IDEMPOTENCY_KEY",
                "Envie um Idempotency-Key UUID válido.");
        }

        var attempt = await _cropCareService.CareAsync(
            visitorUserId,
            farmId,
            plotId,
            request.CareOpportunityId,
            idempotencyKey,
            cancellationToken);

        if (attempt.Succeeded)
            return Ok(attempt.Response);

        return attempt.Failure switch
        {
            CropCareFailure.PlotNotFound =>
                CareProblem(
                    StatusCodes.Status404NotFound,
                    "CARE_PLOT_NOT_FOUND",
                    "Plantação não encontrada."),
            CropCareFailure.SelfCare =>
                CareProblem(
                    StatusCodes.Status400BadRequest,
                    "SELF_CARE_NOT_ALLOWED",
                    "Você não pode cuidar da própria plantação."),
            CropCareFailure.NotFriends =>
                CareProblem(
                    StatusCodes.Status403Forbidden,
                    "CARE_REQUIRES_FRIENDSHIP",
                    "Apenas amigos podem deixar um cuidado."),
            CropCareFailure.NotGrowing =>
                CareProblem(
                    StatusCodes.Status409Conflict,
                    "CARE_PLOT_NOT_GROWING",
                    "Esta plantação não está mais crescendo."),
            CropCareFailure.OpportunityUnavailable =>
                CareProblem(
                    StatusCodes.Status409Conflict,
                    "CARE_OPPORTUNITY_CHANGED",
                    "Esta oportunidade de cuidado não está mais disponível."),
            CropCareFailure.CooldownActive =>
                CareProblem(
                    StatusCodes.Status409Conflict,
                    "CARE_COOLDOWN",
                    "Você ainda precisa aguardar para regar este cultivo novamente.",
                    nextCareAt: attempt.NextCareAt),
            CropCareFailure.IdempotencyKeyReused =>
                CareProblem(
                    StatusCodes.Status409Conflict,
                    "IDEMPOTENCY_KEY_REUSED",
                    "Este Idempotency-Key já foi usado em outro cuidado."),
            CropCareFailure.InventoryNotFound =>
                CareProblem(
                    StatusCodes.Status500InternalServerError,
                    "CARE_REWARD_FAILED",
                    "Não foi possível creditar a recompensa do cuidado.",
                    title: "Falha ao creditar o cuidado."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private ObjectResult CareProblem(
        int statusCode,
        string code,
        string detail,
        string title = "Não foi possível concluir o cuidado.",
        DateTime? nextCareAt = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
        problem.Extensions["code"] = code;
        if (nextCareAt.HasValue)
            problem.Extensions["nextCareAt"] = nextCareAt.Value;

        return StatusCode(statusCode, problem);
    }
}
