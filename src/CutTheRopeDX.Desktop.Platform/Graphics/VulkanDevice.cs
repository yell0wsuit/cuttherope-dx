using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SDL3;

using SkiaSharp;

namespace CutTheRopeDX.Desktop.Platform.Graphics
{
    /// <summary>SDL Vulkan presentation with Skia drawing into a device-owned offscreen image.</summary>
    /// <remarks>
    /// Skia is never given a swapchain image. It renders into an image this device owns, whose layout
    /// this device sets before handing it over, and the finished frame reaches the swapchain through a
    /// GPU blit. Published SkiaSharp exposes neither a present-access flush nor the image layout Skia
    /// left behind, so a swapchain image handed to Skia could not be transitioned for presentation.
    /// </remarks>
    /// <param name="fault">Fault-injection hook invoked at named initialization and frame points.</param>
    public sealed unsafe class VulkanDevice(Action<string> fault) : SdlGraphicsDevice
    {
        private readonly Action<string> fault = fault;

        private VulkanApi vk;

        private nint instance;

        private nint physicalDevice;

        private nint device;

        private nint queue;

        private uint queueFamily;

        private ulong surface;

        private ulong swapchain;

        private ulong[] swapchainImages = [];

        private uint swapchainFormat;

        private ulong commandPool;

        private nint commandBuffer;

        private ulong acquireFence;

        /// <summary>The offscreen image Skia draws into, blitted to the swapchain on present.</summary>
        private ulong image;

        private ulong imageMemory;

        /// <summary>The layout this device last set on <see cref="image" />.</summary>
        private uint imageLayout;

        private string[] instanceExtensions = [];

        private string[] deviceExtensions = [];

        private uint acquiredIndex;

        private bool frameAcquired;

        /// <summary>Creates the SDL Vulkan window, instance, device and Skia context.</summary>
        public void Initialize()
        {
            if (OperatingSystem.IsMacOS())
            {
                throw new PlatformNotSupportedException(
                    "SkiaSharp's macOS native library is built without Skia's Vulkan backend, so its " +
                    "Vulkan entry points return null however the device is configured. Installing " +
                    "MoltenVK does not change this. Use Metal on macOS.");
            }

            CreateWindow(SDL.WindowFlags.Vulkan);
            Check(SDL.VulkanLoadLibrary(null));
            Own(SDL.VulkanUnloadLibrary);
            nint procAddress = SDL.VulkanGetVkGetInstanceProcAddr();
            if (procAddress == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            vk = new VulkanApi(procAddress);
            vk.LoadGlobal();
            CreateInstance();
            vk.LoadInstance(instance);
            SelectPhysicalDevice();
            CreateSurface();
            CreateDevice();
            fault("after-device");
            CreateSkiaContext(procAddress);
            CreateCommandResources();
            CreateSwapchain();
            Own(DestroySwapchain);
            Own(DestroyImage);
        }

        /// <summary>Creates the instance with the surface extensions SDL requires.</summary>
        private void CreateInstance()
        {
            string[] required = SDL.VulkanGetInstanceExtensions(out _)
                ?? throw new InvalidOperationException(SDL.GetError());
            instanceExtensions = required;
            nint applicationName = Marshal.StringToCoTaskMemUTF8("Desktop Skia probe");
            nint[] extensionNames = new nint[required.Length];
            for (int index = 0; index < required.Length; index++)
            {
                extensionNames[index] = Marshal.StringToCoTaskMemUTF8(required[index]);
            }

            try
            {
                VkApplicationInfo application = new()
                {
                    Type = Vk.StructureApplicationInfo,
                    ApplicationName = (byte*)applicationName,
                    EngineName = (byte*)applicationName,
                    ApiVersion = Vk.ApiVersion11,
                };
                fixed (nint* extensions = extensionNames)
                {
                    VkInstanceCreateInfo create = new()
                    {
                        Type = Vk.StructureInstanceCreateInfo,
                        ApplicationInfo = &application,
                        EnabledExtensionCount = (uint)extensionNames.Length,
                        EnabledExtensionNames = (byte**)extensions,
                    };
                    nint created;
                    VulkanApi.Check(vk.CreateInstance(&create, 0, &created), "vkCreateInstance");
                    instance = created;
                }

                Own(() => vk.DestroyInstance(instance, 0));
            }
            finally
            {
                Marshal.FreeCoTaskMem(applicationName);
                foreach (nint name in extensionNames)
                {
                    Marshal.FreeCoTaskMem(name);
                }
            }
        }

        /// <summary>Picks the first adapter with a graphics queue, preferring discrete hardware.</summary>
        private void SelectPhysicalDevice()
        {
            uint count = 0;
            VulkanApi.Check(vk.EnumeratePhysicalDevices(instance, &count, null), "vkEnumeratePhysicalDevices");
            if (count == 0)
            {
                throw new InvalidOperationException("No Vulkan adapter is available.");
            }

            nint[] candidates = new nint[count];
            fixed (nint* handles = candidates)
            {
                VulkanApi.Check(vk.EnumeratePhysicalDevices(instance, &count, handles), "vkEnumeratePhysicalDevices");
            }

            nint best = 0;
            uint bestType = uint.MaxValue;
            uint bestFamily = 0;
            string bestName = string.Empty;
            foreach (nint candidate in candidates)
            {
                if (!TryFindGraphicsQueue(candidate, out uint family))
                {
                    continue;
                }

                VkPhysicalDeviceProperties properties;
                vk.GetPhysicalDeviceProperties(candidate, &properties);
                uint rank = properties.DeviceType switch
                {
                    2 => 0, // Discrete.
                    1 => 1, // Integrated.
                    3 => 2, // Virtual.
                    _ => 3, // CPU or other; accepted only when nothing else exists.
                };
                if (rank >= bestType)
                {
                    continue;
                }

                bestType = rank;
                best = candidate;
                bestFamily = family;
                bestName = Marshal.PtrToStringUTF8((nint)properties.DeviceName) ?? "unknown";
            }

            if (best == 0)
            {
                throw new InvalidOperationException("No Vulkan adapter exposes a graphics queue.");
            }

            physicalDevice = best;
            queueFamily = bestFamily;
            Console.WriteLine($"Vulkan adapter-type={(bestType == 0 ? "discrete" : bestType == 1 ? "integrated" : bestType == 2 ? "virtual" : "software")} name={bestName}");
            if (bestType == 3)
            {
                throw new InvalidOperationException("Only a software Vulkan adapter is available.");
            }
        }

        /// <summary>Finds a queue family that supports graphics work.</summary>
        /// <param name="candidate">The adapter to inspect.</param>
        /// <param name="family">The first graphics-capable family index.</param>
        /// <returns>True when the adapter can render.</returns>
        private bool TryFindGraphicsQueue(nint candidate, out uint family)
        {
            uint count = 0;
            vk.GetPhysicalDeviceQueueFamilyProperties(candidate, &count, null);
            VkQueueFamilyProperties[] families = new VkQueueFamilyProperties[count];
            fixed (VkQueueFamilyProperties* items = families)
            {
                vk.GetPhysicalDeviceQueueFamilyProperties(candidate, &count, items);
            }

            for (uint index = 0; index < count; index++)
            {
                if ((families[index].QueueFlags & Vk.QueueGraphics) != 0)
                {
                    family = index;
                    return true;
                }
            }

            family = 0;
            return false;
        }

        /// <summary>Creates the window surface through SDL and verifies the queue can present to it.</summary>
        private void CreateSurface()
        {
            if (!SDL.VulkanCreateSurface(Window, instance, 0, out nint created) || created == 0)
            {
                throw new InvalidOperationException(SDL.GetError());
            }

            surface = (ulong)created;
            Own(() => SDL.VulkanDestroySurface(instance, (nint)surface, 0));
            uint supported = 0;
            VulkanApi.Check(vk.GetPhysicalDeviceSurfaceSupport(physicalDevice, queueFamily, surface, &supported),
                "vkGetPhysicalDeviceSurfaceSupportKHR");
            if (supported == 0)
            {
                throw new InvalidOperationException("The graphics queue cannot present to this window surface.");
            }
        }

        /// <summary>Creates the logical device with the swapchain and any required portability extension.</summary>
        private void CreateDevice()
        {
            List<string> required = ["VK_KHR_swapchain"];
            if (HasDeviceExtension("VK_KHR_portability_subset"))
            {
                required.Add("VK_KHR_portability_subset");
            }

            deviceExtensions = [.. required];

            nint[] names = new nint[required.Count];
            for (int index = 0; index < required.Count; index++)
            {
                names[index] = Marshal.StringToCoTaskMemUTF8(required[index]);
            }

            try
            {
                float priority = 1.0f;
                VkDeviceQueueCreateInfo queueCreate = new()
                {
                    Type = Vk.StructureDeviceQueueCreateInfo,
                    QueueFamilyIndex = queueFamily,
                    QueueCount = 1,
                    QueuePriorities = &priority,
                };
                fixed (nint* extensionNames = names)
                {
                    VkDeviceCreateInfo create = new()
                    {
                        Type = Vk.StructureDeviceCreateInfo,
                        QueueCreateInfoCount = 1,
                        QueueCreateInfos = &queueCreate,
                        EnabledExtensionCount = (uint)names.Length,
                        EnabledExtensionNames = (byte**)extensionNames,
                    };
                    nint created;
                    VulkanApi.Check(vk.CreateDevice(physicalDevice, &create, 0, &created), "vkCreateDevice");
                    device = created;
                }

                Own(() => vk.DestroyDevice(device, 0));
            }
            finally
            {
                foreach (nint name in names)
                {
                    Marshal.FreeCoTaskMem(name);
                }
            }

            vk.LoadDevice(instance);
            nint acquired;
            vk.GetDeviceQueue(device, queueFamily, 0, &acquired);
            queue = acquired;
        }

        /// <summary>Reports whether the selected adapter offers an extension.</summary>
        /// <param name="name">The extension name to look for.</param>
        /// <returns>True when the adapter reports it.</returns>
        private bool HasDeviceExtension(string name)
        {
            uint count = 0;
            VulkanApi.Check(vk.EnumerateDeviceExtensionProperties(physicalDevice, null, &count, null),
                "vkEnumerateDeviceExtensionProperties");
            VkExtensionProperties[] available = new VkExtensionProperties[count];
            fixed (VkExtensionProperties* items = available)
            {
                VulkanApi.Check(vk.EnumerateDeviceExtensionProperties(physicalDevice, null, &count, items),
                    "vkEnumerateDeviceExtensionProperties");
                for (uint index = 0; index < count; index++)
                {
                    if (Marshal.PtrToStringUTF8((nint)items[index].ExtensionName) == name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Builds the Skia Vulkan context over this device's instance, adapter and queue.</summary>
        /// <param name="procAddress">SDL's resolved <c>vkGetInstanceProcAddr</c>.</param>
        private void CreateSkiaContext(nint procAddress)
        {
            delegate* unmanaged<nint, byte*, nint> resolveInstance = (delegate* unmanaged<nint, byte*, nint>)procAddress;
            delegate* unmanaged<nint, byte*, nint> resolveDevice = vk.GetDeviceProcAddress;
            nint GetProcedure(string name, nint forInstance, nint forDevice)
            {
                nint utf8 = Marshal.StringToCoTaskMemUTF8(name);
                try
                {
                    // Device-level functions resolve against the device first, as Vulkan intends; the
                    // instance lookup is the documented fallback and also serves global functions.
                    nint address = forDevice != 0 ? resolveDevice(forDevice, (byte*)utf8) : 0;
                    return address != 0 ? address : resolveInstance(forInstance, (byte*)utf8);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(utf8);
                }
            }
            GRVkGetProcedureAddressDelegate procedure = GetProcedure;
            GRVkExtensions extensions = Own(GRVkExtensions.Create(procedure, instance, physicalDevice,
                instanceExtensions, deviceExtensions));
            GRVkBackendContext backend = Own(new GRVkBackendContext
            {
                Extensions = extensions,
                VkInstance = instance,
                VkPhysicalDevice = physicalDevice,
                VkDevice = device,
                VkQueue = queue,
                GraphicsQueueIndex = queueFamily,
                GetProcedureAddress = procedure,
            });
            Context = Own(GRContext.CreateVulkan(backend)
                ?? throw new InvalidOperationException(
                    "Skia rejected this Vulkan device. Skia reports no detail; the usual causes are an " +
                    "adapter below Skia's feature requirements or a native library built without the " +
                    "Vulkan backend."));
        }

        /// <summary>Creates the command pool, blit command buffer and acquisition fence.</summary>
        private void CreateCommandResources()
        {
            VkCommandPoolCreateInfo poolCreate = new()
            {
                Type = Vk.StructureCommandPoolCreateInfo,
                Flags = Vk.CommandPoolResetBuffer,
                QueueFamilyIndex = queueFamily,
            };
            ulong createdPool;
            VulkanApi.Check(vk.CreateCommandPool(device, &poolCreate, 0, &createdPool), "vkCreateCommandPool");
            commandPool = createdPool;
            Own(() => vk.DestroyCommandPool(device, commandPool, 0));

            VkCommandBufferAllocateInfo bufferAllocate = new()
            {
                Type = Vk.StructureCommandBufferAllocateInfo,
                CommandPool = commandPool,
                Level = Vk.CommandBufferLevelPrimary,
                CommandBufferCount = 1,
            };
            nint allocated;
            VulkanApi.Check(vk.AllocateCommandBuffers(device, &bufferAllocate, &allocated), "vkAllocateCommandBuffers");
            commandBuffer = allocated;

            VkFenceCreateInfo fenceCreate = new() { Type = Vk.StructureFenceCreateInfo };
            ulong createdFence;
            VulkanApi.Check(vk.CreateFence(device, &fenceCreate, 0, &createdFence), "vkCreateFence");
            acquireFence = createdFence;
            Own(() => vk.DestroyFence(device, acquireFence, 0));
        }

        /// <summary>Creates the swapchain for the window's current drawable size.</summary>
        private void CreateSwapchain()
        {
            VkSurfaceCapabilities capabilities;
            VulkanApi.Check(vk.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, &capabilities),
                "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");

            uint formatCount = 0;
            VulkanApi.Check(vk.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, &formatCount, null),
                "vkGetPhysicalDeviceSurfaceFormatsKHR");
            VkSurfaceFormat[] formats = new VkSurfaceFormat[formatCount];
            fixed (VkSurfaceFormat* items = formats)
            {
                VulkanApi.Check(vk.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, &formatCount, items),
                    "vkGetPhysicalDeviceSurfaceFormatsKHR");
            }

            swapchainFormat = Vk.FormatB8G8R8A8Unorm;
            bool supported = false;
            foreach (VkSurfaceFormat format in formats)
            {
                if (format.Format == swapchainFormat && format.ColorSpace == Vk.ColorSpaceSrgbNonlinear)
                {
                    supported = true;
                    break;
                }
            }

            if (!supported)
            {
                throw new InvalidOperationException("The surface does not support a BGRA8 non-linear sRGB format.");
            }

            VkExtent2D extent = capabilities.CurrentExtent;
            if (extent.Width == uint.MaxValue)
            {
                Check(SDL.GetWindowSizeInPixels(Window, out int windowWidth, out int windowHeight));
                extent.Width = Math.Clamp((uint)windowWidth, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
                extent.Height = Math.Clamp((uint)windowHeight, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);
            }

            uint imageCount = capabilities.MinImageCount + 1;
            if (capabilities.MaxImageCount != 0 && imageCount > capabilities.MaxImageCount)
            {
                imageCount = capabilities.MaxImageCount;
            }

            VkSwapchainCreateInfo create = new()
            {
                Type = Vk.StructureSwapchainCreateInfoKhr,
                Surface = surface,
                MinImageCount = imageCount,
                ImageFormat = swapchainFormat,
                ImageColorSpace = Vk.ColorSpaceSrgbNonlinear,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = Vk.UsageTransferDestination | Vk.UsageColorAttachment,
                ImageSharingMode = Vk.SharingExclusive,
                PreTransform = (capabilities.SupportedTransforms & Vk.SurfaceTransformIdentity) != 0
                    ? Vk.SurfaceTransformIdentity
                    : capabilities.CurrentTransform,
                CompositeAlpha = Vk.CompositeAlphaOpaque,
                PresentMode = Vk.PresentModeFifo,
                Clipped = 1,
            };
            ulong created;
            VulkanApi.Check(vk.CreateSwapchain(device, &create, 0, &created), "vkCreateSwapchainKHR");
            swapchain = created;

            uint count = 0;
            VulkanApi.Check(vk.GetSwapchainImages(device, swapchain, &count, null), "vkGetSwapchainImagesKHR");
            swapchainImages = new ulong[count];
            fixed (ulong* items = swapchainImages)
            {
                VulkanApi.Check(vk.GetSwapchainImages(device, swapchain, &count, items), "vkGetSwapchainImagesKHR");
            }

            Width = (int)extent.Width;
            Height = (int)extent.Height;
            CreateOffscreenImage();
        }

        /// <summary>Creates the image Skia renders into for the current swapchain size.</summary>
        private void CreateOffscreenImage()
        {
            VkImageCreateInfo create = new()
            {
                Type = Vk.StructureImageCreateInfo,
                ImageType = Vk.ImageType2D,
                Format = swapchainFormat,
                Extent = new VkExtent3D { Width = (uint)Width, Height = (uint)Height, Depth = 1 },
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = Vk.SampleCount1,
                Tiling = Vk.TilingOptimal,
                Usage = Vk.UsageColorAttachment | Vk.UsageTransferSource | Vk.UsageTransferDestination | Vk.UsageSampled,
                SharingMode = Vk.SharingExclusive,
                InitialLayout = Vk.LayoutUndefined,
            };
            ulong created;
            VulkanApi.Check(vk.CreateImage(device, &create, 0, &created), "vkCreateImage");
            image = created;

            VkMemoryRequirements requirements;
            vk.GetImageMemoryRequirements(device, image, &requirements);
            VkMemoryAllocateInfo allocate = new()
            {
                Type = Vk.StructureMemoryAllocateInfo,
                AllocationSize = requirements.Size,
                MemoryTypeIndex = FindMemoryType(requirements.MemoryTypeBits, Vk.MemoryDeviceLocal),
            };
            ulong allocated;
            VulkanApi.Check(vk.AllocateMemory(device, &allocate, 0, &allocated), "vkAllocateMemory");
            imageMemory = allocated;
            VulkanApi.Check(vk.BindImageMemory(device, image, imageMemory, 0), "vkBindImageMemory");
            imageLayout = Vk.LayoutUndefined;
        }

        /// <summary>Finds a memory type satisfying both the image's bits and the required properties.</summary>
        /// <param name="typeBits">The allowed memory type mask from the image's requirements.</param>
        /// <param name="properties">The required memory property flags.</param>
        /// <returns>The chosen memory type index.</returns>
        private uint FindMemoryType(uint typeBits, uint properties)
        {
            VkPhysicalDeviceMemoryProperties memory;
            vk.GetPhysicalDeviceMemoryProperties(physicalDevice, &memory);
            for (uint index = 0; index < memory.MemoryTypeCount; index++)
            {
                if ((typeBits & (1u << (int)index)) != 0 && (memory.MemoryTypes[(int)index].PropertyFlags & properties) == properties)
                {
                    return index;
                }
            }

            throw new InvalidOperationException("No Vulkan memory type satisfies the offscreen image.");
        }

        /// <inheritdoc />
        public override bool AcquireFrame()
        {
            if (!GetDrawableSize(out int width, out int height))
            {
                return false;
            }

            if (width != Width || height != Height)
            {
                Resize();
                if (!GetDrawableSize(out _, out _))
                {
                    return false;
                }
            }

            uint index = 0;
            fixed (ulong* fence = &acquireFence)
            {
                int acquired = vk.AcquireNextImage(device, swapchain, Vk.WholeTimeout, 0, acquireFence, &index);
                if (acquired is Vk.ErrorOutOfDateKhr)
                {
                    Resize();
                    return false;
                }

                if (acquired is not (Vk.Success or Vk.SuboptimalKhr))
                {
                    VulkanApi.Check(acquired, "vkAcquireNextImageKHR");
                }

                VulkanApi.Check(vk.WaitForFences(device, 1, fence, 1, Vk.WholeTimeout), "vkWaitForFences");
                VulkanApi.Check(vk.ResetFences(device, 1, fence), "vkResetFences");
            }

            acquiredIndex = index;
            frameAcquired = true;
            fault("before-surface");
            TransitionOffscreenImage(Vk.LayoutColorAttachmentOptimal);
            SetSurface(
                new GRBackendRenderTarget(Width, Height, new GRVkImageInfo
                {
                    Image = image,
                    ImageLayout = imageLayout,
                    ImageTiling = Vk.TilingOptimal,
                    ImageUsageFlags = Vk.UsageColorAttachment | Vk.UsageTransferSource | Vk.UsageTransferDestination | Vk.UsageSampled,
                    Format = swapchainFormat,
                    LevelCount = 1,
                    SampleCount = 1,
                    SharingMode = Vk.SharingExclusive,
                    CurrentQueueFamily = queueFamily,
                }),
                GRSurfaceOrigin.TopLeft,
                SKColorType.Bgra8888);
            fault("after-surface");
            return true;
        }

        /// <inheritdoc />
        public override void Present()
        {
            CheckThread();
            if (!frameAcquired)
            {
                throw new InvalidOperationException("No acquired Vulkan swapchain image to present.");
            }

            ClearSurface();
            BlitAndPresent();
            frameAcquired = false;
        }

        /// <summary>Blits the finished offscreen image onto the acquired swapchain image and presents it.</summary>
        private void BlitAndPresent()
        {
            VulkanApi.Check(vk.ResetCommandPool(device, commandPool, 0), "vkResetCommandPool");
            VkCommandBufferBeginInfo begin = new()
            {
                Type = Vk.StructureCommandBufferBeginInfo,
                Flags = Vk.CommandBufferOneTimeSubmit,
            };
            VulkanApi.Check(vk.BeginCommandBuffer(commandBuffer, &begin), "vkBeginCommandBuffer");

            ulong target = swapchainImages[acquiredIndex];
            RecordBarrier(image, imageLayout, Vk.LayoutTransferSourceOptimal,
                Vk.AccessColorAttachmentWrite, Vk.AccessTransferRead, Vk.StageAllCommands, Vk.StageTransfer);
            imageLayout = Vk.LayoutTransferSourceOptimal;
            RecordBarrier(target, Vk.LayoutUndefined, Vk.LayoutTransferDestinationOptimal,
                Vk.AccessNone, Vk.AccessTransferWrite, Vk.StageTopOfPipe, Vk.StageTransfer);

            VkImageBlit blit = new()
            {
                SourceSubresource = new VkImageSubresourceLayers { AspectMask = Vk.AspectColor, LayerCount = 1 },
                SourceOffset1 = new VkOffset3D { X = Width, Y = Height, Z = 1 },
                DestinationSubresource = new VkImageSubresourceLayers { AspectMask = Vk.AspectColor, LayerCount = 1 },
                DestinationOffset1 = new VkOffset3D { X = Width, Y = Height, Z = 1 },
            };
            vk.CmdBlitImage(commandBuffer, image, Vk.LayoutTransferSourceOptimal, target,
                Vk.LayoutTransferDestinationOptimal, 1, &blit, 0);

            RecordBarrier(target, Vk.LayoutTransferDestinationOptimal, Vk.LayoutPresentSourceKhr,
                Vk.AccessTransferWrite, Vk.AccessMemoryRead, Vk.StageTransfer, Vk.StageBottomOfPipe);
            VulkanApi.Check(vk.EndCommandBuffer(commandBuffer), "vkEndCommandBuffer");

            fixed (nint* buffers = &commandBuffer)
            {
                VkSubmitInfo submit = new()
                {
                    Type = Vk.StructureSubmitInfo,
                    CommandBufferCount = 1,
                    CommandBuffers = buffers,
                };
                VulkanApi.Check(vk.QueueSubmit(queue, 1, &submit, 0), "vkQueueSubmit");
            }

            // The prototype keeps one frame in flight, so the queue drains before presenting.
            VulkanApi.Check(vk.QueueWaitIdle(queue), "vkQueueWaitIdle");

            fixed (ulong* swapchains = &swapchain)
            fixed (uint* indices = &acquiredIndex)
            {
                VkPresentInfo present = new()
                {
                    Type = Vk.StructurePresentInfoKhr,
                    SwapchainCount = 1,
                    Swapchains = swapchains,
                    ImageIndices = indices,
                };
                int presented = vk.QueuePresent(queue, &present);
                if (presented is Vk.ErrorOutOfDateKhr or Vk.SuboptimalKhr)
                {
                    Resize();
                    return;
                }

                VulkanApi.Check(presented, "vkQueuePresentKHR");
            }
        }

        /// <summary>Transitions the offscreen image on its own submission, outside frame recording.</summary>
        /// <param name="layout">The layout to leave the image in.</param>
        private void TransitionOffscreenImage(uint layout)
        {
            if (imageLayout == layout)
            {
                return;
            }

            VulkanApi.Check(vk.ResetCommandPool(device, commandPool, 0), "vkResetCommandPool");
            VkCommandBufferBeginInfo begin = new()
            {
                Type = Vk.StructureCommandBufferBeginInfo,
                Flags = Vk.CommandBufferOneTimeSubmit,
            };
            VulkanApi.Check(vk.BeginCommandBuffer(commandBuffer, &begin), "vkBeginCommandBuffer");
            RecordBarrier(image, imageLayout, layout, Vk.AccessNone, Vk.AccessColorAttachmentWrite,
                Vk.StageTopOfPipe, Vk.StageColorAttachmentOutput);
            VulkanApi.Check(vk.EndCommandBuffer(commandBuffer), "vkEndCommandBuffer");
            fixed (nint* buffers = &commandBuffer)
            {
                VkSubmitInfo submit = new()
                {
                    Type = Vk.StructureSubmitInfo,
                    CommandBufferCount = 1,
                    CommandBuffers = buffers,
                };
                VulkanApi.Check(vk.QueueSubmit(queue, 1, &submit, 0), "vkQueueSubmit");
            }

            VulkanApi.Check(vk.QueueWaitIdle(queue), "vkQueueWaitIdle");
            imageLayout = layout;
        }

        /// <summary>Records one image layout transition into the open command buffer.</summary>
        /// <param name="subject">The image to transition.</param>
        /// <param name="oldLayout">The layout the image is currently in.</param>
        /// <param name="newLayout">The layout to move it to.</param>
        /// <param name="sourceAccess">Access that must complete before the transition.</param>
        /// <param name="destinationAccess">Access that becomes available after it.</param>
        /// <param name="sourceStage">The stage that must reach the barrier first.</param>
        /// <param name="destinationStage">The stage that waits on it.</param>
        private void RecordBarrier(ulong subject, uint oldLayout, uint newLayout, uint sourceAccess,
            uint destinationAccess, uint sourceStage, uint destinationStage)
        {
            VkImageMemoryBarrier barrier = new()
            {
                Type = Vk.StructureImageMemoryBarrier,
                SourceAccessMask = sourceAccess,
                DestinationAccessMask = destinationAccess,
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SourceQueueFamilyIndex = uint.MaxValue,
                DestinationQueueFamilyIndex = uint.MaxValue,
                Image = subject,
                SubresourceRange = new VkImageSubresourceRange
                {
                    AspectMask = Vk.AspectColor,
                    LevelCount = 1,
                    LayerCount = 1,
                },
            };
            vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
        }

        /// <inheritdoc />
        public override void Resize()
        {
            CheckThread();
            Context.Flush(submit: true, synchronous: true);
            ClearSurface();
            frameAcquired = false;
            VulkanApi.Check(vk.DeviceWaitIdle(device), "vkDeviceWaitIdle");
            Context.ResetContext();
            DestroySwapchain();
            DestroyImage();
            if (GetDrawableSize(out _, out _))
            {
                CreateSwapchain();
            }
        }

        /// <summary>Releases the swapchain and its image handles.</summary>
        private void DestroySwapchain()
        {
            if (swapchain == 0)
            {
                return;
            }

            vk.DestroySwapchain(device, swapchain, 0);
            swapchain = 0;
            swapchainImages = [];
        }

        /// <summary>Releases the offscreen image and its memory.</summary>
        private void DestroyImage()
        {
            if (image != 0)
            {
                vk.DestroyImage(device, image, 0);
                image = 0;
            }

            if (imageMemory != 0)
            {
                vk.FreeMemory(device, imageMemory, 0);
                imageMemory = 0;
            }

            imageLayout = Vk.LayoutUndefined;
        }
    }
}
