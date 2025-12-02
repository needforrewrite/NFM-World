using System.Diagnostics;
using System.Runtime.InteropServices;
using NFMWorld.DriverInterface;
using NFMWorld.Mad;
using NImpeller;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.SDL;
using Color = NFMWorld.Util.Color;

namespace NFMWorld;

public unsafe class NFMWVulkan
{
    private readonly Sdl _sdl;
    private Window* _window;
    private ImpellerContext _impellerContext;
    private ImpellerVulkanSwapchain _vulkanSwapchain;
    
    private double _updatePeriod;
    private Stopwatch _updateStopwatch = Stopwatch.StartNew();
    private NImpellerBackend _backend;
    private ImpellerSurface _surface;

    public double UpdatesPerSecond
    {
        get => _updatePeriod <= double.Epsilon ? 0 : 1 / _updatePeriod;
        set => _updatePeriod = value <= double.Epsilon ? 0 : 1 / value;
    }
    
    public NFMWVulkan()
    {
        _sdl = Sdl.GetApi();
        UpdatesPerSecond = 63f;
    }
    
    public bool Initialize()
    {
        if (_sdl.Init(Sdl.InitVideo) < 0)
        {
            Console.WriteLine("SDL initialization failed: {0}", _sdl.GetErrorS());
            return false;
        }

        const WindowFlags windowFlags = WindowFlags.Shown | WindowFlags.Resizable | WindowFlags.Vulkan;
        
        const string title = "NFM World - Vulkan";
        const int width = 1280;
        const int height = 720;

        _window = _sdl.CreateWindow(
            title,
            Sdl.WindowposCentered,
            Sdl.WindowposCentered,
            width,
            height,
            (uint)windowFlags
        );

        if (_window == null)
        {
            Console.WriteLine("Window creation failed: {0}", _sdl.GetErrorS());
            _sdl.Quit();
            return false;
        }

        uint extensionCount;
        byte* extensions;
        _sdl.VulkanGetInstanceExtensions(_window, &extensionCount, &extensions);
        var vkGetProcAddress = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr>)_sdl.VulkanGetVkGetInstanceProcAddr();
        _impellerContext = ImpellerContext.CreateVulkanNew((instance, proc) => vkGetProcAddress(instance, proc), false)!;

        var info = _impellerContext.GetVulkanInfo()!.Value;

        VkNonDispatchableHandle surfaceHandle = default;
        _sdl.VulkanCreateSurface(_window, new(info.Vk_instance), ref surfaceHandle);
        _vulkanSwapchain = _impellerContext.VulkanSwapchainCreateNew(new IntPtr((long)surfaceHandle.Handle))!;

        _backend = new NImpellerBackend();
        IBackend.Backend = _backend;

        return true;
    }

    public void Run()
    {
        GameSparker.Load();
        
        bool running = true;

        while (running)
        {
            Event evt = default;
            while (_sdl.PollEvent(ref evt) != 0)
            {
                if (evt.Type == (uint)EventType.Quit)
                {
                    running = false;
                }
            }

            int width = 0, height = 0;
            _sdl.GetWindowSize(_window, ref width, ref height);
            
            // Console.WriteLine($"Width: {width}, Height: {height}");

            var windowSize = new ImpellerISize(width, height);

            ImpellerDisplayList displayList;
            using (var drawListBuilder = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, width, height))!)
            {
                DoUpdate();
                DoRender(drawListBuilder);

                displayList = drawListBuilder.CreateDisplayListNew()!;
            }

            using (displayList)
            {
                using (_surface = _vulkanSwapchain.AcquireNextSurfaceNew()!)
                {
                    _surface.DrawDisplayList(displayList);
                    _surface.Present();
                }
            }
        }
    }

    private void BeforeDoUpdate()
    {
        var delta = _updateStopwatch.Elapsed.TotalSeconds;
        if (delta >= _updatePeriod)
        {
            _updateStopwatch.Restart();
            DoUpdate();
        }
    }

    private void DoUpdate()
    {
        GameSparker.GameTick();
        
    }

    private void DoRender(ImpellerDisplayListBuilder drawListBuilder)
    {
        _backend.DrawListBuilder = drawListBuilder;
        
        
        using (var paint = ImpellerPaint.New()!)
        {
            paint.SetColor(ImpellerColor.FromArgb(1, 1, 0, 0));
            drawListBuilder.DrawRect(new ImpellerRect(0, 0, 1280, 720), paint);
        }
        
        GameSparker.Render();
        
        // G.SetColor(new Color(0, 0, 0));
        // G.DrawString("Render: " + GameSparker.lastRenderTime + "us", 100, 100);
        // G.DrawString("Update: " + GameSparker.lastTickTime + "us", 100, 125);
    }
    
    public static void Main()
    {
        var program = new NFMWVulkan();
        if (!program.Initialize())
        {
            Console.WriteLine($"Could not initialize NFMWVulkan.");
            return;
        }
        
        program.Run();
    }
}