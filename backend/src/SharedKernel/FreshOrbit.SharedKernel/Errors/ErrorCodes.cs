namespace FreshOrbit.SharedKernel.Errors;

/// <summary>Validation of error and stored codes (UPPER_SNAKE: A-Z, 0-9 and single underscores).</summary>
public static class ErrorCodes
{
    public static bool IsUpperSnake(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > 80 || code[0] == '_' || code[^1] == '_' || char.IsAsciiDigit(code[0]))
        {
            return false;
        }

        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '_')
            {
                if (code[i - 1] == '_')
                {
                    return false;
                }
            }
            else if (!char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    public static void EnsureValid(string code)
    {
        if (!IsUpperSnake(code))
        {
            throw new ArgumentException($"Code '{code}' is not UPPER_SNAKE.", nameof(code));
        }
    }
}
