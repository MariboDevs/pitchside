using Pitchside.Contracts;
using Pitchside.Stats;

namespace Pitchside.Moments;

/// <summary>Pure, deterministic moment detection over the event stream, one event at a time.</summary>
public interface IMomentDetector
{
    DetectorState Start(MatchDocument document);

    (DetectorState Next, IReadOnlyList<Moment> Moments) Apply(DetectorState state, MatchEvent matchEvent, FactSheet sheet, IReadOnlyList<Tag> tags);
}
