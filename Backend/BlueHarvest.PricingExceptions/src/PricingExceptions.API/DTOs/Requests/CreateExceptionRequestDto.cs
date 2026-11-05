using System.ComponentModel.DataAnnotations;

namespace PricingExceptions.API.DTOs.Requests;

// API validation handles field shape; the service checks identity, assignment, and workflow rules.
// DiscountBps is measured in basis points: 100 basis points equals one percentage point.
public sealed record CreateExceptionRequestDto(
    Guid MortgageApplicationId,
    [Range(1, 500)] int DiscountBps,
    [Required, StringLength(2000)] string Reason,
    [Required] Guid? ReviewerId);
