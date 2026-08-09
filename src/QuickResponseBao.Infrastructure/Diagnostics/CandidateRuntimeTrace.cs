namespace QuickResponseBao.Infrastructure.Diagnostics;

public sealed record CandidateRuntimeTrace(long SequenceId, string Stage, string Details)
{
    public override string ToString() => $"SearchSequence #{SequenceId} | {Stage} | {Details}";
}
