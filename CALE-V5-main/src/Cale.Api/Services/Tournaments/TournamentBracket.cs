namespace Cale.Api.Services.Tournaments;

/// <summary>Pure bracket helpers for single-elimination (power-of-two size with BYEs).</summary>
public static class TournamentBracket
{
    public static int NextPowerOfTwo(int participantCount)
    {
        if (participantCount < 2)
        {
            return 2;
        }

        var size = 1;
        while (size < participantCount)
        {
            size <<= 1;
        }

        return size;
    }

    public static int RoundCount(int bracketSize)
    {
        var n = 0;
        for (var s = bracketSize; s > 1; s >>= 1)
        {
            n++;
        }

        return n;
    }

    public static string RoundName(int roundNumber, int totalRounds) =>
        (totalRounds - roundNumber) switch
        {
            0 => "Gran final",
            1 => "Semifinal",
            2 => "Cuartos",
            3 => "Octavos",
            _ => $"Ronda {roundNumber}"
        };

    /// <summary>
    /// Shuffles participants and places them into bracketSize slots; remaining slots are BYE.
    /// Returns slot → userId (null = BYE).
    /// </summary>
    public static int?[] SeedSlots(IReadOnlyList<int> participantUserIds, int bracketSize, Random rng)
    {
        if (bracketSize < 2 || (bracketSize & (bracketSize - 1)) != 0)
        {
            throw new ArgumentException("Bracket size must be a power of two.", nameof(bracketSize));
        }

        var shuffled = participantUserIds.OrderBy(_ => rng.Next()).ToList();
        var slots = new int?[bracketSize];
        for (var i = 0; i < shuffled.Count && i < bracketSize; i++)
        {
            slots[i] = shuffled[i];
        }

        return slots;
    }

    /// <summary>
    /// First-round pairings: slot 0 vs 1, 2 vs 3, …
    /// </summary>
    public static IReadOnlyList<(int? Player1, int? Player2, bool P1Bye, bool P2Bye)> FirstRoundPairs(
        int?[] slots)
    {
        var pairs = new List<(int? Player1, int? Player2, bool P1Bye, bool P2Bye)>(slots.Length / 2);
        for (var i = 0; i < slots.Length; i += 2)
        {
            var p1 = slots[i];
            var p2 = slots[i + 1];
            pairs.Add((p1, p2, p1 is null, p2 is null));
        }

        return pairs;
    }
}
