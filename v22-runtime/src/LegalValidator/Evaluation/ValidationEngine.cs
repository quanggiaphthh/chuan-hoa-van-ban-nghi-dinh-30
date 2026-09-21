using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Evaluation;

public sealed class ValidationEngine
{
    private readonly RuleCatalog _catalog;
    private readonly RuleEvaluator _evaluator = new();
    public ValidationEngine(RuleCatalog catalog)=>_catalog=catalog;

    public ValidationReport Validate(ValidationContext context, IEnumerable<string>? ruleIds=null)
    {
        var filter=ruleIds?.ToHashSet(StringComparer.Ordinal);
        var rules=filter is null?_catalog.Rules:_catalog.Rules.Where(x=>filter.Contains(x.Id)).ToList();
        var results=rules.Select(r=>_evaluator.Evaluate(r,context)).ToList();
        var na=results.Count(x=>x.Status==ValidationStatus.NOT_APPLICABLE);
        var applicable=results.Count-na;
        var evaluated=results.Count(x=>x.Status is ValidationStatus.PASS or ValidationStatus.FAIL);
        var pass=results.Count(x=>x.Status==ValidationStatus.PASS);var fail=results.Count(x=>x.Status==ValidationStatus.FAIL);var review=results.Count(x=>x.Status==ValidationStatus.NEEDS_REVIEW);var ne=results.Count(x=>x.Status==ValidationStatus.NOT_EVALUATED);
        var coverage=applicable==0?100d:100d*evaluated/applicable;
        var full=applicable>0&&Math.Abs(coverage-100d)<1e-9&&fail==0&&review==0&&ne==0;
        var statement=full?"All applicable rules were evaluated and passed.":$"Full compliance cannot be claimed: evaluated coverage {coverage:F2}%; FAIL={fail}; NEEDS_REVIEW={review}; NOT_EVALUATED={ne}.";
        return new(results,applicable,evaluated,coverage,pass,fail,review,ne,na,full,statement);
    }
}
