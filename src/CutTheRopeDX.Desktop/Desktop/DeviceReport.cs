using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CutTheRopeDX.Desktop
{
    /// <summary>
    /// Describes the machine a log came from.
    /// </summary>
    /// <remarks>
    /// A report that says the game ran badly is worth little without this. Every field is
    /// best-effort: a probe that will not answer on some platform reports what it does know
    /// rather than failing the startup that was only trying to describe itself.
    /// </remarks>
    internal static partial class DeviceReport
    {
        /// <summary>Longest sysctl answer worth reading; a brand string is far shorter.</summary>
        private const int LongestSysctlValue = 512;

        private const long BytesPerMegabyte = 1024 * 1024;

        /// <summary>Gets the operating system and the architecture it runs the game as.</summary>
        public static string OperatingSystem =>
            $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

        /// <summary>Gets the processor model, with the core count the runtime sees.</summary>
        public static string Processor =>
            $"{ProcessorModel()} ({Environment.ProcessorCount} cores)";

        /// <summary>
        /// Gets the total memory available to the process, in megabytes.
        /// </summary>
        /// <remarks>
        /// What the collector believes it may use, which is physical memory on a desktop and the
        /// cap on a container. The second is the more useful answer when the two differ.
        /// </remarks>
        public static long TotalMemoryMegabytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / BytesPerMegabyte;

        /// <summary>
        /// Names the processor as its own platform reports it.
        /// </summary>
        /// <returns>The model, or the architecture when no platform probe answers.</returns>
        private static string ProcessorModel()
        {
            string model = System.OperatingSystem.IsMacOS() ? ReadSysctl("machdep.cpu.brand_string")
                : System.OperatingSystem.IsLinux() ? ReadProcCpuInfo()
                : System.OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
                : null;

            return string.IsNullOrWhiteSpace(model)
                ? RuntimeInformation.ProcessArchitecture.ToString()
                : model.Trim();
        }

        /// <summary>
        /// Reads a string-valued sysctl.
        /// </summary>
        /// <param name="name">The sysctl name.</param>
        /// <returns>The value, or null when it cannot be read.</returns>
        /// <remarks>Asked for its length first, which is how sysctl reports the buffer it wants.</remarks>
        private static string ReadSysctl(string name)
        {
            try
            {
                nuint length = 0;
                if (SysctlByName(name, null, ref length, 0, 0) != 0 || length == 0 || length > LongestSysctlValue)
                {
                    return null;
                }

                byte[] buffer = new byte[(int)length];
                if (SysctlByName(name, buffer, ref length, 0, 0) != 0)
                {
                    return null;
                }

                int end = Array.IndexOf(buffer, (byte)0);
                return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
            }
            catch (DllNotFoundException)
            {
                return null;
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads the processor model out of the kernel's own listing.
        /// </summary>
        /// <returns>The model, or null when the file is absent or says nothing useful.</returns>
        private static string ReadProcCpuInfo()
        {
            try
            {
                foreach (string line in File.ReadLines("/proc/cpuinfo"))
                {
                    // "model name" on x86, "Hardware" on many ARM boards; neither is guaranteed.
                    if (line.StartsWith("model name", StringComparison.Ordinal)
                        || line.StartsWith("Hardware", StringComparison.Ordinal))
                    {
                        int separator = line.IndexOf(':', StringComparison.Ordinal);
                        if (separator >= 0)
                        {
                            return line[(separator + 1)..];
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return null;
        }

        [LibraryImport("libc", EntryPoint = "sysctlbyname", StringMarshalling = StringMarshalling.Utf8)]
        private static partial int SysctlByName(
            string name, byte[] value, ref nuint length, nint newValue, nuint newLength);

        /// <summary>Formats the memory figure for the banner.</summary>
        /// <returns>The total, in megabytes.</returns>
        public static string Memory()
        {
            return TotalMemoryMegabytes.ToString(CultureInfo.InvariantCulture) + " MB";
        }
    }
}
