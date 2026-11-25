using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAPI.API;
using WebAPI.Kata.Puzzles;
using WebAPI.Scoring;
using WebAPI.Service;

namespace WebAPI.Pages;

public class TeamModel : PageModel
{
    public IReadOnlyList<Puzzle> Puzzles => PuzzleCatalogue.All;
    public Puzzle Finale => PuzzleCatalogue.All[^1];
    public int FrameRows => ShowEndpoints.FrameRows;
    public int MaxFrames => ShowEndpoints.MaxFrames;
    public int PassPercent => Points.PassPercent;
    public string NamePattern => TeamNames.Pattern;
    public string KeyPattern => TeamSecrets.Pattern;
}
