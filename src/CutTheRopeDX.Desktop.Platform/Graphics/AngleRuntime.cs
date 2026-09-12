using System.IO;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>Finds the ANGLE libraries the Windows release ships beside the executable.</summary>
    /// <remarks>
    /// A build that does not ship them — a development build, or any non-Windows host — reports
    /// nothing here, which is what lets the ANGLE candidate fail at once and cleanly instead of
    /// half-initializing on its way to the same answer.
    /// </remarks>
    public static class AngleRuntime
    {
        /// <summary>Directory the release places the ANGLE libraries in.</summary>
        public const string DirectoryName = "angle";

        /// <summary>Resolves both libraries, or reports that ANGLE is not installed.</summary>
        /// <param name="baseDirectory">Directory the executable runs from.</param>
        /// <param name="eglLibrary">Absolute path to <c>libEGL</c> when both were found.</param>
        /// <param name="glesLibrary">Absolute path to <c>libGLESv2</c> when both were found.</param>
        /// <returns>Whether both libraries are present.</returns>
        public static bool TryLocate(string baseDirectory, out string eglLibrary, out string glesLibrary)
        {
            eglLibrary = null;
            glesLibrary = null;
            if (string.IsNullOrEmpty(baseDirectory))
            {
                return false;
            }

            string directory = Path.Combine(baseDirectory, DirectoryName);
            string egl = Path.Combine(directory, "libEGL.dll");
            string gles = Path.Combine(directory, "libGLESv2.dll");
            if (!File.Exists(egl) || !File.Exists(gles))
            {
                return false;
            }

            eglLibrary = egl;
            glesLibrary = gles;
            return true;
        }
    }
}
