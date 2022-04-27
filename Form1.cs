using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace OPENTK_2._0
{
    
    
    public partial class Form1 : Form
    {
        bool loaded = false;
        int count = 0;

        int currentLayer = 0;
        View view = new View();
        Bin bin = new Bin();
        bool needReload = false;


        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            label1.Text = "";
            Application.Idle += Application_Idle;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog()==DialogResult.OK) {
                
                string str = dialog.FileName;
                int size = bin.readBIN(str);
                label1.Text = size.ToString();
                view.setUpView(glControl1.Width,glControl1.Height);
                loaded = true;
                glControl1.Invalidate();
            }
        }
        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (loaded)
            {
                if (radioButton1.Checked)
                {
                    needReload = true;
                    if (needReload)
                    {
                        view.generateTextureImage(currentLayer);
                        view.Load2DTexture();
                        needReload = false;
                    }

                    view.DrawTexture();
                    glControl1.SwapBuffers();
                } else if (radioButton2.Checked) {
                    view.FreeShader();
                    view.drawQuadsStrip(currentLayer);
                    glControl1.SwapBuffers();
                } else { 
                    view.FreeShader();
                    view.drawQuads(currentLayer);
                    glControl1.SwapBuffers();
                }


                label1.Text = (currentLayer+1).ToString();
                
            }
        }

        void Application_Idle(object sender, EventArgs e) {
            while (glControl1.IsIdle) {
                glControl1.Invalidate();
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            currentLayer = trackBar1.Value;
            needReload = true;
        }

        private void glControl1_Load(object sender, EventArgs e)
        {

        }

        
    }
    class Bin
    {
        public static int X, Y, Z;
        public static short[] array;
        public Bin() { }
        public int readBIN(string path)
        {
            if (File.Exists(path)) {
                BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open));
                X = reader.ReadInt32();
                Y = reader.ReadInt32();
                Z = reader.ReadInt32(); 
                int arraySize = X*Y*Z;
                
                array = new short[arraySize];
                for (int i = 0; i < arraySize; ++i) {
                    array[i] = reader.ReadInt16();
                }
            }
            return array.Length;
        }
    }

        class View 
    {
        //public static int DrawQuads(currentLayer)
        int VBOtexture;
        Bitmap textureImage;
        int ShaderProgram;
        int vertex_shader;
        int fragment_shader;
        private int compileSource(string source, ShaderType type)
        { 
            int shader = GL.CreateShader(type); //! Передаем исходный код
            GL.ShaderSource(shader, source); //! Компилируем шейдер
            GL.CompileShader(shader); 
            return shader; 
        }
        private void linkProgram()
        {
            if (vertex_shader == 0 || fragment_shader == 0) { 
                ShaderProgram = 0; 
                return; 
            } 

            ShaderProgram = GL.CreateProgram(); 
            GL.AttachShader(ShaderProgram, vertex_shader); 
            GL.AttachShader(ShaderProgram, fragment_shader); 
            GL.LinkProgram(ShaderProgram);
            string log = GL.GetProgramInfoLog(ShaderProgram); 
            if (string.IsNullOrEmpty(log) == false)
                Console.WriteLine(log); 
        }
        public int load(string vertex_source, string fragment_source) {
            vertex_shader = compileSource(vertex_source, ShaderType.VertexShader); 
            fragment_shader = compileSource(fragment_source, ShaderType.FragmentShader); 
            linkProgram();
            return ShaderProgram;
        }
        
        public void initShaders() {
            const string vShader = @"
                varying vec2 pos;

                void main() {
                    gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
                    pos = gl_MultiTexCoord0.st;
                }
            ";
            const string fShader = @"
                varying vec2 pos;
                uniform sampler2D s_texture;

                void main() {
                    float tx = pos.x;
                    float ty = pos.y;
                    float r;
                    float g;
                    float b;
                    float i;
                    float y;

                    vec4 v1 = texture2D(s_texture, vec2(tx, ty)).rgba;

                                r = v1.r;
                                g = v1.g;
                                b = v1.b;

              

                    y = 0.299*r + 0.587*g + 0.114*b;
                    i = (y/255.0)*255.0;

                    if(i<=0.18){
                        r = .8;
                        g = 0.0;
                        b = 0.0;
                    }else if((i>0.18)&&(i<=0.33)){
                        r = .50;
                        g = .35;
                        b = .15;
                    }else if((i>0.33)&&(i<=0.46)){
                        r = .0;
                        g = .8;
                        b = .0;    
                    }else if((i>0.46)&&(i<=0.75)){
                        r = .15;
                        g = .50;
                        b = .35;    
                    }else{
                        r = .0;
                        g = .0;
                        b = .8;
                    }


                    gl_FragColor = vec4(r, g, b, 1);
                }
            ";
            if (load(vShader, fShader) == -1) {
                Console.WriteLine("error load shader"); 
                return;
            }

        }
        public void FreeShader()
        { 
            GL.UseProgram(0);
            GL.DeleteProgram( ShaderProgram); 
        }
            public void Load2DTexture() { //load on GPU
            GL.BindTexture(TextureTarget.Texture2D, VBOtexture);
            BitmapData data = textureImage.LockBits(
                new System.Drawing.Rectangle(0, 0, textureImage.Width, textureImage.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb
            );

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                data.Width,
                data.Height,
                0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                PixelType.UnsignedByte,
                data.Scan0
            );

            textureImage.UnlockBits(data);

            GL.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear
            );
            GL.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear
            );

            ErrorCode Er = GL.GetError();
            string str = Er.ToString();
        }

        public void generateTextureImage(int layerNumber) {
            textureImage = new Bitmap(Bin.X, Bin.Y);
            for (int i = 0; i < Bin.X; ++i)
                for (int j = 0; j < Bin.Y; ++j) {
                    int pixelNumber = i + j * Bin.X + layerNumber * Bin.X * Bin.Y;
                    textureImage.SetPixel(i, j, transferFunction(Bin.array[pixelNumber]));
                }
        }

        public void DrawTexture() {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            initShaders();
            GL.UseProgram(ShaderProgram);
            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, VBOtexture);

            GL.Begin(BeginMode.Quads);
            
            GL.Color3(Color.White);

            GL.TexCoord2(0f, 0f);
            GL.Vertex2(0, 0);

            GL.TexCoord2(0f, 1f);
            GL.Vertex2(0, Bin.Y);

            GL.TexCoord2(1f, 1f);
            GL.Vertex2(Bin.X, Bin.Y);

            GL.TexCoord2(1f, 0f);
            GL.Vertex2(Bin.X, 0);
            GL.End();

            GL.Uniform1(GL.GetUniformLocation(ShaderProgram, "s_texture"), 0);


            GL.Disable(EnableCap.Texture2D);
        }

        public void setUpView(int width, int height) {
            GL.ShadeModel(ShadingModel.Smooth);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, Bin.X, 0, Bin.Y, -1, 1);
            GL.Viewport(0, 0, width, height);

        }
        public static int Clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
        Color transferFunction(int value) 
        {
            int min = 0;
            int max = 255;
            int newVal = Clamp((value - min) * 255 / (max - min), 0, 255);
            
            return Color.FromArgb(255, newVal, newVal, newVal);
        }

        public int drawQuadsStrip(int layerNumber) {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Begin(BeginMode.QuadStrip);


            for (int x_coord = 0; x_coord < Bin.X - 1; x_coord++)
                for (int y_coord = 0; y_coord < Bin.Y - 1; y_coord++)
                {
                    int value;
                    value = Bin.array[x_coord + y_coord * Bin.X + layerNumber * Bin.X * Bin.Y];
                    GL.Color3(transferFunction(value));
                    GL.Vertex2(x_coord, y_coord);

                    value = Bin.array[(x_coord +1) + y_coord * Bin.X + layerNumber * Bin.X * Bin.Y];
                    GL.Color3(transferFunction(value));
                    GL.Vertex2(x_coord+1, y_coord);

                }
            GL.End();
            return layerNumber;
        }
        public int drawQuads(int layerNumber) {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Begin(BeginMode.QuadStrip);
           

            for (int x_coord=0; x_coord < Bin.X - 1; x_coord++)
                for (int y_coord = 0; y_coord < Bin.Y - 1; y_coord++)
                {
                    int value;

                    value = Bin.array[x_coord+ y_coord * Bin.X+layerNumber * Bin.X * Bin.Y];
                    GL.Color3(transferFunction(value));
                    GL.Vertex2(x_coord, y_coord);

                    value = Bin.array[x_coord + (y_coord+1) * Bin.X + layerNumber * Bin.X * Bin.Y];
                    GL.Color3(transferFunction(value));
                    GL.Vertex2(x_coord, y_coord+1);

                    value = Bin.array[x_coord+1 + (y_coord+1) * Bin.X + layerNumber * Bin.X * Bin.Y];
                    GL.Color3(transferFunction(value));
                    GL.Vertex2(x_coord+1, y_coord+1);

                    value = Bin.array[x_coord+1 + y_coord * Bin.X + layerNumber * Bin.X * Bin.Y];
                    GL.Color3(transferFunction(value));
                    GL.Vertex2(x_coord+1, y_coord);
                }
            GL.End();
            return layerNumber;
        }

    }

}
