using System.Reflection;
using System.Resources;
using System.Text.Json;
using WebAPI.Requests;

namespace WebAPI.Service;

public interface IScoringService
{
    Task<Participant> Score(string teamName, string teamSecret, NaughtyNiceCandidateSubmissionRequest request);
}

public class ScoringService(
    IValidationResultReader validationResultReader,
    IParticipantRepository participantRepository) : IScoringService
{
    public async Task<Participant> Score(string teamName, string teamSecret, NaughtyNiceCandidateSubmissionRequest request)
    {
        var validationDataDictionary =
            (await validationResultReader.GetValidationResults())
            .ToDictionary(x => x.Id, x => x.NaughtyOrNice);


        var matchingResultIds = request.NaughtyNiceCandidateRequests.Where(x =>
            validationDataDictionary.TryGetValue(x.Id, out var naughtyOrNice) &&
            x.NaughtOrNice.Equals(naughtyOrNice)).Select(x => x.Id)
            .ToList();

        var participant = participantRepository.TryGetOrCreateParticipant(teamName, teamSecret);
        participant.AddMatchingResultIds(matchingResultIds);
            //update the repo to save the entities
        
        return participant;
        
        //check the validationResultsByIds against what the candiate has submitted and return them
    }
}
