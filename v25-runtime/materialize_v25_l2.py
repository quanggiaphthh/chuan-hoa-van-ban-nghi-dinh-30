#!/usr/bin/env python3
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
EVAL=ROOT/'v22-runtime/src/LegalValidator/Evaluation/RuleEvaluator.cs'
TEST=ROOT/'v22-runtime/tests/LegalValidator.Tests/V25L2HardeningTests.cs'

text=EVAL.read_text(encoding='utf-8')
old='''            if(!c.Fields.TryGetValue(field,out var actual))return (ValidationStatus.NOT_EVALUATED,$"Applicability field unavailable: {field}.");\n            if(!Condition(op,actual,expected))return (ValidationStatus.NOT_APPLICABLE,$"Applicability condition is false: {field} {op} {Fmt(expected)}.");\n        }\n        return (ValidationStatus.PASS,"Applicability conditions satisfied.");\n    }\n\n    private static bool Condition(string op,object? actual,object? expected)=>op switch\n    {\n        "equals"=>Eq(actual,expected),\n        "not_equals"=>!Eq(actual,expected),\n        "in"=>RuleCatalog.AsList(expected).Any(x=>Eq(actual,x)),\n        "greater_than_or_equal"=>Num(actual,out var a)&&Num(expected,out var e)&&a>=e,\n        _=>false\n    };'''
new='''            if(!c.Fields.TryGetValue(field,out var actual))return (ValidationStatus.NOT_EVALUATED,$"Applicability field unavailable: {field}.");\n            var condition=Condition(op,actual,expected);\n            if(condition is null)return (ValidationStatus.NOT_EVALUATED,$"Applicability operator is unsupported: {op}.");\n            if(condition==false)return (ValidationStatus.NOT_APPLICABLE,$"Applicability condition is false: {field} {op} {Fmt(expected)}.");\n        }\n        return (ValidationStatus.PASS,"Applicability conditions satisfied.");\n    }\n\n    private static bool? Condition(string op,object? actual,object? expected)=>op switch\n    {\n        "equals"=>Eq(actual,expected),\n        "not_equals"=>!Eq(actual,expected),\n        "in"=>RuleCatalog.AsList(expected).Any(x=>Eq(actual,x)),\n        "greater_than_or_equal"=>Num(actual,out var a)&&Num(expected,out var e)&&a>=e,\n        _=>null\n    };'''
if old not in text:
    raise SystemExit('expected V24 applicability block not found; refusing non-deterministic overlay')
EVAL.write_text(text.replace(old,new,1),encoding='utf-8')

TEST.write_text(r'''using Xunit;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Tests;

public sealed class V25L2HardeningTests
{
    private static RuleDefinition Rule(string id="TEST.RULE", string maturity="verified", IReadOnlyDictionary<string,object?>? applicability=null, string capability="cap") => new(
        id,"administrative","document_format",maturity,new RuleSource("VN-STATE-ND30-2020","test"),
        new DateOnly(2020,3,5),null,new RuleTarget("semantic_component","test","value"),
        new Dictionary<string,object?>{{"type","equals"},{"value","ok"}},applicability,"LEGAL_ERROR","PROHIBITED",[capability]);
    private static EvidenceRecord Auth(string kind="semantic_component_detection")=>new(kind,EvidenceAuthority.Authoritative,"fixture",1.0,false);

    [Fact] public void Missing_required_capability_is_not_evaluated_not_pass(){var r=new RuleEvaluator().Evaluate(Rule(),new ValidationContext{EvaluationDate=new(2026,9,21)});Assert.Equal(ValidationStatus.NOT_EVALUATED,r.Status);Assert.Contains("cap",r.MissingCapabilities);}
    [Fact] public void Missing_observation_is_not_evaluated_not_pass(){var c=new ValidationContext{EvaluationDate=new(2026,9,21)};c.AddCapability("cap");var r=new RuleEvaluator().Evaluate(Rule(),c);Assert.Equal(ValidationStatus.NOT_EVALUATED,r.Status);}
    [Fact] public void Unsupported_applicability_operator_is_not_evaluated_not_not_applicable(){var a=new Dictionary<string,object?>{{"all",new object?[]{new Dictionary<string,object?>{{"field","document.type"},{"operator","future_operator"},{"value","decision"}}}}};var c=new ValidationContext{EvaluationDate=new(2026,9,21)};c.SetField("document.type","decision").AddCapability("cap").Observe("semantic_component.test.value","ok",Auth());var r=new RuleEvaluator().Evaluate(Rule(applicability:a),c);Assert.Equal(ValidationStatus.NOT_EVALUATED,r.Status);Assert.Contains("unsupported",r.Reason,StringComparison.OrdinalIgnoreCase);}
    [Fact] public void Non_applicable_rule_is_not_fail(){var a=new Dictionary<string,object?>{{"all",new object?[]{new Dictionary<string,object?>{{"field","document.type"},{"operator","equals"},{"value","decision"}}}}};var c=new ValidationContext{EvaluationDate=new(2026,9,21)};c.SetField("document.type","official_letter").AddCapability("cap").Observe("semantic_component.test.value","bad",Auth());Assert.Equal(ValidationStatus.NOT_APPLICABLE,new RuleEvaluator().Evaluate(Rule(applicability:a),c).Status);}
    [Fact] public void Temporally_inactive_rule_is_not_evaluated_as_active(){var r=Rule() with { EffectiveTo=new DateOnly(2025,12,31) };var c=new ValidationContext{EvaluationDate=new(2026,9,21)};c.AddCapability("cap").Observe(r.Target.Key,"bad",Auth());Assert.Equal(ValidationStatus.NOT_APPLICABLE,new RuleEvaluator().Evaluate(r,c).Status);}
    [Fact] public void Constraint_exception_is_isolated_as_not_evaluated(){var r=Rule() with { Constraint=new Dictionary<string,object?>{{"type","regex"},{"pattern","["}} };var c=new ValidationContext{EvaluationDate=new(2026,9,21)};c.AddCapability("cap").Observe(r.Target.Key,"anything",Auth());var result=new RuleEvaluator().Evaluate(r,c);Assert.Equal(ValidationStatus.NOT_EVALUATED,result.Status);Assert.Contains("evaluation error",result.Reason,StringComparison.OrdinalIgnoreCase);}
    [Fact] public void Evidence_and_provenance_are_preserved_on_evaluated_result(){var r=Rule();var ev=Auth("semantic_component_detection");var c=new ValidationContext{EvaluationDate=new(2026,9,21)};c.AddCapability("cap").Observe(r.Target.Key,"ok",ev);var result=new RuleEvaluator().Evaluate(r,c);Assert.Equal(ValidationStatus.PASS,result.Status);Assert.Single(result.Evidence);Assert.Equal("fixture",result.Evidence[0].Reference);Assert.Equal(r.Id,result.RuleId);Assert.Equal(r.Source,result.Source);}
}
''',encoding='utf-8')
print('V25 L2 overlay materialized: tri-state applicability + 7 targeted hardening tests')
