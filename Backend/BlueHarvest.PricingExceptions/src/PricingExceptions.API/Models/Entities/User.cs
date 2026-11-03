using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Models.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; }
}

