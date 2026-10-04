namespace Cale.Modules.Identity.Application.Services;

/// <summary>
/// Datos de la cuenta donde las escuelas pagan la membresía. Se leen de la configuración
/// (Payments:School:*); mientras no estén completos la app no muestra datos bancarios.
/// </summary>
public sealed class SchoolPaymentOptions
{
    public const string SectionName = "Payments:School";

    public string? BankName { get; set; }
    public string? AccountType { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolder { get; set; }
    public string? HolderTaxId { get; set; }
    public string? WhatsApp { get; set; }
    public string? SupportEmail { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BankName)
        && !string.IsNullOrWhiteSpace(AccountNumber)
        && !string.IsNullOrWhiteSpace(AccountHolder)
        && !string.IsNullOrWhiteSpace(HolderTaxId);
}
