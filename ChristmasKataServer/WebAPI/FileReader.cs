using Newtonsoft.Json;
using WebAPI.Model;

namespace WebAPI
{
    public static class FileReader
    {
        public static List<Person> GetPeopleFromFile()
        {
            List<Person> people = new();

            using (StreamReader r = new StreamReader("Data/sample-data.json"))
            {
                string json = r.ReadToEnd();
                people = JsonConvert.DeserializeObject<List<Person>>(json);
            }

            return people;
        }
    }
}
