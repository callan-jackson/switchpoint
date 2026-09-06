namespace SwitchPoint.Application.Exceptions;

/// <summary>The request conflicts with the current state of the entity, e.g. editing a locked analysis or deleting a calculated one (API: 409).</summary>
public sealed class ConflictException : Exception
{
    public ConflictException()
        : base("The request conflicts with the current state of the resource.")
    {
    }

    public ConflictException(string message)
        : base(message)
    {
    }
}
