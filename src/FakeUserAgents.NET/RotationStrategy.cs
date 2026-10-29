namespace Dotnet.FakeUserAgents;

/// <summary>
/// Defines strategies for rotating across user agent profiles.
/// </summary>
public enum RotationStrategy
{
    /// <summary>
    /// Sequentially iterates through the user agent pool in fixed round-robin order.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Selects a completely random user agent on each call.
    /// </summary>
    Random,

    /// <summary>
    /// Shuffles the entire candidate pool using Fisher-Yates and exhausts every entry before generating a new shuffle.
    /// Guarantees that every user agent in the pool is used before any repeat occurs.
    /// </summary>
    Shuffled,

    /// <summary>
    /// Selects a random user agent while guaranteeing that the chosen entry is never identical
    /// to the immediately preceding entry (provided the candidate pool has at least 2 entries).
    /// </summary>
    NoImmediateRepeat
}
