namespace WebAPI.Service;

public interface IValidationResultReader
{
    Task<List<ValidatedNaughtyOrNicePerson>> GetValidationResults();
}