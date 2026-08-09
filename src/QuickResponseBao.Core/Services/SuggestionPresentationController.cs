using QuickResponseBao.Core.Models;

namespace QuickResponseBao.Core.Services;

public readonly record struct SuggestionPresentationTicket(long Generation, long SequenceId);

public sealed class SuggestionPresentationController
{
    private readonly object _gate = new();
    private long _generation;
    private long _currentSequence;
    private string _currentQuery = string.Empty;
    private nint _currentTarget;

    public SuggestionPresentationTicket Register(CandidateSearchContext context)
    {
        lock (_gate)
        {
            if (context.SequenceId < _currentSequence)
                return new SuggestionPresentationTicket(_generation - 1, context.SequenceId);
            _currentSequence = context.SequenceId; _currentQuery = context.NormalizedQuery; _currentTarget = context.TargetWindowHandle;
            return new SuggestionPresentationTicket(++_generation, context.SequenceId);
        }
    }

    public SuggestionPresentationTicket Cancel()
    {
        lock (_gate)
        {
            _currentQuery = string.Empty; _currentTarget = 0;
            return new SuggestionPresentationTicket(++_generation, _currentSequence);
        }
    }

    public SuggestionPresentationTicket RegisterManual(CandidateSearchContext context)
    {
        lock (_gate)
        {
            _currentQuery = context.NormalizedQuery; _currentTarget = context.TargetWindowHandle;
            return new SuggestionPresentationTicket(++_generation, _currentSequence);
        }
    }

    public bool IsCurrent(SuggestionPresentationTicket ticket, CandidateSearchContext? context = null)
    {
        lock (_gate)
        {
            return ticket.Generation == _generation && ticket.SequenceId == _currentSequence
                && (context is null || (context.NormalizedQuery == _currentQuery && context.TargetWindowHandle == _currentTarget));
        }
    }
}
