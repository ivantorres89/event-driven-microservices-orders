namespace OrderProcess.Persistence.Abstractions.Entities;

/// <summary>
/// Customer record (system of record is Azure SQL / SQL Server).
///
/// Notes:
/// - <see cref="NationalId"/> is intended to be treated as sensitive (e.g., Dynamic Data Masking).
/// </summary>
public sealed class Customer : EntityBase
{
    // External identity from upstream systems (kept to map incoming messages to internal keys).
    public string ExternalCustomerId { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// National ID / DNI (sensitive column).
    /// </summary>
    public string NationalId { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
