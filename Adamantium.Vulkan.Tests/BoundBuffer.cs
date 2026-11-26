using System;
using Adamantium.Vulkan.Core;
using Buffer = Adamantium.Vulkan.Core.Buffer;

namespace Adamantium.Vulkan.Tests;

/// <summary>A buffer and the memory bound to it, let go of together.</summary>
internal sealed unsafe class BoundBuffer(Device device, Buffer buffer, DeviceMemory memory, ulong size) : IDisposable
{
    public Buffer Buffer { get; } = buffer;

    public DeviceMemory Memory { get; } = memory;

    public ulong Size { get; } = size;

    /// <summary>Writes <paramref name="data"/> at the start of host-visible memory.</summary>
    public void Write<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);
        var mapped = device.MapMemory(Memory, 0, Size, 0);
        bytes.CopyTo(new Span<byte>((void*)mapped, (int)Size));
        device.UnmapMemory(Memory);
    }

    /// <summary>The whole of host-visible memory, copied out.</summary>
    public byte[] Read()
    {
        var mapped = device.MapMemory(Memory, 0, Size, 0);
        var bytes = new ReadOnlySpan<byte>((void*)mapped, (int)Size).ToArray();
        device.UnmapMemory(Memory);
        return bytes;
    }

    public void Dispose()
    {
        device.DestroyBuffer(Buffer);
        device.FreeMemory(Memory);
    }
}
