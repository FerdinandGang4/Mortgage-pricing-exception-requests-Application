using PricingExceptions.API.DTOs.Responses;
using PricingExceptions.API.Models.Entities;

namespace PricingExceptions.API.Mappings;

public static class UserMapper
{
    public static UserResponseDto ToResponse(User user) => new(user.Id, user.DisplayName, user.Role);
}
