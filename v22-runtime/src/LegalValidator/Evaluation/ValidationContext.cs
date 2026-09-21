using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Evaluation;

public interface IWorkingDayCalendar
{
    DateOnly NextWorkingDay(DateOnly date);
}

public sealed class ValidationContext
{
    public DateOnly EvaluationDate { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public Dictionary<string,object?> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,object?> ObservedValues { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,List<EvidenceRecord>> Evidence { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AvailableCapabilities { get; } = new(StringComparer.Ordinal);
    public HashSet<string> UncertainTargets { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string,bool> ReferenceResults { get; } = new(StringComparer.OrdinalIgnoreCase);
    public PatchPolicy DocumentPatchPolicy { get; set; } = PatchPolicy.NORMAL;
    public IWorkingDayCalendar? WorkingDayCalendar { get; set; }

    public ValidationContext SetField(string key, object? value){ Fields[key]=value; return this; }
    public ValidationContext Observe(string key, object? value, EvidenceRecord? evidence=null)
    {
        ObservedValues[key]=value;
        if(evidence is not null){if(!Evidence.TryGetValue(key,out var list))Evidence[key]=list=[];list.Add(evidence);}
        return this;
    }
    public ValidationContext AddCapability(params string[] ids){foreach(var id in ids)AvailableCapabilities.Add(id);return this;}
    public ValidationContext MarkUncertain(string targetKey){UncertainTargets.Add(targetKey);return this;}
}
