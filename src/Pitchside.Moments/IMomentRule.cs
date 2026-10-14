using Pitchside.Contracts;

namespace Pitchside.Moments;

/// <summary>One moment type: a rule that decides, from code alone, whether an event triggers it.</summary>
public interface IMomentRule
{
    string Type { get; }

    int Priority { get; }

    Moment? Evaluate(RuleContext context);
}
