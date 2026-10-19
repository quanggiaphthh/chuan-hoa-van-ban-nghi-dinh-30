using YamlDotNet.Serialization;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Catalog;

public sealed class RuleCatalog
{
    public IReadOnlyList<RuleDefinition> Rules { get; }
    public IReadOnlySet<string> ReleaseRuleIds { get; }

    private RuleCatalog(IReadOnlyList<RuleDefinition> rules, IReadOnlySet<string> releaseRuleIds)
    {
        Rules = rules;
        ReleaseRuleIds = releaseRuleIds;
    }

    public static RuleCatalog LoadVerifiedRelease(string root)
    {
        var releasePath = Path.Combine(root, "release", "admin-nd30-verified-rc-v20.yaml");
        if (!File.Exists(releasePath)) throw new FileNotFoundException("Verified release pack not found.", releasePath);
        var deserializer = new DeserializerBuilder().Build();
        var release = NormalizeMap(deserializer.Deserialize<object>(File.ReadAllText(releasePath)));
        var ids = AsList(Get(release,"rules")).Select(ScalarString).Where(x=>!string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0) throw new InvalidDataException("Verified release pack is empty.");

        var byId = new Dictionary<string,RuleDefinition>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root,"rules"), "*.yaml", SearchOption.AllDirectories))
        {
            var map = NormalizeMap(deserializer.Deserialize<object>(File.ReadAllText(file)));
            if (!map.TryGetValue("id",out var idObj)) continue;
            var id = ScalarString(idObj);
            if (!ids.Contains(id)) continue;
            var rule = ParseRule(map);
            if (!string.Equals(rule.Maturity,"verified",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Release pack contains non-verified rule: {id} ({rule.Maturity}).");
            if (!byId.TryAdd(id,rule)) throw new InvalidDataException($"Duplicate rule id: {id}");
        }
        var missing = ids.Where(x=>!byId.ContainsKey(x)).ToArray();
        if (missing.Length>0) throw new InvalidDataException($"Release pack rule files missing: {string.Join(',',missing)}");
        return new RuleCatalog(ids.Select(x=>byId[x]).ToList(), ids);
    }

    private static RuleDefinition ParseRule(Dictionary<string,object?> m)
    {
        var source=AsMap(Get(m,"source")); var target=AsMap(Get(m,"target")); var temp=AsMap(Get(m,"temporality"));
        var validation=AsMap(Get(m,"validation")); var autofix=AsMap(Get(m,"autofix"));
        return new RuleDefinition(
            ScalarString(Get(m,"id")), ScalarString(Get(m,"regime")), ScalarString(Get(m,"domain")), ScalarString(Get(m,"maturity")),
            new RuleSource(ScalarString(Get(source,"source_id")),ScalarString(Get(source,"locator"))),
            Date(Get(temp,"effective_from")), Date(Get(temp,"effective_to")),
            new RuleTarget(ScalarString(Get(target,"object_type")), NullableString(Get(target,"role")), NullableString(Get(target,"property"))),
            AsMap(Get(m,"constraint")), m.TryGetValue("applicability",out var a)&&a is not null?AsMap(a):null,
            ScalarString(Get(validation,"severity")), ScalarString(Get(autofix,"policy")),
            AsList(Get(m,"requires_capability")).Select(ScalarString).Where(x=>x.Length>0).ToList());
    }

    public static Dictionary<string,object?> NormalizeMap(object? value) => AsMap(Normalize(value));
    public static object? Normalize(object? value)
    {
        if (value is IDictionary<object,object> od) return od.ToDictionary(k=>ScalarString(k.Key),v=>Normalize(v.Value),StringComparer.Ordinal);
        if (value is IDictionary<string,object> sd) return sd.ToDictionary(k=>k.Key,v=>Normalize(v.Value),StringComparer.Ordinal);
        if (value is IEnumerable<object> seq && value is not string) return seq.Select(Normalize).ToList();
        return value;
    }
    public static Dictionary<string,object?> AsMap(object? x) => x as Dictionary<string,object?> ?? new(StringComparer.Ordinal);
    public static List<object?> AsList(object? x) => x switch { null=>[], List<object?> l=>l, IEnumerable<object?> e=>e.ToList(), _=>[x] };
    public static object? Get(IReadOnlyDictionary<string,object?> m,string k)=>m.TryGetValue(k,out var v)?v:null;
    public static string ScalarString(object? x)=>x switch { null=>"", bool b=>b?"true":"false", _=>Convert.ToString(x,System.Globalization.CultureInfo.InvariantCulture)??"" };
    public static string? NullableString(object? x){var s=ScalarString(x);return string.IsNullOrWhiteSpace(s)?null:s;}
    private static DateOnly? Date(object? x)=>DateOnly.TryParse(ScalarString(x),out var d)?d:null;
}
