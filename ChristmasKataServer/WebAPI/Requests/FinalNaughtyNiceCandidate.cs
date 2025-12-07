using WebAPI.Model;

namespace WebAPI.Requests;

public record NaughtyNiceCandidateSubmissionRequest(string UserId, List<NaughtyNiceCandidateRequest> NaughtyNiceCandidateRequests);

public record NaughtyNiceCandidateRequest(string Id, Niceness NaughtOrNice);
