using System.Text;
using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Model;
using Nd30.SemanticDetector.Model;

namespace Nd30.SemanticDetector.Detection;

public sealed class AdministrativeSemanticDetector
{
    private static readonly Regex NumberLine = new(@"^\s*Số\s*:\s*(?<number>[^/\s]+)(?:\s*/\s*(?<notation>.+))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PlaceDate = new(@"^\s*[^,]{1,120},\s*ngày\s+\d{1,2}\s+tháng\s+\d{1,2}\s+năm\s+\d{4}\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OfficialSubject = new(@"^\s*V\s*/\s*v\s+.+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Addressee = new(@"^\s*Kính\s+gửi\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Recipient = new(@"^\s*Nơi\s+nhận\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Retention = new(@"^\s*Lưu\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LegalBasis = new(@"^\s*Căn\s+cứ\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Part = new(@"^\s*PHẦN\s+([IVXLCDM]+|\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Chapter = new(@"^\s*CHƯƠNG\s+([IVXLCDM]+|\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Section = new(@"^\s*MỤC\s+([IVXLCDM]+|\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Subsection = new(@"^\s*TIỂU\s+MỤC\s+([IVXLCDM]+|\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Article = new(@"^\s*Điều\s+\d+[a-zA-Z]?\s*[\.:]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Clause = new(@"^\s*\d+\.\s+\S+", RegexOptions.Compiled);
    private static readonly Regex Point = new(@"^\s*[a-zđ]\)\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AppendixNo = new(@"^\s*PHỤ\s+LỤC(?:\s+(?<n>[IVXLCDM]+|\d+))?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AppendixRef = new(@"^\s*\(?\s*Kèm\s+theo\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IssuedCopies = new(@"^\s*(Số\s+lượng\s+bản\s+phát\s+hành|Số\s+bản)\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Contact = new(@"(https?://|www\.|[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}|Điện\s+thoại\s*:|Tel\s*:|Fax\s*:|Địa\s+chỉ\s*:)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SignPrefix = new(@"^\s*(TM\.|KT\.|TL\.|TUQ\.|Q\.)\s*(?<rest>.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PersonName = new(@"^[\p{L}Đđ]+(?:\s+[\p{L}Đđ]+){1,5}$", RegexOptions.Compiled);
    private static readonly Regex Drafter = new(@"^\s*(Nơi soạn|Người soạn|Soạn thảo)\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> NationalHeaderTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM"
    };

    private static readonly HashSet<string> MottoTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "ĐỘC LẬP - TỰ DO - HẠNH PHÚC"
    };

    private static readonly HashSet<string> Urgency = new(StringComparer.OrdinalIgnoreCase)
    {
        "KHẨN", "THƯỢNG KHẨN", "HỎA TỐC"
    };

    private static readonly HashSet<string> ClassificationMarks = new(StringComparer.OrdinalIgnoreCase)
    {
        "MẬT", "TỐI MẬT", "TUYỆT MẬT"
    };

    private static readonly HashSet<string> Circulation = new(StringComparer.OrdinalIgnoreCase)
    {
        "XEM XONG TRẢ LẠI", "KHÔNG PHỔ BIẾN", "LƯU HÀNH NỘI BỘ"
    };

    private static readonly HashSet<string> CopyHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "SAO Y", "SAO LỤC", "TRÍCH SAO"
    };

    private static readonly Dictionary<string,string> NamedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NGHỊ QUYẾT"]="nghi_quyet",
        ["QUYẾT ĐỊNH"]="quyet_dinh",
        ["CHỈ THỊ"]="chi_thi",
        ["QUY CHẾ"]="quy_che",
        ["QUY ĐỊNH"]="quy_dinh",
        ["THÔNG CÁO"]="thong_cao",
        ["THÔNG BÁO"]="thong_bao",
        ["HƯỚNG DẪN"]="huong_dan",
        ["CHƯƠNG TRÌNH"]="chuong_trinh",
        ["KẾ HOẠCH"]="ke_hoach",
        ["PHƯƠNG ÁN"]="phuong_an",
        ["ĐỀ ÁN"]="de_an",
        ["DỰ ÁN"]="du_an",
        ["BÁO CÁO"]="bao_cao",
        ["TỜ TRÌNH"]="to_trinh",
        ["GIẤY ỦY QUYỀN"]="giay_uy_quyen",
        ["PHIẾU GỬI"]="phieu_gui",
        ["PHIẾU CHUYỂN"]="phieu_chuyen",
        ["PHIẾU BÁO"]="phieu_bao",
        ["CÔNG ĐIỆN"]="cong_dien",
        ["GIẤY MỜI"]="giay_moi",
        ["GIẤY GIỚI THIỆU"]="giay_gioi_thieu",
        ["BIÊN BẢN"]="bien_ban",
        ["GIẤY NGHỈ PHÉP"]="giay_nghi_phep",
        ["HỢP ĐỒNG"]="hop_dong",
        ["BẢN GHI NHỚ"]="ban_ghi_nho",
        ["BẢN THỎA THUẬN"]="ban_thoa_thuan"
    };

    private sealed record Block(string SourceKind, string ParagraphId, IReadOnlyList<string> RunIds, int Order, string Text);

    public SemanticDocumentModel Detect(DocumentModel document)
    {
        var blocks = Flatten(document);
        var components = new List<SemanticComponentCandidate>();
        var ambiguities = new List<SemanticAmbiguity>();
        var diagnostics = new List<string>();
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var seq = 0;

        SemanticComponentCandidate Add(Block b, SemanticRole role, double confidence, ClassificationMethod method, ClassificationStatus status, params string[] reasons)
        {
            var c = new SemanticComponentCandidate(
                $"cmp-{++seq:000}", role, b.Text.Trim(), confidence, method, status,
                new SemanticSourceEvidence(b.SourceKind,b.ParagraphId,b.RunIds,b.Order,b.Text.Trim(),reasons),
                Array.Empty<string>());
            components.Add(c);
            consumed.Add(b.ParagraphId + ":" + role);
            return c;
        }

        // Exact/strong top-level components.
        foreach (var b in blocks.Where(x => !string.IsNullOrWhiteSpace(x.Text)))
        {
            var t = Canon(b.Text);
            if (NationalHeaderTexts.Contains(t)) Add(b,SemanticRole.NationalHeader,1.0,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"exact national header text");
            if (MottoTexts.Contains(t)) Add(b,SemanticRole.NationalMotto,1.0,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"exact national motto text");
            if (NumberLine.IsMatch(b.Text))
            {
                Add(b,SemanticRole.DocumentNumber,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"Số: number-line pattern");
                if (!string.IsNullOrWhiteSpace(NumberLine.Match(b.Text).Groups["notation"].Value))
                    Add(b,SemanticRole.DocumentNotation,.97,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"notation follows slash in number line");
            }
            if (PlaceDate.IsMatch(b.Text)) Add(b,SemanticRole.IssuePlaceAndDate,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"place/date lexical pattern");
            if (OfficialSubject.IsMatch(b.Text)) Add(b,SemanticRole.SubjectOfficialLetter,.99,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"V/v subject prefix");
            if (Addressee.IsMatch(b.Text)) Add(b,SemanticRole.Addressee,.99,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"Kính gửi: label");
            if (Recipient.IsMatch(b.Text)) Add(b,SemanticRole.RecipientList,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"Nơi nhận: label");
            if (Retention.IsMatch(b.Text)) Add(b,SemanticRole.RetentionLine,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"Lưu: pattern");
            if (LegalBasis.IsMatch(b.Text)) Add(b,SemanticRole.LegalBasisBlock,.97,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"Căn cứ opening");
            if (AppendixNo.IsMatch(b.Text)) Add(b,SemanticRole.AppendixNumber,.99,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"Phụ lục label");
            if (AppendixRef.IsMatch(b.Text)) Add(b,SemanticRole.AppendixReference,.96,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"Kèm theo reference");
            if (CopyHeadings.Contains(t)) Add(b,SemanticRole.CopyFormHeading,.99,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"copy-form heading");
            if (Urgency.Contains(t)) Add(b,SemanticRole.UrgencyMark,.99,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"urgency vocabulary");
            if (ClassificationMarks.Contains(t)) Add(b,SemanticRole.ClassificationMark,.99,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"classification-mark vocabulary; legal classification is not inferred");
            if (Circulation.Contains(t)) Add(b,SemanticRole.CirculationInstruction,.96,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"circulation vocabulary");
            if (IssuedCopies.IsMatch(b.Text)) Add(b,SemanticRole.IssuedCopyCount,.94,ClassificationMethod.Deterministic,ClassificationStatus.INFERRED_HIGH,"issued-copy lexical pattern");
            if (Drafter.IsMatch(b.Text)) Add(b,SemanticRole.DrafterCode,.90,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_HIGH,"drafter label");
            if (Contact.IsMatch(b.Text)) Add(b,SemanticRole.ContactInformation,.94,ClassificationMethod.Deterministic,ClassificationStatus.INFERRED_HIGH,"contact lexical pattern");
        }

        // Document type headings.
        var headingCandidates = new List<(Block block,string type,SemanticComponentCandidate component)>();
        foreach (var b in blocks.Where(x => !string.IsNullOrWhiteSpace(x.Text)))
        {
            var key = Canon(b.Text);
            if (NamedTypes.TryGetValue(key,out var type))
            {
                var c=Add(b,SemanticRole.DocumentTypeHeading,.99,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,$"recognized administrative type: {type}");
                headingCandidates.Add((b,type,c));
            }
        }

        // Named subject: first substantive block after a unique type heading, before legal basis/article.
        if (headingCandidates.Count == 1)
        {
            var h=headingCandidates[0].block;
            var subject=blocks.Where(x=>x.Order>h.Order && !string.IsNullOrWhiteSpace(x.Text))
                .FirstOrDefault(x=>!LegalBasis.IsMatch(x.Text) && !Article.IsMatch(x.Text) && !SignPrefix.IsMatch(x.Text) && !Recipient.IsMatch(x.Text));
            if (subject is not null && !OfficialSubject.IsMatch(subject.Text))
                Add(subject,SemanticRole.SubjectNamedDocument,.84,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_HIGH,"first substantive block after unique type heading");
        }

        // Hierarchy.
        foreach (var b in blocks.Where(x=>!string.IsNullOrWhiteSpace(x.Text)))
        {
            if (Part.IsMatch(b.Text)) Add(b,SemanticRole.PartHeading,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"PHẦN heading");
            else if (Chapter.IsMatch(b.Text)) Add(b,SemanticRole.ChapterHeading,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"CHƯƠNG heading");
            else if (Subsection.IsMatch(b.Text)) Add(b,SemanticRole.SubsectionHeading,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"TIỂU MỤC heading");
            else if (Section.IsMatch(b.Text)) Add(b,SemanticRole.SectionHeading,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"MỤC heading");
            else if (Article.IsMatch(b.Text)) Add(b,SemanticRole.ArticleHeading,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"Điều heading");
            else if (Point.IsMatch(b.Text)) Add(b,SemanticRole.Point,.96,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"point pattern");
            else if (Clause.IsMatch(b.Text)) Add(b,SemanticRole.Clause,.94,ClassificationMethod.Deterministic,ClassificationStatus.INFERRED_HIGH,"clause pattern");
        }

        // Issuer block heuristic: uppercase blocks before number line, excluding national header/motto and markings.
        var numberOrder=components.Where(x=>x.Role==SemanticRole.DocumentNumber).Select(x=>x.Evidence.Order).DefaultIfEmpty(int.MaxValue).Min();
        var issuerBlocks=blocks.Where(x=>x.Order<numberOrder && IsAllUpperText(x.Text))
            .Where(x=>!NationalHeaderTexts.Contains(Canon(x.Text)) && !MottoTexts.Contains(Canon(x.Text)) && !Urgency.Contains(Canon(x.Text)) && !ClassificationMarks.Contains(Canon(x.Text)))
            .ToList();
        if (issuerBlocks.Count==1)
            Add(issuerBlocks[0],SemanticRole.IssuingAuthority,.82,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_HIGH,"single uppercase issuer-zone block before number line");
        else if (issuerBlocks.Count>=2)
        {
            Add(issuerBlocks[^2],SemanticRole.IssuingAuthorityParent,.70,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_LOW,"upper issuer-zone block before issuing authority");
            Add(issuerBlocks[^1],SemanticRole.IssuingAuthority,.84,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_HIGH,"last uppercase issuer-zone block before number line");
        }

        // Signature block heuristics.
        foreach (var b in blocks.Where(x=>!string.IsNullOrWhiteSpace(x.Text)))
        {
            var m=SignPrefix.Match(b.Text);
            if (!m.Success) continue;
            var pref=Add(b,SemanticRole.SigningAuthorityPrefix,.98,ClassificationMethod.Deterministic,ClassificationStatus.CONFIRMED,"recognized signing-authority prefix");
            var rest=m.Groups["rest"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(rest))
                Add(b,SemanticRole.SignerTitle,.86,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_HIGH,"title text follows signing-authority prefix");
            var subsequent=blocks.Where(x=>x.Order>b.Order && x.Order<=b.Order+5 && !string.IsNullOrWhiteSpace(x.Text)).ToList();
            foreach(var s in subsequent)
            {
                if (IsAllUpperText(s.Text) && !Recipient.IsMatch(s.Text) && !Retention.IsMatch(s.Text))
                {
                    Add(s,SemanticRole.SignerTitle,.72,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_LOW,"uppercase text near signing prefix");
                    continue;
                }
                if (LooksLikePersonName(s.Text))
                {
                    Add(s,SemanticRole.SignerName,.80,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_HIGH,"person-name shape near signing prefix");
                    break;
                }
            }
            _=pref;
        }

        // Appendix title: first uppercase/substantive block after appendix number and before reference/body.
        var app=components.FirstOrDefault(x=>x.Role==SemanticRole.AppendixNumber);
        if(app is not null)
        {
            var b=blocks.Where(x=>x.Order>app.Evidence.Order && !string.IsNullOrWhiteSpace(x.Text)).FirstOrDefault(x=>!AppendixRef.IsMatch(x.Text));
            if(b is not null) Add(b,SemanticRole.AppendixTitle,.88,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_HIGH,"first substantive block after appendix label");
        }

        // Copy authority/signature evidence.
        var copy=components.FirstOrDefault(x=>x.Role==SemanticRole.CopyFormHeading);
        if(copy is not null)
        {
            var auth=blocks.Where(x=>x.Order>copy.Evidence.Order && x.Order<=copy.Evidence.Order+5 && !string.IsNullOrWhiteSpace(x.Text)).FirstOrDefault(x=>IsAllUpperText(x.Text));
            if(auth is not null) Add(auth,SemanticRole.CopyAuthority,.72,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_LOW,"uppercase authority block near copy heading");
            var sig=components.FirstOrDefault(x=>x.Role==SemanticRole.SignerName && x.Evidence.Order>copy.Evidence.Order);
            if(sig is not null)
            {
                var source=blocks.First(x=>x.ParagraphId==sig.Evidence.ParagraphId);
                Add(source,SemanticRole.CopyCertificationSignature,.72,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_LOW,"signer-name evidence inside copy form");
            }
        }

        var classification=Classify(components,headingCandidates,ambiguities);
        var template=MatchTemplate(classification,components);

        // Ambiguity links for multiple type candidates.
        if(headingCandidates.Count>1)
        {
            var ids=headingCandidates.Select(x=>x.component.Id).ToArray();
            ambiguities.Add(new("MULTIPLE_DOCUMENT_TYPE_HEADINGS","Multiple recognized administrative document-type headings compete for classification.",ids));
            LinkCompetitors(components,ids);
        }
        var namedIds=headingCandidates.Select(x=>x.component.Id).ToList();
        var officialIds=components.Where(x=>x.Role==SemanticRole.SubjectOfficialLetter).Select(x=>x.Id).ToList();
        if(namedIds.Count>0 && officialIds.Count>0)
        {
            var ids=namedIds.Concat(officialIds).ToArray();
            ambiguities.Add(new("NAMED_AND_OFFICIAL_LETTER_SIGNALS","Named-document heading and V/v official-letter subject are both present.",ids));
            LinkCompetitors(components,ids);
        }

        // Body fallback only after structured detection. Do not structurally rewrite anything.
        var structuredParagraphs=components.Where(x=>x.Role!=SemanticRole.Body).Select(x=>x.Evidence.ParagraphId).ToHashSet(StringComparer.Ordinal);
        foreach(var b in blocks.Where(x=>!string.IsNullOrWhiteSpace(x.Text) && !structuredParagraphs.Contains(x.ParagraphId)))
            Add(b,SemanticRole.Body,.62,ClassificationMethod.Heuristic,ClassificationStatus.INFERRED_LOW,"unclassified substantive paragraph treated as body candidate");

        if(classification.NeedsReview) diagnostics.Add("Document classification is ambiguous or insufficient; structural auto-fix must remain disabled.");
        var requiresReview=classification.NeedsReview || template.NeedsReview || ambiguities.Count>0;
        return new(document.Id,classification,template,components.OrderBy(x=>x.Evidence.Order).ThenBy(x=>x.Id).ToList(),ambiguities,requiresReview,diagnostics);
    }

    private static DocumentClassificationResult Classify(
        IReadOnlyList<SemanticComponentCandidate> components,
        IReadOnlyList<(Block block,string type,SemanticComponentCandidate component)> headings,
        List<SemanticAmbiguity> ambiguities)
    {
        var appendix=components.Where(x=>x.Role==SemanticRole.AppendixNumber).ToList();
        var copy=components.Where(x=>x.Role==SemanticRole.CopyFormHeading).ToList();
        var official=components.Where(x=>x.Role==SemanticRole.SubjectOfficialLetter).ToList();
        var addressee=components.Any(x=>x.Role==SemanticRole.Addressee);
        if(appendix.Count>0 && copy.Count==0 && headings.Count==0 && official.Count==0)
            return new(AdministrativeDocumentClass.Appendix,"phu_luc",.99,ClassificationStatus.CONFIRMED,false,appendix.Select(x=>x.Id).ToList(),Array.Empty<string>());
        if(copy.Count>0 && appendix.Count==0 && headings.Count==0 && official.Count==0)
            return new(AdministrativeDocumentClass.Copy,"ban_sao",.99,ClassificationStatus.CONFIRMED,false,copy.Select(x=>x.Id).ToList(),Array.Empty<string>());
        if(headings.Count==1 && official.Count==0)
            return new(AdministrativeDocumentClass.NamedAdministrativeDocument,headings[0].type,.99,ClassificationStatus.CONFIRMED,false,new[]{headings[0].component.Id},Array.Empty<string>());
        if(headings.Count==0 && official.Count>0)
        {
            var confidence = addressee ? .95 : .88;
            return new(AdministrativeDocumentClass.OfficialLetter,"cong_van",confidence,addressee ? ClassificationStatus.CONFIRMED : ClassificationStatus.INFERRED_HIGH,!addressee,official.Select(x=>x.Id).ToList(),Array.Empty<string>());
        }
        var competing=headings.Select(x=>x.type).Concat(official.Count>0?new[]{"cong_van"}:Array.Empty<string>()).Distinct().ToList();
        if(competing.Count>1)
        {
            ambiguities.Add(new("DOCUMENT_TYPE_AMBIGUOUS","Competing document-type signals require human review.",headings.Select(x=>x.component.Id).Concat(official.Select(x=>x.Id)).ToList()));
            return new(AdministrativeDocumentClass.Unknown,null,.35,ClassificationStatus.UNKNOWN,true,headings.Select(x=>x.component.Id).Concat(official.Select(x=>x.Id)).ToList(),competing);
        }
        return new(AdministrativeDocumentClass.Unknown,null,.20,ClassificationStatus.UNKNOWN,true,Array.Empty<string>(),Array.Empty<string>());
    }

    private static TemplateMatchResult MatchTemplate(DocumentClassificationResult c,IReadOnlyList<SemanticComponentCandidate> components)
    {
        var ev=c.EvidenceComponentIds;
        if(c.NeedsReview) return new(null,.25,ClassificationStatus.UNKNOWN,true,ev,Array.Empty<string>());
        if(c.DocumentClass==AdministrativeDocumentClass.OfficialLetter) return new("ND30.PL3.TEMPLATE_1_5",.99,ClassificationStatus.CONFIRMED,false,ev,Array.Empty<string>());
        if(c.DocumentClass==AdministrativeDocumentClass.Appendix) return new(null,.60,ClassificationStatus.INFERRED_LOW,true,ev,new[]{"ND30.PL3.TEMPLATE_2_1","ND30.PL3.TEMPLATE_2_2"});
        if(c.DocumentClass==AdministrativeDocumentClass.Copy) return new(null,.60,ClassificationStatus.INFERRED_LOW,true,ev,new[]{"ND30.PL3.TEMPLATE_3_1","ND30.PL3.TEMPLATE_3_2"});
        if(c.DocumentClass==AdministrativeDocumentClass.NamedAdministrativeDocument)
        {
            return c.SpecificType switch
            {
                "nghi_quyet" => new("ND30.PL3.TEMPLATE_1_1",.98,ClassificationStatus.CONFIRMED,false,ev,Array.Empty<string>()),
                "quyet_dinh" => new(null,.70,ClassificationStatus.INFERRED_HIGH,true,ev,new[]{"ND30.PL3.TEMPLATE_1_2","ND30.PL3.TEMPLATE_1_3"}),
                "cong_dien" => new("ND30.PL3.TEMPLATE_1_6",.98,ClassificationStatus.CONFIRMED,false,ev,Array.Empty<string>()),
                "giay_moi" => new("ND30.PL3.TEMPLATE_1_7",.98,ClassificationStatus.CONFIRMED,false,ev,Array.Empty<string>()),
                "giay_gioi_thieu" => new("ND30.PL3.TEMPLATE_1_8",.98,ClassificationStatus.CONFIRMED,false,ev,Array.Empty<string>()),
                "bien_ban" => new("ND30.PL3.TEMPLATE_1_9",.98,ClassificationStatus.CONFIRMED,false,ev,Array.Empty<string>()),
                "giay_nghi_phep" => new("ND30.PL3.TEMPLATE_1_10",.98,ClassificationStatus.CONFIRMED,false,ev,Array.Empty<string>()),
                _ => new("ND30.PL3.TEMPLATE_1_4",.96,ClassificationStatus.CONFIRMED,false,ev,Array.Empty<string>())
            };
        }
        return new(null,.20,ClassificationStatus.UNKNOWN,true,ev,Array.Empty<string>());
    }

    private static void LinkCompetitors(List<SemanticComponentCandidate> components,IReadOnlyList<string> ids)
    {
        for(int i=0;i<components.Count;i++)
        {
            if(!ids.Contains(components[i].Id)) continue;
            components[i]=components[i] with { CompetingCandidateIds=ids.Where(x=>x!=components[i].Id).ToArray() };
        }
    }

    private static List<Block> Flatten(DocumentModel d)
    {
        var result=new List<Block>();
        int order=0;
        // Table blocks first: NĐ30 top blocks are commonly table-based. Source IDs remain explicit.
        foreach(var table in d.Tables)
            foreach(var row in table.Rows)
                foreach(var cell in row.Cells)
                    foreach(var p in cell.Paragraphs)
                        result.Add(new("table-cell",p.Id,p.Runs.Select(x=>x.Id).ToList(),++order,Text(p)));
        foreach(var p in d.Paragraphs)
            result.Add(new("document",p.Id,p.Runs.Select(x=>x.Id).ToList(),++order,Text(p)));
        return result;
    }

    private static string Text(ParagraphModel p)=>string.Concat(p.Runs.Select(x=>x.Text)).Trim();
    private static string Canon(string text)
    {
        var s=text.Trim().Replace('–','-').Replace('—','-');
        s=Regex.Replace(s,@"\s+"," ");
        s=Regex.Replace(s,@"\s*-\s*"," - ");
        return s.ToUpperInvariant();
    }
    private static bool IsAllUpperText(string text)
    {
        var letters=text.Where(char.IsLetter).ToArray();
        return letters.Length>=3 && letters.All(ch=>char.ToUpperInvariant(ch)==ch);
    }
    private static bool LooksLikePersonName(string text)
    {
        var t=Regex.Replace(text.Trim(),@"\s+"," ");
        if(!PersonName.IsMatch(t) || IsAllUpperText(t)) return false;
        var words=t.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        return words.All(w=>char.IsUpper(w[0]));
    }
}
