using FluentAssertions;
using WebAPI;

namespace Tests
{
    public class Tests
    {
        [Test]
        public void AcceptanceTest()
        {
            // Arrange
            TestHarness testHarness = new();
            Determiner determiner = new ();
            var people = FileReader.GetPeopleFromFile();

            // Act
            foreach (var person in people)
            {
                determiner.EnrichNiceness(person);
            }

            var nicePeople = people.Where(p => p.Niceness == WebAPI.Model.Niceness.NICE);

            // Assert
            nicePeople.Count().Should().Be(5000);
        }
    }
}