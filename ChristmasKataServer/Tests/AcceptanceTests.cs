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
            foreach (var person in people)
            {
                determiner.EnrichNiceness(person);
            }

            // Act
            var result = testHarness.VerifyResults(people);

            // Assert
            result.Should().Be("You have found 20 people successfully");
        }
    }
}