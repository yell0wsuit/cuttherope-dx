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

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 4)]
        [InlineData(5, 5)]
        [InlineData(6, 6)]
        [InlineData(7, 7)]
        [InlineData(8, 8)]
        [InlineData(9, 9)]
        [InlineData(10, 10)]
        [InlineData(11, 11)]
        [InlineData(12, 0)]
        public void TimeTravelMenusSelectChairsInChapterOrder(int pack, int quad)
        {
            MenuStyle previous = MenuTheme.Current;
            MenuTheme.Current = MenuStyle.TimeTravel;
            try
            {
                Assert.Equal(("char_support_timetravel", quad), GameScene.ResolveSupport(pack, false, false, true));
                string[] resources = LevelResourceScanner.GetRequiredResources(
                    XElement.Parse("<map><gameDesign><target /></gameDesign></map>"), pack);
                Assert.Contains("char_support_timetravel", resources);
                Assert.DoesNotContain(Resources.Img.CharSupports, resources);
            }
            finally { MenuTheme.Current = previous; }
        }

        [Fact]
        public void TimeTravelChairOverrideUsesTheAtlasQuad()
        {
            PackDefinition pack = TimeTravelPackConfigurationTests.Parse(
                "\"ttSittingPlatform\":12,\"sittingPlatformTheme\":\"timetravel\"");
            TimeTravelPackConfigurationTests.WithPack(0, pack, () =>
                Assert.Equal(("char_support_timetravel", 12), GameScene.ResolveSupport(0, false, false)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(16)]
        [InlineData(99)]
        public void InvalidTimeTravelQuadFollowsChapterOrder(int quad)
        {
            PackDefinition pack = TimeTravelPackConfigurationTests.Parse($"\"ttSittingPlatform\":{quad}");
            TimeTravelPackConfigurationTests.WithPack(0, pack, () =>
                Assert.Equal(("char_support_timetravel", 0), GameScene.ResolveSupport(0, false, false, true)));
        }

        [Theory]
        [InlineData("original", Resources.Img.CharSupports)]
        [InlineData("experiments", Resources.Img.CharSupportExperiments)]
        public void ExplicitPlatformThemeWinsOverTimeTravelMenus(string theme, string resource)
        {
            PackDefinition pack = TimeTravelPackConfigurationTests.Parse($"\"sittingPlatformTheme\":\"{theme}\"");
            TimeTravelPackConfigurationTests.WithPack(0, pack, () =>
                Assert.Equal(resource, GameScene.ResolveSupport(0, false, false, true).Resource));
        }

        [Fact]
        public void PaddingtonKeepsHisSuitcaseInTimeTravel()
        {
            Assert.Equal((Resources.Img.CharSupportsXmas, 1), GameScene.ResolveSupport(0, true, false, true));
        }

        [Fact]
        public void TimeTravelSupportLoadsWithItsRealAtlasDimensions()
        {
            MenuTimeTravelTests.WithTimeTravel(1920, 1080, _ =>
            {
                CutTheRopeDX.Framework.Visual.Texture2D texture = CutTheRopeDX.Framework.Core.Application.GetTexture("char_support_timetravel");
                Assert.Equal(16, texture.quadRects.Length);
                CutTheRopeDX.Framework.Visual.Image support = CutTheRopeDX.Framework.Visual.Image.FromResource("char_support_timetravel", 0);
                support.DoRestoreCutTransparency();
                Assert.Equal(576, support.width);
                Assert.Equal(576, support.height);
            });
        }

        private static SittingPlatformTheme Theme(bool experiments)
        {
            return experiments ? SittingPlatformTheme.Experiments : SittingPlatformTheme.Original;
        }
    }
}
