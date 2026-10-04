using System.Globalization;
using Cale.Modules.Identity.Domain;

namespace Cale.Modules.Identity.Application.Services;

public static class AccountSuspensionMessages
{
    public const string Code = "account_suspended";

    /// <summary>Lo que ve el usuario suspendido al intentar entrar: motivo, fechas y cómo pedir revisión.</summary>
    public static string ForUser(AccountStatusEvent suspension)
    {
        var since = ColombiaDate(suspension.CreatedAt);
        var until = suspension.SuspendedUntil is { } end
            ? $"La suspensión termina el {ColombiaDate(end)}."
            : "La suspensión no tiene fecha de fin.";
        return $"Tu cuenta está suspendida desde el {since}. Motivo: {suspension.Reason} {until} "
            + "Si crees que es un error, solicita la revisión escribiendo al correo de contacto de Luz Verde.";
    }

    // Colombia no tiene horario de verano: UTC-5 todo el año.
    private static string ColombiaDate(DateTime utc) =>
        utc.AddHours(-5).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
