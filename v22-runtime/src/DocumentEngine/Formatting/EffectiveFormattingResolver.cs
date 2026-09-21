using Nd30.DocumentEngine.Model;
namespace Nd30.DocumentEngine.Formatting;
public sealed class StyleInheritanceCycleException(string message):Exception(message);
public sealed class EffectiveFormattingResolver {
 public EffectiveFormatting Resolve(DocumentModel document,ParagraphModel paragraph,RunModel? run=null){
  var layers=new List<(FormattingValues Values,string Source)>();
  AddStyle(document.Styles,paragraph.StyleId,layers,"paragraph-style");
  if(run?.StyleId is not null) AddStyle(document.Styles,run.StyleId,layers,"run-style");
  layers.Add((paragraph.DirectFormatting,"direct-formatting"));
  if(run is not null) layers.Add((run.DirectFormatting,"direct-formatting"));
  var d=Merge(document.Styles.Defaults.Paragraph,document.Styles.Defaults.Run);
  return new(
   R(d.FontFamily,layers,x=>x.FontFamily),R(d.FontSizePt,layers,x=>x.FontSizePt),R(d.Bold,layers,x=>x.Bold),R(d.Italic,layers,x=>x.Italic),R(d.Underline,layers,x=>x.Underline),R(d.Color,layers,x=>x.Color),
   R(d.Alignment,layers,x=>x.Alignment),R(d.LineSpacing,layers,x=>x.LineSpacing),R(d.SpaceBeforePt,layers,x=>x.SpaceBeforePt),R(d.SpaceAfterPt,layers,x=>x.SpaceAfterPt),R(d.FirstLineIndentPt,layers,x=>x.FirstLineIndentPt),R(d.LeftIndentPt,layers,x=>x.LeftIndentPt),R(d.RightIndentPt,layers,x=>x.RightIndentPt));
 }
 public IReadOnlyList<string> ResolveStyleChain(StyleCatalogModel catalog,string? styleId){var result=new List<string>(); if(styleId is null)return result;var seen=new HashSet<string>();string? cur=styleId;while(cur is not null&&catalog.Styles.TryGetValue(cur,out var s)){if(!seen.Add(cur))throw new StyleInheritanceCycleException($"STYLE_INHERITANCE_CYCLE:{cur}");result.Insert(0,cur);cur=s.BasedOn;}return result;}
 void AddStyle(StyleCatalogModel c,string? id,List<(FormattingValues,string)> l,string terminal){var chain=ResolveStyleChain(c,id);for(int i=0;i<chain.Count;i++){var sid=chain[i];var s=c.Styles[sid];var source=i==chain.Count-1?terminal:$"inherited-style:{sid}";l.Add((Merge(s.ParagraphProperties,s.RunProperties),source));}}
 static EffectiveProperty<T> R<T>(T? def,IEnumerable<(FormattingValues Values,string Source)> layers,Func<FormattingValues,T?> pick){T? v=def;var p=def is null?"unset":"document-default";foreach(var x in layers){var q=pick(x.Values);if(q is not null){v=q;p=x.Source;}}return new(v,p);}
 static FormattingValues Merge(FormattingValues a,FormattingValues b)=>new(b.FontFamily??a.FontFamily,b.FontSizePt??a.FontSizePt,b.Bold??a.Bold,b.Italic??a.Italic,b.Underline??a.Underline,b.Color??a.Color,b.Alignment??a.Alignment,b.LineSpacing??a.LineSpacing,b.SpaceBeforePt??a.SpaceBeforePt,b.SpaceAfterPt??a.SpaceAfterPt,b.FirstLineIndentPt??a.FirstLineIndentPt,b.LeftIndentPt??a.LeftIndentPt,b.RightIndentPt??a.RightIndentPt);
}
