using System.Runtime.CompilerServices;

namespace SwitchPoint.Domain.Common;

/// <summary>
/// Argument validation helpers. Single-argument checks throw the standard
/// <see cref="ArgumentException"/> family; cross-field invariants use <see cref="Against"/>,
/// which throws <see cref="DomainException"/>. Every helper returns the validated value so it
/// can be used inline in constructors and property accessors.
/// </summary>
public static class Guard
{
    /// <summary>Ensures a reference is not null.</summary>
    public static T NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }

    /// <summary>Ensures a string is not null, empty or white space.</summary>
    public static string NotNullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    /// <summary>Ensures a <see cref="Guid"/> is not <see cref="Guid.Empty"/>.</summary>
    public static Guid NotEmpty(Guid value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must not be empty.", paramName);
        }

        return value;
    }

    /// <summary>Ensures a sequence is not null and has at least one item; returns a defensive copy.</summary>
    public static IReadOnlyList<T> NotEmpty<T>(IEnumerable<T>? items, [CallerArgumentExpression(nameof(items))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(items, paramName);
        var copy = items.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("Collection must contain at least one item.", paramName);
        }

        return copy;
    }

    /// <summary>Ensures a decimal is zero or greater.</summary>
    public static decimal NonNegative(decimal value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        return value;
    }

    /// <summary>Ensures an integer is zero or greater.</summary>
    public static int NonNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        return value;
    }

    /// <summary>Ensures a decimal is strictly greater than zero.</summary>
    public static decimal Positive(decimal value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
        return value;
    }

    /// <summary>Ensures an integer is strictly greater than zero.</summary>
    public static int Positive(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
        return value;
    }

    /// <summary>Ensures a decimal fraction lies in [0, 1]. Rates are fractions: 0.05m is five per cent.</summary>
    public static decimal Fraction(decimal value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => InRange(value, 0m, 1m, paramName);

    /// <summary>Ensures a decimal lies within an inclusive range.</summary>
    public static decimal InRange(decimal value, decimal min, decimal max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max} (inclusive).");
        }

        return value;
    }

    /// <summary>Ensures an integer lies within an inclusive range.</summary>
    public static int InRange(int value, int min, int max, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Value must be between {min} and {max} (inclusive).");
        }

        return value;
    }

    /// <summary>Ensures an enum value is one of the declared members.</summary>
    public static TEnum Defined<TEnum>(TEnum value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"'{value}' is not a defined {typeof(TEnum).Name} value.");
        }

        return value;
    }

    /// <summary>Throws <see cref="DomainException"/> when <paramref name="condition"/> is true.</summary>
    public static void Against(bool condition, string message)
    {
        if (condition)
        {
            throw new DomainException(message);
        }
    }
}
