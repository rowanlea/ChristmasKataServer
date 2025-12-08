using WebAPI.Model;

namespace WebAPI.Service;

public class ValidatedNaughtyOrNicePerson
{
    public int Id { get; set; }
    public Niceness NaughtyOrNice { get; set; }
}