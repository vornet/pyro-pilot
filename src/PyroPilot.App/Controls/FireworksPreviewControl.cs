using System.Numerics;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using PyroPilot.Core.Model;
using PyroPilot.Core.Simulation;
using Silk.NET.OpenGL;

namespace PyroPilot.App.Controls;

/// <summary>A GPU particle viewport hosted in Avalonia's managed OpenGL surface.</summary>
public sealed unsafe class FireworksPreviewControl : OpenGlControlBase
{
    private const int FloatsPerVertex = 8; // position(3), color(4), point size(1)

    public static readonly StyledProperty<Show?> ShowProperty =
        AvaloniaProperty.Register<FireworksPreviewControl, Show?>(nameof(Show));

    public static readonly StyledProperty<int> CurrentTimeMsProperty =
        AvaloniaProperty.Register<FireworksPreviewControl, int>(nameof(CurrentTimeMs));

    private GL? _gl;
    private uint _program;
    private uint _vertexBuffer;
    private int _viewProjectionLocation;

    public Show? Show
    {
        get => GetValue(ShowProperty);
        set => SetValue(ShowProperty, value);
    }

    public int CurrentTimeMs
    {
        get => GetValue(CurrentTimeMsProperty);
        set => SetValue(CurrentTimeMsProperty, value);
    }

    static FireworksPreviewControl()
    {
        AffectsRender<FireworksPreviewControl>(ShowProperty, CurrentTimeMsProperty);
    }

    protected override void OnOpenGlInit(GlInterface glInterface)
    {
        _gl = GL.GetApi(glInterface.GetProcAddress);
        _program = CreateProgram(_gl, VertexShaderSource, FragmentShaderSource);
        _viewProjectionLocation = _gl.GetUniformLocation(_program, "uViewProjection");

        _vertexBuffer = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
    }

    protected override void OnOpenGlRender(GlInterface glInterface, int framebuffer)
    {
        if (_gl is null) return;

        uint width = (uint)Math.Max(1, Bounds.Width);
        uint height = (uint)Math.Max(1, Bounds.Height);
        _gl.Viewport(0, 0, width, height);
        _gl.ClearColor(0.005f, 0.01f, 0.03f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        float[] vertices = BuildVertices();
        if (vertices.Length == 0) return;

        Matrix4x4 view = Matrix4x4.CreateLookAt(new Vector3(0, 18, -55), new Vector3(0, 20, 20), Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, width / (float)height, 0.1f, 300f);
        // System.Numerics uses row-vector/row-major conventions while GLSL reads
        // uniform matrices as column-major. GLES requires transpose=false, so
        // transpose the combined matrix on the CPU before uploading it.
        Matrix4x4 viewProjection = Matrix4x4.Transpose(view * projection);

        _gl.UseProgram(_program);
        _gl.UniformMatrix4(_viewProjectionLocation, 1, false, (float*)&viewProjection);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StreamDraw);

        uint stride = FloatsPerVertex * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, null);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, (void*)(7 * sizeof(float)));
        _gl.DrawArrays(PrimitiveType.Points, 0, (uint)(vertices.Length / FloatsPerVertex));
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface glInterface)
    {
        if (_gl is null) return;
        if (_vertexBuffer != 0) _gl.DeleteBuffer(_vertexBuffer);
        if (_program != 0) _gl.DeleteProgram(_program);
        _gl.Dispose();
        _gl = null;
    }

    private float[] BuildVertices()
    {
        if (Show is null) return [];

        Dictionary<Guid, FireworkDefinition> definitions = Show.Library.ToDictionary(item => item.Id);
        var particles = new List<ParticleSnapshot>();
        foreach (Track track in Show.Tracks.Where(item => item.Kind == TrackKind.Fire && !item.Muted))
        {
            foreach (FireCue cue in track.Clips.OfType<FireCue>())
            {
                if (!definitions.TryGetValue(cue.FireworkDefinitionId, out FireworkDefinition? definition)) continue;
                float elapsedSeconds = (CurrentTimeMs - cue.StartMs) / 1000f;
                particles.AddRange(FireworkSimulator.Sample(definition.Effect, cue, elapsedSeconds));
            }
        }

        var data = new float[particles.Count * FloatsPerVertex];
        for (int index = 0; index < particles.Count; index++)
        {
            ParticleSnapshot particle = particles[index];
            Vector4 color = ParseColor(particle.ColorHex, particle.Brightness);
            int offset = index * FloatsPerVertex;
            data[offset] = particle.Position.X;
            data[offset + 1] = particle.Position.Y;
            data[offset + 2] = particle.Position.Z;
            data[offset + 3] = color.X;
            data[offset + 4] = color.Y;
            data[offset + 5] = color.Z;
            data[offset + 6] = color.W;
            data[offset + 7] = Math.Clamp(particle.Size * 70f, 2.5f, 18f);
        }

        return data;
    }

    private static Vector4 ParseColor(string value, float brightness)
    {
        if (value.Length == 7 && value[0] == '#' && uint.TryParse(value[1..], System.Globalization.NumberStyles.HexNumber, null, out uint rgb))
            return new Vector4(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, brightness);
        return new Vector4(1, 1, 1, brightness);
    }

    private static uint CreateProgram(GL gl, string vertexSource, string fragmentSource)
    {
        uint vertex = CompileShader(gl, ShaderType.VertexShader, vertexSource);
        uint fragment = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);
        uint program = gl.CreateProgram();
        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        // GLSL ES 1.00 has no layout(location=...) syntax, so keep the buffer
        // contract explicit here.
        gl.BindAttribLocation(program, 0, "aPosition");
        gl.BindAttribLocation(program, 1, "aColor");
        gl.BindAttribLocation(program, 2, "aPointSize");
        gl.LinkProgram(program);
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
        string log = gl.GetProgramInfoLog(program);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        if (linked == 0)
        {
            gl.DeleteProgram(program);
            throw new InvalidOperationException($"OpenGL program link failed: {log}");
        }

        return program;
    }

    private static uint CompileShader(GL gl, ShaderType type, string source)
    {
        uint shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        string log = gl.GetShaderInfoLog(shader);
        if (compiled == 0)
        {
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"OpenGL shader compilation failed: {log}");
        }

        return shader;
    }

    private const string VertexShaderSource = """
        #version 100
        attribute vec3 aPosition;
        attribute vec4 aColor;
        attribute float aPointSize;
        uniform mat4 uViewProjection;
        varying vec4 vColor;
        void main()
        {
            gl_Position = uViewProjection * vec4(aPosition, 1.0);
            gl_PointSize = aPointSize;
            vColor = aColor;
        }
        """;

    private const string FragmentShaderSource = """
        #version 100
        precision mediump float;
        varying vec4 vColor;
        void main()
        {
            vec2 p = gl_PointCoord * 2.0 - 1.0;
            float radius = dot(p, p);
            if (radius > 1.0) discard;
            float glow = exp(-3.5 * radius);
            gl_FragColor = vec4(vColor.rgb * (0.6 + glow), vColor.a * glow);
        }
        """;
}
