namespace DeviceManagerMVC.Domain.Common;

/// <summary>Base type for errors raised by the domain.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>Input breaks an invariant (required, max length, unknown enum). Maps to 400.</summary>
public sealed class DomainValidationException(string message) : DomainException(message);

/// <summary>Valid input, but a business rule forbids the operation in the current state. Maps to 409.</summary>
public sealed class BusinessRuleViolationException(string message) : DomainException(message);
