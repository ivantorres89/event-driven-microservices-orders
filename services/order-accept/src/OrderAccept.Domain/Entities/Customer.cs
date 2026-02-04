using OrderAccept.Domain.Entities.Base;

namespace OrderAccept.Domain.Entities;

/// <summary>
/// Customer record (system of record is Azure SQL / SQL Server).
///
/// Notes:
/// - <see cref="NationalId"/> is intended to be treated as sensitive (e.g., Dynamic Data Masking).
/// </summary>
public sealed class Customer : EntityBase
{
    /// <summary>
    /// External identity from upstream systems (kept to map incoming messages to internal keys).
    /// </summary>
    public string ExternalCustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Customer first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Customer last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Customer email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Customer phone number.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// National ID / DNI (sensitive column).
    /// </summary>
    public string NationalId { get; set; } = string.Empty;

    /// <summary>
    /// Customer date of birth (optional).
    /// </summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>
    /// Primary street address line.
    /// </summary>
    public string AddressLine1 { get; set; } = string.Empty;

    /// <summary>
    /// City or locality.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Postal or ZIP code.
    /// </summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>
    /// ISO country code (e.g., US, ES).
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Orders placed by the customer.
    /// </summary>
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
