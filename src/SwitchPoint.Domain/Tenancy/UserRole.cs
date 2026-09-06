namespace SwitchPoint.Domain.Tenancy;

/// <summary>Roles carried as claims on every authenticated user.</summary>
public enum UserRole
{
    Adviser = 0,
    Paraplanner = 1,
    Compliance = 2,
    FirmAdmin = 3,
    PlatformAdmin = 4,
}
