using WebAPI.Model;

namespace WebAPI.Requests;

public record NaughtyNiceCandidateSubmissionRequest(string TeamName, List<NaughtyNiceCandidateRequest> NaughtyNiceCandidateRequests);

public record NaughtyNiceCandidateRequest(int Id, Niceness NaughtOrNice);
