using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;

namespace WebScraper.Api.Controllers;

/// <summary>Serves the versioned agent Skill document.</summary>
[ApiController]
[Route("api/v1/skill")]
[AllowAnonymous]
public class SkillController : ControllerBase
{
    private static string? FindSkillFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "skills", "nfl-db", "SKILL.md");
            if (System.IO.File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    [HttpGet]
    [Produces("text/markdown")]
    public IActionResult GetSkill()
    {
        var path = FindSkillFile();
        if (path is null)
            return NotFound(new { message = "Skill file not found. See skills/nfl-db/SKILL.md in the repository." });

        return Content(System.IO.File.ReadAllText(path), "text/markdown");
    }
}
