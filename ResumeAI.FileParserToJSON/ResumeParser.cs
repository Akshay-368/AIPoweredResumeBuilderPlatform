using UglyToad.PdfPig; // for pdfs
using DocumentFormat.OpenXml.Packaging; // for docx
using DocumentFormat.OpenXml.Wordprocessing; // for docx
using System.Text;
using System.Text.RegularExpressions;
using ResumeAI.FileParserToJson.Interfaces;

namespace ResumeAI.FileParserToJson.Services;

public class ResumeParserService : IResumeParserService
{
    public string ExtractAndValidatePdf(Stream pdfStream)
    {
        var rawText = new StringBuilder();
        
        using (var document = PdfDocument.Open(pdfStream))
        {
            foreach (var page in document.GetPages())
            {
                // Preserve basic layout order
                var words = page.GetWords();
                rawText.Append(string.Join(" ", words.Select(w => w.Text)));
            }
        }

        string content = rawText.ToString();

        // --- STEP 1: Basic Validation (Is it even a Resume?) ---
        // Look for common "Anchors" using Regex
        string[] resumeKeywords = { "Education", "Experience", "Skills", "Project", "University", "Contact" };
        int matchCount = resumeKeywords.Count(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (matchCount < 2) 
        {
            throw new Exception("Invalid File: This doesn't look like a resume. Please upload a valid document.");
        }

        // --- STEP 2: Prompt Injection Sanitization ---
        // Clean suspicious characters or "Hidden Instructions"
        content = SanitizeContent(content);

        return content;
    }

    public string ExtractAndValidateDocx(Stream docxStream)
    {
        var rawText = new StringBuilder();

        using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(docxStream, false))
        {
            // Get the main document part and extract text while preserving basic layout
            var mainPart = wordDoc.MainDocumentPart;
            if (mainPart?.Document?.Body is null)
            {
                throw new Exception("Invalid DOCX file: readable content was not found.");
            }

            Body body = mainPart.Document.Body;
            
            // loop through all paragrapsh and append the text into rawText
            foreach(var paragraph in body.Descendants<Paragraph>())
            {
                rawText.AppendLine(paragraph.InnerText);
            }
        }

        string content = rawText.ToString();

        // --- STEP 1: Basic Validation (Is it even a Resume?) ---
        // Look for common "Anchors" using Regex
        string[] resumeKeywords = { "Education", "Experience", "Skills", "Project", "Projects", "University", "Contact" ,   "Certification", "Awards", "Publications" , "References" , "Internship" };
        int matchCount = resumeKeywords.Count(k => content.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (matchCount < 2) 
        {
            throw new Exception("Invalid File: This doesn't look like a resume. Please upload a valid document.");
        }

        // --- STEP 2: Prompt Injection Sanitization ---
        // Clean suspicious characters or "Hidden Instructions"
        content = SanitizeContent(content);

        return content;
    }

    private string SanitizeContent(string input)
    {
        // Remove common prompt injection keywords if they appear in weird patterns
        // e.g., "Ignore all previous instructions", "System Message", etc.
        var pattern = @"(ignore|system|instruction|bypass|override)\b";
        return Regex.Replace(input, pattern, "[REDACTED]", RegexOptions.IgnoreCase);
    }

    public string ParseFile(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            throw new Exception("Please upload a non-empty file.");
        }

        using var stream = file.OpenReadStream();
        string rawText;

        if (file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            rawText = ExtractAndValidatePdf(stream); //  PdfPig logic
        }
        else if (file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            rawText = ExtractAndValidateDocx(stream); //  OpenXML logic
        }
        else 
        {
            throw new Exception("Unsupported file format.");
        }

        return rawText;
    }
}