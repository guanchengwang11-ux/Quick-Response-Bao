using System.Text.Json.Serialization;

namespace QuickResponseBao.Core.Models;

public enum CandidateConfirmationMethod { Enter, Tab, Mouse }

public sealed record CandidateSearchContext(
    string NormalizedQuery,
    int RawTypedCharacterCount,
    nint TargetWindowHandle,
    uint TargetProcessId,
    string TargetProcessName,
    DateTimeOffset CapturedAt,
    [property: JsonIgnore] string RawTypedText,
    long SequenceId = 0)
{
    public CandidateConfirmationContext Confirm(QuickResponse response, CandidateConfirmationMethod method) => new(
        response, NormalizedQuery, RawTypedCharacterCount, TargetWindowHandle, TargetProcessId,
        TargetProcessName, method, CapturedAt, RawTypedText, SequenceId);

    public override string ToString() =>
        $"queryLength={NormalizedQuery.Length}, rawCount={RawTypedCharacterCount}, target={TargetProcessName}, pid={TargetProcessId}";
}

public sealed record CandidateConfirmationContext(
    QuickResponse SelectedResponse,
    string NormalizedQuery,
    int RawTypedCharacterCount,
    nint TargetWindowHandle,
    uint TargetProcessId,
    string TargetProcessName,
    CandidateConfirmationMethod ConfirmationMethod,
    DateTimeOffset CapturedAt,
    [property: JsonIgnore] string RawTypedText,
    long SequenceId = 0)
{
    public override string ToString() =>
        $"queryLength={NormalizedQuery.Length}, rawCount={RawTypedCharacterCount}, target={TargetProcessName}, pid={TargetProcessId}, method={ConfirmationMethod}";
}
