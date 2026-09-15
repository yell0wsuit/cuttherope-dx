using System;

using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    /// <summary>Covers the roll that decides whether a tap on Om Nom plays the easter egg.</summary>
    public sealed class EasterEggOmNomTriggerChanceTests
    {
        private static int Hits(EasterEggOmNom egg, int rolls)
        {
            int hits = 0;
            for (int i = 0; i < rolls; i++)
            {
                if (egg.TryTrigger())
                {
                    hits++;
                    egg.Clear();
                }
            }
            return hits;
        }

        [Fact]
        public void PlaysAboutHalfTheTimeByDefault()
        {
            EasterEggOmNom egg = new(new Random(12345));

            int hits = Hits(egg, 2000);

            // Four standard deviations either side of 1000 for 2000 fair coin flips.
            Assert.InRange(hits, 910, 1090);
        }

        [Fact]
        public void AlwaysPlaysAtFullChance()
        {
            EasterEggOmNom egg = new(new Random(1)) { TriggerChance = 1f };

            Assert.Equal(100, Hits(egg, 100));
        }

        [Fact]
        public void NeverPlaysAtZeroChance()
        {
            EasterEggOmNom egg = new(new Random(1)) { TriggerChance = 0f };

            Assert.Equal(0, Hits(egg, 100));
            Assert.False(egg.IsActive);
        }

        [Fact]
        public void AWinningRollStartsTheEgg()
        {
            EasterEggOmNom egg = new(new Random(1)) { TriggerChance = 1f };

            Assert.True(egg.TryTrigger());

            Assert.True(egg.IsActive);
        }
    }
}
