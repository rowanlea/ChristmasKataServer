using Microsoft.AspNetCore.Mvc.RazorPages;
using WebAPI.API;
using WebAPI.Kata.Puzzles;

namespace WebAPI.Pages;

public class TeamModel : PageModel
{
    public IReadOnlyList<Puzzle> Puzzles => PuzzleCatalogue.All;
    public Puzzle Finale => PuzzleCatalogue.All[^1];
    public int FrameRows => ShowEndpoints.FrameRows;
    public int MaxFrames => ShowEndpoints.MaxFrames;
}
