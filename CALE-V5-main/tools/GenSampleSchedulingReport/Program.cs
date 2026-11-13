using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Application.DTOs;

var day = new DateOnly(2026, 10, 28);
var items = new List<AgendaItemDto>
{
    new(
        "theory",
        101,
        "2026-10-28",
        "08:00",
        "09:59",
        "Normas de tránsito · Aula 1",
        "Laura Gómez",
        "Scheduled",
        20,
        3,
        17,
        [
            Stu(11, "Ana María Pérez López", "Reserved", "1020304050", "3001112233", HoursAgo(18)),
            Stu(12, "Carlos Andrés Ruiz", "Reserved", "1122334455", "3109876543", HoursAgo(12)),
            Stu(13, "Diana Sofía Vargas", "Confirmed", "9988776655", "3205556677", HoursAgo(6))
        ]),
    new(
        "theory",
        102,
        "2026-10-28",
        "10:00",
        "11:59",
        "Señales y prevención · Aula 1",
        "Laura Gómez",
        "Scheduled",
        20,
        0,
        20,
        []),
    new(
        "theory",
        103,
        "2026-10-28",
        "16:00",
        "17:59",
        "Mecánica básica · Aula 2",
        "Pedro Castillo",
        "Scheduled",
        15,
        2,
        13,
        [
            Stu(14, "Elena Patricia Mora", "Reserved", "1010101010", "3012223344", HoursAgo(30)),
            Stu(15, "Felipe José Herrera", "Attended", "2020202020", "3156667788", HoursAgo(4))
        ]),
    new(
        "practical",
        201,
        "2026-10-28",
        "09:00",
        "10:00",
        "Clase de manejo · Moto 12 (ABC123)",
        "Andrés Melo",
        "Scheduled",
        1,
        1,
        0,
        [
            Stu(16, "Gabriel Eduardo Niño", "Reserved", "3030303030", "3004445566", HoursAgo(40))
        ]),
    new(
        "practical",
        202,
        "2026-10-28",
        "14:00",
        "15:00",
        "Clase de manejo · Auto 3 (XYZ789)",
        "Andrés Melo",
        "Scheduled",
        1,
        0,
        1,
        []),
    new(
        "exam",
        0,
        "2026-10-28",
        "09:00",
        null,
        "Examen teórico",
        null,
        "Past",
        1,
        1,
        0,
        [
            Stu(17, "Braynner Estudiante Demo", "Booked", "4040404040", "3117778899", HoursAgo(50))
        ]),
    new(
        "exam",
        0,
        "2026-10-28",
        "10:00",
        null,
        "Examen teórico",
        null,
        "Available",
        1,
        0,
        1,
        []),
    new(
        "exam",
        0,
        "2026-10-28",
        "11:00",
        null,
        "Examen teórico",
        null,
        "Available",
        1,
        0,
        1,
        [])
};

var bytes = SchedulingReportGenerator.BuildWorkbook(day, items);
var path = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "programacion-hoy-ejemplo.xlsx"));
File.WriteAllBytes(path, bytes);
Console.WriteLine(path);

static AgendaStudentDto Stu(
    int id,
    string name,
    string status,
    string doc,
    string phone,
    DateTime reservedAt) =>
    new(id, name, status, doc, phone, reservedAt);

static DateTime HoursAgo(int hours) =>
    DateTime.UtcNow.AddHours(-hours);
