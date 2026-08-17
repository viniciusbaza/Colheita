using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Land;
using FarmAndFriends.Api.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Authorize]
[Route("plots")]
public sealed class LandPurchaseController : ControllerBase
{
    private readonly LandExpansionService _landExpansionService;

    public LandPurchaseController(
        LandExpansionService landExpansionService)
    {
        _landExpansionService = landExpansionService;
    }

    [HttpPost("{plotId:guid}/purchase")]
    public async Task<IActionResult> Purchase(
        Guid plotId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        PurchaseLandRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyValue,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        if (!Guid.TryParse(idempotencyKeyValue, out var idempotencyKey)
            || idempotencyKey == Guid.Empty)
        {
            return LandProblem(
                StatusCodes.Status400BadRequest,
                LandErrorCodes.IdempotencyKeyRequired,
                "Chave idempotente inválida",
                "Envie um Idempotency-Key UUID válido.");
        }

        if (request == null
            || !LandExpansionService.TryParsePaymentCurrency(
                request.PaymentCurrency,
                out var paymentCurrency))
        {
            return LandProblem(
                StatusCodes.Status400BadRequest,
                LandErrorCodes.InvalidPaymentCurrency,
                "Moeda de pagamento inválida",
                "Use paymentCurrency 'coins' ou 'premiumCoins'.");
        }

        var attempt = await _landExpansionService.PurchaseAsync(
            userId,
            plotId,
            paymentCurrency,
            idempotencyKey,
            cancellationToken);
        return Map(attempt);
    }

    private IActionResult Map(LandPurchaseAttempt attempt) =>
        attempt.Failure switch
        {
            LandPurchaseFailure.None => Ok(attempt.Response),
            LandPurchaseFailure.Unauthorized => Unauthorized(),
            LandPurchaseFailure.FeatureDisabled => LandProblem(
                StatusCodes.Status409Conflict,
                LandErrorCodes.FeatureDisabled,
                "Expansão de terrenos desativada",
                "Novas compras de terreno estão temporariamente indisponíveis."),
            LandPurchaseFailure.PlotNotFound => LandProblem(
                StatusCodes.Status404NotFound,
                LandErrorCodes.PlotNotFound,
                "Lote não encontrado",
                "O lote não existe ou não pertence à sua fazenda."),
            LandPurchaseFailure.StaleOffer => LandProblem(
                StatusCodes.Status409Conflict,
                LandErrorCodes.StaleOffer,
                "Oferta de terreno desatualizada",
                "Atualize sua fazenda para obter a oferta atual."),
            LandPurchaseFailure.LevelRequired => LandProblem(
                StatusCodes.Status409Conflict,
                LandErrorCodes.LevelRequired,
                "Nível insuficiente",
                "Alcance o nível exigido pela oferta atual."),
            LandPurchaseFailure.InsufficientCoins => LandProblem(
                StatusCodes.Status409Conflict,
                LandErrorCodes.InsufficientCoins,
                "Moedas insuficientes",
                "Você não possui moedas suficientes para esta compra."),
            LandPurchaseFailure.InsufficientPremiumCoins => LandProblem(
                StatusCodes.Status409Conflict,
                LandErrorCodes.InsufficientPremiumCoins,
                "Moedas premium insuficientes",
                "Você não possui moedas premium suficientes para esta compra."),
            LandPurchaseFailure.UnsupportedLayout => LandProblem(
                StatusCodes.Status409Conflict,
                LandErrorCodes.UnsupportedLayout,
                "Layout de fazenda não suportado",
                "A fazenda precisa ser revisada antes de comprar novos terrenos."),
            LandPurchaseFailure.InventoryNotFound => LandProblem(
                StatusCodes.Status409Conflict,
                LandErrorCodes.InventoryNotFound,
                "Inventário não encontrado",
                "O inventário do jogador não foi encontrado."),
            LandPurchaseFailure.IdempotencyKeyReused => LandProblem(
                StatusCodes.Status409Conflict,
                LandErrorCodes.IdempotencyKeyReused,
                "Chave idempotente reutilizada",
                "Este Idempotency-Key já foi usado para outra compra ou moeda."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError)
        };

    private ObjectResult LandProblem(
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
