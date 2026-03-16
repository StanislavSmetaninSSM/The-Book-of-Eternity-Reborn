using System.Security.Cryptography;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Core;

/// <summary>
/// Session and turn state tracker. Provides secure dice generation.
/// Turn processing is handled by GameEngine which coordinates with the GM daemon.
/// </summary>
public class GameLoop
{
    private int _turnNumber;
    private string _sessionId;

    public int TurnNumber => _turnNumber;
    public string SessionId => _sessionId;

    public GameLoop()
    {
        _sessionId = Guid.NewGuid().ToString();
        _turnNumber = 0;
    }

    public void SetSession(string sessionId, int turnNumber)
    {
        _sessionId = sessionId;
        _turnNumber = turnNumber;
    }

    public void IncrementTurn() => _turnNumber++;

    /// <summary>
    /// Generate cryptographically secure random d20 dice rolls.
    /// Prevents LLM bias in dice outcomes.
    /// </summary>
    public static int[] GenerateSecureRandomDice(int count = 20)
    {
        if (count <= 0)
            return Array.Empty<int>();

        var dice = new int[count];
        var bytes = new byte[4];

        for (int i = 0; i < count; i++)
        {
            RandomNumberGenerator.Fill(bytes);
            var value = BitConverter.ToUInt32(bytes, 0);
            dice[i] = (int)(value % 20) + 1;
        }

        return dice;
    }

    /// <summary>
    /// Compute the client-side gacha base result from a separate hidden 4d20 roll.
    /// This result is intentionally kept separate from the GM-facing dice pool.
    /// Rarity thresholds: 4–48 Common, 49–67 Uncommon, 68–75 Rare, 76–79 Epic, 80 Legendary.
    /// The GM may only add modifiers on top (guardian reputation, mode bonuses), never reduce.
    /// </summary>
    public static GachaResult ComputeGachaBase(int[] dice)
    {
        if (dice.Length < 4)
            return new GachaResult
            {
                DiceUsed = Array.Empty<int>(),
                BaseScore = 0,
                BaseRarity = "Common",
                Formula = "client-computed gacha base (insufficient internal rolls)"
            };

        var used = new[] { dice[0], dice[1], dice[2], dice[3] };
        int score = used[0] + used[1] + used[2] + used[3];

        string rarity = score switch
        {
            80 => "Legendary",
            >= 76 => "Epic",
            >= 68 => "Rare",
            >= 49 => "Uncommon",
            _ => "Common"
        };

        return new GachaResult
        {
            DiceUsed = Array.Empty<int>(),
            BaseScore = score,
            BaseRarity = rarity,
            Formula = "client-computed gacha base (range 4-80)"
        };
    }
}
