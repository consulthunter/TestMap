/*
 * consulthunter
 * 2025-03-26
 *
 * The location of a particular piece of code
 * within a file, from the span
 *
 * Location.cs
 */

namespace TestMap.Models.Code;

/// <summary>
///     Location of a piece of code
///     in the tree
/// </summary>
/// <param name="bodyStartPosition">Start position of the piece of code</param>
/// <param name="bodyEndPosition">End position of the piece of code</param>
public class Location(
    int bodyStartPosition,
    int bodyEndPosition)
{
    public int BodyStartPosition { get; set; } = bodyStartPosition;
    public int BodyEndPosition { get; set; } = bodyEndPosition;
}