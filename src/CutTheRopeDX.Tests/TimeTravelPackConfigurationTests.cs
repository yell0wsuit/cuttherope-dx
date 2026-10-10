using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using CutTheRopeDX.GameMain;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public sealed class TimeTravelPackConfigurationTests
    {
        [Fact]
        public void OmittedPresetsKeepAutomaticArtworkAndClassicPackFields()
        {
            PackDefinition pack = Parse();
            Assert.Equal(-1, pack.TimeTravelPackPicture);
            Assert.Equal(-1, pack.TimeTravelPackBackground);
            Assert.Equal(Resources.Img.MenuPackSelection, pack.PackSpritesheet);
            Assert.Equal("bgr_01_p1", pack.BoxBackgrounds[0]);
            Assert.Equal("bgr_01_cover", pack.BoxCovers[0]);
        }

        [Fact]
        public void IconAndBackgroundPresetsAreIndependent()
        {
            PackDefinition pack = Parse("\"ttPackPicture\":8,\"ttPackBackground\":4");
            Assert.Equal(8, pack.TimeTravelPackPicture);
            Assert.Equal(4, pack.TimeTravelPackBackground);
            Assert.Equal(Resources.Img.MenuPackSelection, pack.PackSpritesheet);
            Assert.Equal(0, pack.PackQuadIndex);
        }

        [Theory]
        [InlineData("\"ttPackPicture\":\"invalid\"")]
        [InlineData("\"ttPackBackground\":true")]
        public void MalformedPresetTypesAreRejected(string fields)
        {
            _ = Assert.Throws<InvalidDataException>(() => Parse(fields));
        }

        internal static PackDefinition Parse(string artworkFields = "")
        {
            using JsonDocument json = JsonDocument.Parse($$"""
                {"packName":"BOX1_LABEL", "levelCount":25, "unlockStars":0,
                 "boxBackground":["bgr_01_p1"], "boxCover":["bgr_01_cover"],
                 "musicPack":["ctr_original"], "packSpritesheet":"1", "packQuadIndex":0,
                 "sittingPlatform":0 {{(artworkFields.Length > 0 ? "," + artworkFields : "")}}}
                """);
            return PackConfig.ParsePackDefinition(json.RootElement, "test_packs.json", 0, false);
        }

        internal static void WithPack(int index, PackDefinition replacement, Action body)
        {
            List<PackDefinition> packs = (List<PackDefinition>)PackConfig.Packs;
            PackDefinition previous = packs[index];
            packs[index] = replacement;
            try
            {
                body();
            }
            finally
            {
                packs[index] = previous;
            }
        }
    }
}
