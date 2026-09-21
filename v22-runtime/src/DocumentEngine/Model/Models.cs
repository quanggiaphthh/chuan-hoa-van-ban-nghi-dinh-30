namespace Nd30.DocumentEngine.Model;

public enum PatchPolicy { NORMAL, GUARDED, AUDIT_ONLY, PROHIBITED }
public enum StyleKind { Paragraph, Character, Table, Numbering, Unknown }
public enum HeaderFooterKind { Default, First, Even }
public enum FieldType { Unknown, Page, NumPages }
public sealed record Diagnostic(string Code,string Message);
public sealed record PackageSafetyState(bool IsReadable,bool IsSigned,bool IsProtected,bool HasMacros,bool HasTrackedChanges,bool HasOleObjects,bool HasExternalRelationships,IReadOnlyList<string> UnsupportedFeatures,PatchPolicy PatchPolicy,IReadOnlyList<Diagnostic> Diagnostics);
public sealed record RelationshipModel(string Id,string Type,string Target,bool IsExternal,string SourcePart,string? TargetPart);
public enum LineSpacingRule { Auto, AtLeast, Exact, Unknown }
public sealed record LineSpacingModel(string RawValue,LineSpacingRule Rule,double? Lines,double? Points);
public sealed record FormattingValues(string? FontFamily,double? FontSizePt,bool? Bold,bool? Italic,bool? Underline,string? Color,string? Alignment,LineSpacingModel? LineSpacing,double? SpaceBeforePt,double? SpaceAfterPt,double? FirstLineIndentPt,double? LeftIndentPt,double? RightIndentPt);
public sealed record DocumentDefaultsModel(FormattingValues Paragraph,FormattingValues Run);
public sealed record StyleModel(string Id,StyleKind Kind,string? BasedOn,FormattingValues ParagraphProperties,FormattingValues RunProperties);
public sealed record StyleCatalogModel(DocumentDefaultsModel Defaults,IReadOnlyDictionary<string,StyleModel> Styles);
public sealed record StyleReference(string? StyleId,string? BasedOn);
public sealed record FieldModel(string Id,string Instruction,string? Result,FieldType FieldType,string ParagraphId,IReadOnlyList<string> SourceRunIds);
public sealed record RunModel(string Id,string Text,string? StyleId,FormattingValues DirectFormatting,IReadOnlyList<FieldModel> Fields);
public sealed record ParagraphModel(string Id,string? StyleId,string? NumberingId,int? NumberingLevel,FormattingValues DirectFormatting,IReadOnlyList<RunModel> Runs,IReadOnlyList<FieldModel> Fields);
public sealed record TableCellModel(string Id,IReadOnlyList<ParagraphModel> Paragraphs);
public sealed record TableRowModel(string Id,IReadOnlyList<TableCellModel> Cells);
public sealed record TableModel(string Id,IReadOnlyList<TableRowModel> Rows);
public sealed record HeaderModel(string Id,string RelationshipId,IReadOnlyList<ParagraphModel> Paragraphs);
public sealed record FooterModel(string Id,string RelationshipId,IReadOnlyList<ParagraphModel> Paragraphs);
public sealed record HeaderFooterReferenceModel(HeaderFooterKind Kind,string RelationshipId);
public sealed record NumberingLevelModel(int Level,string? NumberFormat,string? LevelText,int? StartValue);
public sealed record AbstractNumberingModel(string Id,IReadOnlyList<NumberingLevelModel> Levels);
public sealed record NumberingModel(string Id,string? AbstractNumberId);
public sealed record NumberingCatalogModel(IReadOnlyList<AbstractNumberingModel> AbstractNumbers,IReadOnlyList<NumberingModel> Instances);
public sealed record SectionModel(string Id,int Index,uint? PageWidthTwips,uint? PageHeightTwips,string Orientation,int? MarginTopTwips,int? MarginRightTwips,int? MarginBottomTwips,int? MarginLeftTwips,IReadOnlyList<HeaderFooterReferenceModel> HeaderReferences,IReadOnlyList<HeaderFooterReferenceModel> FooterReferences,string? PageNumberFormat,int? PageNumberStart,string BoundaryParagraphId);
public sealed record DocumentModel(string Id,IReadOnlyList<ParagraphModel> Paragraphs,IReadOnlyList<TableModel> Tables,IReadOnlyList<SectionModel> Sections,IReadOnlyList<HeaderModel> Headers,IReadOnlyList<FooterModel> Footers,NumberingCatalogModel Numbering,StyleCatalogModel Styles,IReadOnlyList<RelationshipModel> Relationships,PackageSafetyState Safety,IReadOnlyList<string> ReadOnlyParts);
public sealed record EffectiveProperty<T>(T? Value,string Provenance);
public sealed record EffectiveFormatting(EffectiveProperty<string> FontFamily,EffectiveProperty<double?> FontSizePt,EffectiveProperty<bool?> Bold,EffectiveProperty<bool?> Italic,EffectiveProperty<bool?> Underline,EffectiveProperty<string> Color,EffectiveProperty<string> Alignment,EffectiveProperty<LineSpacingModel> LineSpacing,EffectiveProperty<double?> SpaceBeforePt,EffectiveProperty<double?> SpaceAfterPt,EffectiveProperty<double?> FirstLineIndentPt,EffectiveProperty<double?> LeftIndentPt,EffectiveProperty<double?> RightIndentPt);
public sealed record ParseResult(DocumentModel? Document,IReadOnlyList<Diagnostic> Diagnostics);
