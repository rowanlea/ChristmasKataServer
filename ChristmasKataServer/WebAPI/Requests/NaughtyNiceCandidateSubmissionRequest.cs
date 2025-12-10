using WebAPI.Model;

namespace WebAPI.Requests;

public record NaughtyNiceCandidateSubmissionRequest(List<NaughtyNiceCandidateRequest> NaughtyNiceCandidateRequests);

public record NaughtyNiceCandidateRequest(int Id, Niceness NaughtyOrNice);
