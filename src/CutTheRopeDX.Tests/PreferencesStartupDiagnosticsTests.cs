using System.Collections.Generic;

using CutTheRopeDX.Framework.Core;

using Microsoft.Extensions.Logging;

using Xunit;

namespace CutTheRopeDX.Tests
{
    public class PreferencesStartupDiagnosticsTests
    {
        [Fact]
        public void ResolvingTheSaveDirectoryRecordsWhereItLanded()
        {
            // The property caches, and only the resolution that populates it is diagnosed, so
            // this forces a fresh one rather than depending on being first to touch it.
            Preferences.ForgetSaveDirectory();
            _ = Preferences.DrainStartupDiagnostics();

            string directory = Preferences.SaveDirectory;

            IReadOnlyList<Preferences.StartupDiagnostic> drained = Preferences.DrainStartupDiagnostics();
            Assert.Contains(drained, entry =>
                entry.Level == LogLevel.Information && entry.Message.Contains(directory));
        }

        [Fact]
        public void DrainingTwiceReturnsTheRecordsOnce()
        {
            _ = Preferences.SaveDirectory;
            _ = Preferences.DrainStartupDiagnostics();

            Assert.Empty(Preferences.DrainStartupDiagnostics());
        }
    }
}
