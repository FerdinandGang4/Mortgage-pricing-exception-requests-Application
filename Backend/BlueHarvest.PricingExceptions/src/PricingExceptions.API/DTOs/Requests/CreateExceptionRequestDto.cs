using System.ComponentModel.DataAnnotations;

namespace PricingExceptions.API.DTOs.Requests;

public sealed record CreateExceptionRequestDto(
    Guid MortgageApplicationId,
    [Range(1, 500)] int DiscountBps,
    [Required, StringLength(2000)] string Reason);
