using WebAPI.Model;

namespace WebAPI.Service;

public class FakeValidationResultReader : IValidationResultReader
{
    public Task<List<ValidatedNaughtyOrNicePerson>> GetValidationResults()
    {
        var result = new List<ValidatedNaughtyOrNicePerson>()
        {
            new ValidatedNaughtyOrNicePerson()
            {
                Id = "1",
                NaughtyOrNice = Niceness.NAUGHTY,
            }
        };
        
        return Task.FromResult(result);
    }
}