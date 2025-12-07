namespace WebAPI.Service;

public record Participant
{
    public string TeamName { get; set; }
    public string UserSecret { get; set; }
    public List<string> CorrectIds { get; protected set; } = new();
    
    public int GetParticipantScore() => CorrectIds.Count;
    
    public void AddMatchingResultIds(List<string> matchingResultIds)
    {
        var resultsToAdd = matchingResultIds.Where(x => !CorrectIds.Contains(x));
        CorrectIds.AddRange(resultsToAdd);
    }
}