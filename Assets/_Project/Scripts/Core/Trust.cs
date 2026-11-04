namespace Subnet.Core
{
    /// <summary>
    /// The two hidden values from the author's bible. Never shown on screen (only in the dev panel).
    ///  - Closeness (with Byte and Kira): listening to the end, talking in the quiet moments, petting Byte, accepting help.
    ///  - Openness (Leo's): listening to Mia's voicemails, picking vulnerable answers over ironic ones, staying in silences.
    /// Raise them from any flag field with "+closeness" / "+openness" (see StoryFlags.Set).
    /// Later acts read them: companion lines and endings (Closeness), hacking arena and the final call (Openness).
    /// </summary>
    public static class Trust
    {
        public const string ClosenessKey = "closeness";
        public const string OpennessKey = "openness";

        public static int Closeness => StoryFlags.GetCount(ClosenessKey);
        public static int Openness => StoryFlags.GetCount(OpennessKey);

        public static void AddCloseness() => StoryFlags.Increment(ClosenessKey);
        public static void AddOpenness() => StoryFlags.Increment(OpennessKey);

        /// <summary>Rough tiers used by later content: 0 = closed, 1 = warming, 2 = open.</summary>
        public static int Tier(int value) => value >= 5 ? 2 : value >= 2 ? 1 : 0;
    }
}
