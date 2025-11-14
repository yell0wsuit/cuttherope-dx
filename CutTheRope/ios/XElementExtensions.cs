using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using CutTheRope.game;

using Microsoft.Xna.Framework;

namespace CutTheRope.ios
{
    internal static class XElementExtensions
    {
        public static XElement LoadContentXml(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            XDocument document = null;

            try
            {
                using Stream stream = TitleContainer.OpenStream($"content/{ResDataPhoneFull.ContentFolder}{fileName}");
                document = XDocument.Load(stream);
            }
            catch (Exception)
            {
            }

            if (document == null)
            {
                string inlineXml = ResDataPhoneFull.GetXml(fileName);
                if (!string.IsNullOrEmpty(inlineXml))
                {
                    document = XDocument.Parse(inlineXml);
                }
            }

            return document?.Root;
        }

        public static XElement FindChildWithTagNameRecursively(this XElement element, NSString tag, bool recursively)
        {
            return element.FindChildWithTagNameRecursively(tag?.ToString(), recursively);
        }

        public static XElement FindChildWithTagNameRecursively(this XElement element, string tag, bool recursively)
        {
            if (element == null || string.IsNullOrEmpty(tag))
            {
                return null;
            }

            foreach (XElement child in element.Elements())
            {
                if (string.Equals(child.Name.LocalName, tag, StringComparison.Ordinal))
                {
                    return child;
                }

                if (recursively)
                {
                    XElement descendant = child.FindChildWithTagNameRecursively(tag, true);
                    if (descendant != null)
                    {
                        return descendant;
                    }
                }
            }

            return null;
        }

        public static NSString AttributeAsNSString(this XElement element, string attributeName)
        {
            return new NSString(element?.Attribute(attributeName)?.Value ?? string.Empty);
        }

        public static NSString ValueAsNSString(this XElement element)
        {
            return new NSString(element?.Value ?? string.Empty);
        }
    }
}
