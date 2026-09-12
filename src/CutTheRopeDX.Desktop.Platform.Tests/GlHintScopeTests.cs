using System;
using System.Collections.Generic;

using CutTheRopeDX.Desktop.Platform.Graphics;

using SDL3;

using Xunit;

namespace CutTheRopeDX.Desktop.Platform.Tests
{
    /// <summary>What a renderer attempt leaves behind in SDL's global hints.</summary>
    public sealed class GlHintScopeTests
    {
        private const string Name = "CTRDX_TEST_HINT";

        [Fact]
        public void AHintWithNoPreviousValueIsUnsetAgain()
        {
            _ = SDL.ResetHint(Name);

            using (GlHintScope scope = new())
            {
                scope.Set(Name, "angle");
                Assert.Equal("angle", SDL.GetHint(Name));
            }

            Assert.Null(SDL.GetHint(Name));
        }

        [Fact]
        public void AHintThatAlreadyHadAValueGetsItBack()
        {
            _ = SDL.SetHint(Name, "original");

            using (GlHintScope scope = new())
            {
                scope.Set(Name, "angle");
                Assert.Equal("angle", SDL.GetHint(Name));
            }

            Assert.Equal("original", SDL.GetHint(Name));
            _ = SDL.ResetHint(Name);
        }

        [Fact]
        public void RestoringTwiceChangesNothingTheSecondTime()
        {
            _ = SDL.ResetHint(Name);
            GlHintScope scope = new();
            scope.Set(Name, "angle");

            scope.Dispose();
            _ = SDL.SetHint(Name, "set by someone else");
            scope.Dispose();

            Assert.Equal("set by someone else", SDL.GetHint(Name));
            _ = SDL.ResetHint(Name);
        }

        [Fact]
        public void AHintSdlRefusesFailsTheAttemptRatherThanPassingSilently()
        {
            GlHintScope scope = new(_ => null, (_, _) => false, _ => true);

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
                () => scope.Set(SDL.Hints.OpenGLLibrary, "/opt/angle/libGLESv2.dll"));

            Assert.Contains(SDL.Hints.OpenGLLibrary, failure.Message);
        }

        [Fact]
        public void ARefusedHintIsNotRestoredBecauseItWasNeverChanged()
        {
            List<string> reset = [];
            GlHintScope scope = new(_ => "from the environment", (_, _) => false, name =>
            {
                reset.Add(name);
                return true;
            });

            _ = Assert.Throws<InvalidOperationException>(
                () => scope.Set(SDL.Hints.OpenGLLibrary, "/opt/angle/libGLESv2.dll"));
            scope.Dispose();

            Assert.Empty(reset);
        }

        [Fact]
        public void EarlierHintsAreStillPutBackWhenALaterOneIsRefused()
        {
            Dictionary<string, string> live = new() { [SDL.Hints.OpenGLESDriver] = null };
            List<string> reset = [];
            GlHintScope scope = new(
                name => live.TryGetValue(name, out string value) ? value : null,
                (name, value) =>
                {
                    if (name == SDL.Hints.OpenGLLibrary)
                    {
                        return false;
                    }

                    live[name] = value;
                    return true;
                },
                name =>
                {
                    reset.Add(name);
                    return true;
                });

            scope.Set(SDL.Hints.OpenGLESDriver, "1");
            _ = Assert.Throws<InvalidOperationException>(() => scope.Set(SDL.Hints.OpenGLLibrary, "x"));
            scope.Dispose();

            Assert.Equal([SDL.Hints.OpenGLESDriver], reset);
        }
    }
}
