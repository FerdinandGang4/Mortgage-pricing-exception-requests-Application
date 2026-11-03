namespace PricingExceptions.API.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
