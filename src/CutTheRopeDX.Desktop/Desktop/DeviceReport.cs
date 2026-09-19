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
                : System.OperatingSystem.IsWindows() ? WindowsProcessorName()
                : null;

            return string.IsNullOrWhiteSpace(model)
                ? RuntimeInformation.ProcessArchitecture.ToString()
                : model.Trim();
        }

        /// <summary>Where Windows keeps the name the processor's own vendor gives it.</summary>
        private const string ProcessorKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";

        /// <summary>
        /// Names a Windows processor the way its vendor does, rather than the way the part is
        /// numbered.
        /// </summary>
        /// <returns>The vendor's name, or the environment's description of the part.</returns>
        /// <remarks>
        /// PROCESSOR_IDENTIFIER, which is what the environment offers, describes a part instead of
        /// naming it: "Intel64 Family 6 Model 142 Stepping 12" is one string across a whole
        /// generation of laptops, so two reports of the same fault cannot be told apart by it, and
        /// a reader cannot tell what they are looking at without a table. Windows keeps the name
        /// the vendor prints on the box beside it, which is the one Task Manager shows. The
        /// environment variable stays as the fallback, because it is always there.
        /// <para>
        /// The other platforms already read a vendor name - a sysctl brand string on macOS, the
        /// model name on Linux - so this is what makes the three say the same kind of thing.
        /// </para>
        /// </remarks>
        private static string WindowsProcessorName()
        {
            return PreferVendorName(
                ReadRegistryString(ProcessorKey, "ProcessorNameString"),
                Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"));
        }

        /// <summary>
        /// Chooses between the vendor's name and the environment's description of the part.
        /// </summary>
        /// <param name="vendorName">What the registry answered, if anything.</param>
        /// <param name="identifier">What the environment offers.</param>
        /// <returns>The better of the two, or null when neither says anything.</returns>
        internal static string PreferVendorName(string vendorName, string identifier)
        {
            return !string.IsNullOrWhiteSpace(vendorName) ? vendorName.Trim()
                : string.IsNullOrWhiteSpace(identifier) ? null
                : identifier.Trim();
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

        /// <summary>
        /// Reads a string value out of the local machine's registry.
        /// </summary>
        /// <param name="subKey">Key to read, below HKEY_LOCAL_MACHINE.</param>
        /// <param name="valueName">Value to read from it.</param>
        /// <returns>The value, or null when it cannot be read.</returns>
        /// <remarks>
        /// Called through rather than through <c>Microsoft.Win32.Registry</c>, which this target
        /// framework would have to take a package reference for: one value read once at startup is
        /// not worth a dependency, and the file already reaches for a platform's own probe this way.
        /// </remarks>
        private static string ReadRegistryString(string subKey, string valueName)
        {
            try
            {
                byte[] buffer = new byte[LongestRegistryValue];
                uint length = (uint)buffer.Length;
                if (RegGetValue(HkeyLocalMachine, subKey, valueName, RestrictToString, 0, buffer, ref length) != 0
                    || length == 0
                    || length > buffer.Length)
                {
                    return null;
                }

                // The answer is UTF-16 and counted in bytes, terminator included.
                return Encoding.Unicode.GetString(buffer, 0, (int)length).TrimEnd('\0');
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

        /// <summary>Longest registry answer worth reading; a processor name is far shorter.</summary>
        private const int LongestRegistryValue = 512;

        /// <summary>The registry root the processor is described under.</summary>
        /// <remarks>
        /// A negative 32-bit constant widened with its sign, which is how the Windows headers
        /// define it and what the call expects on a 64-bit process.
        /// </remarks>
        private static readonly nint HkeyLocalMachine = unchecked((int)0x80000002);

        /// <summary>Refuse anything that is not a plain string, rather than returning its bytes.</summary>
        private const uint RestrictToString = 0x00000002;

        [LibraryImport("libc", EntryPoint = "sysctlbyname", StringMarshalling = StringMarshalling.Utf8)]
        private static partial int SysctlByName(
            string name, byte[] value, ref nuint length, nint newValue, nuint newLength);

        [LibraryImport("advapi32.dll", EntryPoint = "RegGetValueW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int RegGetValue(
            nint key, string subKey, string value, uint flags, nint type, byte[] data, ref uint dataLength);

        /// <summary>Formats the memory figure for the banner.</summary>
        /// <returns>The total, in megabytes.</returns>
        public static string Memory()
        {
            return TotalMemoryMegabytes.ToString(CultureInfo.InvariantCulture) + " MB";
        }
    }
}
