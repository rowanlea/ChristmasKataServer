using FluentAssertions;
using NSubstitute;
using WebAPI.Model;
using WebAPI.Requests;
using WebAPI.Service;

namespace Tests.ServiceTests;

public class ScoringServiceTests
{
    private const string TeamName = "TestTeam";

    [Test]
    public async Task  GivenParticipantIsSubmittingScoreForTheFirstTime_AddScore()
    {
        var validationResultReader = Substitute.For<IValidationResultReader>();
        var validatedNaughtyOrNicePerson = new ValidatedNaughtyOrNicePerson()
        {
            Id = 1,
            NaughtyOrNice = Niceness.NICE
        };
        validationResultReader.GetValidationResults().Returns(new List<ValidatedNaughtyOrNicePerson>()
        {
            validatedNaughtyOrNicePerson
        });
        var participantRepository = Substitute.For<IParticipantRepository>();
        participantRepository.GetParticipantBy(Arg.Any<string>()).Returns(new Participant()
        {
            TeamName = TeamName,
        });
        
        var service = new ScoringService(validationResultReader,  participantRepository);

        var request = new NaughtyNiceCandidateSubmissionRequest(new List<NaughtyNiceCandidateRequest>()
        {
            new NaughtyNiceCandidateRequest(1, Niceness.NICE)
        });
        
        var result = await service.Score(TeamName, request);
        
        result.Should().BeEquivalentTo(new ParticipantScore(1, [1]));
    }
    
    [Test]
    public async Task GivenParticipantIsSubmittingResultsWhichTheyAlreadyHaveScoresFor_ScoreIsNotUpdatedForParticipant()
    {
        var validationResultReader = Substitute.For<IValidationResultReader>();
        var validatedNaughtyOrNicePerson = new ValidatedNaughtyOrNicePerson()
        {
            Id = 1,
            NaughtyOrNice = Niceness.NICE
        };
        
        validationResultReader.GetValidationResults().Returns(
            [validatedNaughtyOrNicePerson]);
        
        var participantRepository = Substitute.For<IParticipantRepository>();
        var retrievedParticipantScore = new Participant()
        {
            TeamName = TeamName,
        };
        
        participantRepository.GetParticipantBy(Arg.Any<string>()).Returns(retrievedParticipantScore);
        
        var service = new ScoringService(validationResultReader,  participantRepository);
        
        var request = new NaughtyNiceCandidateSubmissionRequest([new(1, Niceness.NICE)]);

        var result = await service.Score(TeamName, request);

        result.Should().BeEquivalentTo(new ParticipantScore(1, [1]));
    }

    [Test]
    public async Task GivenUserIsSubmittingTwoOfTheSameValidationData_ItFiltersOutDuplicateToPreventAddingTheScores()
    {
        var validationResultReader = Substitute.For<IValidationResultReader>();
        var validatedNaughtyOrNicePerson = new ValidatedNaughtyOrNicePerson()
        {
            Id = 1,
            NaughtyOrNice = Niceness.NICE
        };
        
        validationResultReader.GetValidationResults().Returns(
            [validatedNaughtyOrNicePerson]);
        
        var participantRepository = Substitute.For<IParticipantRepository>();
        var retrievedParticipantScore = new Participant()
        {
            TeamName = TeamName,
        };
        
        participantRepository.GetParticipantBy(Arg.Any<string>()).Returns(retrievedParticipantScore);
        
        var service = new ScoringService(validationResultReader,  participantRepository);
        
        var request = new NaughtyNiceCandidateSubmissionRequest([
            new(1, Niceness.NICE), 
            new(1, Niceness.NICE)]);

        var result = await service.Score(TeamName, request);

        result.Should().BeEquivalentTo(new ParticipantScore(1, [1]));
    }

    [Test]
    public async Task GivenUserIsSubmitsDataAndGetsAPoint_AndThenSubmitsAgainAndIsWrongScoreDoesNotChange()
    {
        var validationResultReader = Substitute.For<IValidationResultReader>();
        var validatedNaughtyOrNicePerson = new ValidatedNaughtyOrNicePerson()
        {
            Id = 1,
            NaughtyOrNice = Niceness.NICE
        };
        
        validationResultReader.GetValidationResults().Returns(
            [validatedNaughtyOrNicePerson]);
        
        var participantRepository = Substitute.For<IParticipantRepository>();
        var retrievedParticipantScore = new Participant()
        {
            TeamName = TeamName,
        };
        
        participantRepository.GetParticipantBy(Arg.Any<string>()).Returns(retrievedParticipantScore);
        
        var service = new ScoringService(validationResultReader,  participantRepository);
        
        var request = new NaughtyNiceCandidateSubmissionRequest([
            new(1, Niceness.NICE)]);
        
        var result = await service.Score(TeamName, request);

        result.Should().BeEquivalentTo(new ParticipantScore(1, [1]));
        
        var requestSubmittingWithIncorrectData = new NaughtyNiceCandidateSubmissionRequest([
            new(1, Niceness.NAUGHTY)]);
        await service.Score(TeamName, requestSubmittingWithIncorrectData);
        
        result.Should().BeEquivalentTo(new ParticipantScore(1, [1]));
        
    }

    [Test]
    public async Task Test()
    {
        var validationResultReader = Substitute.For<IValidationResultReader>();
        var validatedNaughtyOrNicePerson = new ValidatedNaughtyOrNicePerson()
        {
            Id = 1,
            NaughtyOrNice = Niceness.NICE
        };
        var secondValidatedNaughtOrNice = new ValidatedNaughtyOrNicePerson()
        {
            Id = 2,
            NaughtyOrNice = Niceness.NAUGHTY
        };
        
        validationResultReader.GetValidationResults().Returns(
            [validatedNaughtyOrNicePerson, secondValidatedNaughtOrNice]);
        
        var participantRepository = Substitute.For<IParticipantRepository>();
        var retrievedParticipantScore = new Participant
        {
            TeamName = TeamName,
        };
        
        participantRepository.GetParticipantBy(TeamName).Returns(_ => retrievedParticipantScore);
        
        var service = new ScoringService(validationResultReader,  participantRepository);
        
        var request = new NaughtyNiceCandidateSubmissionRequest([
            new(1, Niceness.NICE)]);
        
        var result = await service.Score(TeamName, request);
        
        result.Should().BeEquivalentTo(new ParticipantScore(1, [1]));
        
        var requestWithAdditionalData = new NaughtyNiceCandidateSubmissionRequest([
            new(1, Niceness.NICE),
            new (2, Niceness.NAUGHTY)
        ]);
        
       var finalResult =  await service.Score(TeamName, requestWithAdditionalData);
        
        finalResult.Should().BeEquivalentTo(new ParticipantScore(2, [1, 2]));

    }

    [Test]
    public async Task Test2()
    {
        var validationResultReader = Substitute.For<IValidationResultReader>();
        var validatedNaughtyOrNicePerson = new ValidatedNaughtyOrNicePerson()
        {
            Id = 1,
            NaughtyOrNice = Niceness.NICE
        };
        var secondValidatedNaughtOrNice = new ValidatedNaughtyOrNicePerson()
        {
            Id = 2,
            NaughtyOrNice = Niceness.NAUGHTY
        };
        
        validationResultReader.GetValidationResults().Returns(
            [validatedNaughtyOrNicePerson, secondValidatedNaughtOrNice]);
        
        var participantRepository = Substitute.For<IParticipantRepository>();
        var retrievedParticipantScore = new Participant
        {
            TeamName = TeamName,
        };
        
        participantRepository.GetParticipantBy(TeamName).Returns(_ => retrievedParticipantScore);
        
        var service = new ScoringService(validationResultReader,  participantRepository);
        
        var request = new NaughtyNiceCandidateSubmissionRequest([
            new(1, Niceness.NICE)]);
        
        var result = await service.Score(TeamName, request);
        
        result.Should().BeEquivalentTo(new ParticipantScore(1, [1]));
        
        var requestWithAdditionalData = new NaughtyNiceCandidateSubmissionRequest([
            new(1, Niceness.NAUGHTY),
            new (2, Niceness.NAUGHTY)
        ]);
        
        var finalResult =  await service.Score(TeamName, requestWithAdditionalData);
        
        finalResult.Should().BeEquivalentTo(new ParticipantScore(1, [2]));
    }
}