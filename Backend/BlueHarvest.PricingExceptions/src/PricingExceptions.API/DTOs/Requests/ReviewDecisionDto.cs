using System.ComponentModel.DataAnnotations;
using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.DTOs.Requests;

// Nullable Outcome lets validation distinguish an omitted decision from a valid enum value.
// Version and revision identify the exact state the reviewer intends to decide.
public sealed record ReviewDecisionDto(
    [Required] DecisionOutcome? Outcome,
    [Range(1, long.MaxValue)] long ExpectedVersion,
    [Range(1, int.MaxValue)] int ReviewedRevisionNumber,
    [StringLength(2000)] string? Reason = null);
