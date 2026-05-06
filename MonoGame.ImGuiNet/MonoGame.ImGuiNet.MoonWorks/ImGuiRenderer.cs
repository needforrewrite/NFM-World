#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Input;
using MoonWorks.Storage;
using GpuBuffer = MoonWorks.Graphics.Buffer;
using GpuCommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace MonoGame.ImGuiNet.MoonWorks;

internal class TextureInfo
{
	public Texture? Texture;
	public bool IsManaged;
}

/// <summary>
/// ImGui renderer for MoonWorks. Replaces the FNA-based ImGuiRenderer.
/// </summary>
public class ImGuiRenderer : IDisposable
{
	[StructLayout(LayoutKind.Sequential)]
	private struct ImGuiVertex : IVertexType
	{
		public Vector2 Position;
		public Vector2 TexCoord;
		public uint Color; // RGBA packed as uint (matches ImDrawVert layout)

		public static VertexElementFormat[] Formats =>
		[
			VertexElementFormat.Float2,     // Position
			VertexElementFormat.Float2,     // TexCoord
			VertexElementFormat.Ubyte4Norm  // Color
		];

		public static uint[] Offsets => [0, 8, 16];
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct VertexUniforms
	{
		public Matrix4x4 ProjectionMatrix;
	}

	private readonly GraphicsDevice _device;
	private readonly Window _window;

	private Shader _vertexShader;
	private Shader _fragmentShader;
	private GraphicsPipeline _pipeline;
	private readonly Sampler _sampler;

	private GpuBuffer? _vertexBuffer;
	private GpuBuffer? _indexBuffer;
	private int _vertexBufferSize;
	private int _indexBufferSize;

	private readonly Dictionary<ImTextureID, TextureInfo> _textures = new();
	private int _nextTexId = 1;

	private readonly TextureFormat _colorTargetFormat;

	public ImGuiRenderer(
		GraphicsDevice device,
		Window window,
		TitleStorage storage,
		string shaderDir,
		TextureFormat colorTargetFormat
	)
	{
		_device = device ?? throw new ArgumentNullException(nameof(device));
		_window = window;
		_colorTargetFormat = colorTargetFormat;

		var context = ImGui.CreateContext();
		ImGui.SetCurrentContext(context);

		_sampler = Sampler.Create(device, SamplerCreateInfo.LinearClamp);

		LoadShaders(storage, shaderDir);
		CreatePipeline();
		SetupBackendCapabilities();
	}

	[MemberNotNull(nameof(_vertexShader), nameof(_fragmentShader))]
	private void LoadShaders(TitleStorage storage, string shaderDir)
	{
		_vertexShader = ShaderCross.Create(
			_device, storage,
			$"{shaderDir}/ImGui.vert.hlsl", "main",
			ShaderCross.ShaderFormat.HLSL, ShaderStage.Vertex,
			name: "ImGuiVert",
			includeDir: shaderDir
		);

		_fragmentShader = ShaderCross.Create(
			_device, storage,
			$"{shaderDir}/ImGui.frag.hlsl", "main",
			ShaderCross.ShaderFormat.HLSL, ShaderStage.Fragment,
			name: "ImGuiFrag",
			includeDir: shaderDir
		);
	}

	[MemberNotNull(nameof(_pipeline))]
	private void CreatePipeline()
	{
		_pipeline = GraphicsPipeline.Create(_device, new GraphicsPipelineCreateInfo
		{
			Name = "ImGui",
			VertexShader = _vertexShader,
			FragmentShader = _fragmentShader,
			VertexInputState = VertexInputState.CreateSingleBinding<ImGuiVertex>(),
			PrimitiveType = PrimitiveType.TriangleList,
			RasterizerState = new RasterizerState
			{
				CullMode = CullMode.None,
				FillMode = FillMode.Fill,
				FrontFace = FrontFace.CounterClockwise,
				EnableDepthClip = false
			},
			MultisampleState = MultisampleState.None,
			DepthStencilState = DepthStencilState.Disable,
			TargetInfo = new GraphicsPipelineTargetInfo
			{
				ColorTargetDescriptions =
				[
					new ColorTargetDescription
					{
						Format = _colorTargetFormat,
						BlendState = ColorTargetBlendState.NonPremultipliedAlphaBlend
					}
				]
			}
		});
	}

	private void SetupBackendCapabilities()
	{
		var io = ImGui.GetIO();
		io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

		var platformIO = ImGui.GetPlatformIO();
		platformIO.RendererTextureMaxWidth = 8192;
		platformIO.RendererTextureMaxHeight = 8192;
	}

	public virtual void RebuildFontAtlas() { }

	public virtual unsafe ImTextureRef BindTexture(Texture texture)
	{
		var texId = new IntPtr(_nextTexId++);
		_textures[texId] = new TextureInfo
		{
			Texture = texture,
			IsManaged = false,
		};
		return new ImTextureRef(null, texId);
	}

	public virtual void UnbindTexture(ImTextureRef textureRef)
	{
		if (_textures.TryGetValue(textureRef.TexID, out var textureInfo))
		{
			if (textureInfo.IsManaged)
				textureInfo.Texture?.Dispose();
			_textures.Remove(textureRef.TexID);
		}
	}

	public virtual void BeginLayout(float deltaTime, Inputs inputs, uint windowWidth, uint windowHeight)
	{
		var io = ImGui.GetIO();
		io.DeltaTime = Math.Max(deltaTime, 0.0001f);
		io.DisplaySize = new Vector2(windowWidth, windowHeight);
		io.DisplayFramebufferScale = new Vector2(1f, 1f);

		UpdateInput(inputs);
		ImGui.NewFrame();
	}

	public virtual void EndLayout(GpuCommandBuffer gpuCommandBuffer, RenderPass renderPass)
	{
		ImGui.Render();
		unsafe
		{
			var drawData = ImGui.GetDrawData();
			ProcessTextureUpdates(drawData);
			RenderDrawData(gpuCommandBuffer, renderPass, drawData);
		}
	}

	public virtual void UpdateTexture(ImTextureDataPtr textureData)
	{
		switch (textureData.Status)
		{
			case ImTextureStatus.WantCreate:  CreateManagedTexture(textureData);  break;
			case ImTextureStatus.WantUpdates: UpdateManagedTexture(textureData);  break;
			case ImTextureStatus.WantDestroy: DestroyManagedTexture(textureData); break;
		}
	}

	private unsafe void CreateManagedTexture(ImTextureDataPtr textureData)
	{
		var format = textureData.Format == ImTextureFormat.Rgba32
			? TextureFormat.R8G8B8A8Unorm
			: TextureFormat.R8Unorm;

		var texture = Texture.Create2D(_device, (uint)textureData.Width, (uint)textureData.Height,
			format, TextureUsageFlags.Sampler);

		if (textureData.Pixels != null)
			UploadTextureData(texture, textureData);

		_textures[textureData.TexID] = new TextureInfo { Texture = texture, IsManaged = true };
		textureData.SetStatus(ImTextureStatus.Ok);
	}

	private unsafe void UpdateManagedTexture(ImTextureDataPtr textureData)
	{
		var texId = textureData.GetTexID();
		if (!_textures.TryGetValue(texId, out var textureInfo) || textureInfo.Texture == null)
			return;

		var texture = textureInfo.Texture;
		var newFormat = textureData.Format == ImTextureFormat.Rgba32
			? TextureFormat.R8G8B8A8Unorm
			: TextureFormat.R8Unorm;

		if (texture.Width != (uint)textureData.Width || texture.Height != (uint)textureData.Height || texture.Format != newFormat)
		{
			texture.Dispose();
			texture = Texture.Create2D(_device, (uint)textureData.Width, (uint)textureData.Height,
				newFormat, TextureUsageFlags.Sampler);
			textureInfo.Texture = texture;
		}

		if (textureData.Pixels != null)
			UploadTextureData(texture, textureData);

		textureData.SetStatus(ImTextureStatus.Ok);
	}

	private unsafe void UploadTextureData(Texture texture, ImTextureDataPtr textureData)
	{
		var bytesPerPixel = textureData.Format == ImTextureFormat.Rgba32 ? 4 : 1;
		var dataSize = (uint)(textureData.Width * textureData.Height * bytesPerPixel);

		using var transferBuffer = TransferBuffer.Create<byte>(_device, TransferBufferUsage.Upload, dataSize);
		var span = transferBuffer.Map<byte>(false);
		new Span<byte>(textureData.Pixels, (int)dataSize).CopyTo(span);
		transferBuffer.Unmap();

		var cmd = _device.AcquireCommandBuffer();
		var copyPass = cmd.BeginCopyPass();
		copyPass.UploadToTexture(transferBuffer, texture, false);
		cmd.EndCopyPass(copyPass);
		_device.Submit(cmd);
	}

	private void DestroyManagedTexture(ImTextureDataPtr textureData)
	{
		var texId = textureData.GetTexID();
		if (_textures.TryGetValue(texId, out var textureInfo))
		{
			if (textureInfo.IsManaged)
				textureInfo.Texture?.Dispose();
			_textures.Remove(texId);
		}
	}

	private void UpdateInput(Inputs inputs)
	{
		var io = ImGui.GetIO();
		var keyboard = inputs.Keyboard;
		var mouse = inputs.Mouse;

		io.AddMousePosEvent(mouse.X, mouse.Y);
		io.AddMouseButtonEvent(0, mouse.LeftButton.IsHeld);
		io.AddMouseButtonEvent(1, mouse.RightButton.IsHeld);
		io.AddMouseButtonEvent(2, mouse.MiddleButton.IsHeld);
		io.AddMouseWheelEvent(0, mouse.Wheel);

		// Keyboard
		foreach (var scancode in Enum.GetValues<ScanCode>())
		{
			if (scancode == ScanCode.Unknown) continue;
			if (TryMapKey(scancode, out var imguiKey))
				io.AddKeyEvent(imguiKey, keyboard.IsHeld(scancode));
		}
	}

	private static bool TryMapKey(ScanCode scancode, out ImGuiKey imguiKey)
	{
		imguiKey = scancode switch
		{
			ScanCode.Backspace => ImGuiKey.Backspace,
			ScanCode.Tab => ImGuiKey.Tab,
			ScanCode.Return => ImGuiKey.Enter,
			ScanCode.CapsLock => ImGuiKey.CapsLock,
			ScanCode.Escape => ImGuiKey.Escape,
			ScanCode.Space => ImGuiKey.Space,
			ScanCode.PageUp => ImGuiKey.PageUp,
			ScanCode.PageDown => ImGuiKey.PageDown,
			ScanCode.End => ImGuiKey.End,
			ScanCode.Home => ImGuiKey.Home,
			ScanCode.Left => ImGuiKey.LeftArrow,
			ScanCode.Right => ImGuiKey.RightArrow,
			ScanCode.Up => ImGuiKey.UpArrow,
			ScanCode.Down => ImGuiKey.DownArrow,
			ScanCode.PrintScreen => ImGuiKey.PrintScreen,
			ScanCode.Insert => ImGuiKey.Insert,
			ScanCode.Delete => ImGuiKey.Delete,
			ScanCode.D0 => ImGuiKey.Key0,
			>= ScanCode.D1 and <= ScanCode.D9 => ImGuiKey.Key1 + (scancode - ScanCode.D1),
			>= ScanCode.A and <= ScanCode.Z => ImGuiKey.A + (scancode - ScanCode.A),
			ScanCode.Keypad0 => ImGuiKey.Keypad0,
			>= ScanCode.Keypad1 and <= ScanCode.Keypad9 => ImGuiKey.Keypad1 + (scancode - ScanCode.Keypad1),
			ScanCode.KeypadMultiply => ImGuiKey.KeypadMultiply,
			ScanCode.KeypadPlus => ImGuiKey.KeypadAdd,
			ScanCode.KeypadMinus => ImGuiKey.KeypadSubtract,
			ScanCode.KeypadPeriod => ImGuiKey.KeypadDecimal,
			ScanCode.KeypadDivide => ImGuiKey.KeypadDivide,
			>= ScanCode.F1 and <= ScanCode.F12 => ImGuiKey.F1 + (scancode - ScanCode.F1),
			ScanCode.NumLockClear => ImGuiKey.NumLock,
			ScanCode.ScrollLock => ImGuiKey.ScrollLock,
			ScanCode.LeftShift => ImGuiKey.ModShift,
			ScanCode.LeftControl => ImGuiKey.ModCtrl,
			ScanCode.LeftAlt => ImGuiKey.ModAlt,
			ScanCode.Semicolon => ImGuiKey.Semicolon,
			ScanCode.Equals => ImGuiKey.Equal,
			ScanCode.Comma => ImGuiKey.Comma,
			ScanCode.Minus => ImGuiKey.Minus,
			ScanCode.Period => ImGuiKey.Period,
			ScanCode.Slash => ImGuiKey.Slash,
			ScanCode.Grave => ImGuiKey.GraveAccent,
			ScanCode.LeftBracket => ImGuiKey.LeftBracket,
			ScanCode.RightBracket => ImGuiKey.RightBracket,
			ScanCode.Backslash => ImGuiKey.Backslash,
			ScanCode.Apostrophe => ImGuiKey.Apostrophe,
			_ => ImGuiKey.None,
		};

		return imguiKey != ImGuiKey.None;
	}

	private unsafe void ProcessTextureUpdates(ImDrawDataPtr drawData)
	{
		if (drawData.Textures.Data == null) return;
		for (var i = 0; i < drawData.Textures.Size; i++)
			UpdateTexture(drawData.Textures.Data[i]);
	}

	private unsafe void RenderDrawData(GpuCommandBuffer gpuCommandBuffer, RenderPass renderPass, ImDrawData* drawData)
	{
		if (drawData->TotalVtxCount == 0) return;

		var io = ImGui.GetIO();
		drawData->ScaleClipRects(io.DisplayFramebufferScale);

		// Upload vertex/index data
		UpdateBuffers(drawData);

		// Set projection
		var projection = Matrix4x4.CreateOrthographicOffCenter(
			0f, io.DisplaySize.X,
			io.DisplaySize.Y, 0f,
			-1f, 1f
		);
		gpuCommandBuffer.PushVertexUniformData(new VertexUniforms { ProjectionMatrix = projection });

		renderPass.BindGraphicsPipeline(_pipeline);
		renderPass.BindVertexBuffers(new BufferBinding(_vertexBuffer, 0));
		renderPass.BindIndexBuffer(new BufferBinding(_indexBuffer, 0), IndexElementSize.Sixteen);
		renderPass.SetViewport(new Viewport { X = 0, Y = 0, W = (uint)io.DisplaySize.X, H = (uint)io.DisplaySize.Y, MinDepth = 0, MaxDepth = 1 });

		RenderCommandLists(gpuCommandBuffer, renderPass, drawData);
	}

	private unsafe void UpdateBuffers(ImDrawData* drawData)
	{
		// Ensure vertex GpuBuffer
		if (drawData->TotalVtxCount > _vertexBufferSize)
		{
			_vertexBuffer?.Dispose();
			_vertexBufferSize = (int)(drawData->TotalVtxCount * 1.5f);
			_vertexBuffer = GpuBuffer.Create<ImGuiVertex>(_device, BufferUsageFlags.Vertex, (uint)_vertexBufferSize);
		}

		// Ensure index GpuBuffer
		if (drawData->TotalIdxCount > _indexBufferSize)
		{
			_indexBuffer?.Dispose();
			_indexBufferSize = (int)(drawData->TotalIdxCount * 1.5f);
			_indexBuffer = GpuBuffer.Create<ushort>(_device, BufferUsageFlags.Index, (uint)_indexBufferSize);
		}

		// Upload via transfer GpuBuffer
		var vtxTransfer = TransferBuffer.Create<ImGuiVertex>(_device, TransferBufferUsage.Upload, (uint)drawData->TotalVtxCount);
		var idxTransfer = TransferBuffer.Create<ushort>(_device, TransferBufferUsage.Upload, (uint)drawData->TotalIdxCount);

		var vtxSpan = vtxTransfer.Map<byte>(false);
		var idxSpan = idxTransfer.Map<byte>(false);

		int vtxOffset = 0, idxOffset = 0;
		var vtxStride = sizeof(ImGuiVertex);
		for (var n = 0; n < drawData->CmdListsCount; n++)
		{
			ImDrawList* cmdList = drawData->CmdLists.Data[n];
			var vtxSize = cmdList->VtxBuffer.Size * vtxStride;
			var idxSize = cmdList->IdxBuffer.Size * sizeof(ushort);

			new Span<byte>(cmdList->VtxBuffer.Data, vtxSize)
				.CopyTo(vtxSpan.Slice(vtxOffset * vtxStride, vtxSize));
			new Span<byte>(cmdList->IdxBuffer.Data, idxSize)
				.CopyTo(idxSpan.Slice(idxOffset * sizeof(ushort), idxSize));

			vtxOffset += cmdList->VtxBuffer.Size;
			idxOffset += cmdList->IdxBuffer.Size;
		}

		vtxTransfer.Unmap();
		idxTransfer.Unmap();

		var cmd = _device.AcquireCommandBuffer();
		var copyPass = cmd.BeginCopyPass();
		copyPass.UploadToBuffer(
			new TransferBufferLocation(vtxTransfer, 0),
			new BufferRegion(_vertexBuffer, 0, (uint)(drawData->TotalVtxCount * vtxStride)),
			true
		);
		copyPass.UploadToBuffer(
			new TransferBufferLocation(idxTransfer, 0),
			new BufferRegion(_indexBuffer, 0, (uint)(drawData->TotalIdxCount * sizeof(ushort))),
			true
		);
		cmd.EndCopyPass(copyPass);
		_device.Submit(cmd);

		vtxTransfer.Dispose();
		idxTransfer.Dispose();
	}

	private unsafe void RenderCommandLists(GpuCommandBuffer gpuCommandBuffer, RenderPass renderPass, ImDrawData* drawData)
	{
		int vtxOffset = 0, idxOffset = 0;
		for (var n = 0; n < drawData->CmdListsCount; n++)
		{
			ImDrawList* cmdList = drawData->CmdLists.Data[n];
			for (var cmdi = 0; cmdi < cmdList->CmdBuffer.Size; cmdi++)
			{
				var drawCmd = &cmdList->CmdBuffer.Data[cmdi];
				if (drawCmd->ElemCount == 0) continue;

				var texId = drawCmd->TexRef.GetTexID();
				if (!_textures.TryGetValue(texId, out var textureInfo) || textureInfo.Texture == null)
					throw new InvalidOperationException($"Could not find a texture with id '{texId}'");

				renderPass.SetScissor(new Rect
				{
					X = (int)drawCmd->ClipRect.X,
					Y = (int)drawCmd->ClipRect.Y,
					W = (int)(drawCmd->ClipRect.Z - drawCmd->ClipRect.X),
					H = (int)(drawCmd->ClipRect.W - drawCmd->ClipRect.Y)
				});

				renderPass.BindFragmentSamplers(new TextureSamplerBinding(textureInfo.Texture, _sampler));

				renderPass.DrawIndexedPrimitives(
					drawCmd->ElemCount,
					1,
					(uint)drawCmd->IdxOffset + (uint)idxOffset,
					(int)drawCmd->VtxOffset + vtxOffset,
					0
				);
			}
			vtxOffset += cmdList->VtxBuffer.Size;
			idxOffset += cmdList->IdxBuffer.Size;
		}
	}

	public void Dispose()
	{
		_vertexShader.Dispose();
		_fragmentShader.Dispose();
		_pipeline.Dispose();
		_sampler.Dispose();
		_vertexBuffer?.Dispose();
		_indexBuffer?.Dispose();

		foreach (var t in _textures.Values)
			if (t.IsManaged) t.Texture?.Dispose();
		_textures.Clear();

		ImGui.DestroyContext();
	}
}
