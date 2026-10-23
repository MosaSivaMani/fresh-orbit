namespace FreshOrbit.SharedKernel.Web.Errors;

/// <summary>One entry of the Problem Details <c>errors</c> extension (field → list of these).</summary>
public sealed record ProblemError(string Code, string Message);
