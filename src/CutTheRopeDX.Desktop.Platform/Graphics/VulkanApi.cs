using System;
using System.Runtime.InteropServices;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>Vulkan core and swapchain entry points resolved through SDL's loader.</summary>
    /// <remarks>
    /// Every entry point is resolved with <c>vkGetInstanceProcAddr</c>, which also serves device-level
    /// functions, so no managed Vulkan binding is needed. Dispatchable handles are pointers and
    /// non-dispatchable handles are 64 bits on every platform, so <see cref="nint" /> and
    /// <see cref="ulong" /> respectively match the native ABI. The unmanaged calling convention is the
    /// platform default, which matches Vulkan on x64 and arm64; 32-bit x86 is not supported.
    /// </remarks>
    /// <param name="getInstanceProcAddress">SDL's resolved <c>vkGetInstanceProcAddr</c>.</param>
    internal sealed unsafe class VulkanApi(nint getInstanceProcAddress)
    {
        private readonly delegate* unmanaged<nint, byte*, nint> resolve =
            (delegate* unmanaged<nint, byte*, nint>)getInstanceProcAddress;

        internal delegate* unmanaged<VkInstanceCreateInfo*, nint, nint*, int> CreateInstance;
        internal delegate* unmanaged<nint, nint, void> DestroyInstance;
        internal delegate* unmanaged<nint, uint*, nint*, int> EnumeratePhysicalDevices;
        internal delegate* unmanaged<nint, VkPhysicalDeviceProperties*, void> GetPhysicalDeviceProperties;
        internal delegate* unmanaged<nint, uint*, VkQueueFamilyProperties*, void> GetPhysicalDeviceQueueFamilyProperties;
        internal delegate* unmanaged<nint, VkPhysicalDeviceMemoryProperties*, void> GetPhysicalDeviceMemoryProperties;
        internal delegate* unmanaged<nint, byte*, uint*, VkExtensionProperties*, int> EnumerateDeviceExtensionProperties;
        internal delegate* unmanaged<nint, uint, ulong, uint*, int> GetPhysicalDeviceSurfaceSupport;
        internal delegate* unmanaged<nint, ulong, VkSurfaceCapabilities*, int> GetPhysicalDeviceSurfaceCapabilities;
        internal delegate* unmanaged<nint, ulong, uint*, VkSurfaceFormat*, int> GetPhysicalDeviceSurfaceFormats;
        internal delegate* unmanaged<nint, ulong, uint*, uint*, int> GetPhysicalDeviceSurfacePresentModes;
        internal delegate* unmanaged<nint, VkDeviceCreateInfo*, nint, nint*, int> CreateDevice;
        internal delegate* unmanaged<nint, byte*, nint> GetDeviceProcAddress;

        internal delegate* unmanaged<nint, nint, void> DestroyDevice;
        internal delegate* unmanaged<nint, uint, uint, nint*, void> GetDeviceQueue;
        internal delegate* unmanaged<nint, int> DeviceWaitIdle;
        internal delegate* unmanaged<nint, int> QueueWaitIdle;
        internal delegate* unmanaged<nint, VkSwapchainCreateInfo*, nint, ulong*, int> CreateSwapchain;
        internal delegate* unmanaged<nint, ulong, nint, void> DestroySwapchain;
        internal delegate* unmanaged<nint, ulong, uint*, ulong*, int> GetSwapchainImages;
        internal delegate* unmanaged<nint, ulong, ulong, ulong, ulong, uint*, int> AcquireNextImage;
        internal delegate* unmanaged<nint, VkPresentInfo*, int> QueuePresent;
        internal delegate* unmanaged<nint, VkImageCreateInfo*, nint, ulong*, int> CreateImage;
        internal delegate* unmanaged<nint, ulong, nint, void> DestroyImage;
        internal delegate* unmanaged<nint, ulong, VkMemoryRequirements*, void> GetImageMemoryRequirements;
        internal delegate* unmanaged<nint, ulong, ulong, ulong, int> BindImageMemory;
        internal delegate* unmanaged<nint, VkMemoryAllocateInfo*, nint, ulong*, int> AllocateMemory;
        internal delegate* unmanaged<nint, ulong, nint, void> FreeMemory;
        internal delegate* unmanaged<nint, VkCommandPoolCreateInfo*, nint, ulong*, int> CreateCommandPool;
        internal delegate* unmanaged<nint, ulong, nint, void> DestroyCommandPool;
        internal delegate* unmanaged<nint, ulong, uint, int> ResetCommandPool;
        internal delegate* unmanaged<nint, VkCommandBufferAllocateInfo*, nint*, int> AllocateCommandBuffers;
        internal delegate* unmanaged<nint, VkCommandBufferBeginInfo*, int> BeginCommandBuffer;
        internal delegate* unmanaged<nint, int> EndCommandBuffer;
        internal delegate* unmanaged<nint, uint, VkSubmitInfo*, ulong, int> QueueSubmit;
        internal delegate* unmanaged<nint, uint, uint, uint, uint, VkMemoryBarrier*, uint, VkBufferMemoryBarrier*, uint, VkImageMemoryBarrier*, void> CmdPipelineBarrier;
        internal delegate* unmanaged<nint, ulong, uint, ulong, uint, uint, VkImageBlit*, uint, void> CmdBlitImage;
        internal delegate* unmanaged<nint, VkFenceCreateInfo*, nint, ulong*, int> CreateFence;
        internal delegate* unmanaged<nint, ulong, nint, void> DestroyFence;
        internal delegate* unmanaged<nint, uint, ulong*, uint, ulong, int> WaitForFences;
        internal delegate* unmanaged<nint, uint, ulong*, int> ResetFences;

        /// <summary>Resolves the entry points available before an instance exists.</summary>
        internal void LoadGlobal()
        {
            CreateInstance = (delegate* unmanaged<VkInstanceCreateInfo*, nint, nint*, int>)Resolve(0, "vkCreateInstance");
        }

        /// <summary>Resolves instance-scoped entry points, including the surface queries.</summary>
        /// <param name="instance">The created Vulkan instance.</param>
        internal void LoadInstance(nint instance)
        {
            DestroyInstance = (delegate* unmanaged<nint, nint, void>)Resolve(instance, "vkDestroyInstance");
            EnumeratePhysicalDevices = (delegate* unmanaged<nint, uint*, nint*, int>)Resolve(instance, "vkEnumeratePhysicalDevices");
            GetPhysicalDeviceProperties = (delegate* unmanaged<nint, VkPhysicalDeviceProperties*, void>)Resolve(instance, "vkGetPhysicalDeviceProperties");
            GetPhysicalDeviceQueueFamilyProperties = (delegate* unmanaged<nint, uint*, VkQueueFamilyProperties*, void>)Resolve(instance, "vkGetPhysicalDeviceQueueFamilyProperties");
            GetPhysicalDeviceMemoryProperties = (delegate* unmanaged<nint, VkPhysicalDeviceMemoryProperties*, void>)Resolve(instance, "vkGetPhysicalDeviceMemoryProperties");
            EnumerateDeviceExtensionProperties = (delegate* unmanaged<nint, byte*, uint*, VkExtensionProperties*, int>)Resolve(instance, "vkEnumerateDeviceExtensionProperties");
            GetPhysicalDeviceSurfaceSupport = (delegate* unmanaged<nint, uint, ulong, uint*, int>)Resolve(instance, "vkGetPhysicalDeviceSurfaceSupportKHR");
            GetPhysicalDeviceSurfaceCapabilities = (delegate* unmanaged<nint, ulong, VkSurfaceCapabilities*, int>)Resolve(instance, "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");
            GetPhysicalDeviceSurfaceFormats = (delegate* unmanaged<nint, ulong, uint*, VkSurfaceFormat*, int>)Resolve(instance, "vkGetPhysicalDeviceSurfaceFormatsKHR");
            GetPhysicalDeviceSurfacePresentModes = (delegate* unmanaged<nint, ulong, uint*, uint*, int>)Resolve(instance, "vkGetPhysicalDeviceSurfacePresentModesKHR");
            CreateDevice = (delegate* unmanaged<nint, VkDeviceCreateInfo*, nint, nint*, int>)Resolve(instance, "vkCreateDevice");
            GetDeviceProcAddress = (delegate* unmanaged<nint, byte*, nint>)Resolve(instance, "vkGetDeviceProcAddr");
        }

        /// <summary>Resolves device-scoped entry points once a logical device exists.</summary>
        /// <param name="instance">The instance that owns the device.</param>
        internal void LoadDevice(nint instance)
        {
            DestroyDevice = (delegate* unmanaged<nint, nint, void>)Resolve(instance, "vkDestroyDevice");
            GetDeviceQueue = (delegate* unmanaged<nint, uint, uint, nint*, void>)Resolve(instance, "vkGetDeviceQueue");
            DeviceWaitIdle = (delegate* unmanaged<nint, int>)Resolve(instance, "vkDeviceWaitIdle");
            QueueWaitIdle = (delegate* unmanaged<nint, int>)Resolve(instance, "vkQueueWaitIdle");
            CreateSwapchain = (delegate* unmanaged<nint, VkSwapchainCreateInfo*, nint, ulong*, int>)Resolve(instance, "vkCreateSwapchainKHR");
            DestroySwapchain = (delegate* unmanaged<nint, ulong, nint, void>)Resolve(instance, "vkDestroySwapchainKHR");
            GetSwapchainImages = (delegate* unmanaged<nint, ulong, uint*, ulong*, int>)Resolve(instance, "vkGetSwapchainImagesKHR");
            AcquireNextImage = (delegate* unmanaged<nint, ulong, ulong, ulong, ulong, uint*, int>)Resolve(instance, "vkAcquireNextImageKHR");
            QueuePresent = (delegate* unmanaged<nint, VkPresentInfo*, int>)Resolve(instance, "vkQueuePresentKHR");
            CreateImage = (delegate* unmanaged<nint, VkImageCreateInfo*, nint, ulong*, int>)Resolve(instance, "vkCreateImage");
            DestroyImage = (delegate* unmanaged<nint, ulong, nint, void>)Resolve(instance, "vkDestroyImage");
            GetImageMemoryRequirements = (delegate* unmanaged<nint, ulong, VkMemoryRequirements*, void>)Resolve(instance, "vkGetImageMemoryRequirements");
            BindImageMemory = (delegate* unmanaged<nint, ulong, ulong, ulong, int>)Resolve(instance, "vkBindImageMemory");
            AllocateMemory = (delegate* unmanaged<nint, VkMemoryAllocateInfo*, nint, ulong*, int>)Resolve(instance, "vkAllocateMemory");
            FreeMemory = (delegate* unmanaged<nint, ulong, nint, void>)Resolve(instance, "vkFreeMemory");
            CreateCommandPool = (delegate* unmanaged<nint, VkCommandPoolCreateInfo*, nint, ulong*, int>)Resolve(instance, "vkCreateCommandPool");
            DestroyCommandPool = (delegate* unmanaged<nint, ulong, nint, void>)Resolve(instance, "vkDestroyCommandPool");
            ResetCommandPool = (delegate* unmanaged<nint, ulong, uint, int>)Resolve(instance, "vkResetCommandPool");
            AllocateCommandBuffers = (delegate* unmanaged<nint, VkCommandBufferAllocateInfo*, nint*, int>)Resolve(instance, "vkAllocateCommandBuffers");
            BeginCommandBuffer = (delegate* unmanaged<nint, VkCommandBufferBeginInfo*, int>)Resolve(instance, "vkBeginCommandBuffer");
            EndCommandBuffer = (delegate* unmanaged<nint, int>)Resolve(instance, "vkEndCommandBuffer");
            QueueSubmit = (delegate* unmanaged<nint, uint, VkSubmitInfo*, ulong, int>)Resolve(instance, "vkQueueSubmit");
            CmdPipelineBarrier = (delegate* unmanaged<nint, uint, uint, uint, uint, VkMemoryBarrier*, uint, VkBufferMemoryBarrier*, uint, VkImageMemoryBarrier*, void>)Resolve(instance, "vkCmdPipelineBarrier");
            CmdBlitImage = (delegate* unmanaged<nint, ulong, uint, ulong, uint, uint, VkImageBlit*, uint, void>)Resolve(instance, "vkCmdBlitImage");
            CreateFence = (delegate* unmanaged<nint, VkFenceCreateInfo*, nint, ulong*, int>)Resolve(instance, "vkCreateFence");
            DestroyFence = (delegate* unmanaged<nint, ulong, nint, void>)Resolve(instance, "vkDestroyFence");
            WaitForFences = (delegate* unmanaged<nint, uint, ulong*, uint, ulong, int>)Resolve(instance, "vkWaitForFences");
            ResetFences = (delegate* unmanaged<nint, uint, ulong*, int>)Resolve(instance, "vkResetFences");
        }

        /// <summary>Resolves one entry point, rejecting a driver that cannot supply it.</summary>
        /// <param name="scope">The instance to resolve within, or zero for global entry points.</param>
        /// <param name="name">The Vulkan function name.</param>
        /// <returns>The resolved function address.</returns>
        internal void* Resolve(nint scope, string name)
        {
            nint utf8 = Marshal.StringToCoTaskMemUTF8(name);
            try
            {
                nint address = resolve(scope, (byte*)utf8);
                return address != 0
                    ? (void*)address
                    : throw new InvalidOperationException("Vulkan entry point unavailable: " + name);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8);
            }
        }

        /// <summary>Turns a Vulkan result into the failure it actually represents.</summary>
        /// <param name="result">The <c>VkResult</c> the call returned.</param>
        /// <param name="operation">The entry point, for the message.</param>
        /// <remarks>
        /// A lost device and a lost surface are separated from every other failure because the
        /// host can do something about them. Reported as an ordinary error they read as a bug in
        /// the call that happened to notice, and the recovery the host has for exactly this is
        /// never reached.
        /// </remarks>
        internal static void Check(int result, string operation)
        {
            if (result == Vk.Success)
            {
                return;
            }

            string message = $"{operation} failed with VkResult {result}.";
            throw IsDeviceLost(result)
                ? new GraphicsDeviceLostException(message)
                : new InvalidOperationException(message);
        }

        /// <summary>Whether a result means the device or its surface has gone.</summary>
        /// <param name="result">The <c>VkResult</c> to classify.</param>
        internal static bool IsDeviceLost(int result)
        {
            return result is Vk.ErrorDeviceLost or Vk.ErrorSurfaceLostKhr
                or Vk.ErrorFullScreenExclusiveModeLostExt;
        }
    }

    /// <summary>The Vulkan enumerant values used by this backend.</summary>
    internal static class Vk
    {
        internal const int Success = 0;
        internal const int SuboptimalKhr = 1000001003;
        internal const int ErrorOutOfDateKhr = -1000001004;

        /// <summary>The driver has abandoned the device; nothing made by it can be used again.</summary>
        internal const int ErrorDeviceLost = -4;

        /// <summary>The window's surface has gone, which takes the swapchain with it.</summary>
        internal const int ErrorSurfaceLostKhr = -1000000000;

        /// <summary>Exclusive fullscreen was taken away, which invalidates the swapchain.</summary>
        internal const int ErrorFullScreenExclusiveModeLostExt = -1000255000;

        internal const uint StructureApplicationInfo = 0;
        internal const uint StructureInstanceCreateInfo = 1;
        internal const uint StructureDeviceQueueCreateInfo = 2;
        internal const uint StructureDeviceCreateInfo = 3;
        internal const uint StructureSubmitInfo = 4;
        internal const uint StructureMemoryAllocateInfo = 5;
        internal const uint StructureFenceCreateInfo = 8;
        internal const uint StructureImageCreateInfo = 14;
        internal const uint StructureCommandPoolCreateInfo = 39;
        internal const uint StructureCommandBufferAllocateInfo = 40;
        internal const uint StructureCommandBufferBeginInfo = 42;
        internal const uint StructureImageMemoryBarrier = 45;
        internal const uint StructureSwapchainCreateInfoKhr = 1000001000;
        internal const uint StructurePresentInfoKhr = 1000001001;

        internal const uint LayoutUndefined = 0;
        internal const uint LayoutColorAttachmentOptimal = 2;
        internal const uint LayoutTransferSourceOptimal = 6;
        internal const uint LayoutTransferDestinationOptimal = 7;
        internal const uint LayoutPresentSourceKhr = 1000001002;

        internal const uint AccessNone = 0;
        internal const uint AccessColorAttachmentWrite = 0x100;
        internal const uint AccessTransferRead = 0x800;
        internal const uint AccessTransferWrite = 0x1000;
        internal const uint AccessMemoryRead = 0x8000;

        internal const uint StageTopOfPipe = 0x1;
        internal const uint StageTransfer = 0x1000;
        internal const uint StageColorAttachmentOutput = 0x400;
        internal const uint StageBottomOfPipe = 0x2000;
        internal const uint StageAllCommands = 0x10000;

        internal const uint UsageTransferSource = 0x1;
        internal const uint UsageTransferDestination = 0x2;
        internal const uint UsageSampled = 0x4;
        internal const uint UsageColorAttachment = 0x10;

        internal const uint FormatB8G8R8A8Unorm = 44;
        internal const uint FormatR8G8B8A8Unorm = 37;
        internal const uint ColorSpaceSrgbNonlinear = 0;

        internal const uint MemoryDeviceLocal = 0x1;
        internal const uint QueueGraphics = 0x1;
        internal const uint SharingExclusive = 0;
        internal const uint ImageType2D = 1;
        internal const uint TilingOptimal = 0;
        internal const uint SampleCount1 = 1;
        internal const uint AspectColor = 1;
        internal const uint CommandBufferLevelPrimary = 0;
        internal const uint CommandPoolResetBuffer = 0x2;
        internal const uint CommandBufferOneTimeSubmit = 0x1;
        internal const uint PresentModeFifo = 2;
        internal const uint CompositeAlphaOpaque = 1;
        internal const uint SurfaceTransformIdentity = 1;
        internal const ulong WholeTimeout = ulong.MaxValue;

        /// <summary>Vulkan 1.1, the version this backend requests and reports to Skia.</summary>
        internal const uint ApiVersion11 = (1 << 22) | (1 << 12);
    }
}
