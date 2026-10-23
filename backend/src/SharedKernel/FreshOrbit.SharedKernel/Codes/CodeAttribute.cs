namespace FreshOrbit.SharedKernel.Codes;

/// <summary>Overrides the stored UPPER_SNAKE code of an enum member.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class CodeAttribute : Attribute
{
    public CodeAttribute(string code) => Code = code;

    public string Code { get; }
}
