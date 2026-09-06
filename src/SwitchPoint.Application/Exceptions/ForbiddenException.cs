namespace SwitchPoint.Application.Exceptions;

/// <summary>The caller is authenticated but not permitted to perform the action (API: 403).</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException()
        : base("You are not permitted to perform this action.")
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }
}
