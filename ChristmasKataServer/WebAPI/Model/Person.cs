namespace WebAPI.Model
{
    public enum Niceness
    {
        None = 0,
        NAUGHTY = 1,
        NICE = 2,
        GREYAREA
    }

    public enum EventType
    {
        HELPED_SOMEONE = 1,
        DONATED_TO_CHARITY = 2,
        WAVED_AT_NEIGHBOR = 3,
        HELPED_OLD_LADY_CROSS_STREET = 4,
        ATE_GREEN_VEGETABLES = 5,
        SANG_IN_SHOWER = 6,
        DANCED_LIKE_NO_ONE_WAS_WATCHING = 7,
        TOLD_A_FUNNY_JOKE = 8,
        MADE_A_FUNNY_FACE = 9,
        RESCUED_A_KITTEN = 10,
        MADE_MESS = 11,
        BROKE_SOMETHING = 12,
        LIED = 13,
        STOLE_SOMETHING = 14,
        CHEATED = 15,
        WAS_MEAN = 16,
        LEFT_TOILET_SEAT_UP = 17,
        DID_A_MASSIVE_FART_AND_BLAMED_DOG = 18,
        FLIPPED_OFF_A_COP = 19,
        LAUGHED_AT_OWN_JOKE = 20
    }

    public class Event
    {
        public EventType type { get; set; }
        public DateTime date { get; set; }
        public bool has_apologised { get; set; }
    }

    public class Person
    {
        public int id { get; set; }
        public string name { get; set; }
        public int age { get; set; }
        public string location { get; set; }
        public string occupation { get; set; }
        public string[] favourite_katas { get; set; }
        public Event[] events { get; set; }
        public Niceness Niceness { get; set; }
    }
}
