namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>Which flavor of GL a context asks for, and which libraries it comes from.</summary>
    public sealed class GlContextProfile
    {
        /// <summary>SDL's core profile mask.</summary>
        public const int CoreMask = 1;

        /// <summary>SDL's OpenGL ES profile mask.</summary>
        public const int EsMask = 4;

        private GlContextProfile(int profileMask, int major, int minor, string eglLibrary, string glesLibrary)
        {
            ProfileMask = profileMask;
            Major = major;
            Minor = minor;
            EglLibrary = eglLibrary;
            GlesLibrary = glesLibrary;
        }

        /// <summary>The profile mask to hand SDL.</summary>
        public int ProfileMask { get; }

        /// <summary>Major context version.</summary>
        public int Major { get; }

        /// <summary>Minor context version.</summary>
        public int Minor { get; }

        /// <summary>Absolute path to the EGL library, or <c>null</c> to let SDL choose.</summary>
        public string EglLibrary { get; }

        /// <summary>Absolute path to the GLES library, or <c>null</c> to let SDL choose.</summary>
        public string GlesLibrary { get; }

        /// <summary>Whether this profile loads its GL from libraries the game ships.</summary>
        public bool UsesAngle => EglLibrary != null;

        /// <summary>Core 3.2 from whichever driver the system provides.</summary>
        public static GlContextProfile DesktopCore { get; } = new(CoreMask, 3, 2, null, null);

        /// <summary>OpenGL ES 3.0 from the given ANGLE libraries.</summary>
        /// <param name="eglLibrary">Absolute path to <c>libEGL</c>.</param>
        /// <param name="glesLibrary">Absolute path to <c>libGLESv2</c>.</param>
        public static GlContextProfile Angle(string eglLibrary, string glesLibrary)
        {
            return new(EsMask, 3, 0, eglLibrary, glesLibrary);
        }

        /// <summary>The one lower profile worth asking for, or <c>null</c> when there is none.</summary>
        /// <remarks>
        /// ANGLE reports ES 2.0 rather than ES 3.0 on Direct3D feature level 10_0, because the
        /// feature that would lift it is off and cannot be turned on through SDL. That level is
        /// the DX10 generation, which is inside the set of machines this exists to serve, so a
        /// refused ES 3.0 context is worth one more attempt rather than a rejected renderer.
        /// </remarks>
        public GlContextProfile Retry => UsesAngle && Major == 3
            ? new(EsMask, 2, 0, EglLibrary, GlesLibrary)
            : null;
    }
}
