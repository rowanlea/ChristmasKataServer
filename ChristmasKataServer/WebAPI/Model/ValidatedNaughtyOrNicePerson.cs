using WebAPI.Model;

namespace WebAPI.Service;

public class ValidatedNaughtyOrNicePerson
{
    public string Id { get; set; }
    public Niceness NaughtyOrNice { get; set; }
}