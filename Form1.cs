
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;



namespace citacop
{
    public partial class Form1 : Form
    {
        private Color[,] amigaPaletteArray = new Color[64, 64]; // 64x64 Amiga palet array
        private List<Color> reducedPalette;
        private Bitmap backupImage = null;
        private List<Color> backupPalette = null;
        private Form paletteWindow = null;
        private Thread quantizeThread = null;
        private bool isQuantizing = false; // Flag to track if the thread should stop



        bool integerscale = false;
        int scalew = 320;
        int scaleh = 256;

        string lastfile = "";
        private float imageAspectRatio = 1.0f;

        private bool isDragging = false;
        private Point dragStartPoint;

        private bool isPaletteLocked = false;

        private float zoomFactor = 1.0f;
        private const float zoomStep = 0.1f; // How much to zoom in/out per step

        private List<Color> amigaPaletteList = new List<Color>();

        // Convert amigaPaletteArray to a 1D list once, to speed up lookup
        private void PrecomputeAmigaPalette()
        {
            for (int i = 0; i < 64; i++)
                for (int j = 0; j < 64; j++)
                    amigaPaletteList.Add(amigaPaletteArray[i, j]);
        }




        public Form1()
        {
            InitializeComponent();
            reducedPalette = new List<Color>();
            GenerateAmigaPaletteArray(); // Paleti oluştur
            PrecomputeAmigaPalette(); //1d amiga paleti
            pictureBox1.MouseWheel += pictureBox1_MouseWheel;
            pictureBox1.MouseEnter += pictureBox1_MouseEnter;
            comboBox1.SelectedIndex = 1;
            groupBox1.MouseDown += groupBox1_MouseDown;
            groupBox1.MouseMove += groupBox1_MouseMove;
            groupBox1.MouseUp += groupBox1_MouseUp;

        }
        private void pictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
                zoomFactor += zoomStep; // Zoom in
            else if (e.Delta < 0)
                zoomFactor = Math.Max(zoomStep, zoomFactor - zoomStep); // Zoom out, prevent negative scale

            ApplyZoom();
        }
        private void ApplyZoom()
        {
            if (pictureBox1.Image == null) return;
            int lacer = 1;
            if (medResToolStripMenuItem.Checked && !interlacedToolStripMenuItem.Checked) lacer = 2;
            int newWidth = (int)(pictureBox1.Image.Width * zoomFactor);
            int newHeight = (int)(pictureBox1.Image.Height * (zoomFactor * lacer));

            pictureBox1.Size = new Size(newWidth, newHeight);
        }

        private string CheckMenuItem(ToolStripMenuItem mnu, ToolStripMenuItem checked_item)
        {
            string ret = string.Empty;
            // Uncheck the menu items except checked_item.
            foreach (ToolStripItem item in mnu.DropDownItems)
            {
                if (item is ToolStripMenuItem menu_item)
                {
                    if (menu_item == checked_item)
                    {
                        menu_item.Checked = true;
                        ret = menu_item.Text ?? string.Empty;
                    }
                    else
                    {
                        menu_item.Checked = false;
                    }
                }
            }
            return ret;
        }

        private string CheckColorSelection(ToolStripMenuItem mnu)
        {
            // Uncheck the menu items except checked_item.
            foreach (ToolStripItem item in mnu.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Checked)
                {
                    return menuItem.Tag?.ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        private void GenerateAmigaPaletteArray()
        {
            int index = 0;
            for (int r = 0; r < 16; r++)
            {
                for (int g = 0; g < 16; g++)
                {
                    for (int b = 0; b < 16; b++)
                    {
                        int x = index % 64;
                        int y = index / 64;

                        int red = (r << 4) | r;   // 4-bit to 8-bit dönüşüm (0x1 -> 0x11)
                        int green = (g << 4) | g;
                        int blue = (b << 4) | b;

                        amigaPaletteArray[x, y] = Color.FromArgb(red, green, blue);
                        index++;
                    }
                }
            }
        }






        private void button2_Click(object sender, EventArgs e)
        {
            isQuantizing = false;
            button2.Visible = false;

            Application.DoEvents();

        }

        private void saveAs()
        {
            if (pictureBox1.Image == null)
            {
                UpdateLabel("Nothing to save!");
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "Save Image";
                saveFileDialog.Filter = "ILBM Amiga IFF|*.iff|Bitmap Image|*.bmp|PNG Image|*.png|JPEG Image|*.jpg;*.jpeg";
                saveFileDialog.FileName = "AmigafiedImage.bmp"; // Varsayılan dosya adı

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // PictureBox içeriğini doğrudan kaydetmek yerine, yeni bitmap oluşturup kaydediyoruz.
                    Bitmap bitmapToSave = new Bitmap(pictureBox1.Image);

                    string extension = Path.GetExtension(saveFileDialog.FileName).ToLower();
                    System.Drawing.Imaging.ImageFormat format = System.Drawing.Imaging.ImageFormat.Bmp; // Varsayılan BMP

                    if (extension == ".iff")
                    {
                        int bitplanes = (int)Math.Ceiling(Math.Log2(reducedPalette.Count));  // 16 renk için 4 bit, 32 renk için 5 bit
                        SaveAsILBM(new Bitmap(pictureBox1.Image), saveFileDialog.FileName, reducedPalette, bitplanes, false);
                        UpdateLabel($"Saved as: {saveFileDialog.FileName}");
                        return;
                    }
                    else if (extension == ".png")
                        format = System.Drawing.Imaging.ImageFormat.Png;
                    else if (extension == ".jpg" || extension == ".jpeg")
                        format = System.Drawing.Imaging.ImageFormat.Jpeg;

                    // Bitmap'i kullanıcı tarafından seçilen formatta kaydet
                    bitmapToSave.Save(saveFileDialog.FileName, format);

                    UpdateLabel($"Saved as: {saveFileDialog.FileName}");
                }
            }
        }



        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (int.TryParse(textBox1.Text, out int scw) && int.TryParse(textBox2.Text, out int sch))
            {
                Crop(scw, sch);

            }
        }

        private void LoadImage()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Pick an image";
                openFileDialog.Filter = "Picture Files|*.bmp;*.png;*.jpg;*.jpeg|All Files|*.*";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedFilePath = openFileDialog.FileName;
                    pictureBox1.Image = Image.FromFile(selectedFilePath);
                    displaySetup(false);
                    lastfile = openFileDialog.FileName;
                    reopenLastFileToolStripMenuItem.Enabled = true;
                    UpdateInfo();
                    UpdateLabel("Loaded. Select an OPERATION from operations menu.");
                }
            }
        }

        private void reopen(string filename)
        {
            string selectedFilePath = filename;
            pictureBox1.Image = Image.FromFile(selectedFilePath);
            pictureBox1.Width = pictureBox1.Image.Width;
            pictureBox1.Height = pictureBox1.Image.Height;
            UpdateInfo();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            groupBox1.Visible = false;
        }
        private void Quantize(Bitmap sourceImage)
        {
            int width = sourceImage.Width;
            int height = sourceImage.Height;
            int totalPixels = width * height;
            int processedPixels = 0;

            Bitmap result = new Bitmap(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color originalColor = sourceImage.GetPixel(x, y);

                    // Extract the 4-bit Amiga equivalent
                    int amigaR = (originalColor.R / 16); // 4-bit (0-15)
                    int amigaG = (originalColor.G / 16);
                    int amigaB = (originalColor.B / 16);

                    // Expand to 8-bit using (x << 4) | x
                    int fixedR = (amigaR << 4) | amigaR; // Ensures F0 → FF, A0 → AA
                    int fixedG = (amigaG << 4) | amigaG;
                    int fixedB = (amigaB << 4) | amigaB;

                    Color amigaColor = Color.FromArgb(originalColor.A, fixedR, fixedG, fixedB);
                    result.SetPixel(x, y, amigaColor);

                    processedPixels++;
                }

                // Update progress (UI thread)
                int progress = (processedPixels * 100) / totalPixels;
                UpdateLabel($"Progress %{progress}");
                if (!isQuantizing)
                {
                    UpdateLabel($"ABORTED!", Color.Red);
                    return;
                }
            }

            // Update the final image
            UpdatePictureBox(result);
            UpdateLabel("Done!");
            isQuantizing = false;
            pictureBox1.Invoke(new Action(() => button2.Visible = false));
        }



        private void StopQuantization()
        {
            if (quantizeThread != null && isQuantizing)
            {
                isQuantizing = false; // Tell the thread to stop
                quantizeThread.Join(); // Wait for the thread to finish
                quantizeThread = null;
                UpdateLabel("Quantization stopped.");
            }
        }
        private bool IsQuantizing()
        {
            return quantizeThread != null && quantizeThread.IsAlive;
        }


        private List<Color> getReducedPalette(Bitmap image, int colorCount)
        {
            List<Color> pixels = new List<Color>();

            // Görüntüdeki tüm pikselleri topla
            for (int y = 0; y < image.Height; y++)
            {
                UpdateLabel("Building pixel list... " + y.ToString());
                for (int x = 0; x < image.Width; x++)
                {
                    pixels.Add(image.GetPixel(x, y));
                }
            }
            /*
            OctreeQuantizer quantizer = new OctreeQuantizer(colorCount, UpdateLabel);
            List<Color> npalette = quantizer.Quantize(pixels);
            UpdateLabel("Palette Ready");
            return npalette; */
            return KMeansCluster(pixels, colorCount);
        }
        private Bitmap ReduceColors(Bitmap image)
        {

            //Eksik renkleri siyahla doldur (2, 4, 8, 16 veya 32'ye tamamla)
            int[] validColorCounts = { 2, 4, 8, 16, 32, 64 };
            int targetColors = validColorCounts.First(n => n >= reducedPalette.Count);

            while (reducedPalette.Count < targetColors)
            {
                reducedPalette.Add(Color.Black);
            }

            //Görüntüyü bu palete göre quantize et
            Bitmap quantizedImage = new Bitmap(image.Width, image.Height);
            for (int y = 0; y < image.Height; y++)
            {
                UpdateLabel("Processing Raster " + y.ToString());
                for (int x = 0; x < image.Width; x++)
                {
                    Color originalColor = image.GetPixel(x, y);
                    Color closestColor = FindClosestColorInList(originalColor, reducedPalette);
                    closestColor = Color.FromArgb(originalColor.A, closestColor.R, closestColor.G, closestColor.B);
                    quantizedImage.SetPixel(x, y, closestColor);
                }
            }

            return quantizedImage;
        }

        private Bitmap ReduceColorsWithDithering(Bitmap image)
        {
            int width = image.Width;
            int height = image.Height;

            Bitmap ditheredImage = new Bitmap(width, height);

            // Floyd-Steinberg Error Diffusion Weights
            double[][] errorDiffusion = {
                new double[] { 0, 0, 7.0 / 16 },
                new double[] { 3.0 / 16, 5.0 / 16, 1.0 / 16 }
            };

            // Convert image to array for processing
            Color[,] imageData = new Color[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    imageData[x, y] = image.GetPixel(x, y);

            // Process each pixel with dithering
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color oldColor = imageData[x, y];
                    int oldR = oldColor.R, oldG = oldColor.G, oldB = oldColor.B;

                    // Find closest color ONLY from reducedPalette
                    int closestIndex = FindClosestPaletteIndex(oldColor);
                    Color newColor = reducedPalette[closestIndex];
                    newColor = Color.FromArgb(oldColor.A, newColor.R, newColor.G, newColor.B);
                    ditheredImage.SetPixel(x, y, newColor);

                    // Compute error
                    int errR = oldR - newColor.R;
                    int errG = oldG - newColor.G;
                    int errB = oldB - newColor.B;

                    // Distribute error to neighboring pixels
                    for (int dy = 0; dy < 2; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                            {
                                Color neighbor = imageData[nx, ny];
                                int nr = Clamp(neighbor.R + (int)(errR * errorDiffusion[dy][dx + 1]), 0, 255);
                                int ng = Clamp(neighbor.G + (int)(errG * errorDiffusion[dy][dx + 1]), 0, 255);
                                int nb = Clamp(neighbor.B + (int)(errB * errorDiffusion[dy][dx + 1]), 0, 255);
                                imageData[nx, ny] = Color.FromArgb(nr, ng, nb);
                            }
                        }
                    }
                }
            }

            return ditheredImage;
        }

        private Bitmap ReduceColorsWithOrderedDithering(Bitmap image, int matrixSize = 2)
        {
            int width = image.Width;
            int height = image.Height;

            Bitmap ditheredImage = new Bitmap(width, height);

            // Choose the correct Bayer matrix
            double[,] bayerMatrix = GetBayerMatrix(matrixSize);
            double ditheringStrength = 1.0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color oldColor = image.GetPixel(x, y);

                    // Get Bayer matrix threshold
                    double threshold = bayerMatrix[y % matrixSize, x % matrixSize] * ditheringStrength;

                    // Adjust each channel using the threshold
                    int adjustedR = Clamp(oldColor.R + (int)(threshold * 64 - 32), 0, 255);
                    int adjustedG = Clamp(oldColor.G + (int)(threshold * 64 - 32), 0, 255);
                    int adjustedB = Clamp(oldColor.B + (int)(threshold * 64 - 32), 0, 255);

                    // Adjusted color after dithering
                    Color ditheredColor = Color.FromArgb(adjustedR, adjustedG, adjustedB);

                    // Find the closest color in the reduced palette
                    int closestIndex = FindClosestPaletteIndex(ditheredColor);
                    Color newColor = reducedPalette[closestIndex];
                    newColor = Color.FromArgb(oldColor.A, newColor.R, newColor.G, newColor.B);

                    // Set the new color in the dithered image
                    ditheredImage.SetPixel(x, y, newColor);
                }
            }

            return ditheredImage;
        }


        private double[,] GetBayerMatrix(int size)
        {
            if (size == 4)
            {
                return new double[,] {
            {  0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0 },
            { 12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0 },
            {  3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0 },
            { 15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0 }
        };
            }
            else if (size == 8)
            {
                return new double[,] {
            {  0.0 / 64.0, 32.0 / 64.0,  8.0 / 64.0, 40.0 / 64.0,  2.0 / 64.0, 34.0 / 64.0, 10.0 / 64.0, 42.0 / 64.0 },
            { 48.0 / 64.0, 16.0 / 64.0, 56.0 / 64.0, 24.0 / 64.0, 50.0 / 64.0, 18.0 / 64.0, 58.0 / 64.0, 26.0 / 64.0 },
            { 12.0 / 64.0, 44.0 / 64.0,  4.0 / 64.0, 36.0 / 64.0, 14.0 / 64.0, 46.0 / 64.0,  6.0 / 64.0, 38.0 / 64.0 },
            { 60.0 / 64.0, 28.0 / 64.0, 52.0 / 64.0, 20.0 / 64.0, 62.0 / 64.0, 30.0 / 64.0, 54.0 / 64.0, 22.0 / 64.0 },
            {  3.0 / 64.0, 35.0 / 64.0, 11.0 / 64.0, 43.0 / 64.0,  1.0 / 64.0, 33.0 / 64.0,  9.0 / 64.0, 41.0 / 64.0 },
            { 51.0 / 64.0, 19.0 / 64.0, 59.0 / 64.0, 27.0 / 64.0, 49.0 / 64.0, 17.0 / 64.0, 57.0 / 64.0, 25.0 / 64.0 },
            { 15.0 / 64.0, 47.0 / 64.0,  7.0 / 64.0, 39.0 / 64.0, 13.0 / 64.0, 45.0 / 64.0,  5.0 / 64.0, 37.0 / 64.0 },
            { 63.0 / 64.0, 31.0 / 64.0, 55.0 / 64.0, 23.0 / 64.0, 61.0 / 64.0, 29.0 / 64.0, 53.0 / 64.0, 21.0 / 64.0 }
        };
            }
            else // Default to 2x2
            {
                return new double[,] {
            { 0.0 / 4.0, 2.0 / 4.0 },
            { 3.0 / 4.0, 1.0 / 4.0 }
        };
            }
        }


        private int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(value, max));
        }


        private List<Color> KMeansCluster(List<Color> pixels, int clusterCount)
        {

            if (pixels.Count == 0) return new List<Color>();

            // .. Use K-Means++ initialization for better starting points

            UpdateLabel("Clustering... This may take a while... ", SystemColors.MenuHighlight);

            List<Color> centers = InitializeCenters(pixels, clusterCount);
            bool changes = true;

            while (changes)
            {
                // .. Assign each pixel to the closest center
                Dictionary<Color, List<Color>> clusters = centers.ToDictionary(c => c, c => new List<Color>());
                foreach (var pixel in pixels)
                {
                    Color closest = centers.OrderBy(c => ColorDistance(pixel, c)).First();
                    clusters[closest].Add(pixel);
                }

                changes = false;
                List<Color> newCenters = new List<Color>();
                UpdateLabel("Processing color clusters...");
                foreach (var cluster in clusters)
                {
                    if (cluster.Value.Count > 0)
                    {
                        // .. Compute mean color
                        int r = (int)cluster.Value.Average(c => c.R);
                        int g = (int)cluster.Value.Average(c => c.G);
                        int b = (int)cluster.Value.Average(c => c.B);
                        Color newCenter = Color.FromArgb(r, g, b);

                        // .. Find closest Amiga palette color
                        Color closestAmigaColor = FindClosest2DTableSlow(newCenter);

                        if (!newCenters.Contains(closestAmigaColor)) // Prevent duplicates
                        {
                            newCenters.Add(closestAmigaColor);
                            if (!centers.Contains(closestAmigaColor)) changes = true;
                        }
                    }
                }

                // .. Ensure exactly `clusterCount` colors
                while (newCenters.Count < clusterCount)
                {
                    Color randomPixel = pixels[new Random().Next(pixels.Count)];
                    Color closestAmigaColor = FindClosest2DTableSlow(randomPixel);
                    if (!newCenters.Contains(closestAmigaColor))
                        newCenters.Add(closestAmigaColor);
                }

                centers = newCenters;
            }
            UpdateLabel("Color Palette is ready.");
            return centers;
        }


        private List<Color> InitializeCenters(List<Color> pixels, int clusterCount)
        {
            List<Color> centers = new List<Color>();
            Random rand = new Random();

            // .. Pick the first color randomly
            centers.Add(pixels[rand.Next(pixels.Count)]);

            // .. Pick remaining colors based on max distance
            while (centers.Count < clusterCount)
            {
                Color bestCandidate = pixels.OrderByDescending(p =>
                    centers.Min(c => ColorDistance(p, c))
                ).First();

                centers.Add(bestCandidate);
            }

            return centers;
        }

        private double ColorDistance(Color c1, Color c2)
        {
            return Math.Sqrt(Math.Pow(c1.R - c2.R, 2) + Math.Pow(c1.G - c2.G, 2) + Math.Pow(c1.B - c2.B, 2));
        }


        private Color FindClosestColorInList(Color input, List<Color> palette)
        {
            return palette.OrderBy(c => ColorDistance(input, c)).First();
        }

        private int FindClosestPaletteIndex(Color inputColor)
        {
            int bestIndex = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < reducedPalette.Count; i++) // .. Artık reducedPalette içinde arama yapıyoruz
            {
                Color paletteColor = reducedPalette[i];
                double distance = ColorDistance(inputColor, paletteColor);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private Color FindClosestAmigaColorNolookup(Color inputColor)
        {
            // Convert 8-bit RGB to 4-bit Amiga color space
            int amigaR = inputColor.R / 16;
            int amigaG = inputColor.G / 16;
            int amigaB = inputColor.B / 16;
            //return Color.FromArgb(inputColor.A, amigaR*16, amigaG*16, amigaB*16);

            // Convert back to 8-bit using (x << 4) | x to expand properly
            int fixedR = (amigaR << 4) | amigaR;
            int fixedG = (amigaG << 4) | amigaG;
            int fixedB = (amigaB << 4) | amigaB;

            return Color.FromArgb(inputColor.A, fixedR, fixedG, fixedB);
        }

        // Optimized FindClosestAmigaColor
        private Color FindClosestFrom1DTableParallel(Color inputColor)
        {

            Color bestMatch = Color.Black;
            int minDistance = int.MaxValue;

            Parallel.ForEach(amigaPaletteList, amigaColor =>
            {
                int distance =
                    (inputColor.R - amigaColor.R) * (inputColor.R - amigaColor.R) +
                    (inputColor.G - amigaColor.G) * (inputColor.G - amigaColor.G) +
                    (inputColor.B - amigaColor.B) * (inputColor.B - amigaColor.B);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestMatch = amigaColor;
                }
            });

            return bestMatch;
        }

        private Color FindClosest2DTableSlow(Color inputColor)
        {
            Color bestMatch = Color.Black;
            double minDistance = double.MaxValue;

            for (int i = 0; i < 64; i++)
            {
                for (int j = 0; j < 64; j++)
                {
                    Color amigaColor = amigaPaletteArray[i, j];

                    double distance = Math.Sqrt(
                        Math.Pow(inputColor.R - amigaColor.R, 2) +
                        Math.Pow(inputColor.G - amigaColor.G, 2) +
                        Math.Pow(inputColor.B - amigaColor.B, 2)
                    );

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestMatch = amigaColor;
                    }
                }
            }

            return bestMatch;
        }



        private void UpdateLabel(string text, Color col)
        {
            if (statusStrip1.InvokeRequired)
            {
                statusStrip1.Invoke(new Action(() =>
                {
                    toolStripStatusLabel1.Text = text;
                    toolStripStatusLabel1.ForeColor = col;
                }));
            }
            else
            {
                toolStripStatusLabel1.Text = text;
                toolStripStatusLabel1.ForeColor = col;
            }
        }

        private void UpdateLabel(string text)
        {
            UpdateLabel(text, SystemColors.WindowText);
        }



        private void UpdateInfo()
        {
            string tag = CheckColorSelection(colorReduceSettingToolStripMenuItem);
            string mode = (solidToolStripMenuItem.Checked ? "S" : floydSteinbergToolStripMenuItem.Checked ? "F" : "O");
            string display = medResToolStripMenuItem.Checked ? "Medres " : "";
            display += interlacedToolStripMenuItem.Checked ? "Laced" : "";
            if (pictureBox1.Image != null) toolStripStatusLabel2.Text = "[" + pictureBox1.Image.Width.ToString() + "x" + pictureBox1.Image.Height.ToString() + "-" + mode + ">" + scalew.ToString() + "x" + scaleh.ToString() + "@" + tag + "] " + display;

        }

        private void UpdatePictureBox(Bitmap image)
        {
            if (pictureBox1.InvokeRequired)
            {
                pictureBox1.Invoke(new Action(() => pictureBox1.Image = image));
            }
            else
            {
                pictureBox1.Image = image;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {

        }

        private void reduceNow(int colorcount, int dither, bool paletteOnly = false)
        {
            bool isEHB = false;
            if (colorcount == 64)
            {
                isEHB = true;
                colorcount = 32;
            }

            if (isQuantizing)
            {
                UpdateLabel("Quantization is already in progress...", Color.Red);
                return;
            }

            if (pictureBox1.Image != null)
            {
                isQuantizing = true;
                UpdateLabel("Quantizing...");
                Thread quantizeThread = new Thread(() =>
                {
                    Bitmap inputImage = new Bitmap(pictureBox1.Image);

                    // Görseldeki toplam renkleri say
                    HashSet<Color> uniqueColors = new HashSet<Color>();
                    for (int y = 0; y < inputImage.Height; y++)
                        for (int x = 0; x < inputImage.Width; x++)
                            uniqueColors.Add(inputImage.GetPixel(x, y));

                    int targetColors = Math.Min(colorcount, uniqueColors.Count); // 32'den az renk varsa, mevcut olanları kullan

                    if (paletteOnly) { reducedPalette = getReducedPalette(inputImage, targetColors); isQuantizing = false; return; } //only generate palette and exit.

                    if (isPaletteLocked && reducedPalette.Count() == targetColors)
                    {
                        UpdateLabel("Using locked palette.");
                    }
                    else if (isPaletteLocked)
                    {
                        UpdateLabel("Color count mismatch. UNLOCK PALETTE FIRST: PRESS F3", Color.Red);
                        return;

                    }
                    else //generate new palette.
                    {
                        reducedPalette = getReducedPalette(inputImage, targetColors);
                    }



                    if (isEHB)
                    {
                        List<Color> ehbPalette = new List<Color>(reducedPalette); // Copy original palette

                        for (int i = 0; i < reducedPalette.Count; i++)
                        {
                            Color original = reducedPalette[i];

                            // Generate half-bright color (divide RGB values by 2)
                            Color halfBright = Color.FromArgb(
                                original.A,
                                original.R / 2,
                                original.G / 2,
                                original.B / 2
                            );

                            ehbPalette.Add(halfBright); // Append to palette
                        }

                        reducedPalette = ehbPalette;
                    }


                    Bitmap reducedImage;
                    switch (dither)
                    {

                        case 2:
                            UpdateLabel("Actual Color count:" + uniqueColors.Count.ToString() + ". Floyd dithering...");
                            reducedImage = ReduceColorsWithDithering(inputImage);
                            break;
                        case 3:
                            int bayersize = (ordered8x8ToolStripMenuItem.Checked ? 8 : ordered4x4ToolStripMenuItem.Checked ? 4 : 2);
                            UpdateLabel("Actual Color count:" + uniqueColors.Count.ToString() + ". Ordered dithering...");
                            reducedImage = ReduceColorsWithOrderedDithering(inputImage, bayersize);
                            break;
                        default:
                            UpdateLabel("Actual Color count:" + uniqueColors.Count.ToString() + ". Processing without dithering...");
                            reducedImage = ReduceColors(inputImage);
                            break;

                    }
                    UpdatePictureBox(reducedImage);

                    UpdateLabel($"Done! Color count now: {targetColors}");
                    isQuantizing = false;
                    pictureBox1.Invoke(new Action(() =>
                    {
                        // If the window is already open, just update the colors
                        if (paletteWindow != null && !paletteWindow.IsDisposed)
                        {
                            UpdatePaletteImage();  // Update the existing image
                            paletteWindow.BringToFront();

                        }
                        button2.Visible = false;
                    }));

                });

                quantizeThread.IsBackground = true;
                quantizeThread.Start();

            }
            else
            {
                UpdateLabel("Load an image first!");
            }
        }


        private void ShowPalette()
        {
            if (reducedPalette == null || reducedPalette.Count == 0)
            {
                string colorCountStr = CheckColorSelection(colorReduceSettingToolStripMenuItem);
                if (int.TryParse(colorCountStr, out int colorCount) && colorCount > 0)
                {
                    reducedPalette = GenerateDefaultPalette(colorCount);
                }
                else
                {
                    UpdateLabel("No valid palette available.");
                    return;
                }
            }

            if (paletteWindow != null && !paletteWindow.IsDisposed)
            {
                UpdatePaletteImage();
                paletteWindow.BringToFront();
                return;
            }

            paletteWindow = new Form
            {
                Text = "Palette Preview",
                Size = new Size(250, 140), // Increased size to fit checkbox
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                TopMost = true
            };

            int offsetX = this.Location.X + this.Width - paletteWindow.Width - 20;
            int offsetY = this.Location.Y + 40;
            paletteWindow.StartPosition = FormStartPosition.Manual;
            paletteWindow.Location = new Point(offsetX, offsetY);

            PictureBox paletteBox = new PictureBox
            {
                Size = new Size(210, 50),
                Location = new Point(10, 10),
                BorderStyle = BorderStyle.FixedSingle
            };

            paletteBox.Image = GeneratePaletteBitmap();
            paletteBox.MouseClick += PaletteBox_MouseClick; // Enable color editing

            // Lock Palette Checkbox
            CheckBox lockCheckBox = new CheckBox
            {
                Text = "Lock Palette",
                Checked = isPaletteLocked,
                Location = new Point(10, 70),
                AutoSize = true
            };
            lockCheckBox.CheckedChanged += (s, e) => isPaletteLocked = lockCheckBox.Checked;

            paletteWindow.Controls.Add(paletteBox);
            paletteWindow.Controls.Add(lockCheckBox); // Add the checkbox

            paletteWindow.FormClosed += (s, e) => paletteWindow = null;
            paletteWindow.Show();
        }


        private List<Color> GenerateDefaultPalette(int colorCount)
        {
            List<Color> palette = new List<Color>();

            for (int i = 0; i < colorCount; i++)
            {
                Random n = new Random();
                int r = ((n.Next(255)) & 0xF0); // Scale to 4-bit Amiga range
                int g = ((n.Next(255)) & 0xF0); //>> 1;
                int b = ((n.Next(255)) & 0xF0);// >> 2;

                palette.Add(Color.FromArgb(r | (r >> 4), g | (g >> 4), b | (b >> 4))); // Expand 4-bit to 8-bit
            }

            return palette;
        }


        private void PaletteBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (reducedPalette == null || reducedPalette.Count == 0) return;

            PictureBox paletteBox = sender as PictureBox;
            if (paletteBox == null || paletteBox.Image == null) return;

            int colorsPerRow = 8; // Fixed columns
            int boxWidth = paletteBox.Width / colorsPerRow;
            int boxHeight = paletteBox.Height / ((reducedPalette.Count + colorsPerRow - 1) / colorsPerRow); // Adjust rows dynamically

            int col = e.X / boxWidth; // Column index
            int row = e.Y / boxHeight; // Row index
            int colorIndex = row * colorsPerRow + col; // Compute color index

            if (colorIndex >= reducedPalette.Count) return; // Out of range check

            using (ColorDialog colorDialog = new ColorDialog())
            {
                colorDialog.Color = reducedPalette[colorIndex]; // Start with current color
                colorDialog.FullOpen = true;
                colorDialog.AnyColor = true;

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    reducedPalette[colorIndex] = colorDialog.Color; // Update palette color
                    paletteBox.Image = GeneratePaletteBitmap(); // Refresh palette display
                }
            }
        }


        private void ShowPalette2()
        {
            if (reducedPalette == null || reducedPalette.Count == 0)
            {
                UpdateLabel("No palette available.");
                return;
            }

            // If the window is already open, just update the colors
            if (paletteWindow != null && !paletteWindow.IsDisposed)
            {
                UpdatePaletteImage();  // .. Update the existing image
                paletteWindow.BringToFront();
                return;
            }

            // Create a new tool window
            paletteWindow = new Form
            {
                Text = "Palette Preview",
                Size = new Size(250, 110),
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                TopMost = true
            };

            // Position palette window at the top-right of the main window
            int offsetX = this.Location.X + this.Width - paletteWindow.Width - 20;
            int offsetY = this.Location.Y + 40;
            paletteWindow.StartPosition = FormStartPosition.Manual;
            paletteWindow.Location = new Point(offsetX, offsetY);

            // Create PictureBox
            PictureBox paletteBox = new PictureBox
            {
                Size = new Size(210, 50),
                Location = new Point(10, 10),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Assign bitmap to PictureBox
            paletteBox.Image = GeneratePaletteBitmap();
            paletteWindow.Controls.Add(paletteBox);

            // When the palette window closes, reset the reference
            paletteWindow.FormClosed += (s, e) => paletteWindow = null;

            paletteWindow.Show();
        }



        private void UpdatePaletteImage()
        {
            if (paletteWindow != null && !paletteWindow.IsDisposed)
            {
                foreach (Control control in paletteWindow.Controls)
                {
                    if (control is PictureBox pictureBox)
                    {
                        if (pictureBox.Image != null) { pictureBox.Image.Dispose(); }
                        pictureBox.Image = GeneratePaletteBitmap();

                        pictureBox.Refresh();
                        break;
                    }
                }
            }
        }

        private Bitmap GeneratePaletteBitmap()
        {
            Bitmap paletteImage = new Bitmap(210, 50);
            using (Graphics g = Graphics.FromImage(paletteImage))
            {
                g.Clear(Color.Black);

                int colorsPerRow = 8;
                int boxWidth = paletteImage.Width / colorsPerRow;
                int boxHeight = paletteImage.Height / ((reducedPalette.Count + colorsPerRow - 1) / colorsPerRow);

                for (int i = 0; i < reducedPalette.Count; i++)
                {
                    int x = (i % colorsPerRow) * boxWidth;
                    int y = (i / colorsPerRow) * boxHeight;
                    using (Brush brush = new SolidBrush(reducedPalette[i]))
                    {
                        g.FillRectangle(brush, x, y, boxWidth, boxHeight);
                    }
                }
            }
            return paletteImage;
        }


        private void button6_Click(object sender, EventArgs e)
        {

        }

        private void button7_Click(object sender, EventArgs e)
        {
            textBox1.Text = pictureBox1.Image.Width.ToString();

            textBox2.Text = pictureBox1.Image.Height.ToString();
        }


        // ushort için Big Endian yazma
        private void WriteBigEndian(BinaryWriter writer, ushort value)
        {
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        // short için Big Endian yazma (signed değerler için)
        private void WriteBigEndian(BinaryWriter writer, short value)
        {
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        // uint için Big Endian yazma
        private void WriteBigEndian(BinaryWriter writer, uint value)
        {
            writer.Write((byte)((value >> 24) & 0xFF));
            writer.Write((byte)((value >> 16) & 0xFF));
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }



        private void SaveAsILBM(Bitmap image, string filePath, List<Color> palette, int bitplanes, bool savePalette = false)
        {
            bool useCompression = false; //not working :D
            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                //FORM Chunk (Ana IFF Başlığı)
                writer.Write(Encoding.ASCII.GetBytes("FORM"));
                long formSizePos = fs.Position;
                WriteBigEndian(writer, (uint)0); // Geçici olarak 0 yaz, en son güncellenecek
                writer.Write(Encoding.ASCII.GetBytes("ILBM"));

                //BMHD Chunk (Bitmap Header)
                writer.Write(Encoding.ASCII.GetBytes("BMHD"));
                WriteBigEndian(writer, (uint)20); // Header uzunluğu

                WriteBigEndian(writer, (ushort)image.Width);
                WriteBigEndian(writer, (ushort)image.Height);
                WriteBigEndian(writer, (short)0); // X pozisyon
                WriteBigEndian(writer, (short)0); // Y pozisyon
                writer.Write((byte)bitplanes);
                writer.Write((byte)0); // Masking: mskNone
                writer.Write((byte)(useCompression ? 1 : 0)); // Compression: cmpByteRun1 veya cmpNone
                writer.Write((byte)0); // Pad
                WriteBigEndian(writer, (ushort)0); // Transparent Color
                writer.Write((byte)10); // X Aspect
                writer.Write((byte)10); // Y Aspect
                WriteBigEndian(writer, (short)image.Width);
                WriteBigEndian(writer, (short)image.Height);

                // CMAP Chunk (Color Palette)
                writer.Write(Encoding.ASCII.GetBytes("CMAP"));

                // Write only the first 32 colors in EHB mode, full palette otherwise
                int colorCount = (bitplanes < 6) ? palette.Count : 32;
                WriteBigEndian(writer, (uint)(colorCount * 3)); // Palette size

                foreach (var color in palette.Take(colorCount))
                {
                    writer.Write(color.R);
                    writer.Write(color.G);
                    writer.Write(color.B);
                }



                //CAMG Chunk (Ekran Modu)
                writer.Write(Encoding.ASCII.GetBytes("CAMG")); // Chunk ID
                WriteBigEndian(writer, (uint)4); // Chunk boyutu (4 bayt)
                WriteBigEndian(writer, GetCAMGMode()); // Correct CAMG mode
                long bodySizePos= fs.Position;
                if (!savePalette)
                {
                    //BODY Chunk (Bitmap Verisi)
                    writer.Write(Encoding.ASCII.GetBytes("BODY"));
                    bodySizePos = fs.Position;
                    WriteBigEndian(writer, (uint)0); // Geçici olarak 0 yaz

                    byte[] bitmapData = ConvertBitmapToPlanar(image, bitplanes);
                    byte[] finalData = useCompression ? ByteRun1Compress(bitmapData) : bitmapData;
                    writer.Write(finalData);
                }
                // Güncellenmiş Boyutları Yaz (FORM ve BODY)
                long endPos = fs.Position;
                fs.Seek(formSizePos, SeekOrigin.Begin);
                WriteBigEndian(writer, (uint)(endPos - 8)); // FORM toplam boyut
                if (!savePalette)
                {
                    fs.Seek(bodySizePos, SeekOrigin.Begin);
                    WriteBigEndian(writer, (uint)(endPos - (bodySizePos + 4))); // BODY boyutu
                }
            }
        }



        private byte[] ByteRun1Compress(byte[] input)
        {
            List<byte> output = new List<byte>();
            int i = 0;

            while (i < input.Length)
            {
                int j = i + 1;
                while (j < input.Length && input[j] == input[i] && (j - i) < 128)
                    j++;

                int count = j - i;
                if (count > 1) // Tekrarlanan bayt
                {
                    output.Add((byte)(257 - count)); // Negatif uzunluk kodu
                    output.Add(input[i]);
                }
                else // Tekrarlanmayan bayt
                {
                    int start = i;
                    while (j < input.Length && (input[j] != input[j - 1] || (j - i) >= 128))
                        j++;

                    count = j - i;
                    output.Add((byte)(count - 1)); // Pozitif uzunluk kodu
                    for (int k = start; k < j; k++)
                        output.Add(input[k]);
                }
                i = j;
            }

            return output.ToArray();
        }

        private uint GetCAMGMode()
        {
            uint camgMode = 0x5000; // Default: Low Resolution (LowRes = 0x5000)

            if (medResToolStripMenuItem.Checked)
                camgMode = 0xD000; // Medium Resolution (Hires = 0xD000)

            if (interlacedToolStripMenuItem.Checked)
                camgMode |= 0x0004; // Interlaced (LACE)

            if (reducedPalette.Count() == 64)
                camgMode |= 0x0080; // Extra Half-Brite (EHB)

            return camgMode;
        }

        private byte[] ConvertBitmapToPlanar(Bitmap image, int bitplanes)
        {
            int widthBytes = ((image.Width + 15) / 16) * 2; // 16-bit word align
            int rowSize = bitplanes * widthBytes;
            byte[] output = new byte[rowSize * image.Height];

            for (int y = 0; y < image.Height; y++)
            {
                byte[] rowData = new byte[rowSize];
                UpdateLabel("Processing row: " + y.ToString());
                Application.DoEvents();

                for (int x = 0; x < image.Width; x++)
                {
                    // Get the closest palette index
                    Color pixel = image.GetPixel(x, y);
                    int colorIndex = FindClosestPaletteIndex(pixel);

                    int byteOffset = x / 8;
                    int bitPosition = 7 - (x % 8);

                    // If using EHB mode (64 colors, 6 bitplanes)
                    if (bitplanes == 6 && colorIndex > 31)
                    {
                        colorIndex -= 32; // Reduce to base 32
                        rowData[(5 * widthBytes) + byteOffset] |= (byte)(1 << bitPosition); // Set 6th bitplane (index 5)
                    }

                    // Process 5 main bitplanes (same for normal 32 or EHB mode)
                    for (int plane = 0; plane < Math.Min(bitplanes, 5); plane++)
                    {
                        if ((colorIndex & (1 << plane)) != 0)
                        {
                            rowData[(plane * widthBytes) + byteOffset] |= (byte)(1 << bitPosition);
                        }
                    }
                }

                // Copy row data to output buffer
                Array.Copy(rowData, 0, output, y * rowSize, rowSize);
            }

            return output;
        }


        private byte[] ConvertBitmapToPlanar32(Bitmap image, int bitplanes)
        {
            int widthBytes = ((image.Width + 15) / 16) * 2; // 16-bit word align
            int rowSize = bitplanes * widthBytes;
            byte[] output = new byte[rowSize * image.Height];

            for (int y = 0; y < image.Height; y++)
            {
                byte[] rowData = new byte[rowSize];
                UpdateLabel("Processing row: " + y.ToString());
                Application.DoEvents();
                for (int x = 0; x < image.Width; x++)
                {
                    // .. ColorIndex'i sadece 1 kere hesapla
                    Color pixel = image.GetPixel(x, y);
                    int colorIndex = FindClosestPaletteIndex(pixel);

                    int byteOffset = x / 8;
                    int bitPosition = 7 - (x % 8);

                    // .. Tüm bitplane'leri tek seferde işle
                    for (int plane = 0; plane < bitplanes; plane++)
                    {
                        if ((colorIndex & (1 << plane)) != 0) // Eğer bu bitplane'de bu piksel 1'se
                        {
                            rowData[(plane * widthBytes) + byteOffset] |= (byte)(1 << bitPosition);
                        }
                    }
                }

                // Satırı çıktı buffer'ına ekle
                Array.Copy(rowData, 0, output, y * rowSize, rowSize);
            }
            return output;
        }




        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void openImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadImage();

        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveAs();
        }

        private void processingSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void solidToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(ditheringSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void ditheringSettingToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void floydSteinbergToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(ditheringSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void orderedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(ditheringSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void colorReduceSettingToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void colorsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(colorReduceSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void colorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(colorReduceSettingToolStripMenuItem, item);

            }
            UpdateInfo();

        }

        private void colorsToolStripMenuItem4_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(colorReduceSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void colorsToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(colorReduceSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void colorsToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(colorReduceSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void amigafyNowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
            {
                UpdateLabel("Starting...");
                isQuantizing = true;
                button2.Visible = true;

                quantizeThread = new Thread(() => Quantize(new Bitmap(pictureBox1.Image)));
                quantizeThread.IsBackground = true;
                quantizeThread.Start();
            }
            else
            {
                UpdateLabel("Load an image first!");
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();

        }

        private void reduceColorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExecReduce();
        }

        private void ExecReduce()
        {
            int mode = (solidToolStripMenuItem.Checked ? 1 : floydSteinbergToolStripMenuItem.Checked ? 2 : 3);
            string tag = CheckColorSelection(colorReduceSettingToolStripMenuItem);
            if (tag != string.Empty)
            {
                button2.Visible = true;
                reduceNow(Convert.ToInt32(tag), mode);

            }
        }

        private void rescaleAgainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rescale(scalew, scaleh);

        }


        private void rescale(int newWidth, int newHeight)
        {
            if (pictureBox1.Image == null)
            {
                UpdateLabel("Load an image first!");

                return;
            }

            // Kullanıcının girdiği genişlik ve yükseklik değerlerini al

            if (newWidth > 0 && newHeight > 0)
            {
                // Mevcut resmi yeni boyutlara ölçekle
                Bitmap originalImage = new Bitmap(pictureBox1.Image);
                Bitmap resizedImage = new Bitmap(newWidth, newHeight);

                using (Graphics g = Graphics.FromImage(resizedImage))
                {

                    if (integerscale)
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    }
                    else
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    }

                    g.DrawImage(originalImage, 0, 0, newWidth, newHeight);
                }

                // PictureBox'a yeni resmi ata
                pictureBox1.Image = resizedImage;
                displaySetup();

                UpdateLabel($"Scaled: {newWidth}x{newHeight}");

            }
            else
            {
                UpdateLabel("Need a positive value!");
            }

        }

        private void Crop(int newWidth, int newHeight)
        {
            if (pictureBox1.Image == null)
            {
                UpdateLabel("Load an image first!");
                return;
            }

            Bitmap originalImage = new Bitmap(pictureBox1.Image);
            int originalWidth = originalImage.Width;
            int originalHeight = originalImage.Height;

            // Determine border color
            Color borderColor = (reducedPalette != null && reducedPalette.Count > 0) ? reducedPalette[0] : Color.Black;

            // If the crop size is smaller than the image, do normal crop
            if (newWidth <= originalWidth && newHeight <= originalHeight)
            {
                PerformCrop(originalImage, newWidth, newHeight);
                return;
            }

            // New image with padding (if crop size is larger)
            Bitmap paddedImage = new Bitmap(newWidth, newHeight);
            using (Graphics g = Graphics.FromImage(paddedImage))
            {
                g.Clear(borderColor);

                // Get correct position from comboBox1
                int x, y;
                string selectedPosition = comboBox1.SelectedItem?.ToString() ?? "Center";
                GetCropPosition(selectedPosition, newWidth, newHeight, originalWidth, originalHeight, out x, out y);

                // Draw the original image at the calculated position
                g.DrawImage(originalImage, x, y, originalWidth, originalHeight);
            }

            // Assign padded image to PictureBox
            pictureBox1.Image = paddedImage;
            displaySetup();
            UpdateLabel($"Padded to: {newWidth}x{newHeight} at {comboBox1.SelectedItem}");
        }

        private void GetCropPosition(string position, int newWidth, int newHeight, int originalWidth, int originalHeight, out int x, out int y)
        {
            switch (position)
            {
                case "Top Left":
                    x = 0; y = 0;
                    break;
                case "Top":
                    x = (newWidth - originalWidth) / 2; y = 0;
                    break;
                case "Top Right":
                    x = newWidth - originalWidth; y = 0;
                    break;
                case "Right":
                    x = newWidth - originalWidth; y = (newHeight - originalHeight) / 2;
                    break;
                case "Bottom Right":
                    x = newWidth - originalWidth; y = newHeight - originalHeight;
                    break;
                case "Bottom":
                    x = (newWidth - originalWidth) / 2; y = newHeight - originalHeight;
                    break;
                case "Bottom Left":
                    x = 0; y = newHeight - originalHeight;
                    break;
                case "Left":
                    x = 0; y = (newHeight - originalHeight) / 2;
                    break;
                case "Center":
                default:
                    x = (newWidth - originalWidth) / 2;
                    y = (newHeight - originalHeight) / 2;
                    break;
            }
        }


        private void PerformCrop(Bitmap originalImage, int newWidth, int newHeight)
        {
            int originalWidth = originalImage.Width;
            int originalHeight = originalImage.Height;

            // Determine crop position based on comboBox1 selection
            int x = 0, y = 0;
            string selectedPosition = comboBox1.SelectedItem?.ToString() ?? "Center";

            switch (selectedPosition)
            {
                case "Top Left": x = 0; y = 0; break;
                case "Top": x = (originalWidth - newWidth) / 2; y = 0; break;
                case "Top Right": x = originalWidth - newWidth; y = 0; break;
                case "Right": x = originalWidth - newWidth; y = (originalHeight - newHeight) / 2; break;
                case "Bottom Right": x = originalWidth - newWidth; y = originalHeight - newHeight; break;
                case "Bottom": x = (originalWidth - newWidth) / 2; y = originalHeight - newHeight; break;
                case "Bottom Left": x = 0; y = originalHeight - newHeight; break;
                case "Left": x = 0; y = (originalHeight - newHeight) / 2; break;
                case "Center":
                default:
                    x = (originalWidth - newWidth) / 2;
                    y = (originalHeight - newHeight) / 2;
                    break;
            }

            // Crop the selected region
            Rectangle cropArea = new Rectangle(x, y, newWidth, newHeight);
            Bitmap croppedImage = originalImage.Clone(cropArea, originalImage.PixelFormat);

            // Assign cropped image to PictureBox
            pictureBox1.Image = croppedImage;
            displaySetup();
            UpdateLabel($"Cropped to: {newWidth}x{newHeight} at {selectedPosition}");
        }




        private void displaySetup(bool auto = false)
        {
            int newWidth = pictureBox1.Image.Width;
            int newHeight = pictureBox1.Image.Height;
            pictureBox1.Height = newHeight;
            pictureBox1.Width = newWidth;
            if (auto)
            {
                medResToolStripMenuItem.Checked = false;
                interlacedToolStripMenuItem.Checked = false;

                if (newWidth > 320) medResToolStripMenuItem.Checked = true;
                if (newHeight > 256) interlacedToolStripMenuItem.Checked = true;
            }
            if (medResToolStripMenuItem.Checked) pictureBox1.Height = newHeight * 2;
            //if (interlacedToolStripMenuItem.Checked) pictureBox1.Height = newWidth * 2;

            UpdateInfo();


        }

        private void customToolStripMenuItem_Click(object sender, EventArgs e)
        {
            showCustomScale();

        }

        (int w, int h) parseResolution(string Tag)
        {
            scalew = 0;
            scaleh = 0;

            if (string.IsNullOrWhiteSpace(Tag))
            {
                UpdateLabel("Invalid resolution.");
                return (0, 0); // Geçersiz formatta giriş olursa
            }
            string[] parts = Tag.Split('x');
            if (parts.Length == 2 && int.TryParse(parts[0], out scalew) && int.TryParse(parts[1], out scaleh))
            {
                // Başarılı ayrıştırma
                return (scalew, scaleh);
            }
            else
            {
                // Hatalı giriş durumu, burada gerekirse hata yönetimi ekleyebilirsin

                UpdateLabel("Invalid resolution.");
                return (0, 0); // Geçersiz formatta giriş olursa
            }
        }



        private void x256ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                var (scalew, scaleh) = parseResolution(CheckMenuItem(rescaleToolStripMenuItem, item));
                rescale(scalew, scaleh);
            }
            UpdateInfo();
        }

        private void x200ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                var (scalew, scaleh) = parseResolution(CheckMenuItem(rescaleToolStripMenuItem, item));
                rescale(scalew, scaleh);
            }
            UpdateInfo();
        }

        private void x256ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                var (scalew, scaleh) = parseResolution(CheckMenuItem(rescaleToolStripMenuItem, item));
                rescale(scalew, scaleh);
            }
            UpdateInfo();
        }

        private void x200ToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                var (scalew, scaleh) = parseResolution(CheckMenuItem(rescaleToolStripMenuItem, item));
                rescale(scalew, scaleh);
            }
            UpdateInfo();
        }

        private void x512ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                var (scalew, scaleh) = parseResolution(CheckMenuItem(rescaleToolStripMenuItem, item));
                rescale(scalew, scaleh);
            }
            UpdateInfo();
        }

        private void x400ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                var (scalew, scaleh) = parseResolution(CheckMenuItem(rescaleToolStripMenuItem, item));
                rescale(scalew, scaleh);
            }
            UpdateInfo();
        }

        void showCustomScale()
        {
            if (pictureBox1.Image != null)
            {
                integerscale = integerScalingToolStripMenuItem.Checked;
                checkBox2.Checked = integerscale;
                imageAspectRatio = (float)pictureBox1.Image.Width / pictureBox1.Image.Height;
                groupBox1.Visible = true;

            }


        }

        private void applyScale()
        {
            if (int.TryParse(textBox1.Text, out int scw) && int.TryParse(textBox2.Text, out int sch))
            {
                if (checkBox1.Checked)
                {
                    float f = Math.Abs(((float)scw / sch) - imageAspectRatio);

                    if (f < 0.01)
                    {
                        scaleh = sch;
                        scalew = scw;

                        UpdateLabel($"Scale Width: {scalew}, Scale Height: {scaleh}");
                        groupBox1.Visible = false;
                        rescale(scalew, scaleh);
                    }
                    else
                    {
                        if (int.TryParse(textBox1.Text, out int newWidth))
                        {
                            int newHeight = (int)(newWidth / imageAspectRatio);
                            textBox2.Text = newHeight.ToString();
                            //checkBox1.Checked = false;
                        }
                    }
                }
                else
                {
                    scaleh = sch;
                    scalew = scw;

                    UpdateLabel($"Scale Width: {scalew}, Scale Height: {scaleh}");
                    groupBox1.Visible = false;
                    rescale(scalew, scaleh);
                }
            }
            else
            {
                UpdateLabel("Invalid resolution.");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            applyScale();

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && checkBox1.Checked && int.TryParse(textBox1.Text, out int newWidth))
            {
                int newHeight = (int)(newWidth / imageAspectRatio);
                textBox2.Text = newHeight.ToString();
            }
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && checkBox1.Checked && int.TryParse(textBox2.Text, out int newHeight))
            {
                int newWidth = (int)(newHeight * imageAspectRatio);
                textBox1.Text = newWidth.ToString();
            }
        }


        private void reopenLastFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            reopen(lastfile);
        }

        private void interlaceIfHeight256ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void medResToolStripMenuItem_Click(object sender, EventArgs e)
        {

            displaySetup(false);
        }

        private void interlacedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            displaySetup(false);
        }

        private void showPaletteToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void x256ToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                var (scalew, scaleh) = parseResolution(CheckMenuItem(rescaleToolStripMenuItem, item));
                rescale(scalew, scaleh);
            }
            UpdateInfo();
        }

        private void createStateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
            {
                backupImage = new Bitmap(pictureBox1.Image);

                // Backup the reduced palette if available
                if (reducedPalette != null && reducedPalette.Count > 0)
                {
                    backupPalette = new List<Color>(reducedPalette);
                    UpdateLabel("State saved (image & palette)!");
                }
                else
                {
                    backupPalette = null;
                    UpdateLabel("State saved (image only).");
                }
            }
            else
            {
                UpdateLabel("No image loaded to create state?!");
            }
        }


        private void revertToStateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (backupImage != null)
            {

                pictureBox1.Image = new Bitmap(backupImage);
                pictureBox1.Refresh();
                zoomFactor = 1.0f;
                ApplyZoom();
                // Restore the reduced palette if it was saved
                if (backupPalette != null && backupPalette.Count > 0)
                {
                    reducedPalette = new List<Color>(backupPalette);
                    ShowPalette(); // Refresh palette view
                    UpdateLabel("State restored (image & palette)!");
                }
                else
                {
                    UpdateLabel("State restored (image only).");
                }
            }
            else
            {
                UpdateLabel("No saved state to revert to.");
            }
        }


        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
            {
                Clipboard.SetImage(new Bitmap(pictureBox1.Image));
                UpdateLabel("Image copied to clipboard.");
            }
            else
            {
                UpdateLabel("No image to copy.");
            }
        }


        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                pictureBox1.Image = new Bitmap(Clipboard.GetImage());
                pictureBox1.Refresh();
                UpdateLabel("Image pasted from clipboard.");
                UpdateInfo();
                displaySetup(false);
            }
            else
            {
                UpdateLabel("No image found in clipboard.");
            }
        }

        private void integerScalingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            integerscale = integerScalingToolStripMenuItem.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            integerScalingToolStripMenuItem.Checked = checkBox2.Checked;
            integerscale = integerScalingToolStripMenuItem.Checked;
        }

        private void ingeterHalveToolStripMenuItem_Click(object sender, EventArgs e)
        {

            int scalew = pictureBox1.Image.Width / 2;
            int scaleh = pictureBox1.Image.Height / 2;
            rescale(scalew, scaleh);

            UpdateInfo();
        }

        private void quarterSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int scalew = pictureBox1.Image.Width / 4;
            int scaleh = pictureBox1.Image.Height / 4;
            rescale(scalew, scaleh);

            UpdateInfo();
        }

        private void pictureBox1_MouseEnter(object sender, EventArgs e)
        {
            //pictureBox1.Focus(); // Allow receiving mouse wheel events
        }

        private void pictureBox1_DoubleClick(object sender, EventArgs e)
        {
            zoomFactor = 1.0f;
            ApplyZoom();
        }

        private void resetZoomToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void doubleZoomToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void menuStrip1_ItemClicked_1(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void resetZoomToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            zoomFactor = 1.0f;
            ApplyZoom();
        }

        private void doubleZoomToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            zoomFactor *= 2.0f;
            ApplyZoom();
        }

        private void showPaletteToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ShowPalette();
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) // Check if it's a right-click
            {
                contextMenuStrip1.Show(pictureBox1, e.Location); // Show menu at click position
            }
        }

        private void convertToAmigaModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExecReduce();
        }

        private void amigafyToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image != null)
            {
                UpdateLabel("Starting...");
                Thread quantizeThread = new Thread(() => Quantize(new Bitmap(pictureBox1.Image)));
                quantizeThread.IsBackground = true;
                quantizeThread.Start();
            }
            else
            {
                UpdateLabel("Load an image first!");
            }
        }

        private void saveAsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            saveAs();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void groupBox1_MouseDown(object sender, MouseEventArgs e)
        {
            isDragging = true;
            dragStartPoint = e.Location; // Store initial click position
        }

        private void groupBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                int newX = groupBox1.Left + (e.X - dragStartPoint.X);
                int newY = groupBox1.Top + (e.Y - dragStartPoint.Y);

                // Ensure GroupBox stays within the form
                newX = Math.Max(0, Math.Min(this.ClientSize.Width - groupBox1.Width, newX));
                newY = Math.Max(0, Math.Min(this.ClientSize.Height - groupBox1.Height, newY));

                groupBox1.Location = new Point(newX, newY);
            }
        }

        private void groupBox1_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void ordered4x4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(ditheringSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void ordered8x8ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(ditheringSettingToolStripMenuItem, item);
            }
            UpdateInfo();
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                CheckMenuItem(colorReduceSettingToolStripMenuItem, item);

            }
            UpdateInfo();
        }

        private void toolStripMenuItem10_Click(object sender, EventArgs e)
        {
            //generate palette only.
            int mode = (solidToolStripMenuItem.Checked ? 1 : floydSteinbergToolStripMenuItem.Checked ? 2 : 3);
            string tag = CheckColorSelection(colorReduceSettingToolStripMenuItem);
            if (tag != string.Empty)
            {
                button2.Visible = true;
                reduceNow(Convert.ToInt32(tag), mode, true);
                while (isQuantizing)
                {
                    Application.DoEvents();
                }
                button2.Visible = false;
                isQuantizing = false;
                ShowPalette();
            }
        }

        private void toolStripMenuItem11_Click(object sender, EventArgs e)
        {
            SortPaletteByHue();
            ShowPalette();
        }
        private void SortPaletteByHue()
        {
            if (reducedPalette == null || reducedPalette.Count == 64 || reducedPalette.Count == 0) return;

            reducedPalette = reducedPalette
                .OrderBy(c => c.GetHue())      // 1️⃣ Sort by Hue (0-360 degrees)
                .ThenBy(c => c.GetSaturation()) // 2️⃣ Sort by Saturation (0-1)
                .ThenBy(c => c.GetBrightness()) // 3️⃣ Sort by Brightness (0-1)
                .ToList();
        }

        private void toolStripMenuItem13_Click(object sender, EventArgs e)
        {
            //Load Palette

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Palette Files|*.set;*.col;*.act;*.pal|Adobe Color Table (*.act)|*.act|Personal Paint Color Set (*.col)|*.col|Deluxe Paint Palette Set(*.set)|*.set|Amiga Palette(*.pal)|*.pal|All Files|*.*";
                openFileDialog.Title = "Load Palette";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string extension = Path.GetExtension(openFileDialog.FileName).ToLower();
                    if (extension == ".act")
                    {
                        LoadPaletteFromACT(openFileDialog.FileName);
                    }
                    else if (extension == ".pal" || (extension == ".set") || (extension == ".col"))
                    {
                        LoadPaletteFromILBM(openFileDialog.FileName);
                    }
                }
            }
        }

        private void toolStripMenuItem12_Click(object sender, EventArgs e)
        {
            //save palette
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Adobe Color Table (*.act)|*.act|Personal Paint Color Set (*.col)|*.col|Deluxe Paint Palette Set(*.set)|*.set|Amiga Palette(*.pal)|*.pal";
                saveFileDialog.Title = "Save Palette";
                saveFileDialog.FileName = "palette";
                
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string extension = Path.GetExtension(saveFileDialog.FileName).ToLower();

                    if (extension == ".act")
                    {
                        SavePaletteAsACT(saveFileDialog.FileName);
                    } 
                    else if (extension == ".pal" || (extension == ".set") || (extension == ".col"))
                    {
                        int bitplanes = (int)Math.Ceiling(Math.Log2(reducedPalette.Count));  // 16 renk için 4 bit, 32 renk için 5 bit
                        SaveAsILBM(new Bitmap(1,1), saveFileDialog.FileName, reducedPalette, bitplanes, true);

                    }
                }
            }

        }

        private void SavePaletteAsACT(string filePath)
        {
            if (reducedPalette == null || reducedPalette.Count == 0)
            {
                UpdateLabel("No palette to save!");
                return;
            }

            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                int maxColors = 256; // ACT format supports up to 256 colors
                int paletteSize = Math.Min(reducedPalette.Count, maxColors);

                // Write the palette colors (RGB triplets)
                for (int i = 0; i < paletteSize; i++)
                {
                    Color color = reducedPalette[i];
                    writer.Write(color.R);
                    writer.Write(color.G);
                    writer.Write(color.B);
                }

                // Fill remaining slots with black (0x00, 0x00, 0x00)
                for (int i = paletteSize; i < maxColors; i++)
                {
                    writer.Write((byte)0x00); // R
                    writer.Write((byte)0x00); // G
                    writer.Write((byte)0x00); // B
                }
            }

            UpdateLabel($"Palette saved as ACT: {filePath}");
        }

        private void LoadPaletteFromACT(string filePath)
        {
            if (!File.Exists(filePath))
            {
                UpdateLabel("File not found!");
                return;
            }

            List<Color> loadedPalette = new List<Color>();

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                int colorCount = (int)(fs.Length / 3); // Each color is 3 bytes (RGB)
                if (colorCount > 256) colorCount = 256; // ACT files have a max of 256 colors

                for (int i = 0; i < colorCount; i++)
                {
                    int r = reader.ReadByte();
                    int g = reader.ReadByte();
                    int b = reader.ReadByte();
                    loadedPalette.Add(Color.FromArgb(r, g, b));
                }
            }

            // 🔹 Remove trailing black colors (0,0,0) at the end of the palette
            for (int i = loadedPalette.Count - 1; i >= 0; i--)
            {
                if (loadedPalette[i].R == 0 && loadedPalette[i].G == 0 && loadedPalette[i].B == 0)
                {
                    loadedPalette.RemoveAt(i);
                }
                else
                {
                    break; // Stop when a non-black color is found
                }
            }

            // 🔹 Find the nearest valid palette size (2, 4, 8, 16, 32, 64, 128, or 256)
            int[] validSizes = { 2, 4, 8, 16, 32 };
            int nearestSize = validSizes.FirstOrDefault(size => size >= loadedPalette.Count);

            // 🔹 Fill up the palette with black if needed
            while (loadedPalette.Count < nearestSize)
            {
                loadedPalette.Add(Color.Black);
            }

            reducedPalette = loadedPalette; // Final updated palette

            UpdateLabel($"Palette loaded from ACT: {filePath} ({reducedPalette.Count} colors)");
            ShowPalette(); // Update the palette window
        }


        private void LoadPaletteFromILBM(string filePath)
        {
            if (!File.Exists(filePath))
            {
                UpdateLabel("File not found!");
                return;
            }

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                while (fs.Position < fs.Length)
                {
                    string chunkID = Encoding.ASCII.GetString(reader.ReadBytes(4)); // Read chunk name
                    

                    if (chunkID == "CMAP") // Found CMAP chunk
                    {
                        int chunkSize = ReadBigEndianInt(reader); // Read chunk size
                        int colorCount = chunkSize / 3; // Each color is 3 bytes (R, G, B)
                        List<Color> palette = new List<Color>();

                        for (int i = 0; i < colorCount; i++)
                        {
                            int r = reader.ReadByte();
                            int g = reader.ReadByte();
                            int b = reader.ReadByte();
                            palette.Add(Color.FromArgb(r, g, b));
                        }

                        reducedPalette = palette;
                        UpdateLabel($"Palette loaded from ILBM: {colorCount} colors");
                        ShowPalette();
                        return;
                    }
                    else
                    {
                        
                        fs.Position -= 3;

                    }
                }
            }

            UpdateLabel("CMAP chunk not found in ILBM file.");
        }

        private int ReadBigEndianInt(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

    }
}
