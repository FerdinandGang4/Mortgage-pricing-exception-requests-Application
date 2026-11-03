using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.DTOs.Responses;

public sealed record UserResponseDto(Guid Id, string DisplayName, UserRole Role);
