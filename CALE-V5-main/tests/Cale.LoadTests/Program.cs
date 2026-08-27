using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cale.LoadTests;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<int> Main(string[] args)
    {
        var opts = Options.Parse(args);
        Console.WriteLine($"CALE load tests â†’ {opts.BaseUrl}");
        Console.WriteLine(
            $"students={opts.Students} questions={opts.Questions} scenarios={string.Join(',', opts.Scenarios)}");

        using var http = new HttpClient
        {
            BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        var health = await http.GetAsync("api/health/ready");
        if (!health.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"API not ready: {(int)health.StatusCode}");
            return 2;
        }

        var report = new List<ScenarioResult>();
        foreach (var scenario in opts.Scenarios)
        {
            Console.WriteLine($"\n=== {scenario} ===");
            var result = scenario switch
            {
                "wave50" => await RunStudentWaveAsync(http, opts with { Students = 50 }),
                "wave100" => await RunStudentWaveAsync(http, opts with { Students = 100 }),
                "wave" => await RunStudentWaveAsync(http, opts),
                "finish-storm" => await RunFinishStormAsync(http, opts),
                "results-storm" => await RunResultsStormAsync(http, opts),
                "publish-storm" => await RunPublishStormAsync(http, opts),
                "all" => await RunAllAsync(http, opts),
                _ => throw new InvalidOperationException($"Unknown scenario: {scenario}")
            };

            if (scenario == "all")
            {
                report.AddRange(((List<ScenarioResult>)result!).Cast<ScenarioResult>());
            }
            else
            {
                report.Add((ScenarioResult)result!);
            }
        }

        Console.WriteLine("\n========== SUMMARY ==========");
        Console.WriteLine(
            $"{"Scenario",-18} {"OK",6} {"Fail",6} {"p50ms",8} {"p95ms",8} {"maxms",8} {"Notes"}");
        foreach (var r in report)
        {
            Console.WriteLine(
                $"{r.Name,-18} {r.Ok,6} {r.Fail,6} {r.P50Ms,8} {r.P95Ms,8} {r.MaxMs,8} {r.Notes}");
        }

        var outPath = Path.GetFullPath(opts.OutFile);
        await File.WriteAllTextAsync(
            outPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"\nWrote {outPath}");
        return report.Any(x => x.Fail > 0) ? 1 : 0;
    }

    private static async Task<object> RunAllAsync(HttpClient http, Options opts)
    {
        var list = new List<ScenarioResult>
        {
            await RunStudentWaveAsync(http, opts with { Students = 50, Name = "wave50" }),
            await RunStudentWaveAsync(http, opts with { Students = 100, Name = "wave100" }),
            await RunFinishStormAsync(http, opts),
            await RunResultsStormAsync(http, opts),
            await RunPublishStormAsync(http, opts)
        };
        return list;
    }

    private static async Task<ScenarioResult> RunStudentWaveAsync(HttpClient http, Options opts)
    {
        var name = opts.Name ?? $"wave{opts.Students}";
        var bankId = await ResolveBankIdAsync(http, opts);
        var students = await EnsureStudentsAsync(http, opts);

        var latencies = new ConcurrentQueue<long>();
        var errors = new ConcurrentQueue<string>();
        var ok = 0;

        var swAll = Stopwatch.StartNew();
        await Parallel.ForEachAsync(
            students,
            new ParallelOptions { MaxDegreeOfParallelism = opts.Students },
            async (student, ct) =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var client = CreateAuthed(http, student.Token);
                    var start = await client.PostAsJsonAsync(
                        "api/exams/start",
                        new
                        {
                            bankId,
                            examId = (int?)null,
                            questionCount = opts.Questions,
                            mode = "practice",
                            timeMinutes = 10
                        },
                        ct);
                    if (!start.IsSuccessStatusCode)
                    {
                        errors.Enqueue($"start {(int)start.StatusCode}: {await start.Content.ReadAsStringAsync(ct)}");
                        return;
                    }

                    var session = await start.Content.ReadFromJsonAsync<StartDto>(Json, ct)
                        ?? throw new InvalidOperationException("empty start");

                    foreach (var q in session.Questions.Take(Math.Min(3, session.Questions.Count)))
                    {
                        var optionId = q.Options.FirstOrDefault()?.Id ?? 0;
                        var ans = await client.PostAsJsonAsync(
                            $"api/exams/{session.AttemptId}/answer",
                            new { questionId = q.Id, optionId },
                            ct);
                        if (!ans.IsSuccessStatusCode && (int)ans.StatusCode != 204)
                        {
                            errors.Enqueue($"answer {(int)ans.StatusCode}");
                            return;
                        }
                    }

                    var finish = await client.PostAsync(
                        $"api/exams/{session.AttemptId}/finish",
                        null,
                        ct);
                    if (!finish.IsSuccessStatusCode)
                    {
                        errors.Enqueue($"finish {(int)finish.StatusCode}: {await finish.Content.ReadAsStringAsync(ct)}");
                        return;
                    }

                    Interlocked.Increment(ref ok);
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex.Message);
                }
                finally
                {
                    latencies.Enqueue(sw.ElapsedMilliseconds);
                }
            });
        swAll.Stop();

        return ScenarioResult.From(
            name,
            ok,
            errors.Count,
            latencies,
            $"wall={swAll.ElapsedMilliseconds}ms bank={bankId} users={students.Count}; sampleErr={errors.FirstOrDefault() ?? "-"}");
    }

    private static async Task<ScenarioResult> RunFinishStormAsync(HttpClient http, Options opts)
    {
        var bankId = await ResolveBankIdAsync(http, opts);
        var student = (await EnsureStudentsAsync(http, opts with { Students = 1 }))[0];
        using var client = CreateAuthed(http, student.Token);

        var start = await client.PostAsJsonAsync(
            "api/exams/start",
            new
            {
                bankId,
                examId = (int?)null,
                questionCount = opts.Questions,
                mode = "practice",
                timeMinutes = 10
            });
        start.EnsureSuccessStatusCode();
        var session = (await start.Content.ReadFromJsonAsync<StartDto>(Json))!;

        var latencies = new ConcurrentQueue<long>();
        var errors = new ConcurrentQueue<string>();
        var ok = 0;
        var conflict = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, opts.FinishParallel),
            new ParallelOptions { MaxDegreeOfParallelism = opts.FinishParallel },
            async (_, ct) =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var finish = await client.PostAsync(
                        $"api/exams/{session.AttemptId}/finish",
                        null,
                        ct);
                    var code = (int)finish.StatusCode;
                    if (finish.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref ok);
                    }
                    else if (code is 409 or 400)
                    {
                        Interlocked.Increment(ref conflict);
                    }
                    else
                    {
                        errors.Enqueue($"finish {code}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex.Message);
                }
                finally
                {
                    latencies.Enqueue(sw.ElapsedMilliseconds);
                }
            });

        // Exactly one success expected; rest conflict (409).
        var unexpected = errors.Count;
        var fail = unexpected + (ok == 1 ? 0 : 1);
        return ScenarioResult.From(
            "finish-storm",
            ok,
            fail,
            latencies,
            $"conflicts={conflict} unexpected={unexpected} expectedOk=1 actualOk={ok}; sampleErr={errors.FirstOrDefault() ?? "-"}");
    }

    private static async Task<ScenarioResult> RunResultsStormAsync(HttpClient http, Options opts)
    {
        var admin = await LoginAsync(http, opts.AdminEmail, opts.AdminPassword);
        var latencies = new ConcurrentQueue<long>();
        var errors = new ConcurrentQueue<string>();
        var ok = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, opts.ResultsParallel),
            new ParallelOptions { MaxDegreeOfParallelism = opts.ResultsParallel },
            async (_, ct) =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var client = CreateAuthed(http, admin);
                    var res = await client.GetAsync("api/admin/results", ct);
                    if (res.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref ok);
                    }
                    else
                    {
                        errors.Enqueue($"results {(int)res.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex.Message);
                }
                finally
                {
                    latencies.Enqueue(sw.ElapsedMilliseconds);
                }
            });

        return ScenarioResult.From(
            "results-storm",
            ok,
            errors.Count,
            latencies,
            $"parallel={opts.ResultsParallel}");
    }

    private static async Task<ScenarioResult> RunPublishStormAsync(HttpClient http, Options opts)
    {
        var teacherToken = await LoginAsync(http, opts.TeacherEmail, opts.TeacherPassword);
        using var teacher = CreateAuthed(http, teacherToken);
        var banks = await teacher.GetFromJsonAsync<List<BankDto>>("api/banks", Json)
            ?? [];
        var bankId = banks.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("No banks for publish storm");

        var latencies = new ConcurrentQueue<long>();
        var errors = new ConcurrentQueue<string>();
        var ok = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, opts.PublishParallel),
            new ParallelOptions { MaxDegreeOfParallelism = opts.PublishParallel },
            async (i, ct) =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var client = CreateAuthed(http, teacherToken);
                    var create = await client.PostAsJsonAsync(
                        "api/exams",
                        new
                        {
                            name = $"Load Exam {Guid.NewGuid():N}"[..24],
                            description = "load",
                            bankId,
                            questionCount = opts.Questions,
                            timeMinutes = 5,
                            allowedAttempts = 1,
                            randomize = true,
                            startsAt = (DateTime?)null,
                            endsAt = (DateTime?)null
                        },
                        ct);
                    if (!create.IsSuccessStatusCode)
                    {
                        errors.Enqueue($"create {(int)create.StatusCode}");
                        return;
                    }

                    var exam = await create.Content.ReadFromJsonAsync<ExamDto>(Json, ct);
                    var pub = await client.PostAsync(
                        $"api/exams/{exam!.Id}/publish?published=true",
                        null,
                        ct);
                    if (pub.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref ok);
                    }
                    else
                    {
                        errors.Enqueue($"publish {(int)pub.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex.Message);
                }
                finally
                {
                    latencies.Enqueue(sw.ElapsedMilliseconds);
                }
            });

        return ScenarioResult.From(
            "publish-storm",
            ok,
            errors.Count,
            latencies,
            $"teachers=1 parallel={opts.PublishParallel}");
    }

    private static async Task<int> ResolveBankIdAsync(HttpClient http, Options opts)
    {
        if (opts.BankId is > 0)
        {
            return opts.BankId.Value;
        }

        var token = await LoginAsync(http, opts.TeacherEmail, opts.TeacherPassword);
        using var client = CreateAuthed(http, token);
        var banks = await client.GetFromJsonAsync<List<BankDto>>("api/banks", Json) ?? [];
        return banks.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("No banks available");
    }

    private static async Task EnsureSchoolCapacityAsync(HttpClient http, Options opts)
    {
        var adminToken = await LoginAsync(http, opts.AdminEmail, opts.AdminPassword);
        using var admin = CreateAuthed(http, adminToken);
        var schools = await admin.GetFromJsonAsync<List<SchoolRowDto>>("api/admin/schools", Json)
            ?? [];
        var school = schools.FirstOrDefault(s =>
            s.Email.Equals(opts.SchoolEmail, StringComparison.OrdinalIgnoreCase))
            ?? schools.FirstOrDefault()
            ?? throw new InvalidOperationException("No school for seat override");

        var seats = await admin.PutAsJsonAsync(
            $"api/admin/schools/{school.UserId}/seats",
            new { teachersMax = 50, studentsMax = Math.Max(500, opts.Students + 50) });
        if (!seats.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Seat override failed {(int)seats.StatusCode}: {await seats.Content.ReadAsStringAsync()}");
        }
    }

    private static async Task<List<StudentAuth>> EnsureStudentsAsync(HttpClient http, Options opts)
    {
        // Reuse demo student for small waves when --reuse-demo; else mint school members.
        if (opts.ReuseDemo || opts.Students <= 1)
        {
            var token = await LoginAsync(http, opts.StudentEmail, opts.StudentPassword);
            return [new StudentAuth(opts.StudentEmail, token)];
        }

        await EnsureSchoolCapacityAsync(http, opts);

        var schoolToken = await LoginAsync(http, opts.SchoolEmail, opts.SchoolPassword);
        using var school = CreateAuthed(http, schoolToken);

        var list = new ConcurrentQueue<StudentAuth>();
        await Parallel.ForEachAsync(
            Enumerable.Range(1, opts.Students),
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (i, ct) =>
            {
                var email = $"load.student.{opts.RunId}.{i}@cale.local";
                var password = "LoadTest123!";
                var create = await school.PostAsJsonAsync(
                    "api/school/members",
                    new
                    {
                        name = $"Load Student {i}",
                        email,
                        password,
                        role = "Student"
                    },
                    ct);

                // 409 / email_taken â†’ already created in prior run
                if (!create.IsSuccessStatusCode)
                {
                    var body = await create.Content.ReadAsStringAsync(ct);
                    if (!body.Contains("email_taken", StringComparison.OrdinalIgnoreCase)
                        && !body.Contains("taken", StringComparison.OrdinalIgnoreCase)
                        && (int)create.StatusCode is not (400 or 409))
                    {
                        throw new InvalidOperationException(
                            $"Create member failed {(int)create.StatusCode}: {body}");
                    }
                }

                var token = await LoginAsync(http, email, password);
                list.Enqueue(new StudentAuth(email, token));
            });

        return list.ToList();
    }

    private static async Task<string> LoginAsync(HttpClient http, string email, string password)
    {
        var res = await http.PostAsJsonAsync(
            "api/auth/login",
            new { email, password });
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Login failed for {email}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }

        var auth = await res.Content.ReadFromJsonAsync<AuthDto>(Json)
            ?? throw new InvalidOperationException("empty auth");
        return auth.Token;
    }

    private static HttpClient CreateAuthed(HttpClient prototype, string token)
    {
        var client = new HttpClient
        {
            BaseAddress = prototype.BaseAddress,
            Timeout = prototype.Timeout
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed record Options(
        string BaseUrl,
        int Students,
        int Questions,
        int FinishParallel,
        int ResultsParallel,
        int PublishParallel,
        bool ReuseDemo,
        int? BankId,
        string OutFile,
        string RunId,
        IReadOnlyList<string> Scenarios,
        string AdminEmail,
        string AdminPassword,
        string TeacherEmail,
        string TeacherPassword,
        string StudentEmail,
        string StudentPassword,
        string SchoolEmail,
        string SchoolPassword,
        string? Name = null)
    {
        public static Options Parse(string[] args)
        {
            string Get(string key, string fallback)
            {
                for (var i = 0; i < args.Length - 1; i++)
                {
                    if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return args[i + 1];
                    }
                }

                return fallback;
            }

            bool Flag(string key) =>
                args.Any(a => a.Equals(key, StringComparison.OrdinalIgnoreCase));

            var scenarios = Get("--scenario", "all")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new Options(
                Get("--base", "http://127.0.0.1:5000"),
                int.Parse(Get("--students", "50")),
                int.Parse(Get("--questions", "5")),
                int.Parse(Get("--finish-parallel", "40")),
                int.Parse(Get("--results-parallel", "40")),
                int.Parse(Get("--publish-parallel", "10")),
                Flag("--reuse-demo"),
                int.TryParse(Get("--bank", ""), out var bank) ? bank : null,
                Get("--out", Path.Combine("TestResults", "load-report.json")),
                Get("--run-id", DateTime.UtcNow.ToString("yyyyMMddHHmmss")),
                scenarios,
                Get("--admin-email", "admin@cale.local"),
                Get("--admin-password", "Admin123!"),
                Get("--teacher-email", "profesor@cale.local"),
                Get("--teacher-password", "Profesor123!"),
                Get("--student-email", "estudiante@cale.local"),
                Get("--student-password", "Estudiante123!"),
                Get("--school-email", "escuela@cale.local"),
                Get("--school-password", "Escuela123!"));
        }
    }

    private sealed record ScenarioResult(
        string Name,
        int Ok,
        int Fail,
        long P50Ms,
        long P95Ms,
        long MaxMs,
        string Notes)
    {
        public static ScenarioResult From(
            string name,
            int ok,
            int fail,
            IEnumerable<long> latencies,
            string notes)
        {
            var sorted = latencies.OrderBy(x => x).ToArray();
            long Pct(double p) =>
                sorted.Length == 0
                    ? 0
                    : sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(p * sorted.Length) - 1)];

            return new ScenarioResult(
                name,
                ok,
                fail,
                Pct(0.50),
                Pct(0.95),
                sorted.LastOrDefault(),
                notes);
        }
    }

    private sealed record StudentAuth(string Email, string Token);
    private sealed record AuthDto(string Token);
    private sealed record BankDto(int Id, string Name);
    private sealed record ExamDto(int Id);
    private sealed record SchoolRowDto(int UserId, string Email);
    private sealed record StartDto(
        int AttemptId,
        DateTime StartedAt,
        DateTime? ExpiresAt,
        int TimeMinutes,
        List<QuestionDto> Questions);
    private sealed record QuestionDto(int Id, List<OptionDto> Options);
    private sealed record OptionDto(int Id);
}

