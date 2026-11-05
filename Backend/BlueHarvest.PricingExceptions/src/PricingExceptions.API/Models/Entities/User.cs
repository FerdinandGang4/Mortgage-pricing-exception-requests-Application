using PricingExceptions.API.Models.Enums;

namespace PricingExceptions.API.Models.Entities;

// Internal identity and role used for workflow access and reviewer assignment.
public sealed class User
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; }
}

