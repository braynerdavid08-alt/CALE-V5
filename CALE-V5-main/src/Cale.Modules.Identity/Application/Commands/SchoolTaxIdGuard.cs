using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Commands;

internal static class SchoolTaxIdGuard
{
    public static async Task EnsureUniqueAsync(
        ISchoolProfileStore profiles,
        string taxId,
        int? exceptSchoolUserId,
        CancellationToken ct)
    {
        var key = ColombianTaxId.ComparisonKey(taxId);
        var taken = (await profiles.ListAllAsync(ct))
            .Any(p => p.UserId != exceptSchoolUserId && ColombianTaxId.ComparisonKey(p.TaxId) == key);
        if (taken)
        {
            throw new ConflictException(
                "Ya hay una escuela registrada con ese NIT. Si es la tuya, inicia sesión o escríbenos.",
                "tax_id_taken");
        }
    }
}
