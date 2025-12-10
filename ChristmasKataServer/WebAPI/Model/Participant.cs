using Newtonsoft.Json;

namespace WebAPI.Service;

public record Participant
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }

    [JsonProperty(PropertyName = "teamName")]
    public string TeamName { get; set; }

    public string UserSecret { get; set; }

    public int Score { get; set; }
    
    public void UpdateHighScore(int score)
    {
        if (score > Score)
        {
            Score = score; 
        }
    }
}