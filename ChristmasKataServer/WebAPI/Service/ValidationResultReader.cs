using System.Reflection;
using System.Text.Json;
using WebAPI.Model;

namespace WebAPI.Service;

public class ValidationResultReader : IValidationResultReader
{
    public async Task<List<ValidatedNaughtyOrNicePerson>> GetValidationResults()
    {
        await using var fs = File.OpenRead("data/validation-data.json");
        var validationData = await JsonSerializer.DeserializeAsync<List<Person>>(fs);

        var determinator = new Determiner();
        
        var results = validationData!.Select(x =>
        {
            determinator.EnrichNiceness(x);
            return new ValidatedNaughtyOrNicePerson
            {
                Id = x.id,
                NaughtyOrNice = x.Niceness,
            };
        }).ToList();
        
        return results!;
    }
    
}