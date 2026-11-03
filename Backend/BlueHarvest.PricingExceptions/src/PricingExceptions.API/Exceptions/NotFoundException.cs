namespace PricingExceptions.API.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);
