﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;

using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using SharpDX.Direct3D;
using System.Windows.Forms;

namespace Lilium
{
	public partial class Game
	{
		public bool IsEditor { get; private set; }

		public SharpDX.DXGI.SwapChain SwapChain { get; private set; }
		public SharpDX.Direct3D11.Device Device { get; private set; }
		public SharpDX.Direct3D11.DeviceContext DeviceContext { get; private set; }

		public RenderTargetView DefaultRenderTargetView { get { return _backbufferView; } }
		public DepthStencilView DefaultDepthStencilView { get { return _zbufferView; } }

		public System.Drawing.Size RenderViewSize { get; private set; }
		public IntPtr ControlHandle { get; private set; }

		private bool needResize = false;

		private RenderTargetView _backbufferView;
		private DepthStencilView _zbufferView;

		private Timer renderTimer;

		public void BindWithWindow(SharpDX.Windows.RenderForm c)
		{
			this.Input = new Input();
			this.Input.Hook(c);

			// Register callback
			c.ClientSizeChanged += (s, e) =>
			{
				RenderViewSize = c.ClientSize;
				needResize = true;
			};
			c.HandleCreated += (s, se) =>
			{
				IsEditor = false;
				CreateDevice(c.ClientSize, c.Handle);
				Init();

				// Dispose
				c.HandleDestroyed += (_0, _1) =>
				{
					this.Dispose();
				};
			};
		}

		public void BindWithWindow(SharpDX.Windows.RenderControl c)
		{
			this.Input = new Input();
			this.Input.Hook(c);

			// Register callback
			c.ClientSizeChanged += (s, e) =>
			{
				RenderViewSize = c.ClientSize;
				needResize = true;
			};
			c.HandleCreated += (s, se) =>
			{
				IsEditor = true;
				CreateDevice(c.ClientSize, c.Handle);
				Init();

				// Dispose
				c.HandleDestroyed += (_0, _1) =>
				{
					this.Dispose();
				};
			};
		}

		void renderTimer_TickUpdate(object sender, EventArgs e)
		{
			if (needResize) ResizeBuffers();

			LoopUpdate();
			SwapChain.Present(0, PresentFlags.None);
		}

		void CreateDevice(System.Drawing.Size size, IntPtr hwnd)
		{
			RenderViewSize = size;
			ControlHandle = hwnd;

			//create device and swapchain
			DriverType driverType = DriverType.Hardware;
			DeviceCreationFlags flags = DeviceCreationFlags.None;
			if (Config.DebugMode) flags |= DeviceCreationFlags.Debug;

			FeatureLevel[] levels = new FeatureLevel[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 };

			SwapChainDescription desc = new SwapChainDescription();
			desc.BufferCount = 1;
			desc.Flags = SharpDX.DXGI.SwapChainFlags.None;
			desc.IsWindowed = true;
			desc.ModeDescription = new ModeDescription(RenderViewSize.Width, RenderViewSize.Height, new Rational(60, 1), Format.R8G8B8A8_UNorm);
			desc.OutputHandle = ControlHandle;
			desc.SampleDescription = new SampleDescription(Config.MSAASampleCount, Config.MSAAQuality);
			desc.SwapEffect = SwapEffect.Discard;
			desc.Usage = Usage.RenderTargetOutput;

			SharpDX.Direct3D11.Device createdDevice;
			SharpDX.DXGI.SwapChain createdSwapChain;
			SharpDX.Direct3D11.Device.CreateWithSwapChain(driverType, flags, levels, desc, out createdDevice, out createdSwapChain);

			// Store references
			this.SwapChain = createdSwapChain;
			this.Device = createdDevice;
			this.DeviceContext = createdDevice.ImmediateContext;

			this.SwapChain.DebugName = "Lilium";
			this.Device.DebugName = "Lilium";
			this.DeviceContext.DebugName = "Lilium";

			// Ignore all windows events
			var factory = SwapChain.GetParent<Factory>();
			factory.MakeWindowAssociation(ControlHandle, WindowAssociationFlags.IgnoreAll);

			ResizeBuffers();

			renderTimer = new Timer();
			renderTimer.Tick += renderTimer_TickUpdate;
			renderTimer.Interval = 1;
			renderTimer.Start();
		}

		void ResizeBuffers()
		{
			// Dispose all previous allocated resources
			Utilities.Dispose(ref _backbufferView);
			Utilities.Dispose(ref _zbufferView);

			if (RenderViewSize.Width == 0 || RenderViewSize.Height == 0)
				return;

			// Resize the backbuffer
			SwapChain.ResizeBuffers(1, RenderViewSize.Width, RenderViewSize.Height, Format.R8G8B8A8_UNorm, SwapChainFlags.AllowModeSwitch);

			// Get the backbuffer from the swapchain
			var _backBufferTexture = SwapChain.GetBackBuffer<Texture2D>(0);
			_backBufferTexture.DebugName = "Lilium BackBuffer";

			// Backbuffer
			_backbufferView = new RenderTargetView(Device, _backBufferTexture);
			_backbufferView.DebugName = "Lilium BackBuffer View";
			_backBufferTexture.Dispose();

			// Depth buffer

			var _zbufferTexture = new Texture2D(Device, new Texture2DDescription()
			{
				Format = Format.D24_UNorm_S8_UInt,
				ArraySize = 1,
				MipLevels = 1,
				Width = RenderViewSize.Width,
				Height = RenderViewSize.Height,
				SampleDescription = new SampleDescription(Config.MSAASampleCount, Config.MSAAQuality),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.DepthStencil,
				CpuAccessFlags = CpuAccessFlags.None,
				OptionFlags = ResourceOptionFlags.None
			});
			_zbufferTexture.DebugName = "Lilium DepthStencilBuffer";

			// Create the depth buffer view
			_zbufferView = new DepthStencilView(Device, _zbufferTexture);
			_zbufferView.DebugName = "Lilium DepthStencilBuffer View";
			_zbufferTexture.Dispose();

			DeviceContext.Rasterizer.SetViewport(0, 0, RenderViewSize.Width, RenderViewSize.Height);
			DeviceContext.OutputMerger.SetTargets(_zbufferView, _backbufferView);

			// Resize UI Surface
			if(mUISurface != null)
				mUISurface.SetDesignHeight(RenderViewSize.Height);

			needResize = false;
		}

		void Dispose_Device()
		{
			Utilities.Dispose(ref _backbufferView);
			Utilities.Dispose(ref _zbufferView);
			DeviceContext.Dispose();
			Device.Dispose();
			SwapChain.Dispose();
			renderTimer.Dispose();
		}
	}
}
