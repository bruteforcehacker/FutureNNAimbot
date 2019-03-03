﻿using Alturos.Yolo;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace FutureNNAimbot
{
    [DataContract]
    public class Settings
    {
        [DataMember]
        public int SizeX { get; set; }
        [DataMember]
        public int SizeY { get; set; }
        [DataMember]
        public string Game { get; set; }
        [DataMember]
        public bool SimpleRCS { get; set; }
        [DataMember]
        public Keys ShootKey { get; set; }
        [DataMember]
        public Keys TrainModeKey { get; set; }
        [DataMember]
        public Keys ScreenshotKey { get; set; }
        [DataMember]
        public Keys ScreenshotModeKey { get; set; }
        [DataMember]
        public float SmoothAim { get; set; }
        [DataMember]
        public bool Information { get; set; }
        [DataMember]
        public bool Head { get; set; }

        public Settings(int SizeX, int SizeY, string Game, bool SimpleRCS, Keys ShootKey, Keys TrainModeKey, Keys ScreenshotKey, Keys ScreenshotModeKey, float SmoothAim, bool Information, bool Head)
        {
            this.SizeX = SizeX;
            this.SizeY = SizeY;
            this.Game = Game;
            this.SimpleRCS = SimpleRCS;
            this.ShootKey = ShootKey;
            this.TrainModeKey = TrainModeKey;
            this.ScreenshotKey = ScreenshotKey;
            this.ScreenshotModeKey = ScreenshotModeKey;
            this.SmoothAim = SmoothAim;
            this.Information = Information;
            this.Head = Head;
        }
    }

    class Program
    {
        public static int screen_width;
        public static int screen_height;
        public static bool screenshotMode;
        public static System.Drawing.Point coordinates;
        public static Bitmap bitmap;

        private class GDI32
        {

            public const int SRCCOPY = 0x00CC0020; // BitBlt dwRop parameter
            [DllImport("gdi32.dll")]
            public static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest,
                int nWidth, int nHeight, IntPtr hObjectSource,
                int nXSrc, int nYSrc, int dwRop);
            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth,
                int nHeight);
            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteDC(IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
            [DllImport("gdi32.dll")]
            public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        }

        class User32
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }
            [DllImport("user32.dll")]
            public static extern IntPtr GetDesktopWindow();
            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowDC(IntPtr hWnd);
            [DllImport("user32.dll")]
            public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);
            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);
            [DllImport("user32.dll")]
            public static extern short GetKeyState(int vKey);
            [DllImport("User32.dll")]
            public static extern short GetAsyncKeyState(System.Windows.Forms.Keys vKey);
            [DllImport("User32.Dll")]
            public static extern long SetCursorPos(int x, int y);
            [DllImport("user32.dll")]
            public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        }

        [DllImport("kernel32.dll")]
        public static extern void ExitProcess([In] uint uExitCode);

        static System.Drawing.Point size = new System.Drawing.Point(320, 320);

        static void Main(string[] args)
        {
            // Read settings
            DataContractJsonSerializer Settings = new DataContractJsonSerializer(typeof(Settings[]));
            Settings[] settings = null;
            Settings auto_config = new Settings(320, 320, "game", true, Keys.MButton, Keys.Insert, Keys.Home, Keys.NumPad9, 0.1f, true, false);
            using (FileStream fs = new FileStream("config.json", FileMode.OpenOrCreate))
            {
                if (fs.Length == 0)
                {
                    Settings.WriteObject(fs, new Settings[1] { auto_config });
                    MessageBox.Show($"Created auto-config, change whatever settings you want and restart.");
                    return;
                }
                else settings = (Settings[])Settings.ReadObject(fs);
            }
           
            //Vars
            size.X = settings[0].SizeX;
            size.Y = settings[0].SizeY;
            string game = settings[0].Game;
            bool SimpleRCS = settings[0].SimpleRCS;
            Keys ShootKey = settings[0].ShootKey;
            Keys TrainModeKey = settings[0].TrainModeKey;
            Keys ScreenshotKey = settings[0].ScreenshotKey;
            Keys ScreenshotModeKey = settings[0].ScreenshotModeKey;
            float SmoothAim = settings[0].SmoothAim;
            bool Information = settings[0].Information;
            bool Head = settings[0].Head;

            int i = 0;
            int selectedObject = 0;
            int shooting = 0;
            string[] objects = null;
            OverlayWindow _window;
            GameOverlay.Drawing.Graphics _graphics;
            bool trainingMode = false;
            screenshotMode = false;

            YoloWrapper yoloWrapper = null;
            //Check compatibility
            if (Process.GetProcessesByName(game).Count() == 0)
            {
                MessageBox.Show($"You have not launched {game}...");
                Process.GetCurrentProcess().Kill();
            }

            if (File.Exists($"trainfiles/{game}.cfg") && File.Exists($"trainfiles/{game}.weights") && File.Exists($"trainfiles/{game}.names"))
            {
                yoloWrapper = new YoloWrapper($"trainfiles/{game}.cfg", $"trainfiles/{game}.weights", $"trainfiles/{game}.names");
                Console.Clear();
                if (yoloWrapper.EnvironmentReport.CudaExists == false)
                {
                    Console.WriteLine("Install CUDA 10");
                    Process.GetCurrentProcess().Kill();
                }
                if (yoloWrapper.EnvironmentReport.CudnnExists == false)
                {
                    Console.WriteLine("Cudnn doesn't exist");
                    Process.GetCurrentProcess().Kill();
                }
                if (yoloWrapper.EnvironmentReport.MicrosoftVisualCPlusPlus2017RedistributableExists == false)
                {
                    Console.WriteLine("Install Microsoft Visual C++ 2017 Redistributable");
                    Process.GetCurrentProcess().Kill();
                }
                if (yoloWrapper.DetectionSystem.ToString() != "GPU")
                {
                    MessageBox.Show("No GPU card detected. Exiting...");
                    Process.GetCurrentProcess().Kill();
                }
                objects = File.ReadAllLines($"trainfiles/{game}.names");
            }
            else
            {
                trainingMode = true;
                MessageBox.Show($"Looks like you have not configured settings for {game}... Let's train the Neural Network! :)\n Preparing files for training....");

                Console.Write("How many objects will the NN be analyzing and training on? Write each object's name via the separator ',' without spaces (EX: 1,2): ");
                objects = Console.ReadLine().Split(',');
            }


            PrepareFiles(game);
            Random random = new Random();
          
            //Make transparent window for drawing
            _window = new OverlayWindow(0, 0, size.X, size.Y)
            {
                IsTopmost = true,
                IsVisible = true
            };

            _graphics = new GameOverlay.Drawing.Graphics()
            {
                MeasureFPS = true,
                Height = _window.Height,
                PerPrimitiveAntiAliasing = true,
                TextAntiAliasing = true,
                UseMultiThreadedFactories = false,
                VSync = true,
                Width = _window.Width,
                WindowHandle = IntPtr.Zero
            };

            _window.CreateWindow();

            _graphics.WindowHandle = _window.Handle;
            _graphics.Setup();


            GameOverlay.Drawing.Graphics gfx = _graphics;

            System.Drawing.Rectangle trainBox = new System.Drawing.Rectangle(0, 0, size.X / 2, size.Y / 2);

            while (true)
            {
                coordinates = Cursor.Position;

                if (screenshotMode)
                {
                    bitmap = new Bitmap(CaptureWindow(game, true), size.X, size.Y);
                }
                else bitmap = new Bitmap(CaptureWindow(game, false), size.X, size.Y);

                trainBox.X = size.X / 2 - trainBox.Width / 2;
                trainBox.Y = size.Y / 2 - trainBox.Height / 2;

                if (User32.GetAsyncKeyState(TrainModeKey) == -32767)
                {
                    if (yoloWrapper != null)
                    {

                        objects = File.ReadAllLines($"trainfiles/{game}.names");
                        trainingMode = trainingMode == true ? false : true;
                    }
                }

                if (User32.GetAsyncKeyState(ScreenshotModeKey) == -32767)
                {
                    if (yoloWrapper != null)
                    {
                        objects = File.ReadAllLines($"trainfiles/{game}.names");
                        screenshotMode = screenshotMode == true ? false : true;
                    }
                }

                _window.X = coordinates.X - size.X / 2;
                _window.Y = coordinates.Y - size.Y / 2;
                gfx.BeginScene();
                gfx.ClearScene();

                gfx.DrawRectangle(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Green), 0, 0, size.X, size.Y, 2);

                if (trainingMode)
                {
                    int rand = random.Next(5000, 999999);
                    File.WriteAllText($"trainfiles/{game}.cfg", File.ReadAllText($"trainfiles/{game}.cfg").Replace("batch=1", "batch=64").Replace("subdivisions=1", "subdivisions=8"));
                    
                    if (User32.GetAsyncKeyState(Keys.Left) != 0)
                    {
                        if (trainBox.Width <= 0)
                        {
                            continue;
                        }
                        else trainBox.Width -= 10;
                    }

                    if (User32.GetAsyncKeyState(Keys.Down) == -32767)
                    {
                        if (trainBox.Height >= size.Y)
                        {
                            continue;
                        }
                        else trainBox.Height += 10;
                    }

                    if (User32.GetAsyncKeyState(Keys.Right) == -32767)
                    {
                        if (trainBox.Width >= size.X)
                        {
                            continue;
                        }
                        else trainBox.Width += 10;
                    }

                    if (User32.GetAsyncKeyState(Keys.Up) == -32767)
                    {
                        if (trainBox.Height <= 0)
                        {
                            continue;
                        }
                        else trainBox.Height -= 10;
                    }

                    float relative_center_x = (float)(trainBox.X + trainBox.Width / 2) / size.X;
                    float relative_center_y = (float)(trainBox.Y + trainBox.Height / 2) / size.Y;
                    float relative_width = (float)trainBox.Width / size.X;
                    float relative_height = (float)trainBox.Height / size.Y;
                    gfx.DrawTextWithBackground(_graphics.CreateFont("Arial", 14), _graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Red), _graphics.CreateSolidBrush(0, 0, 0), new GameOverlay.Drawing.Point(0, 0), "Training mode. Object: " + objects[selectedObject] + Environment.NewLine + "ScreenshotMode: " + (screenshotMode == true ? "following" : "centered"));
                    gfx.DrawRectangle(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Blue), GameOverlay.Drawing.Rectangle.Create(trainBox.X, trainBox.Y, trainBox.Width, trainBox.Height), 1);
                    gfx.DrawRectangle(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Red), GameOverlay.Drawing.Rectangle.Create(trainBox.X + Convert.ToInt32(trainBox.Width / 2.9), trainBox.Y, Convert.ToInt32(trainBox.Width / 3), trainBox.Height / 7), 2);

                    if (User32.GetAsyncKeyState(Keys.PageUp) == -32767)
                    {
                        selectedObject = selectedObject + 1 == objects.Count() ? 0 : selectedObject + 1;
                    }

                    if (User32.GetAsyncKeyState(Keys.PageDown) == -32767)
                    {
                        selectedObject = selectedObject == 0 ? objects.Count() - 1 : selectedObject - 1;
                    }

                    if (User32.GetAsyncKeyState(ScreenshotKey) == -32767)
                    {
                        
                        bitmap.Save($"darknet/data/img/{game}{i.ToString()}{rand}.png", System.Drawing.Imaging.ImageFormat.Png);
                        File.WriteAllText($"darknet/data/img/{game}{i.ToString()}{rand}.txt", string.Format("{0} {1} {2} {3} {4}", selectedObject, relative_center_x, relative_center_y, relative_width, relative_height).Replace(",", "."));
                       // File.WriteAllText($"darknet/data/{game}.txt", File.ReadAllText($"darknet/data/{game}.txt") + $"data/img/{game}{i.ToString()}.png\r\n");
                        i++;
                        Console.Beep();
                    }

                    if (User32.GetAsyncKeyState(Keys.Back) == -32767)
                    {
                        bitmap.Save($"darknet/data/img/{game}{i.ToString()}{rand}.png", System.Drawing.Imaging.ImageFormat.Png);
                        File.WriteAllText($"darknet/data/img/{game}{i.ToString()}{rand}.txt", "");
                       // File.WriteAllText($"darknet/data/{game}.txt", File.ReadAllText($"darknet/data/{game}.txt") + $"data/img/{game}{i.ToString()}.png\r\n");
                        i++;
                        Console.Beep();
                    }

                    if (User32.GetAsyncKeyState(Keys.End) == -32767)
                    {
                        Console.WriteLine("Okay, we have the pictures for training. Let's train the Neural Network....");
                        File.WriteAllText($"darknet/{game}.cfg", File.ReadAllText($"darknet/{game}.cfg").Replace("NUMBER", objects.Count().ToString()).Replace("FILTERNUM", ((objects.Count() + 5) * 3).ToString()));
                        File.WriteAllText($"darknet/data/{game}.data", File.ReadAllText($"darknet/data/{game}.data").Replace("NUMBER", objects.Count().ToString()).Replace("GAME", game));
                        File.WriteAllText($"darknet/{game}.cmd", File.ReadAllText($"darknet/{game}.cmd").Replace("GAME", game));
                        File.WriteAllText($"darknet/{game}_trainmore.cmd", File.ReadAllText($"darknet/{game}_trainmore.cmd").Replace("GAME", game));
                        File.WriteAllText($"darknet/data/{game}.names", string.Join("\n", objects));
                       // DirectoryInfo d = ;//Assuming Test is your Folder
                        FileInfo[] Files = new DirectoryInfo(Application.StartupPath + @"\darknet\data\img").GetFiles($"{game}*.png"); //Getting Text files
                        string PathOfImg = "";
                        foreach (FileInfo file in Files)
                        {
                            PathOfImg += $"data/img/{file.Name}\r\n";
                        }

                        File.WriteAllText($"darknet/data/{game}.txt", PathOfImg);

                        Process.GetProcessesByName(game)[0].Kill();
                        if (File.Exists($"trainfiles/{game}.weights"))
                        {
                            File.Copy($"trainfiles/{game}.weights", $"darknet/{game}.weights", true);
                            Process.Start("cmd", @"/C cd " + Application.StartupPath + $"/darknet/ & {game}_trainmore.cmd");
                        }
                        else Process.Start("cmd", @"/C cd " + Application.StartupPath + $"/darknet/ & {game}.cmd");

                        Console.WriteLine("When you finished training, write \"done\" in this console.");

                        while (true)
                        {
                            if (Console.ReadLine() == "done")
                            {

                                File.Copy($"darknet/data/backup/{game}_last.weights", $"trainfiles/{game}.weights", true);
                                File.Copy($"darknet/data/{game}.names", $"trainfiles/{game}.names", true);
                                File.Copy($"darknet/{game}.cfg", $"trainfiles/{game}.cfg", true);
                                File.WriteAllText($"trainfiles/{game}.cfg", File.ReadAllText($"trainfiles/{game}.cfg").Replace("batch=64", "batch=1").Replace("subdivisions=8", "subdivisions=1"));
                                yoloWrapper = new YoloWrapper($"trainfiles/{game}.cfg", $"trainfiles/{game}.weights", $"trainfiles/{game}.names");
                                trainingMode = false;
                                break;

                            }
                            else Console.WriteLine("When you finished training, write \"done\" in this console.");
                        }
                        Console.WriteLine("Okay! Training has finished. Let's check detection in the game!");
                    }
                }
                else
                {
                    File.WriteAllText($"trainfiles/{game}.cfg", File.ReadAllText($"trainfiles/{game}.cfg").Replace("batch=64", "batch=1").Replace("subdivisions=8", "subdivisions=1"));
                    if (User32.GetAsyncKeyState(Keys.PageUp) == -32767)
                    {
                        selectedObject = selectedObject + 1 == objects.Count() ? 0 : selectedObject + 1;
                    }

                    if (User32.GetAsyncKeyState(Keys.Up) == -32767)
                    {
                        SmoothAim = SmoothAim >= 1 ? SmoothAim : SmoothAim + 0.05f;
                    }

                    if (User32.GetAsyncKeyState(Keys.Down) == -32767)
                    {
                        SmoothAim = SmoothAim <= 0 ? SmoothAim : SmoothAim - 0.05f;
                    }

                    if (User32.GetAsyncKeyState(Keys.Delete) == -32767)
                    {
                        Head = Head == true ? false : true;
                    }

                    if (User32.GetAsyncKeyState(Keys.Home) == -32767)
                    {
                        shooting = 0;
                        SimpleRCS = SimpleRCS == true ? false : true;
                    }

                    if (User32.GetAsyncKeyState(Keys.PageDown) == -32767)
                    {
                        selectedObject = selectedObject == 0 ? objects.Count() - 1 : selectedObject - 1;
                    }
                    gfx.DrawText(_graphics.CreateFont("Arial", 10), _graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Red), new GameOverlay.Drawing.Point(0, 0), $"Object {objects[selectedObject]}; SmoothAim {Math.Round(SmoothAim, 2)}; Head {Head}; SimpleRCS {SimpleRCS}");

                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        IEnumerable<Alturos.Yolo.Model.YoloItem> items = yoloWrapper.Detect(ms.ToArray());
                        if (SimpleRCS)
                            if (User32.GetAsyncKeyState(ShootKey) == 0) shooting = 0;

                        if (items.Count() > 0)
                        {

                            foreach (var item in items)
                            {
                                if (item.Confidence > (double)0.4)
                                {
                                    GameOverlay.Drawing.Rectangle head = GameOverlay.Drawing.Rectangle.Create(item.X + Convert.ToInt32(item.Width / 2.9), item.Y, Convert.ToInt32(item.Width / 3), item.Height / 7);
                                    GameOverlay.Drawing.Rectangle body = GameOverlay.Drawing.Rectangle.Create(item.X + Convert.ToInt32(item.Width / 6), item.Y + item.Height / 6, Convert.ToInt32(item.Width / 1.5f), item.Height / 3);

                                    if (Information)
                                    {
                                        if (Head) gfx.DrawTextWithBackground(_graphics.CreateFont("Arial", 12), _graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Red), _graphics.CreateSolidBrush(0, 0, 0), new GameOverlay.Drawing.Point(item.X + item.Width, item.Y + item.Width), $"{item.Type} {DistanceBetweenCross(head.Left + head.Width / 2, head.Top + head.Height / 2)}");
                                        else gfx.DrawTextWithBackground(_graphics.CreateFont("Arial", 12), _graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Red), _graphics.CreateSolidBrush(0, 0, 0), new GameOverlay.Drawing.Point(item.X + item.Width, item.Y + item.Width), $"{item.Type} {DistanceBetweenCross(body.Left + body.Width / 2, body.Top + body.Height / 2)}");

                                    }

                                    if (item.Type == objects[selectedObject])
                                    {
                                        gfx.DrawRectangle(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Red), GameOverlay.Drawing.Rectangle.Create(item.X, item.Y, item.Width, item.Height), 2);

                                        if (Head)
                                        {

                                            gfx.DrawRectangle(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Blue), head, 2);
                                            gfx.DrawCrosshair(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Blue), head.Left + head.Width / 2, head.Top + head.Height / 2 + Convert.ToInt32(1 * shooting), 2, 2, CrosshairStyle.Cross);
                                            gfx.DrawLine(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Blue), size.X / 2, size.Y / 2, head.Left + head.Width / 2, head.Top + head.Height / 2 + Convert.ToInt32(1 * shooting), 2);

                                        }
                                        else
                                        {
                                            gfx.DrawRectangle(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Blue), body, 2);
                                            gfx.DrawCrosshair(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Blue), body.Left + body.Width / 2, body.Top + body.Height / 2 + Convert.ToInt32(1 * shooting), 2, 2, CrosshairStyle.Cross);
                                            gfx.DrawLine(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Blue), size.X / 2, size.Y / 2, body.Left + body.Width / 2, body.Top + body.Height / 2 + Convert.ToInt32(1 * shooting), 2);

                                        }

                                        if (User32.GetAsyncKeyState(ShootKey) != 0)
                                        {
                                            if (Head)
                                            {
                                                Alturos.Yolo.Model.YoloItem nearestEnemy = items.Where(x => x.Type == objects[selectedObject]).OrderByDescending(x => DistanceBetweenCross(x.X + Convert.ToInt32(x.Width / 2.9) + (x.Width / 3) / 2, x.Y + (x.Height / 7) / 2)).Last();

                                                GameOverlay.Drawing.Rectangle nearestEnemyHead = GameOverlay.Drawing.Rectangle.Create(nearestEnemy.X + Convert.ToInt32(nearestEnemy.Width / 2.9), nearestEnemy.Y, Convert.ToInt32(nearestEnemy.Width / 3), nearestEnemy.Height / 7 + (float)2 * shooting);

                                                if (SmoothAim <= 0)
                                                {
                                                    User32.mouse_event(0x01, Convert.ToInt32(((nearestEnemyHead.Left - size.X / 2) + (nearestEnemyHead.Width / 2))), Convert.ToInt32((nearestEnemyHead.Top - size.Y / 2 + nearestEnemyHead.Height / 7 + 1 * shooting)), 0, (UIntPtr)0);
                                                    User32.mouse_event(0x02, 0, 0, 0, (UIntPtr)0);
                                                    User32.mouse_event(0x04, 0, 0, 0, (UIntPtr)0);
                                                }
                                                else
                                                {

                                                    if (size.X / 2 < nearestEnemyHead.Left | size.X / 2 > nearestEnemyHead.Right
                                                        | size.Y / 2 < nearestEnemyHead.Top | size.Y / 2 > nearestEnemyHead.Bottom)
                                                    {
                                                        User32.mouse_event(0x01, Convert.ToInt32(((nearestEnemyHead.Left - size.X / 2) + (nearestEnemyHead.Width / 2)) * SmoothAim), Convert.ToInt32((nearestEnemyHead.Top - size.Y / 2 + nearestEnemyHead.Height / 7 + 1 * shooting) * SmoothAim), 0, (UIntPtr)0);
                                                    }
                                                    else
                                                    {
                                                        User32.mouse_event(0x02, 0, 0, 0, (UIntPtr)0);
                                                        User32.mouse_event(0x04, 0, 0, 0, (UIntPtr)0);
                                                        if (SimpleRCS) shooting += 2;
                                                    }
                                                }
                                            }
                                            else
                                            {

                                                Alturos.Yolo.Model.YoloItem nearestEnemy = items.Where(x => x.Type == objects[selectedObject]).OrderByDescending(x => DistanceBetweenCross(x.X + Convert.ToInt32(x.Width / 6) + (x.Width / 1.5f) / 2, x.Y + x.Height / 6 + (x.Height / 3) / 2)).Last();

                                                GameOverlay.Drawing.Rectangle nearestEnemyBody = GameOverlay.Drawing.Rectangle.Create(nearestEnemy.X + Convert.ToInt32(nearestEnemy.Width / 6), nearestEnemy.Y + nearestEnemy.Height / 6 + (float)2 * shooting, Convert.ToInt32(nearestEnemy.Width / 1.5f), nearestEnemy.Height / 3 + (float)2 * shooting);
                                                if (SmoothAim <= 0)
                                                {
                                                    User32.mouse_event(0x01, Convert.ToInt32(((nearestEnemyBody.Left - size.X / 2) + (nearestEnemyBody.Width / 2))), Convert.ToInt32((nearestEnemyBody.Top - size.Y / 2 + nearestEnemyBody.Height / 7 + 1 * shooting)), 0, (UIntPtr)0);
                                                    User32.mouse_event(0x02, 0, 0, 0, (UIntPtr)0);
                                                    User32.mouse_event(0x04, 0, 0, 0, (UIntPtr)0);
                                                }
                                                else
                                                {

                                                    if (size.X / 2 < nearestEnemyBody.Left | size.X / 2 > nearestEnemyBody.Right
                                                        | size.Y / 2 < nearestEnemyBody.Top | size.Y / 2 > nearestEnemyBody.Bottom)
                                                    {
                                                        User32.mouse_event(0x01, Convert.ToInt32(((nearestEnemyBody.Left - size.X / 2) + (nearestEnemyBody.Width / 2)) * SmoothAim), Convert.ToInt32((nearestEnemyBody.Top - size.Y / 2 + nearestEnemyBody.Height / 7 + 1 * shooting) * SmoothAim), 0, (UIntPtr)0);
                                                    }
                                                    else
                                                    {
                                                        User32.mouse_event(0x02, 0, 0, 0, (UIntPtr)0);
                                                        User32.mouse_event(0x04, 0, 0, 0, (UIntPtr)0);
                                                        if (SimpleRCS) shooting += 2;
                                                    }
                                                }
                                            }
                                            //System.Threading.Thread.Sleep(120);
                                        }
                                    }
                                    else
                                    {
                                        gfx.DrawRectangle(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Green), GameOverlay.Drawing.Rectangle.Create(item.X, item.Y, item.Width, item.Height), 2);
                                    }
                                }

                            }
                        }
                        else { if (SimpleRCS) shooting = 0; }
                    }

                }
                gfx.FillRectangle(_graphics.CreateSolidBrush(GameOverlay.Drawing.Color.Blue), GameOverlay.Drawing.Rectangle.Create(size.X / 2, size.Y / 2, 4, 4));

                gfx.EndScene();
            }
        }

        //Get screenshot on the center of game
        public static System.Drawing.Image CaptureWindow(string name, bool followMouse)
        {
            if (Process.GetProcessesByName(name).Count() == 0)
            {
                MessageBox.Show($"Looks like you closed {name}...");
                Process.GetCurrentProcess().Kill();
            }
            IntPtr handle = Process.GetProcessesByName(name)[0].MainWindowHandle;
            IntPtr hdcSrc = User32.GetWindowDC(handle);
            User32.RECT windowRect = new User32.RECT();
            User32.GetWindowRect(handle, ref windowRect);
            screen_width = windowRect.right - windowRect.left;
            screen_height = windowRect.bottom - windowRect.top;
            IntPtr hdcDest = GDI32.CreateCompatibleDC(hdcSrc);
            IntPtr hBitmap = GDI32.CreateCompatibleBitmap(hdcSrc, size.X, size.Y);
            IntPtr hOld = GDI32.SelectObject(hdcDest, hBitmap);
            if (followMouse)
            {
                GDI32.BitBlt(hdcDest, 0, 0, size.X, size.Y, hdcSrc, coordinates.X - size.X / 2, coordinates.Y - size.Y / 2, GDI32.SRCCOPY);
            }
            else GDI32.BitBlt(hdcDest, 0, 0, size.X, size.Y, hdcSrc, screen_width / 2 - size.X / 2, screen_height / 2 - size.Y / 2, GDI32.SRCCOPY);
            GDI32.SelectObject(hdcDest, hOld);
            GDI32.DeleteDC(hdcDest);
            User32.ReleaseDC(handle, hdcSrc);
            System.Drawing.Image img = System.Drawing.Image.FromHbitmap(hBitmap);
            GDI32.DeleteObject(hBitmap);
            return img;
        }

        static float DistanceBetweenCross(float X, float Y)
        {
            float ydist = (Y - size.Y / 2);
            float xdist = (X - size.X / 2);
            float Hypotenuse = (float)Math.Sqrt(Math.Pow(ydist, 2) + Math.Pow(xdist, 2));
            return Hypotenuse;
        }

        public static void PrepareFiles(string game)
        {

            File.Copy("defaultfiles/default_trainmore.cmd", $"darknet/{game}_trainmore.cmd", true);
            if (File.Exists($"trainfiles/{game}.cfg")) File.Copy($"trainfiles/{game}.cfg", $"darknet/{game}.cfg", true);
            else File.Copy("defaultfiles/default.cfg", $"darknet/{game}.cfg", true);

            File.Copy("defaultfiles/default.conv.15", $"darknet/{game}.conv.15", true);
            File.Copy("defaultfiles/default.data", $"darknet/data/{game}.data", true);

            if (File.Exists($"trainfiles/{game}.names")) File.Copy($"trainfiles/{game}.names", $"darknet/{game}.names", true);
            else File.Copy("defaultfiles/default.names", $"darknet/data/{game}.names", true);

            File.Copy("defaultfiles/default.txt", $"darknet/data/{game}.txt", true);
            File.Copy("defaultfiles/default.cmd", $"darknet/{game}.cmd", true);
        }
    }
}
