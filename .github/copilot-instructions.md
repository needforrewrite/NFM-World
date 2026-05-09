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

---

## XamlX C# Code Generator (Source Generator Backend)

The project has a source-generator backend that compiles XAML to C# at build time (instead of IL weaving). It lives across two projects, source-included into the Roslyn analyzer DLL.

### Architecture

```
Avalonia.Generators/          — Roslyn incremental source generator (netstandard2.0)
  Compiler/
    RoslynTypeSystem.cs       — IXamlTypeSystem wrapping Roslyn ISymbol API
    XamlCSharpCompiler.cs     — Wraps XamlILCompiler for XAML→C# compilation
  NameGenerator/
    AvaloniaNameIncrementalGenerator.cs — Main source gen entry point & pipeline
    XamlXCodeGenerator.cs     — Generates partial class with InitializeComponent + compiled members

XamlX.IL.CSharp/              — C# emission backend (source-included into Avalonia.Generators)
  CSharpEmitter.cs            — Translates IL opcodes → C# statements via virtual eval stack
  CSharpTypeBuilder.cs        — IXamlTypeBuilder collecting type/method/field defs → C# source
  CSharpFormatting.cs         — Type name formatting (global:: prefix, generics, primitives)
```

XamlX and XamlX.IL.CSharp are **source-included** (not ProjectReference) into Avalonia.Generators via `<Compile Include="..\XamlX\..." />` with `XAMLX_INTERNAL` define for internal visibility.

### Pipeline flow

1. `AvaloniaNameIncrementalGenerator` parses XAML into `parsedXamlClasses`
2. Creates `XamlCSharpCompiler` (wrapping `XamlILCompiler` + `CSharpTypeBuilder`)
3. Each view: `CompileView(xamlSource, filePath, xClassName)` → compiled C# members
4. `XamlXCodeGenerator` embeds compiled members into a partial class with `InitializeComponent()` calling `Populate(null, this)`
5. Shared `__XamlContext.g.cs` is emitted once via `ContextSource`

### Key classes

- **CSharpEmitter** — Virtual eval stack (`_evalStack`) of `CSharpExpression(expr, type?)`. Translates IL opcodes to C# statements. Tracks arg/local types for correct branching. Pre-declares temp variables (`_tempLocals`) for Dup to survive goto boundaries. Uses `_labelStackSnapshots` dict for branch-aware stack reconciliation.
- **CSharpTypeBuilder** — Generates C# class source. Has nested types: `ConstructedCSharpType` (MakeGenericType result), `ConstructedCtorWrapper` (ctor with constructed declaring type), `CSharpGenericParameterType`.
- **CSharpFormatting** — Handles `global::` prefixing, primitive aliases, array types, nested types, generic parameter names.

### Common pitfalls when modifying the emitter

1. **Eval stack type tracking** — Every `Push()` should include the type when known. Methods that `Pop()` and operate on the value must check `.Type` to emit correct casts. Missing types cause `object` temps from Dup and wrong code downstream.

2. **Dup scoping** — `Dup` creates temp variables (`__tmp_N`). These must be pre-declared at method scope (in `_tempLocals`) because they may be referenced across goto labels. Never use `var` for Dup temps inside conditional blocks.

3. **IL→C# property/indexer conversion** — `get_X()` → `.X`, `set_X(val)` → `.X = val`, `get_Item(i)` → `[i]`, `set_Item(i, val)` → `[i] = val`. This happens in `EmitMethodCall`. Explicit interface implementations use full `Interface.Method` naming.

4. **Enum casts** — XamlX compiles enum values as `int` literals. Property setters and Stfld must wrap with `((EnumType)value)` when the target type `.IsEnum`.

5. **Instance method/field casts** — When the eval stack's tracked type doesn't match the method's `DeclaringType`, emit `((DeclaringType)obj)`. Use `IsAssignableFrom` to avoid unnecessary casts.

6. **Generic types** — `CSharpTypeBuilder.IsAssignableFrom` must handle `ConstructedCSharpType` (e.g., `Context<View>` is assignable to `Context<TTarget>` definition). `FormatType` must include generic parameters for `CSharpTypeBuilder` types.

7. **SkipNodeEmitter** — Handles error-recovery `ISkipXamlAstNode` nodes. Must `Pop()` + return `Void(1)` to consume the stack item that `ManipulationGroupEmitter` Dup'd for this child.

8. **Generated file double-inclusion** — `EmitCompilerGeneratedFiles=true` writes `.g.cs` to `Generated/`. The csproj must have `<Compile Remove="Generated/**" />` to prevent the implicit glob from double-compiling them alongside the in-memory source gen output.

9. **Branch stack divergence** — Conditional branches (`Brfalse`/`Brtrue`) flush the eval stack to temps then save a snapshot via `SaveStackSnapshot`. Unconditional `Br` also flushes+saves and then **clears** the eval stack (dead code after goto). At `MarkLabel`, reconcile assignments are emitted **before** the label so `goto` skips them — only the fall-through path executes them. If the target label already has a snapshot (from a prior branch), `SaveStackSnapshot` emits assignments from the current stack's temps into the canonical snapshot's temps instead of overwriting.

### Debugging generated output

- Set `EmitCompilerGeneratedFiles=true` in the csproj (already set)
- Generated files appear in `nfm-world/Generated/Avalonia.Generators/.../*.g.cs`
- `__XamlContext.g.cs` — shared context type (should appear once, not duplicated)
- `__AvaloniaLogs.g.cs` — diagnostic log output from the source generator
- Per-view files: `Namespace.ClassName.g.cs`
- To iterate: `Remove-Item -Recurse nfm-world/Generated; dotnet build nfm-world/NFMWorld.csproj`

### XamlX IL stack semantics (for emitter work)

The XamlX compiler uses IL stack conventions:
- **ValueWithManipulationsEmitter**: emits value (Newobj), then Dup + emit manipulation, leaving one copy
- **ManipulationGroupEmitter**: Dup before each non-final child, each child consumes 1 via its emitter
- **ObjectInitializationNodeEmitter**: optionally Dup for BeginInit/EndInit, pushes/pops parent stack
- **PropertyAssignmentEmitter**: consumes 1 (the parent object), emits property value + setter call
- The CSharpEmitter must faithfully track these stack operations via its `_evalStack`