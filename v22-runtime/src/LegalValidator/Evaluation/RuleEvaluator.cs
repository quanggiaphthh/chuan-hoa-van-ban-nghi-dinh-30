using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Evaluation;

public sealed class RuleEvaluator
{
    public ValidationResult Evaluate(RuleDefinition rule, ValidationContext context)
    {
        if(!string.Equals(rule.Maturity,"verified",StringComparison.OrdinalIgnoreCase))
            return Result(rule,ValidationStatus.NOT_EVALUATED,context,"Rule maturity is not verified.","not evaluated","not evaluated");

        if(rule.EffectiveFrom is not null && context.EvaluationDate < rule.EffectiveFrom.Value || rule.EffectiveTo is not null && context.EvaluationDate > rule.EffectiveTo.Value)
            return Result(rule,ValidationStatus.NOT_APPLICABLE,context,"Rule is outside its effective temporal interval.",Describe(rule.Constraint),"n/a",applicability:"temporal:not_applicable");

        var app=EvaluateApplicability(rule.Applicability,context);
        if(app.Status==ValidationStatus.NOT_APPLICABLE)
            return Result(rule,ValidationStatus.NOT_APPLICABLE,context,app.Reason,Describe(rule.Constraint),"n/a",applicability:"conditions:false");
        if(app.Status==ValidationStatus.NOT_EVALUATED)
            return Result(rule,ValidationStatus.NOT_EVALUATED,context,app.Reason,Describe(rule.Constraint),"unknown",applicability:"conditions:unknown");

        var missing=rule.RequiresCapabilities.Where(x=>!context.AvailableCapabilities.Contains(x)).ToArray();
        if(missing.Length>0)
            return Result(rule,ValidationStatus.NOT_EVALUATED,context,$"Missing runtime capability: {string.Join(", ",missing)}",Describe(rule.Constraint),"not evaluated",missing:missing);

        if(!HasRequiredAuthoritativeEvidence(rule,context,out var authReason))
            return Result(rule,ValidationStatus.NOT_EVALUATED,context,authReason,Describe(rule.Constraint),"not evaluated");

        if(context.UncertainTargets.Contains(rule.Target.Key))
            return Result(rule,ValidationStatus.NEEDS_REVIEW,context,"Semantic target is ambiguous or low-confidence.",Describe(rule.Constraint),Observed(context,rule.Target.Key));

        var type=RuleCatalog.ScalarString(RuleCatalog.Get(rule.Constraint,"type"));
        if(string.Equals(type,"semantic_review",StringComparison.OrdinalIgnoreCase))
            return Result(rule,ValidationStatus.NEEDS_REVIEW,context,"Rule requires human/semantic review by design.",Describe(rule.Constraint),Observed(context,rule.Target.Key));
        if(string.Equals(type,"reference",StringComparison.OrdinalIgnoreCase))
        {
            var relation=RuleCatalog.ScalarString(RuleCatalog.Get(rule.Constraint,"source_relation"));
            if(!context.ReferenceResults.TryGetValue(relation,out var rr))
                return Result(rule,ValidationStatus.NOT_EVALUATED,context,$"External legal overlay/reference is unresolved: {relation}.",Describe(rule.Constraint),"unresolved");
            return Result(rule,rr?ValidationStatus.PASS:ValidationStatus.FAIL,context,$"External legal overlay/reference evaluated: {relation}.",Describe(rule.Constraint),rr.ToString());
        }

        if(!context.ObservedValues.TryGetValue(rule.Target.Key,out var observed))
            return Result(rule,ValidationStatus.NOT_EVALUATED,context,$"No authoritative observation for target {rule.Target.Key}.",Describe(rule.Constraint),"missing observation");

        var ce=EvaluateConstraint(rule.Constraint,observed,context);
        return Result(rule,ce.Status,context,ce.Reason,ce.Expected,ce.Observed);
    }

    private static (ValidationStatus Status,string Reason) EvaluateApplicability(IReadOnlyDictionary<string,object?>? a,ValidationContext c)
    {
        if(a is null || a.Count==0)return (ValidationStatus.PASS,"No applicability restrictions.");
        foreach(var raw in RuleCatalog.AsList(RuleCatalog.Get(a,"all")))
        {
            var m=RuleCatalog.AsMap(raw);var field=RuleCatalog.ScalarString(RuleCatalog.Get(m,"field"));var op=RuleCatalog.ScalarString(RuleCatalog.Get(m,"operator"));var expected=RuleCatalog.Get(m,"value");
            if(!c.Fields.TryGetValue(field,out var actual))return (ValidationStatus.NOT_EVALUATED,$"Applicability field unavailable: {field}.");
            if(!Condition(op,actual,expected))return (ValidationStatus.NOT_APPLICABLE,$"Applicability condition is false: {field} {op} {Fmt(expected)}.");
        }
        return (ValidationStatus.PASS,"Applicability conditions satisfied.");
    }

    private static bool Condition(string op,object? actual,object? expected)=>op switch
    {
        "equals"=>Eq(actual,expected),
        "not_equals"=>!Eq(actual,expected),
        "in"=>RuleCatalog.AsList(expected).Any(x=>Eq(actual,x)),
        "greater_than_or_equal"=>Num(actual,out var a)&&Num(expected,out var e)&&a>=e,
        _=>false
    };

    private static bool HasRequiredAuthoritativeEvidence(RuleDefinition r,ValidationContext c,out string reason)
    {
        string? kind=null;
        if(r.RequiresCapabilities.Contains("workflow_event_log"))kind="workflow_event_log";
        else if(r.RequiresCapabilities.Contains("records_event_log"))kind="records_event_log";
        else if(r.RequiresCapabilities.Contains("system_capability_audit"))kind="system_capability_audit";
        else if(r.RequiresCapabilities.Any(x=>x is "digital_signature_verification" or "signature_verification"))kind="digital_signature_verification";
        if(kind is null){reason="Evidence policy satisfied.";return true;}
        if(c.Evidence.TryGetValue(r.Target.Key,out var list)&&list.Any(e=>e.Authority==EvidenceAuthority.Authoritative&&string.Equals(e.Kind,kind,StringComparison.OrdinalIgnoreCase)))
        {reason="Authoritative evidence present.";return true;}
        reason=$"Authoritative {kind} evidence is required; DOCX metadata or derived inference cannot substitute.";return false;
    }

    private static ConstraintEvaluation EvaluateConstraint(IReadOnlyDictionary<string,object?> q,object? o,ValidationContext c)
    {
        var type=RuleCatalog.ScalarString(RuleCatalog.Get(q,"type"));
        try
        {
            return type switch
            {
                "equals"=>Binary(Eq(o,RuleCatalog.Get(q,"value")),q,o),
                "one_of"=>Binary(RuleCatalog.AsList(RuleCatalog.Get(q,"values")).Any(x=>Eq(o,x)),q,o),
                "numeric_range"=>NumericRange(q,o),
                "regex"=>RegexEval(q,o),
                "required"=>Binary(o is not null && (!(o is bool b)||b),q,o),
                "required_fields"=>RequiredFields(q,o),
                "required_effects"=>RequiredEffects(q,o),
                "ordered"=>Ordered(q,o),
                "all_of"=>AllOf(q,o),
                "same_calendar_day"=>SameDay(q,o,c),
                "deadline"=>Deadline(q,o,c),
                "dimensions"=>Dimensions(q,o),
                "capability"=>Capability(q,o),
                "catalog_reference"=>CatalogReference(q,o),
                _=>new(ValidationStatus.NOT_EVALUATED,$"Constraint evaluator not implemented: {type}.",Describe(q),Fmt(o))
            };
        }
        catch(Exception ex){return new(ValidationStatus.NOT_EVALUATED,$"Constraint evaluation error: {ex.Message}",Describe(q),Fmt(o));}
    }

    private static ConstraintEvaluation Binary(bool ok,IReadOnlyDictionary<string,object?> q,object? o)=>new(ok?ValidationStatus.PASS:ValidationStatus.FAIL,ok?"Constraint satisfied.":"Observed value violates constraint.",Describe(q),Fmt(o));
    private static ConstraintEvaluation NumericRange(IReadOnlyDictionary<string,object?> q,object? o){if(!Num(o,out var v))return new(ValidationStatus.NOT_EVALUATED,"Observed value is not numeric.",Describe(q),Fmt(o));var min=Num(RuleCatalog.Get(q,"min"),out var mi)?mi:double.NegativeInfinity;var max=Num(RuleCatalog.Get(q,"max"),out var ma)?ma:double.PositiveInfinity;return Binary(v>=min&&v<=max,q,o);}
    private static ConstraintEvaluation RegexEval(IReadOnlyDictionary<string,object?> q,object? o){var p=RuleCatalog.ScalarString(RuleCatalog.Get(q,"pattern"));return Binary(o is string s&&Regex.IsMatch(s,p,RegexOptions.CultureInvariant),q,o);}
    private static ConstraintEvaluation RequiredFields(IReadOnlyDictionary<string,object?> q,object? o){var m=ToMap(o);if(m is null)return new(ValidationStatus.NOT_EVALUATED,"Observed target is not a field map.",Describe(q),Fmt(o));var missing=RuleCatalog.AsList(RuleCatalog.Get(q,"fields")).Select(RuleCatalog.ScalarString).Where(f=>!m.TryGetValue(f,out var v)||v is null||v is string s&&string.IsNullOrWhiteSpace(s)).ToArray();return new(missing.Length==0?ValidationStatus.PASS:ValidationStatus.FAIL,missing.Length==0?"Required fields present.":$"Missing required fields: {string.Join(", ",missing)}",Describe(q),Fmt(o));}
    private static ConstraintEvaluation RequiredEffects(IReadOnlyDictionary<string,object?> q,object? o){var actual=ToStrings(o);if(actual is null)return new(ValidationStatus.NOT_EVALUATED,"Observed target is not a collection.",Describe(q),Fmt(o));var req=RuleCatalog.AsList(RuleCatalog.Get(q,"effects")).Select(RuleCatalog.ScalarString).ToArray();var missing=req.Where(x=>!actual.Contains(x,StringComparer.OrdinalIgnoreCase)).ToArray();return new(missing.Length==0?ValidationStatus.PASS:ValidationStatus.FAIL,missing.Length==0?"Required set is present.":$"Missing required values: {string.Join(", ",missing)}",Describe(q),Fmt(o));}
    private static ConstraintEvaluation Ordered(IReadOnlyDictionary<string,object?> q,object? o){var actual=ToStrings(o);if(actual is null)return new(ValidationStatus.NOT_EVALUATED,"Observed target is not an ordered collection.",Describe(q),Fmt(o));var req=RuleCatalog.AsList(RuleCatalog.Get(q,"sequence")).Select(RuleCatalog.ScalarString).ToArray();var pos=-1;foreach(var x in req){var next=actual.FindIndex(pos+1,y=>string.Equals(x,y,StringComparison.OrdinalIgnoreCase));if(next<0)return new(ValidationStatus.FAIL,$"Required ordered event missing/out of order: {x}.",Describe(q),Fmt(o));pos=next;}return new(ValidationStatus.PASS,"Required event order satisfied.",Describe(q),Fmt(o));}
    private static ConstraintEvaluation AllOf(IReadOnlyDictionary<string,object?> q,object? o){var m=ToMap(o);if(m is null)return new(ValidationStatus.NOT_EVALUATED,"Observed target is not a property map.",Describe(q),Fmt(o));foreach(var raw in RuleCatalog.AsList(RuleCatalog.Get(q,"requirements"))){var r=RuleCatalog.AsMap(raw);var p=RuleCatalog.ScalarString(RuleCatalog.Get(r,"property"));if(!m.TryGetValue(p,out var a))return new(ValidationStatus.FAIL,$"Required property missing: {p}.",Describe(q),Fmt(o));if(r.ContainsKey("equals")&&!Eq(a,RuleCatalog.Get(r,"equals")))return new(ValidationStatus.FAIL,$"Property violates required value: {p}.",Describe(q),Fmt(o));}return new(ValidationStatus.PASS,"All required properties satisfied.",Describe(q),Fmt(o));}
    private static ConstraintEvaluation SameDay(IReadOnlyDictionary<string,object?> q,object? o,ValidationContext c){var rel=RuleCatalog.ScalarString(RuleCatalog.Get(q,"relative_to"));if(!TryDateTime(o,out var a)||!c.Fields.TryGetValue(rel,out var rv)||!TryDateTime(rv,out var b))return new(ValidationStatus.NOT_EVALUATED,"Both timestamps are required for same-calendar-day evaluation.",Describe(q),Fmt(o));return Binary(DateOnly.FromDateTime(a.DateTime)==DateOnly.FromDateTime(b.DateTime),q,o);}
    private static ConstraintEvaluation Deadline(IReadOnlyDictionary<string,object?> q,object? o,ValidationContext c){var rel=RuleCatalog.ScalarString(RuleCatalog.Get(q,"relative_to"));var policy=RuleCatalog.ScalarString(RuleCatalog.Get(q,"policy"));if(!TryDateTime(o,out var actual)||!c.Fields.TryGetValue(rel,out var rv)||!TryDateTime(rv,out var basis))return new(ValidationStatus.NOT_EVALUATED,"Deadline timestamps are incomplete.",Describe(q),Fmt(o));if(policy=="same_day_or_next_workday"){if(c.WorkingDayCalendar is null)return new(ValidationStatus.NOT_EVALUATED,"Working-day calendar capability has no runtime calendar implementation.",Describe(q),Fmt(o));var d=DateOnly.FromDateTime(basis.DateTime);var latest=c.WorkingDayCalendar.NextWorkingDay(d);var ad=DateOnly.FromDateTime(actual.DateTime);return Binary(ad==d||ad==latest,q,o);}return new(ValidationStatus.NOT_EVALUATED,$"Deadline policy is not implemented: {policy}.",Describe(q),Fmt(o));}
    private static ConstraintEvaluation Dimensions(IReadOnlyDictionary<string,object?> q,object? o){var m=ToMap(o);if(m is null)return new(ValidationStatus.NOT_EVALUATED,"Dimension observation must be a map.",Describe(q),Fmt(o));if(!m.TryGetValue("width",out var w)||!m.TryGetValue("height",out var h)||!Num(w,out var aw)||!Num(h,out var ah)||!Num(RuleCatalog.Get(q,"width"),out var ew)||!Num(RuleCatalog.Get(q,"height"),out var eh))return new(ValidationStatus.NOT_EVALUATED,"Dimension values are incomplete.",Describe(q),Fmt(o));return Binary(Math.Abs(aw-ew)<1e-9&&Math.Abs(ah-eh)<1e-9,q,o);}

    private static ConstraintEvaluation Capability(IReadOnlyDictionary<string,object?> q,object? o){var copy=q.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);copy["effects"]=RuleCatalog.Get(q,"requires");return RequiredEffects(copy,o);}
    private static ConstraintEvaluation CatalogReference(IReadOnlyDictionary<string,object?> q,object? o){var catalog=RuleCatalog.ScalarString(RuleCatalog.Get(q,"catalog"));var values=ToStrings(o);var ok=values is not null?values.Contains(catalog,StringComparer.OrdinalIgnoreCase):string.Equals(RuleCatalog.ScalarString(o),catalog,StringComparison.OrdinalIgnoreCase);return Binary(ok,q,o);}

    private ValidationResult Result(RuleDefinition r,ValidationStatus s,ValidationContext c,string reason,string expected,string observed,IReadOnlyList<string>? missing=null,string applicability="applicable")
    {
        var ev=c.Evidence.TryGetValue(r.Target.Key,out var e)?e:[];var conf=ev.Where(x=>x.Confidence is not null).Select(x=>x.Confidence!.Value).DefaultIfEmpty().Min();double? confidence=ev.Any(x=>x.Confidence is not null)?conf:null;
        return new(r.Id,s,r.Severity,r.Source,r.Target,expected,observed,ev,r.RequiresCapabilities,missing??[],applicability,confidence,Patch(r,s,c.DocumentPatchPolicy),reason);
    }
    private static PatchEligibility Patch(RuleDefinition r,ValidationStatus s,PatchPolicy p){if(s==ValidationStatus.PASS||s==ValidationStatus.NOT_APPLICABLE)return PatchEligibility.NOT_NEEDED;if(s is ValidationStatus.NOT_EVALUATED or ValidationStatus.NEEDS_REVIEW)return PatchEligibility.UNKNOWN;if(p is PatchPolicy.AUDIT_ONLY or PatchPolicy.PROHIBITED)return PatchEligibility.PROHIBITED;return r.AutofixPolicy.ToUpperInvariant() switch{"SAFE"=>PatchEligibility.ELIGIBLE,"GUARDED"=>PatchEligibility.ELIGIBLE,"SUGGEST_ONLY"=>PatchEligibility.SUGGEST_ONLY,"PROHIBITED"=>PatchEligibility.PROHIBITED,_=>PatchEligibility.UNKNOWN};}
    private static string Observed(ValidationContext c,string key)=>c.ObservedValues.TryGetValue(key,out var v)?Fmt(v):"missing observation";
    private static bool Eq(object? a,object? b){if(a is null||b is null)return a is null&&b is null;if(Num(a,out var an)&&Num(b,out var bn))return Math.Abs(an-bn)<1e-9;if(a is bool ab&&TryBool(b,out var bb))return ab==bb;if(b is bool bb2&&TryBool(a,out var ab2))return ab2==bb2;return string.Equals(RuleCatalog.ScalarString(a),RuleCatalog.ScalarString(b),StringComparison.Ordinal);}
    private static bool Num(object? x,out double v)=>double.TryParse(RuleCatalog.ScalarString(x),NumberStyles.Float,CultureInfo.InvariantCulture,out v);
    private static bool TryBool(object? x,out bool b)=>bool.TryParse(RuleCatalog.ScalarString(x),out b);
    private static bool TryDateTime(object? x,out DateTimeOffset d)=>DateTimeOffset.TryParse(RuleCatalog.ScalarString(x),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out d);
    private static Dictionary<string,object?>? ToMap(object? o)=>o as Dictionary<string,object?> ?? (o as IReadOnlyDictionary<string,object?>)?.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
    private static List<string>? ToStrings(object? o)=>o is string?null:o is IEnumerable e?e.Cast<object?>().Select(RuleCatalog.ScalarString).ToList():null;
    private static string Describe(IReadOnlyDictionary<string,object?> q)=>string.Join("; ",q.Select(x=>$"{x.Key}={Fmt(x.Value)}"));
    private static string Fmt(object? o)=>o switch{null=>"null",string s=>s,IDictionary<string,object?> m=>"{"+string.Join(", ",m.Select(x=>$"{x.Key}:{Fmt(x.Value)}"))+"}",IEnumerable e when o is not string=>"["+string.Join(", ",e.Cast<object?>().Select(Fmt))+"]",_=>RuleCatalog.ScalarString(o)};
}
