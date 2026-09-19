using System;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    /// <summary>
    /// Covers the machine description a log opens with, which is all a bug report says about where
    /// it came from.
    /// </summary>
    public sealed class DeviceReportTests
    {
        [Fact]
        public void TheVendorsNameIsPreferredToTheNumberOfThePart()
        {
            // "Family 6 Model 142 Stepping 12" is one string across a generation of laptops, so it
            // cannot tell two reports apart; the name on the box can.
            string name = DeviceReport.PreferVendorName(
                "Intel(R) Core(TM) i5-8265U CPU @ 1.60GHz",
                "Intel64 Family 6 Model 142 Stepping 12, GenuineIntel");

            Assert.Equal("Intel(R) Core(TM) i5-8265U CPU @ 1.60GHz", name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ThePartNumberIsStillReportedWhenNoVendorNameIsFound(string vendorName)
        {
            // Every probe here is best-effort, and a registry that will not answer is worth less
            // than the environment variable, not worth less than nothing.
            string name = DeviceReport.PreferVendorName(vendorName, "ARMv8 (64-bit) Family 8 Model 0");

            Assert.Equal("ARMv8 (64-bit) Family 8 Model 0", name);
        }

        [Fact]
        public void NothingIsReportedWhenNeitherSourceAnswers()
        {
            Assert.Null(DeviceReport.PreferVendorName(null, null));
            Assert.Null(DeviceReport.PreferVendorName("  ", string.Empty));
        }

        [Fact]
        public void SurroundingSpaceIsNotCarriedIntoTheBanner()
        {
            Assert.Equal("Apple M3 Pro", DeviceReport.PreferVendorName("  Apple M3 Pro\n", null));
            Assert.Equal("Apple M3 Pro", DeviceReport.PreferVendorName(null, "\tApple M3 Pro  "));
        }

        [Fact]
        public void TheProcessorLineNamesSomethingAndCountsTheCores()
        {
            // Whatever this machine is, the line has to carry both halves: a report that says only
            // "4 cores" describes nothing, and one that never falls back describes nothing either.
            string processor = DeviceReport.Processor;

            Assert.EndsWith($"({Environment.ProcessorCount} cores)", processor, StringComparison.Ordinal);
            Assert.NotEqual($"({Environment.ProcessorCount} cores)", processor);
        }
    }
}
