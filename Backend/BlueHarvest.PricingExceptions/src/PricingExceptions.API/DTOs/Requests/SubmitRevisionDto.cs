using System.ComponentModel.DataAnnotations;

namespace PricingExceptions.API.DTOs.Requests;

// Expected values describe the state the caller viewed before submitting replacement terms.
public sealed record SubmitRevisionDto(
    [Range(1, 500)] int DiscountBps,
    [Required, StringLength(2000)] string Reason,
    [Range(1, long.MaxValue)] long ExpectedVersion,
    [Range(1, int.MaxValue)] int ExpectedRevisionNumber);
