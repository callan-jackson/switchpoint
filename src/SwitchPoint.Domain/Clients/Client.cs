using SwitchPoint.Domain.Common;

namespace SwitchPoint.Domain.Clients;

/// <summary>A person the firm advises. Aggregate root for their schemes and analyses.</summary>
public sealed class Client : Entity, ITenantScoped
{
    /// <summary>For EF Core materialisation only.</summary>
    private Client()
    {
        FirstName = null!;
        LastName = null!;
        ExternalReference = ExternalReference.Manual;
    }

    public Client(
        Guid id,
        Guid firmId,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        Sex sex,
        DateTime createdAtUtc,
        string? title = null,
        ExternalReference? externalReference = null)
        : base(id, createdAtUtc)
    {
        FirmId = Guard.NotEmpty(firmId);
        FirstName = Guard.NotNullOrWhiteSpace(firstName);
        LastName = Guard.NotNullOrWhiteSpace(lastName);
        Guard.Against(dateOfBirth > DateOnly.FromDateTime(createdAtUtc), "Date of birth cannot be in the future.");
        Guard.Against(dateOfBirth < new DateOnly(1900, 1, 1), "Date of birth is implausibly early.");
        DateOfBirth = dateOfBirth;
        Sex = Guard.Defined(sex);
        Title = title;
        ExternalReference = externalReference ?? ExternalReference.Manual;
    }

    public Guid FirmId { get; }
    public string? Title { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string FullName => string.IsNullOrWhiteSpace(Title) ? $"{FirstName} {LastName}" : $"{Title} {FirstName} {LastName}";
    public DateOnly DateOfBirth { get; private set; }
    public Sex Sex { get; private set; }

    /// <summary>Stored masked: only the last three characters are retained (e.g. "*****12C").</summary>
    public string? NationalInsuranceNumberMasked { get; private set; }

    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public Address? Address { get; private set; }
    public MaritalStatus MaritalStatus { get; private set; } = MaritalStatus.Single;
    public EmploymentStatus EmploymentStatus { get; private set; } = EmploymentStatus.Employed;
    public decimal AnnualSalary { get; private set; }
    public int TargetRetirementAge { get; private set; } = 67;
    public TaxRegime TaxRegime { get; private set; } = TaxRegime.RestOfUk;

    /// <summary>Attitude to risk on a 1 (lowest) to 7 (highest) scale.</summary>
    public int RiskProfile { get; private set; } = 4;

    public HealthStatus Health { get; private set; } = HealthStatus.Standard;
    public bool IsSmoker { get; private set; }
    public StatePensionForecast StatePension { get; private set; } = StatePensionForecast.Unknown;
    public ExternalReference ExternalReference { get; private set; }

    public int AgeOn(DateOnly date)
    {
        int age = date.Year - DateOfBirth.Year;
        if (date < DateOfBirth.AddYears(age))
        {
            age--;
        }

        return age;
    }

    public void UpdatePersonalDetails(string? title, string firstName, string lastName, DateOnly dateOfBirth, Sex sex, DateTime nowUtc)
    {
        Title = title;
        FirstName = Guard.NotNullOrWhiteSpace(firstName);
        LastName = Guard.NotNullOrWhiteSpace(lastName);
        Guard.Against(dateOfBirth > DateOnly.FromDateTime(nowUtc), "Date of birth cannot be in the future.");
        DateOfBirth = dateOfBirth;
        Sex = Guard.Defined(sex);
        Touch(nowUtc);
    }

    public void UpdateContactDetails(string? email, string? phone, Address? address, DateTime nowUtc)
    {
        Email = email;
        Phone = phone;
        Address = address;
        Touch(nowUtc);
    }

    /// <summary>Accepts a full NI number and stores only its masked form.</summary>
    public void SetNationalInsuranceNumber(string? fullNumber, DateTime nowUtc)
    {
        NationalInsuranceNumberMasked = Mask(fullNumber);
        Touch(nowUtc);
    }

    public void UpdateCircumstances(
        MaritalStatus maritalStatus,
        EmploymentStatus employmentStatus,
        decimal annualSalary,
        int targetRetirementAge,
        TaxRegime taxRegime,
        int riskProfile,
        HealthStatus health,
        bool isSmoker,
        StatePensionForecast statePension,
        DateTime nowUtc)
    {
        MaritalStatus = Guard.Defined(maritalStatus);
        EmploymentStatus = Guard.Defined(employmentStatus);
        AnnualSalary = Guard.NonNegative(annualSalary);
        TargetRetirementAge = Guard.InRange(targetRetirementAge, 50, 80);
        TaxRegime = Guard.Defined(taxRegime);
        RiskProfile = Guard.InRange(riskProfile, 1, 7);
        Health = Guard.Defined(health);
        IsSmoker = isSmoker;
        StatePension = Guard.NotNull(statePension);
        Touch(nowUtc);
    }

    public void LinkExternal(ExternalReference reference, DateTime nowUtc)
    {
        ExternalReference = Guard.NotNull(reference);
        Touch(nowUtc);
    }

    public static string? Mask(string? fullNumber)
    {
        if (string.IsNullOrWhiteSpace(fullNumber))
        {
            return null;
        }

        string compact = new(fullNumber.Where(char.IsLetterOrDigit).ToArray());
        if (compact.Length <= 3)
        {
            return new string('*', compact.Length);
        }

        return new string('*', compact.Length - 3) + compact[^3..].ToUpperInvariant();
    }
}
