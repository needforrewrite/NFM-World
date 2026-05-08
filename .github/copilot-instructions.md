# Copilot / AI assistant instructions — NFM-World

Keep guidance short and actionable. Reference files and patterns below when making changes.

- **Big picture:** The playable app lives in `nfm-world/` (`NFMWorld.csproj`) and depends on many sibling projects (notably `NFMWorld.Library`, `FNA`, `NvgSharp`, `MonoGame.ImGuiNet`). Treat `nfm-world` as the app entry; engine/framework code is in `FNA/` and rendering/GUI glue under `NvgSharp/`, `FontStashSharp/`, and `MonoGame.ImGuiNet/`.

- **Build / run:** Use the .NET SDK (this repo targets `net10.0`). Typical commands:
  - Build entire workspace: `dotnet build nfm-world.sln -c Debug`
  - Build single project: `dotnet build nfm-world/NFMWorld.csproj`
  - Run: `dotnet run --project nfm-world/NFMWorld.csproj`
  - Run tests: `dotnet test --no-build` from solution root or test project folder.

- **Publish / native files:** `NFMWorld.csproj` contains platform-specific copy targets that move native libs from `nfm-world/skiadriver/` into the output folder. Do not remove or duplicate these targets; update them only when adding new native artifacts.

- **Shaders & tools:** Shaders in `data/shaders/*.fx` are compiled to `.fxb` via `fxc.exe` during build (`BuildShaders` target). On non-Windows builds the project expects `wine` + a Windows DirectX SDK `fxc.exe` (winetricks `dxsdk_jun2010`) or a `tools/fxc.exe` helper. If altering shader handling, preserve the MSBuild targets in `nfm-world/NFMWorld.csproj` that produce and include `.fxb` files.

- **Platform nuances:**
  - Native libraries (.dll/.so/.dylib) are copied by MSBuild targets: see `CopyCustomContentWindows`, `CopyCustomContentLinux`, and OS conditions inside `nfm-world/NFMWorld.csproj`.
  - The project sets `AllowUnsafeBlocks` and several compile symbols (e.g. `USE_BAS`). Keep those when editing compilation logic.

- **Project patterns / conventions:**
  - Most subprojects are referenced with `ProjectReference` from `NFMWorld.csproj`; prefer keeping cross-project ref changes small and use `dotnet sln` only when adding/removing whole projects.
  - Game logic vs UI: `NFMWorld.Library` contains backend/game systems; UI, rendering and native interops live in `nfm-world/`, `NvgSharp/`, and `FNA/`.
  - Data and assets: many projects include `None Include="data\**\*" CopyToOutputDirectory=...` — follow existing CopyToOutputDirectory semantics rather than inventing new asset pipelines.

- **Dependencies & runtime notes:**
  - NuGet packages used by the app include `ImGui.NET`, `ManagedBass` (and related), `Silk.NET.OpenGL`. When adding packages, prefer matching versions already in the csproj.
  - For local developer builds on Linux/macOS, ensure native dependencies (OpenGL drivers, libSDL, wine for shader compilation) are present.

- **Tests and CI hints:**
  - Run `dotnet test` at repo root; test projects are co-located with their libraries (e.g. `HoleyDiver.UnitTest`).
  - CI should `dotnet restore` then `dotnet build` then `dotnet test`. If CI runs on Linux/macOS, ensure native copy targets won't fail due to missing platform files — add conditional guards or include stub files as needed.

- **When editing MSBuild targets:** Inspect `nfm-world/NFMWorld.csproj` for patterns: shader compilation targets, copy-to-output items, and platform-specific Publish hooks. Changes here affect runtime asset layout; run a local `dotnet publish` to validate.

- **Where to look for behavior:**
  - Initialization / main loop: `nfm-world/NFMWorld.csproj` → `NFMWWindow.cs`, `NFMWorld.csproj` references `NFMWorld.cs` and `NFMWWindow.cs` as logical entry points.
  - Game backend: [NFMWorld.Library](NFMWorld.Library/NFMWorld.Library.csproj)
  - Rendering and fonts: `NvgSharp/`, `FontStashSharp/` and `FNA/`.

- **Examples to follow:**
  - Adding native files: mirror the `skiadriver` copy targets in `nfm-world/NFMWorld.csproj` rather than ad-hoc scripts.
  - Adding compiled assets (shaders): add `.fx` to `CompileShader` ItemGroup so builders include shader compilation automatically.

- **Do NOT:**
  - Remove or flatten the MSBuild platform conditionals without testing on all OSes.
  - Change shader/tool expectations without keeping a non-Windows fallback path (`tools/fxc.exe` or documented wine steps).

If anything above is unclear or you want examples inserted for a specific task (adding a native plugin, publishing for Linux, or modifying shader flow), tell me which area to expand and I will update this file.

---

## XAML UI System (Yoga Flexbox)

The project uses a custom XAML runtime built on XamlX (IL weaving) with Avalonia-compatible tooling. Use XAML to define UI hierarchies instead of inline C# node construction.

### Creating a new XAML View

1. **Create the XAML file** (e.g., `nfm-world/mad/ui/hud/MyView.xaml`):
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <Node xmlns="clr-namespace:nfm_world.ui.yoga"
         xmlns:elements="clr-namespace:nfm_world.ui.elements"
         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
         x:Class="NFMWorld.UI.Hud.MyView"
         FlexDirection="Column"
         AlignItems="FlexStart"
         Gap="10"
         Padding="10">

       <!-- Child elements here -->
       <elements:TextRun Name="TitleText" Font="24px bold Adventure" Text="Hello" />
   </Node>
   ```

2. **Create the code-behind** (e.g., `nfm-world/mad/ui/hud/MyView.cs`):
   ```csharp
   using nfm_world.ui.yoga;

   namespace NFMWorld.UI.Hud;

   public partial class MyView : Node  // or View
   {
       public MyView()
       {
           InitializeComponent();
           // Post-initialization logic here
       }
   }
   ```

3. **Register in csproj** — Add both files to `NFMWorld.csproj`:
   ```xml
   <ItemGroup>
       <AvaloniaXaml Include="mad\ui\hud\MyView.xaml" />
   </ItemGroup>

   <ItemGroup>
       <Compile Update="mad\ui\hud\MyView.cs">
           <DependentUpon>MyView.xaml</DependentUpon>
       </Compile>
   </ItemGroup>
   ```

### XAML Syntax Reference

**Namespaces:**
- Default: `xmlns="clr-namespace:nfm_world.ui.yoga"` — Node, View, layout types
- Elements: `xmlns:elements="clr-namespace:nfm_world.ui.elements"` — TextRun, TextBlock, MeasureBar
- XAML: `xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"` — x:Class, x:Name

**Required root attribute:**
- `x:Class="namespace.ClassName"` — Must match the code-behind class fully qualified name

**Yoga Flexbox properties (set on Node/View):**
- `FlexDirection` — `Row`, `Column`, `RowReverse`, `ColumnReverse`
- `AlignItems` — `FlexStart`, `FlexEnd`, `Center`, `Stretch`, `Baseline`
- `JustifyContent` — `FlexStart`, `FlexEnd`, `Center`, `SpaceBetween`, `SpaceAround`, `SpaceEvenly`
- `Gap`, `Padding`, `Margin` — numeric values (e.g., `Gap="10"`)
- `Flex` — flex grow factor (e.g., `Flex="1"`)
- `Top`, `Left`, `Right`, `Bottom` — absolute positioning

**Naming elements:**
- Use `Name="ElementName"` to expose elements to code-behind
- Access via generated fields/properties after `InitializeComponent()`

**Type converters (string → type):**
- `Font` — `"FontFamily,Style,Size"` (e.g., `Font="Adventure,1,24"`)
- `Color` — `"R,G,B,A"` (e.g., `Color="255,255,255,255"`)

### Code-Behind Patterns

**Accessing named elements after InitializeComponent:**
```csharp
public partial class MyView : Node
{
    public MyView()
    {
        InitializeComponent();
        // Named elements are now available
        TitleText.Text = "Updated";
    }
}
```

**Post-initialization setup** (see `PowerDamageBars.cs`):
```csharp
public PowerDamageBars()
{
    InitializeComponent();
    // Configure elements that need runtime data
    PowerBar.BarColor = GetPowerBarColor(1f);
    PowerBar.Width = IBackend.Backend.LoadCachedImage("data/images/power.gif").Width;
}
```

### Current Limitations

- **No markup extensions** — `{Binding}`, `{StaticResource}`, etc. are not supported
- **No styles/templates** — All styling is inline or in code-behind
- **No data binding** — Update UI programmatically in code-behind
- **Limited type converters** — Only Font, Color, Measurement types have converters
- **Build task required** — XAML files must be in `<AvaloniaXaml>` ItemGroup to be compiled

### Troubleshooting

- **"Partial class with single part"** warning — Expected; XamlX uses IL weaving, not source generation
- **Missing `InitializeComponent`** — Ensure XAML file is in `<AvaloniaXaml>` ItemGroup and `x:Class` matches
- **Type not found** — Check namespace in `xmlns` matches the actual C# namespace
- **Property not found** — Ensure property has public setter; check for [TypeConverter] attribute if needed

### AOT Publishing

When adding new XAML views, you must update [nfm-world/rd.xml](nfm-world/rd.xml) to preserve the generated `Populate` methods for Native AOT compilation:

```xml
<Type Name="NFMWorld.UI.Hud.MyView" Dynamic="Required All">
  <Method Name="Populate" Dynamic="Required" />
</Type>
```

Without this, `dotnet publish` with AOT will fail with "Could not find Method(s) [NFMWorld]NFMWorld.UI.Hud.MyView.Populate specified by a Runtime Directive"

### Namespace Dependencies

When updating the namespaces for Yoga types, make sure to update Avalonia.Generators:
```cs
    public static bool IsAvaloniaStyledElement(this IXamlType clrType) =>
        Inherits(clrType, "NFMWorld.UI.Yoga.Node");
    public static bool IsAvaloniaWindow(this IXamlType clrType) =>
        Inherits(clrType, "NFMWorld.UI.Yoga.Window");
```