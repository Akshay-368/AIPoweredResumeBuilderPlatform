namespace ResumeAI.FileParserToJson.Models;

public class ResumeData
{
    public PersonalInfo PersonalInfo { get; set; } = new();
    public List<EducationEntry> Education { get; set; } = new();
    public List<ExperienceEntry> Experience { get; set; } = new();
    public List<ProjectEntry> Projects { get; set; } = new();
    public List<string> Skills { get; set; } = new();

    public List<TargetJob> TargetJobs { get; set; } = new(); // like software engineer, data scientist, etc.
}

public class PersonalInfo
{
    public string Name { get; set; } = "";
    public string ProfessionalTitle { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Location { get; set; } = "";
    public string LinkedIn { get; set; } = "";

    public string GitHub { get; set; } = "";

    public List<string> ExternalLink { get; set; } = new(); // like portfolio or personal website or google drive or one drive link to resume
    public string Summary { get; set; } = "";
}

public class EducationEntry
{
    public string School { get; set; } = ""; // can be multiple schools like high school, senior secondary school, etc.
    public string College { get; set; } = "";
    public string University { get; set; } = "";
    public string Degree { get; set; } = "";
    public string FieldOfStudy { get; set; } = "";
    public string StartYear { get; set; } = "";
    public string EndYear { get; set; } = "";
    public string Marks{ get; set; } = "";
}

public class ExperienceEntry
{
    public string Company { get; set; } = ""; 
    public string Role { get; set; } = "";
    public string StartDate{ get; set; } = "";
    public string EndDate{ get; set; } = "";
    public string Description { get; set; } = "";
}

public class ProjectEntry
{
    public string Title { get; set; } = "";
    public string Technologies { get; set; } = "";
    public string Description { get; set; } = "";
    
}

public class TargetJob
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}