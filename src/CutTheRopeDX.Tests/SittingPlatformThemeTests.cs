using System.Xml.Linq;

using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class SittingPlatformThemeTests
    {
        [Theory]
        [InlineData("original", false)]
        [InlineData("experiments", true)]
        [InlineData("Experiments", true)]
        public void ThePackEntryNamesAPlatformSet(string value, bool experiments)
        {
            Assert.Equal(Theme(experiments), PackConfig.ParseSittingPlatformTheme(value, "test_packs.json"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("experimental")]
        public void AMissingOrUnknownSetFollowsTheMenus(string value)
        {
            Assert.Null(PackConfig.ParseSittingPlatformTheme(value, "test_packs.json"));
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void TheConfiguredSetWinsOverTheMenus(bool configuredExperiments, bool isExperiments)
        {
            SittingPlatformTheme configured = Theme(configuredExperiments);
            Assert.Equal(configured, GameScene.ResolveSupportTheme(configured, isExperiments));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AnUnsetPackSitsOnThePlatformsOfTheMenus(bool isExperiments)
        {
            Assert.Equal(Theme(isExperiments), GameScene.ResolveSupportTheme(null, isExperiments));
        }

        [Theory]
        [InlineData(false, Resources.Img.CharSupports, Resources.Img.CharSupportExperiments)]
        [InlineData(true, Resources.Img.CharSupportExperiments, Resources.Img.CharSupports)]
        public void TheScannerPreloadsTheSheetTheTargetDrawsFrom(bool experimentsMenus, string drawn, string unused)
        {
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = experimentsMenus ? MenuStyle.Experiments : MenuStyle.Classic;
            try
            {
                string[] resources = LevelResourceScanner.GetRequiredResources(
                    XElement.Parse("<map><gameDesign><target /></gameDesign></map>"), 0);

                Assert.Equal(drawn, GameScene.ResolveSupport(0, isPaddington: false, MenuTheme.IsExperiments).Resource);
                Assert.Contains(drawn, resources);
                Assert.DoesNotContain(unused, resources);
            }
            finally
            {
                MenuTheme.Current = previous;
            }
        }

        private static SittingPlatformTheme Theme(bool experiments)
        {
            return experiments ? SittingPlatformTheme.Experiments : SittingPlatformTheme.Original;
        }
    }
}
