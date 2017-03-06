using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using SharpDX.MediaFoundation;
using SharpDX.Windows;
using Color = SharpDX.Color;
using D3D11 = SharpDX.Direct3D11;

namespace TestDxConsole
{
    internal class Grabber : IDisposable
    {
        #region Fields

        private readonly D3D11.InputElement[] _inputElements =
        {
            new D3D11.InputElement( "POSITION", 0, Format.R32G32B32_Float, 0, 0, D3D11.InputClassification.PerVertexData, 0 ),
            new D3D11.InputElement( "TEXCOORD", 0, Format.R32G32_Float, 12, 0, D3D11.InputClassification.PerVertexData, 0 )
        };

        private readonly RenderForm _renderForm;

        private readonly Vertex[] _vertices =
        {
            new Vertex( new Vector3( -1, -1, 0 ), new Vector2( 0, 1 ) ),
            new Vertex( new Vector3( -1, 1, 0 ), new Vector2( 0, 0 ) ),
            new Vertex( new Vector3( 1, -1, 0 ), new Vector2( 1, 1 ) ),
            new Vertex( new Vector3( 1, -1, 0 ), new Vector2( 1, 1 ) ),
            new Vertex( new Vector3( -1, 1, 0 ), new Vector2( 0, 0 ) ),
            new Vertex( new Vector3( 1, 1, 0 ), new Vector2( 1, 0 ) )
        };

        private readonly int Height = 720;
        private readonly int Width = 1280;

        private D3D11.Device _d3DDevice;
        private D3D11.DeviceContext _d3DDeviceContext;

        private D3D11.InputElement[] _inputElements2 =
        {
            new D3D11.InputElement( "POSITION", 0, Format.R32G32B32_Float, 0, 0, D3D11.InputClassification.PerVertexData, 0 ),
            new D3D11.InputElement( "COLOR", 0, Format.R32G32B32A32_Float, 12, 0, D3D11.InputClassification.PerVertexData, 0 )
        };

        private ShaderSignature _inputSignature;
        private OutputDuplication _outputDuplication;
        private D3D11.PixelShader _pixelShader;
        private D3D11.RenderTargetView _renderTargetView;
        private D3D11.SamplerState _sampler;
        private SwapChain _swapChain;
        private D3D11.Buffer _vertexBuffer;
        private D3D11.VertexShader _vertexShader;

        #endregion


        #region Initialization

        /// <summary>Initializes a new instance of the <see cref="T:System.Object" /> class.</summary>
        public Grabber()
        {
            _renderForm = new RenderForm( "Grabber" ) { ClientSize = new Size( Width, Height ), AllowUserResizing = false };
            InitializeDeviceResources();
            InitializeMfTransform();
        }

        #endregion


        #region Public methods

        public void Run()
        {
            RenderLoop.Run( _renderForm, RenderCallback );
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            _renderForm?.Dispose();
            _d3DDevice?.Dispose();
            _d3DDeviceContext?.Dispose();
            _renderTargetView?.Dispose();
            _swapChain?.Dispose();
        }

        #endregion


        #region Non-public methods

        private void InitializeMfTransform()
        {
            var transforms = new List<Transform>();

//            MediaFactory.createActivate

            var mftActivates = MediaFactory.FindTransform( TransformCategoryGuids.VideoEncoder, TransformEnumFlag.Hardware, null, null );
            foreach ( var mftActivate in mftActivates )
            {
                try
                {
                    var transform = mftActivate.ActivateObject<Transform>();
                    if (IsAcceptable(transform))
                        transforms.Add( transform );
                    else
                        mftActivate.ShutdownObject();

//                    Console.WriteLine( DescribeTransform( transform ) );
                }
                catch ( Exception ex )
                {
                }
            }

            foreach ( var mftActivate in mftActivates )
                mftActivate.ShutdownObject();
        }

        private bool IsAcceptable( Transform transform )
        {
//            transform.Attributes.Get<>( MediaAttributeKey<> )

            transform.GetStreamCount( out var inputStreamsCount, out var outputStreamsCount );

            var inputStreamsIds = new int[inputStreamsCount];
            var outputStreamsIds = new int[outputStreamsCount];

            if ( !transform.TryGetStreamIDs( inputStreamsIds, outputStreamsIds ) )
            {
//                return $"Failed to get '{transform.Tag}' data";
            }

            var sb = new StringBuilder();

            var inputStreamsInfo = inputStreamsIds.Select( id =>
            {
                transform.GetInputStreamInfo( id, out var inputStreamInformation );
                transform.GetInputStreamAttributes( id, out var inputStreamAttributes );
//                for ( int i = 0; i < UPPER; i++ )
//                {
//                    transform.GetInputAvailableType(id, out var mediaType);
//                }
                transform.GetInputCurrentType( id, out var type );
                return new
                {
                    inputStreamInformation,
//                    inputStreamAttributes,
                    type
                };
            } ).ToList();

            return true;
        }

        /// <summary>
        ///     Initializes all the required D3D objects.
        /// </summary>
        private void InitializeDeviceResources()
        {
            //  Create device and a swap chain
            var swapChainDesc = new SwapChainDescription
            {
                ModeDescription = new ModeDescription( Width, Height, new Rational( 60, 1 ), Format.R8G8B8A8_UNorm ),
                SampleDescription = new SampleDescription( 1, 0 ),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = _renderForm.Handle,
                IsWindowed = true
            };

            D3D11.Device.CreateWithSwapChain( DriverType.Hardware, D3D11.DeviceCreationFlags.None, swapChainDesc, out _d3DDevice, out _swapChain );
            _d3DDeviceContext = _d3DDevice.ImmediateContext;

            _d3DDeviceContext.Rasterizer.SetViewport( new Viewport( 0, 0, Width, Height ) );

            using ( var backBuffer = _swapChain.GetBackBuffer<D3D11.Texture2D>( 0 ) )
                _renderTargetView = new D3D11.RenderTargetView( _d3DDevice, backBuffer );

            _sampler = new D3D11.SamplerState( _d3DDevice, new D3D11.SamplerStateDescription
            {
                Filter = D3D11.Filter.MinMagMipLinear,
                AddressU = D3D11.TextureAddressMode.Clamp,
                AddressV = D3D11.TextureAddressMode.Clamp,
                AddressW = D3D11.TextureAddressMode.Clamp,
                ComparisonFunction = D3D11.Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            } );

            //  Init geometry
            _vertexBuffer = D3D11.Buffer.Create( _d3DDevice, D3D11.BindFlags.VertexBuffer, _vertices );

            //  Init shaders
            using ( var vertexShaderByteCode = ShaderBytecode.CompileFromFile( "vertexshader.hlsl", "main", "vs_4_0", ShaderFlags.Debug ) )
            {
                _inputSignature = ShaderSignature.GetInputSignature( vertexShaderByteCode );
                _vertexShader = new D3D11.VertexShader( _d3DDevice, vertexShaderByteCode );
            }

            using ( var pixelShaderByteCode = ShaderBytecode.CompileFromFile( "pixelshader.hlsl", "main", "ps_4_0", ShaderFlags.Debug ) )
                _pixelShader = new D3D11.PixelShader( _d3DDevice, pixelShaderByteCode );

            //  Init device context
            _d3DDeviceContext.PixelShader.SetSampler( 0, _sampler );
            _d3DDeviceContext.PixelShader.Set( _pixelShader );
            _d3DDeviceContext.VertexShader.Set( _vertexShader );

            _d3DDeviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            var inputLayout = new D3D11.InputLayout( _d3DDevice, _inputSignature, _inputElements );
            _d3DDeviceContext.InputAssembler.InputLayout = inputLayout;

            //  Init desktop duplication
            var dxgiDevice = _d3DDevice.QueryInterface<Device>();
            var output = dxgiDevice.Adapter.Outputs[0];
            var outputDesc = output.Description;

            _outputDuplication = output.QueryInterface<Output1>().DuplicateOutput( _d3DDevice );
        }

        private void RenderCallback()
        {
            Draw();
        }

        /// <summary>
        ///     Performs drawing.
        /// </summary>
        private void Draw()
        {
            try
            {
                _outputDuplication.AcquireNextFrame( 500, out var frameInfoRef, out var desktopResource );
                using ( desktopResource )
                {
                    var texture = desktopResource.QueryInterface<D3D11.Texture2D>();
                    var shaderResourceView = new D3D11.ShaderResourceView( _d3DDevice, texture );

                    _d3DDeviceContext.OutputMerger.SetRenderTargets( _renderTargetView );
                    _d3DDeviceContext.ClearRenderTargetView( _renderTargetView, new Color( 32, 103, 178 ) );

                    //  Setup pixel shader
                    _d3DDeviceContext.PixelShader.SetShaderResource( 0, shaderResourceView );

                    //  Setup input assembler
                    _d3DDeviceContext.InputAssembler.SetVertexBuffers( 0, new D3D11.VertexBufferBinding( _vertexBuffer, Utilities.SizeOf<Vertex>(), 0 ) );

                    _d3DDeviceContext.OutputMerger.SetBlendState( null, new RawColor4( 0, 0, 0, 0 ), 0xFFFFFFFF );

                    _d3DDeviceContext.Draw( _vertices.Length, 0 );

                    _swapChain.Present( 1, PresentFlags.None );

                    _outputDuplication.ReleaseFrame();
                }
            }
            catch ( Exception ex )
            {
            }
        }

        #endregion


        #region Nested type: Vertex

        [StructLayout( LayoutKind.Sequential )]
        public struct Vertex
        {
            #region Fields

            public readonly Vector3 Position;
            public readonly Vector2 TexPos;

            #endregion


            #region Initialization

            public Vertex( Vector3 position, Vector2 texturePosition )
            {
                Position = position;
                TexPos = texturePosition;
            }

            #endregion
        }

        #endregion
    }
}
