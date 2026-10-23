namespace FreshOrbit.SharedKernel.Errors;

/// <summary>One refusal reason tied to a request field (camelCase path, e.g. <c>lines[0].quantity</c>).</summary>
public sealed record FieldError(string Field, string Code, string Message);
