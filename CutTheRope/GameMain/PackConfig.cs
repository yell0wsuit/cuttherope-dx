using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

using CutTheRope.Helpers;

namespace CutTheRope.GameMain
{
    internal sealed class PackDefinition
    {
        public PackDefinition(int unlockStars, int[] packResources, int[] coverResources, int levelCount)
        {
            UnlockStars = unlockStars;
            PackResources = packResources;
            CoverResources = coverResources;
            LevelCount = levelCount;
        }

        public int UnlockStars { get; }

        public int[] PackResources { get; }

        public int[] CoverResources { get; }

        public int LevelCount { get; }
    }

    internal static class PackConfig
    {
        private static readonly int[] EmptyResources = { -1 };

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

        public static int[] GetCoverResources(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].CoverResources : EmptyResources;
        }

        public static int GetUnlockStars(int pack)
        {
            return pack >= 0 && pack < packs.Count ? packs[pack].UnlockStars : 0;
        }

        private static List<PackDefinition> LoadFromXml()
        {
            XElement root = XElementExtensions.LoadContentXml("packs.xml");
            List<PackDefinition> results = new();

            if (root == null)
            {
                return results;
            }

            foreach (XElement packElement in root.Elements("pack"))
            {
                int unlockStars = ParseIntAttribute(packElement, "unlockStars");
                int[] packResources = ParseResources(packElement, "resources");
                int[] coverResources = ParseResources(packElement, "coverResources");
                int levelCount = ParseLevelCount(packElement);

                results.Add(new PackDefinition(unlockStars, packResources, coverResources, levelCount));
            }

            return results;
        }

        private static int ParseIntAttribute(XElement element, string attributeName)
        {
            string value = element.AttributeAsNSString(attributeName);
            return string.IsNullOrWhiteSpace(value) ? 0 : int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static int[] ParseResources(XElement element, string attributeName)
        {
            string value = element.AttributeAsNSString(attributeName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return EmptyResources;
            }

            List<int> ids = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => int.Parse(part.Trim(), CultureInfo.InvariantCulture))
                .ToList();
            ids.Add(-1);
            return ids.ToArray();
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
