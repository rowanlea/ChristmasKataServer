using WebAPI.Model;

namespace WebAPI
{
    public class TestHarness
    {
        public string VerifyResults(List<Person> people)
        {
            int correctCount = 0;
            Determiner determiner = new Determiner();
            foreach (var person in people)
            {
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
