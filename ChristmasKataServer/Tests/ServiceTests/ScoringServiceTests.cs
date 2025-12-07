using FluentAssertions;
using NSubstitute;
using WebAPI.Model;
using WebAPI.Requests;
using WebAPI.Service;

namespace Tests.ServiceTests;

public class ScoringServiceTests
{
    [Test]
    public async Task  GivenParticipantIsSubmittingScoreForTheFirstTime_AddScore()
    {
        var validationResultReader = Substitute.For<IValidationResultReader>();
        var validatedNaughtyOrNicePerson = new ValidatedNaughtyOrNicePerson()
        {
            Id = "1",
            NaughtyOrNice = Niceness.NICE
        };
        validationResultReader.GetValidationResults().Returns(new List<ValidatedNaughtyOrNicePerson>()
        {
            validatedNaughtyOrNicePerson
        });
        var participantRepository = Substitute.For<IParticipantRepository>();
        participantRepository.GetParticipantScore(Arg.Any<string>()).Returns(new Participant());
        
        var service = new ScoringService(validationResultReader,  participantRepository);

        var request = new NaughtyNiceCandidateSubmissionRequest("userId", new List<NaughtyNiceCandidateRequest>()
        {
            new NaughtyNiceCandidateRequest("1", Niceness.NICE)
        });
        
        var result = await service.Score("TestTeam", request);

        var participantScore = new Participant();
        participantScore.AddMatchingResultIds(["1"]);
        
        result.Should().BeEquivalentTo(participantScore);
    }
    
    //create paramtertised test


    [Test]
    public async Task GivenParticipantIsSubmittingResultsWhichTheyAlreadyHaveScoresFor_ScoreIsNotUpdatedForParticipant()
    {
        var validationResultReader = Substitute.For<IValidationResultReader>();
        var validatedNaughtyOrNicePerson = new ValidatedNaughtyOrNicePerson()
        {
            Id = "1",
            NaughtyOrNice = Niceness.NICE
        };
        
        validationResultReader.GetValidationResults().Returns(
            [validatedNaughtyOrNicePerson]);
        
        var participantRepository = Substitute.For<IParticipantRepository>();
        var retrievedParticipantScore = new Participant()
        {
            UserSecret = "UserId",
        };
        retrievedParticipantScore.CorrectIds.Add("1");
        
        participantRepository.GetParticipantScore(Arg.Any<string>()).Returns(retrievedParticipantScore);
        
        var service = new ScoringService(validationResultReader,  participantRepository);
        
        var request = new NaughtyNiceCandidateSubmissionRequest("userId", new List<NaughtyNiceCandidateRequest>()
        {
            new("1", Niceness.NICE)
        });

        var result = await service.Score("TestTeam", request);

        result.currentScore.Should().Be(1);
        result.CorrectIds.Should().BeEquivalentTo(["1"]);

    }
}