namespace SwitchPoint.Application.Exceptions;

/// <summary>The requested entity does not exist in the caller's firm. The API maps it to 404 (cross-firm access is deliberately indistinguishable).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityType, Guid id)
        : base($"{entityType} '{id}' was not found.")
    {
        EntityType = entityType;
        Id = id.ToString("D");
    }

    public NotFoundException(string entityType, string key)
        : base($"{entityType} '{key}' was not found.")
    {
        EntityType = entityType;
        Id = key;
    }

    public NotFoundException(string message)
        : base(message)
    {
        EntityType = "Resource";
        Id = string.Empty;
    }

    public string EntityType { get; }

    public string Id { get; }
}
