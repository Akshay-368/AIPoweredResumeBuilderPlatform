namespace ResumeAI.FileParserToJson.Interfaces;

public interface IResumeParserService
{
    string ParseFile(IFormFile file);
}
