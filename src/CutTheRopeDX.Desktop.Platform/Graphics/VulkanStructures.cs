using System.Runtime.InteropServices;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>A two-dimensional size, matching <c>VkExtent2D</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkExtent2D
    {
        internal uint Width;
        internal uint Height;
    }

    /// <summary>A three-dimensional size, matching <c>VkExtent3D</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkExtent3D
    {
        internal uint Width;
        internal uint Height;
        internal uint Depth;
    }

    /// <summary>A three-dimensional signed offset, matching <c>VkOffset3D</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkOffset3D
    {
        internal int X;
        internal int Y;
        internal int Z;
    }

    /// <summary>Application and API version identification, matching <c>VkApplicationInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkApplicationInfo
    {
        internal uint Type;
        internal void* Next;
        internal byte* ApplicationName;
        internal uint ApplicationVersion;
        internal byte* EngineName;
        internal uint EngineVersion;
        internal uint ApiVersion;
    }

    /// <summary>Instance creation parameters, matching <c>VkInstanceCreateInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkInstanceCreateInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
        internal VkApplicationInfo* ApplicationInfo;
        internal uint EnabledLayerCount;
        internal byte** EnabledLayerNames;
        internal uint EnabledExtensionCount;
        internal byte** EnabledExtensionNames;
    }

    /// <summary>Queue creation parameters, matching <c>VkDeviceQueueCreateInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkDeviceQueueCreateInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
        internal uint QueueFamilyIndex;
        internal uint QueueCount;
        internal float* QueuePriorities;
    }

    /// <summary>Logical device creation parameters, matching <c>VkDeviceCreateInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkDeviceCreateInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
        internal uint QueueCreateInfoCount;
        internal VkDeviceQueueCreateInfo* QueueCreateInfos;
        internal uint EnabledLayerCount;
        internal byte** EnabledLayerNames;
        internal uint EnabledExtensionCount;
        internal byte** EnabledExtensionNames;
        internal void* EnabledFeatures;
    }

    /// <summary>Adapter identification, matching the leading fields of <c>VkPhysicalDeviceProperties</c>.</summary>
    /// <remarks>
    /// Only the fields up to the device name are read. The trailing reservation covers the limits and
    /// sparse properties this backend never inspects, and is deliberately larger than the native
    /// structure so the driver can never write past it.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkPhysicalDeviceProperties
    {
        internal uint ApiVersion;
        internal uint DriverVersion;
        internal uint VendorId;
        internal uint DeviceId;
        internal uint DeviceType;
        internal fixed byte DeviceName[256];
        internal fixed byte PipelineCacheUuid[16];
        internal fixed byte Reserved[1024];
    }

    /// <summary>Queue family capabilities, matching <c>VkQueueFamilyProperties</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkQueueFamilyProperties
    {
        internal uint QueueFlags;
        internal uint QueueCount;
        internal uint TimestampValidBits;
        internal VkExtent3D MinImageTransferGranularity;
    }

    /// <summary>One reported extension, matching <c>VkExtensionProperties</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkExtensionProperties
    {
        internal fixed byte ExtensionName[256];
        internal uint SpecVersion;
    }

    /// <summary>One memory type, matching <c>VkMemoryType</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkMemoryType
    {
        internal uint PropertyFlags;
        internal uint HeapIndex;
    }

    /// <summary>One memory heap, matching <c>VkMemoryHeap</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkMemoryHeap
    {
        internal ulong Size;
        internal uint Flags;
        private readonly uint padding;
    }

    /// <summary>Device memory layout, matching <c>VkPhysicalDeviceMemoryProperties</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkPhysicalDeviceMemoryProperties
    {
        internal uint MemoryTypeCount;
        internal VkMemoryTypeArray MemoryTypes;
        internal uint MemoryHeapCount;
        internal VkMemoryHeapArray MemoryHeaps;
    }

    /// <summary>The fixed 32-entry memory type table Vulkan reports.</summary>
    [StructLayout(LayoutKind.Sequential)]
    [System.Runtime.CompilerServices.InlineArray(32)]
    internal struct VkMemoryTypeArray
    {
        private VkMemoryType element;
    }

    /// <summary>The fixed 16-entry memory heap table Vulkan reports.</summary>
    [StructLayout(LayoutKind.Sequential)]
    [System.Runtime.CompilerServices.InlineArray(16)]
    internal struct VkMemoryHeapArray
    {
        private VkMemoryHeap element;
    }

    /// <summary>Surface limits, matching <c>VkSurfaceCapabilitiesKHR</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkSurfaceCapabilities
    {
        internal uint MinImageCount;
        internal uint MaxImageCount;
        internal VkExtent2D CurrentExtent;
        internal VkExtent2D MinImageExtent;
        internal VkExtent2D MaxImageExtent;
        internal uint MaxImageArrayLayers;
        internal uint SupportedTransforms;
        internal uint CurrentTransform;
        internal uint SupportedCompositeAlpha;
        internal uint SupportedUsageFlags;
    }

    /// <summary>One supported surface format, matching <c>VkSurfaceFormatKHR</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkSurfaceFormat
    {
        internal uint Format;
        internal uint ColorSpace;
    }

    /// <summary>Swapchain creation parameters, matching <c>VkSwapchainCreateInfoKHR</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkSwapchainCreateInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
        internal ulong Surface;
        internal uint MinImageCount;
        internal uint ImageFormat;
        internal uint ImageColorSpace;
        internal VkExtent2D ImageExtent;
        internal uint ImageArrayLayers;
        internal uint ImageUsage;
        internal uint ImageSharingMode;
        internal uint QueueFamilyIndexCount;
        internal uint* QueueFamilyIndices;
        internal uint PreTransform;
        internal uint CompositeAlpha;
        internal uint PresentMode;
        internal uint Clipped;
        internal ulong OldSwapchain;
    }

    /// <summary>Presentation parameters, matching <c>VkPresentInfoKHR</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkPresentInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint WaitSemaphoreCount;
        internal ulong* WaitSemaphores;
        internal uint SwapchainCount;
        internal ulong* Swapchains;
        internal uint* ImageIndices;
        internal int* Results;
    }

    /// <summary>Image creation parameters, matching <c>VkImageCreateInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkImageCreateInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
        internal uint ImageType;
        internal uint Format;
        internal VkExtent3D Extent;
        internal uint MipLevels;
        internal uint ArrayLayers;
        internal uint Samples;
        internal uint Tiling;
        internal uint Usage;
        internal uint SharingMode;
        internal uint QueueFamilyIndexCount;
        internal uint* QueueFamilyIndices;
        internal uint InitialLayout;
    }

    /// <summary>Allocation requirements, matching <c>VkMemoryRequirements</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkMemoryRequirements
    {
        internal ulong Size;
        internal ulong Alignment;
        internal uint MemoryTypeBits;
    }

    /// <summary>Allocation parameters, matching <c>VkMemoryAllocateInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkMemoryAllocateInfo
    {
        internal uint Type;
        internal void* Next;
        internal ulong AllocationSize;
        internal uint MemoryTypeIndex;
    }

    /// <summary>Command pool creation parameters, matching <c>VkCommandPoolCreateInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkCommandPoolCreateInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
        internal uint QueueFamilyIndex;
    }

    /// <summary>Command buffer allocation parameters, matching <c>VkCommandBufferAllocateInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkCommandBufferAllocateInfo
    {
        internal uint Type;
        internal void* Next;
        internal ulong CommandPool;
        internal uint Level;
        internal uint CommandBufferCount;
    }

    /// <summary>Command buffer recording parameters, matching <c>VkCommandBufferBeginInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkCommandBufferBeginInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
        internal void* InheritanceInfo;
    }

    /// <summary>Queue submission parameters, matching <c>VkSubmitInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkSubmitInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint WaitSemaphoreCount;
        internal ulong* WaitSemaphores;
        internal uint* WaitDstStageMask;
        internal uint CommandBufferCount;
        internal nint* CommandBuffers;
        internal uint SignalSemaphoreCount;
        internal ulong* SignalSemaphores;
    }

    /// <summary>Fence creation parameters, matching <c>VkFenceCreateInfo</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkFenceCreateInfo
    {
        internal uint Type;
        internal void* Next;
        internal uint Flags;
    }

    /// <summary>A global memory barrier, matching <c>VkMemoryBarrier</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkMemoryBarrier
    {
        internal uint Type;
        internal void* Next;
        internal uint SourceAccessMask;
        internal uint DestinationAccessMask;
    }

    /// <summary>A buffer memory barrier, matching <c>VkBufferMemoryBarrier</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkBufferMemoryBarrier
    {
        internal uint Type;
        internal void* Next;
        internal uint SourceAccessMask;
        internal uint DestinationAccessMask;
        internal uint SourceQueueFamilyIndex;
        internal uint DestinationQueueFamilyIndex;
        internal ulong Buffer;
        internal ulong Offset;
        internal ulong Size;
    }

    /// <summary>A subresource span, matching <c>VkImageSubresourceRange</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkImageSubresourceRange
    {
        internal uint AspectMask;
        internal uint BaseMipLevel;
        internal uint LevelCount;
        internal uint BaseArrayLayer;
        internal uint LayerCount;
    }

    /// <summary>A subresource selection, matching <c>VkImageSubresourceLayers</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkImageSubresourceLayers
    {
        internal uint AspectMask;
        internal uint MipLevel;
        internal uint BaseArrayLayer;
        internal uint LayerCount;
    }

    /// <summary>An image layout transition, matching <c>VkImageMemoryBarrier</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct VkImageMemoryBarrier
    {
        internal uint Type;
        internal void* Next;
        internal uint SourceAccessMask;
        internal uint DestinationAccessMask;
        internal uint OldLayout;
        internal uint NewLayout;
        internal uint SourceQueueFamilyIndex;
        internal uint DestinationQueueFamilyIndex;
        internal ulong Image;
        internal VkImageSubresourceRange SubresourceRange;
    }

    /// <summary>A blit region, matching <c>VkImageBlit</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct VkImageBlit
    {
        internal VkImageSubresourceLayers SourceSubresource;
        internal VkOffset3D SourceOffset0;
        internal VkOffset3D SourceOffset1;
        internal VkImageSubresourceLayers DestinationSubresource;
        internal VkOffset3D DestinationOffset0;
        internal VkOffset3D DestinationOffset1;
    }
}
