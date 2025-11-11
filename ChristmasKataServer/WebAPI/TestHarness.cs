using WebAPI.Model;

namespace WebAPI
{
    public class TestHarness
    {
        public string VerifyResults(List<Person> people)
        {
            int correctCount = 0;

            foreach(var person in people)
            {
                Determiner determiner = new Determiner();
                Niceness niceness = determiner.DetermineNiceness(person);

                if (niceness == person.Niceness)
                {
                    correctCount++;
                }
            }

            return $"You have found {correctCount} people successfully";
        }
    }
}
