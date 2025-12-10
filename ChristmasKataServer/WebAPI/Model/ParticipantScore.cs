namespace WebAPI.Model;

public class ParticipantScore(int score, List<int> idsThatAreCorrect)
{
    public int score { get; } = score;
    public List<int> idsThatAreCorrect { get; } = idsThatAreCorrect;
}