using FluentValidation.Results;

namespace SwitchPoint.Application.Exceptions;

/// <summary>A request failed validation. Carries a field → messages map that the API renders as RFC 9457 problem details (400).</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] })
    {
    }

    public ValidationException(ValidationResult result)
        : this(ToDictionary(result))
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    private static Dictionary<string, string[]> ToDictionary(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Errors
            .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
    }

    private static string BuildMessage(IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.Count == 0
            ? "The request is invalid."
            : "The request is invalid: " + string.Join("; ", errors.Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}"));
    }
}
