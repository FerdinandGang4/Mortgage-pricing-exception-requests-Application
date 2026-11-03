using System.ComponentModel.DataAnnotations;
using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.DTOs.Requests;

public sealed record ReviewDecisionDto(
    [Required] DecisionOutcome? Outcome,
    [Range(1, long.MaxValue)] long ExpectedVersion,
    [Range(1, int.MaxValue)] int ReviewedRevisionNumber,
    [StringLength(2000)] string? Reason = null);
