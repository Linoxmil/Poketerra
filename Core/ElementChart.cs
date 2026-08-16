using Wildlore.ID;

namespace Wildlore.Core;

/// <summary>
///     Which element lands hard against which.
///     <para>
///     Only the winning side of each pairing is written down; the losing side is derived by
///     inverting it, so a matchup can never be defined twice and contradict itself. Every
///     element beats exactly two others and loses to exactly two, which keeps the whole
///     thing memorable without a lookup table on screen.
///     </para>
/// </summary>
public static class ElementChart
{
    public const float Strong = 2f;
    public const float Weak = 0.5f;
    public const float Even = 1f;

    private static readonly Dictionary<ElementType, ElementType[]> StrongAgainst = new()
    {
        [ElementType.Ember] = [ElementType.Verdant, ElementType.Frost],
        [ElementType.Tide] = [ElementType.Ember, ElementType.Stone],
        [ElementType.Verdant] = [ElementType.Tide, ElementType.Stone],
        [ElementType.Storm] = [ElementType.Tide, ElementType.Gloom],
        [ElementType.Frost] = [ElementType.Verdant, ElementType.Storm],
        [ElementType.Stone] = [ElementType.Ember, ElementType.Storm],
        [ElementType.Gloom] = [ElementType.Neutral, ElementType.Frost],

        // Neutral wins nothing and loses only to Gloom. It is the safe pick, never the
        // strong one.
        [ElementType.Neutral] = []
    };

    public static float Multiplier(ElementType attacker, ElementType defender)
    {
        if (StrongAgainst.TryGetValue(attacker, out var beats) && Array.IndexOf(beats, defender) >= 0)
            return Strong;

        if (StrongAgainst.TryGetValue(defender, out var beatsBack) && Array.IndexOf(beatsBack, attacker) >= 0)
            return Weak;

        return Even;
    }

    /// <summary>
    ///     A single attacking element against a possibly dual-element defender. The two
    ///     defending elements multiply, so a creature can be hit for 4x or shrug a hit off
    ///     at a quarter — dual typing is a real gamble in both directions.
    /// </summary>
    public static float Multiplier(ElementType attacker, IReadOnlyList<ElementType> defenders)
    {
        if (defenders is not { Count: > 0 }) return Even;

        var total = Even;
        foreach (var defender in defenders) total *= Multiplier(attacker, defender);
        return total;
    }

    /// <summary>
    ///     Localization key suffix for the on-screen call-out, or null when the hit was even
    ///     and deserves no text at all.
    /// </summary>
    public static string EffectivenessKey(float multiplier) => multiplier switch
    {
        >= 3.5f => "Devastating",
        >= 1.5f => "Strong",
        <= 0.3f => "Feeble",
        <= 0.6f => "Resisted",
        _ => null
    };
}
