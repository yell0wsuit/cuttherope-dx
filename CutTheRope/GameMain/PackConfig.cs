using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using CutTheRope.Helpers;

namespace CutTheRope.GameMain
{
    /// <summary>
    /// Immutable pack description combining legacy numeric IDs with translated string names.
    /// </summary>
    internal sealed class PackDefinition(
        int unlockStars,
        int[] packResources,
        int supportResources,
        int[] coverResources,
        int levelCount,
        string[] packResourceNames,
        string supportResourceName,
        string[] coverResourceNames)
    {
        /// <summary>Number of stars required to unlock this pack.</summary>
        public int UnlockStars { get; } = unlockStars;

        /// <summary>Legacy numeric resource identifiers for pack assets.</summary>
        public int[] PackResources { get; } = packResources;

        /// <summary>String resource names for pack assets.</summary>
        public string[] PackResourceNames { get; } = packResourceNames;

        /// <summary>Legacy numeric support resource identifier.</summary>
        public int SupportResources { get; } = supportResources;

        /// <summary>String resource name for the support asset.</summary>
        public string SupportResourceName { get; } = supportResourceName;

        /// <summary>Legacy numeric identifiers for cover assets.</summary>
        public int[] CoverResources { get; } = coverResources;

        /// <summary>String resource names for cover assets.</summary>
        public string[] CoverResourceNames { get; } = coverResourceNames;

        /// <summary>Total number of levels in the pack.</summary>
        public int LevelCount { get; } = levelCount;
    }

    /// <summary>
    /// Loads pack metadata from <c>packs.xml</c> and exposes legacy IDs alongside string resource names.
    /// </summary>
    internal static class PackConfig
    {
        private static readonly int[] EmptyResources = [-1];
        private static readonly string[] EmptyResourceNames = [null];

        private static readonly List<PackDefinition> packs;

        static PackConfig()
        {
            packs = LoadFromXml();
            MaxLevelsPerPack = packs.Count > 0 ? packs.Max(p => p.LevelCount) : 0;
        }

        public static IReadOnlyList<PackDefinition> Packs => packs;

        public static int MaxLevelsPerPack { get; }

        public static int GetPackCount()
        {
            return packs.Count;
        }

        public static int GetLevelCount(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].LevelCount : 0;
        }

        public static int[] GetPackResources(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].PackResources : EmptyResources;
        }

        public static string[] GetPackResourceNames(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].PackResourceNames : EmptyResourceNames;
        }

        public static int[] GetCoverResources(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].CoverResources : EmptyResources;
        }

        public static string[] GetCoverResourceNames(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].CoverResourceNames : EmptyResourceNames;
        }

        /// <summary>
        /// Returns the first available cover resource name for a pack or the legacy translated fallback.
        /// </summary>
        /// <param name="pack">Target pack index.</param>
        public static string GetCoverResourceNameOrDefault(int pack)
        {
            string coverResourceName = GetCoverResourceNames(pack).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

            if (string.IsNullOrEmpty(coverResourceName))
            {
                int legacyCoverId = 126 + pack;
                coverResourceName = ResourceNameTranslator.TranslateLegacyId(legacyCoverId);
            }

            return coverResourceName;
        }

        public static int GetSupportResources(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].SupportResources : 100;
        }

        public static string GetSupportResourceName(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].SupportResourceName : null;
        }

        public static int GetUnlockStars(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].UnlockStars : 0;
        }

        private static List<PackDefinition> LoadFromXml()
        {
            XElement root = XElementExtensions.LoadContentXml("packs.xml");
            List<PackDefinition> results = [];

            if (root == null)
            {
                return results;
            }

            foreach (XElement packElement in root.Elements("pack"))
            {
                int unlockStars = ParseIntAttribute(packElement, "unlockStars");
                int[] packResources = ParseResources(packElement, "resources");
                int supportResources = ParseIntAttribute(packElement, "supportResources", 100);
                int[] coverResources = ParseResources(packElement, "coverResources");
                int levelCount = ParseLevelCount(packElement);

                string[] packResourceNames = ResourceNameTranslator.TranslateLegacyPack(packResources);
                string supportResourceName = ResourceNameTranslator.TranslateLegacyId(supportResources) ?? string.Empty;
                string[] coverResourceNames = ResourceNameTranslator.TranslateLegacyPack(coverResources);

                results.Add(new PackDefinition(
                    unlockStars,
                    packResources,
                    supportResources,
                    coverResources,
                    levelCount,
                    packResourceNames,
                    supportResourceName,
                    coverResourceNames));
            }

            return results;
        }

        private static int ParseIntAttribute(XElement element, string attributeName, int defaultValue = 0)
        {
            string value = element.AttributeAsNSString(attributeName);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static int[] ParseResources(XElement element, string attributeName)
        {
            string value = element.AttributeAsNSString(attributeName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return EmptyResources;
            }

            List<int> ids = [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(part => int.Parse(part.Trim(), CultureInfo.InvariantCulture))];
            ids.Add(-1);
            return [.. ids];
        }

        private static int ParseLevelCount(XElement element)
        {
            string attributeValue = element.AttributeAsNSString("levelCount");
            if (!string.IsNullOrWhiteSpace(attributeValue))
            {
                return int.Parse(attributeValue, CultureInfo.InvariantCulture);
            }

            string elementValue = element.Element("levelCount")?.Value;
            return string.IsNullOrWhiteSpace(elementValue) ? 0 : int.Parse(elementValue, CultureInfo.InvariantCulture);
        }
    }
}
