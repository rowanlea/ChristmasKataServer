using Microsoft.AspNetCore.Http.HttpResults;
using WebAPI.Model;

namespace WebAPI
{
    public class Determiner
    {
        public Niceness DetermineNiceness(Person person)
        {
            int morality = 0;

            if (person.favourite_katas.Contains("MARSROVER"))
            {
                return Niceness.NAUGHTY;
            }

            if (person.name == "Santa Claus" || person.age < 3)
            {
                return Niceness.NICE;
            }

            if (person.location == "North Pole")
            {
                morality += 50;
            }

            int goodCounter = 0;
            int badCounter = 0;

            foreach (var occurence in person.events)
            {
                if ((int)occurence.type <= 10)
                {
                    goodCounter++;
                    morality++;
                }
                else
                {
                    if (!occurence.has_apologised)
                    {
                        badCounter++;
                        morality--;
                    }
                    morality--;
                }
            }

            if (badCounter > goodCounter)
            {
                return Niceness.NAUGHTY;
            }
            return morality > 20 ? Niceness.NAUGHTY : Niceness.NICE;
        }

        public void EnrichNiceness(Person person)
        {
            person.Niceness = DetermineNiceness(person);
        }
    }
}
