using System;
using System.Reflection;

using Xunit;

namespace CutTheRopeDX.Desktop.Tests
{
    public sealed class CrashDialogTextTests
    {
        [Fact]
        public void LongMessagesWrapAndPreserveParagraphs()
        {
            string input = "The game stopped unexpectedly.\n\n" + new string('x', 180);
            string result = Format(input);
            Assert.Contains("unexpectedly.\n\n", result);
            Assert.All(result.Split('\n'), line => Assert.True(line.Length <= 80));
            Assert.Equal(input.Replace("\n", ""), result.Replace("\n", ""));
        }

        [Fact]
        public void WrappingPrefersWordBoundaries()
        {
            string prefix = new('a', 75);
            Assert.Equal(prefix + "\nhello world", Format(prefix + " hello world"));
        }

        [Fact]
        public void NestedEmptyAggregatesStillProduceADialog()
        {
            AggregateException failure = new("Startup failed", new AggregateException());
            string result = (string)typeof(CrashHandlers)
                .GetMethod("Describe", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [failure]);
            Assert.Contains("Startup failed", result);
        }

        [Fact]
        public void AggregateDetailsAreNotRepeatedInTheDialog()
        {
            const string reason = "Skia rejected this Vulkan device.";
            AggregateException failure = new("Passed over: Vulkan (" + reason + ")",
                new InvalidOperationException(reason));
            string result = (string)typeof(CrashHandlers)
                .GetMethod("Describe", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [failure]);
            Assert.Equal(result.IndexOf(reason, StringComparison.Ordinal), result.LastIndexOf(reason, StringComparison.Ordinal));
            Assert.Contains(reason, result);
        }

        private static string Format(string message)
        {
            return NativeMessageBox.WrapMessage(message);
        }
    }
}
