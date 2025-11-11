using Microsoft.AspNetCore.Http.HttpResults;
using WebAPI.Model;

namespace WebAPI
{
    public class Determiner
    {
        public Niceness DetermineNiceness(Person person)
        {
            int morality = 0;

            foreach (var occurence in person.events)
            {
                // Ideas
                // For some events, if you've apologised for it, the morality isn't affected
                // For some, if you haven't done it for a while it's also not counted
                // For some, if you have done it recently, all previous instances count
                // For some, if you apologised but then did it after then no apologies count
                // For some, if you apologised it lowers it but not entirely
                // Apologising for laughing at your own joke actually gives you +1 - might be too complex
                // Flipping off a cop gives you +1, unless you apologise then it gives you -1 - might be too complex
                // For some, the more it's done the more it multiplies the affect of it
                switch (occurence.type)
                {
                    case EventType.HELPED_SOMEONE:
                    case EventType.DONATED_TO_CHARITY:
                    case EventType.WAVED_AT_NEIGHBOR:
                    case EventType.HELPED_OLD_LADY_CROSS_STREET:
                    case EventType.ATE_GREEN_VEGETABLES:
                    case EventType.SANG_IN_SHOWER:
                    case EventType.DANCED_LIKE_NO_ONE_WAS_WATCHING:
                    case EventType.TOLD_A_FUNNY_JOKE:
                        morality += 1;
                        break;
                    case EventType.MADE_A_FUNNY_FACE:
                    case EventType.RESCUED_A_KITTEN:
                        morality += 1;
                        break;
                    case EventType.MADE_MESS:
                    case EventType.BROKE_SOMETHING:
                    case EventType.LAUGHED_AT_OWN_JOKE:
                        morality -= 1;
                        break;
                    case EventType.LEFT_TOILET_SEAT_UP:
                    case EventType.DID_A_MASSIVE_FART_AND_BLAMED_DOG:
                        morality -= 2;
                        break;
                    case EventType.LIED:
                    case EventType.WAS_MEAN:
                        morality -= 3;
                        break;
                    case EventType.STOLE_SOMETHING:
                    case EventType.CHEATED:
                        morality -= 5;
                        break;
                    case EventType.FLIPPED_OFF_A_COP:
                        morality += 1;
                        break;
                }
            }
                return morality > 0? Niceness.NAUGHTY : Niceness.NICE;
        }

        public void EnrichNiceness(Person person)
        {
            person.Niceness = DetermineNiceness(person);
        }
    }
}
