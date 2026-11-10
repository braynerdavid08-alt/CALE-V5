namespace Cale.Api.Services.Tournaments;

/// <summary>
/// Single-elimination helpers that maximize concurrent real matches.
/// Odd counts get at most one BYE per round (e.g. 9 → 4 matches + 1 waiting).
/// </summary>
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

    /// <summary>How many rounds until one champion, pairing max each round with ≤1 bye.</summary>
    public static int EstimatedRoundCount(int participantCount)
    {
        if (participantCount < 2)
        {
            return 1;
        }

        var rounds = 0;
        for (var n = participantCount; n > 1; n = (n / 2) + (n % 2))
        {
            rounds++;
        }

        return rounds;
    }

    public static string RoundNameForField(int playersInRound) =>
        playersInRound switch
        {
            <= 2 => "Gran final",
            <= 4 => "Semifinal",
            <= 8 => "Cuartos",
            <= 16 => "Octavos",
            _ => $"Ronda ({playersInRound})"
        };

    /// <summary>Legacy name helper kept for tests / power-of-two UIs.</summary>
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
    /// Shuffle and pair: if odd, one player gets a BYE; everyone else plays.
    /// Never BYE vs BYE. Order: bye match first (slot 0), then real matches.
    /// </summary>
    public static IReadOnlyList<(int? Player1, int? Player2, bool P1Bye, bool P2Bye)> PairRound(
        IReadOnlyList<int> participantUserIds,
        Random rng)
    {
        if (participantUserIds.Count == 0)
        {
            return [];
        }

        if (participantUserIds.Count == 1)
        {
            return [(participantUserIds[0], null, false, true)];
        }

        // Distinct: a corrupt participant list must never create duplicate slots.
        var shuffled = participantUserIds.Distinct().OrderBy(_ => rng.Next()).ToList();
        var pairs = new List<(int? Player1, int? Player2, bool P1Bye, bool P2Bye)>();
        var index = 0;

        if (shuffled.Count % 2 == 1)
        {
            pairs.Add((shuffled[0], null, false, true));
            index = 1;
        }

        while (index < shuffled.Count)
        {
            pairs.Add((shuffled[index], shuffled[index + 1], false, false));
            index += 2;
        }

        return pairs;
    }

    /// <summary>
    /// Power-of-two seeding kept for compatibility tests: BYEs sit in early slots
    /// paired against players (never BYE vs BYE). Prefer <see cref="PairRound"/> for live tournaments.
    /// </summary>
    public static int?[] SeedSlots(IReadOnlyList<int> participantUserIds, int bracketSize, Random rng)
    {
        if (bracketSize < 2 || (bracketSize & (bracketSize - 1)) != 0)
        {
            throw new ArgumentException("Bracket size must be a power of two.", nameof(bracketSize));
        }

        if (participantUserIds.Count > bracketSize)
        {
            throw new ArgumentException("Too many participants for bracket size.", nameof(participantUserIds));
        }

        var shuffled = participantUserIds.OrderBy(_ => rng.Next()).ToList();
        var slots = new int?[bracketSize];
        var byes = bracketSize - shuffled.Count;
        var playerIndex = 0;

        for (var i = 0; i < byes; i++)
        {
            slots[i * 2] = shuffled[playerIndex++];
            slots[i * 2 + 1] = null;
        }

        var nextSlot = byes * 2;
        while (playerIndex < shuffled.Count)
        {
            slots[nextSlot++] = shuffled[playerIndex++];
        }

        return slots;
    }

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
