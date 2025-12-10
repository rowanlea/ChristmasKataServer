using System.Reflection;
using System.Resources;
using System.Text.Json;
using WebAPI.Model;
using WebAPI.Requests;

namespace WebAPI.Service;

public interface IScoringService
{
    Task<ParticipantScore> Score(string teamName, NaughtyNiceCandidateSubmissionRequest request);
}

public class ScoringService(
    IValidationResultReader validationResultReader,
    IParticipantRepository participantRepository) : IScoringService
{
    public async Task<ParticipantScore> Score(string teamName,
        NaughtyNiceCandidateSubmissionRequest request)
    {
        var validationDataDictionary =
            (await validationResultReader.GetValidationResults())
            .ToDictionary(x => x.Id, x => x.NaughtyOrNice);


        var matchingResultIds = request.NaughtyNiceCandidateRequests
            .Where(x => validationDataDictionary.TryGetValue(x.Id, out var naughtyOrNice) &&
                        (x.NaughtyOrNice == naughtyOrNice))
            .DistinctBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        var participant = await participantRepository.GetParticipantBy(teamName);
        participant!.UpdateHighScore(matchingResultIds.Count);

        await participantRepository.UpdateParticipantScore(participant);

        return new ParticipantScore(participant.Score, matchingResultIds);
    }
}