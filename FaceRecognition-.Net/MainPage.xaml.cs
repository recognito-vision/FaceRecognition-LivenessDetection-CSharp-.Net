using FaceSDK;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.Json;

public struct DetectionResult
{
    public FaceBox bbox;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
    public float[] feature;
    public Bitmap bitmap;

    public DetectionResult()
    {
        bbox = new FaceBox();
        feature = new float[128];
        bitmap = new Bitmap(1, 1);
    }
};
namespace FaceRecognition_.Net
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        string image_path1;
        string image_path2;
        int activateResult = (int)SDK_STATUS.SDK_LICENSE_KEY_ERROR;
        FaceEngineClass faceSDK = new FaceEngineClass();

        public MainPage()
        {
            InitializeComponent();

            HWIDEntry.Text = faceSDK.GetHardwareId();

            string exeFolder = AppDomain.CurrentDomain.BaseDirectory;
            string licensePath = Path.Combine(exeFolder, "license.txt");

            if (File.Exists(licensePath))
            {
                string licenseText = File.ReadAllText(licensePath);
                activateResult = faceSDK.Activate(licenseText);
            }
            else
            {
                ActivationStatusEntry.Text = "Can't find license file!";
            }

            if (activateResult != (int)SDK_STATUS.SDK_SUCCESS)
            {
                ActivationStatusEntry.Text = "Activation failed! Status code: " + activateResult.ToString();
            }
            else
            {
                var dictPath = $"{AppDomain.CurrentDomain.BaseDirectory}assets";
                int ret = faceSDK.Init(dictPath);
                if (ret != (int)SDK_STATUS.SDK_SUCCESS)
                {
                    ActivationStatusEntry.Text = "Failed to init SDK";
                } 
                else
                {
                    ActivationStatusEntry.Text = "Activation Success!";
                }
            }

            // *** call async function from sync function
            // Task task = Task.Run(async () => await LoadMauiAsset());
            // var fullPath = System.IO.Path.Combine(FileSystem.AppDataDirectory,"MyFolder","myfile.txt");

        }

        async Task LoadMauiAsset()
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("AboutAssets.txt");

            using var reader = new StreamReader(stream);

            var contents = reader.ReadToEnd();
        }

        public byte[] BitmapToJpegByteArray(Bitmap bitmap)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // Save the Bitmap as JPEG to the MemoryStream
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Jpeg);
                // Return the byte array
                return memoryStream.ToArray();
            }
        }

        public byte[] ImageToByteArray(System.Drawing.Image imageIn)
        {
            using (var ms = new MemoryStream())
            {
                imageIn.Save(ms, imageIn.RawFormat);
                return ms.ToArray();
            }
        }

        public static Bitmap ConvertTo24bpp(System.Drawing.Image img)
        {
            var bmp = new Bitmap(img.Width, img.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            using (var gr = Graphics.FromImage(bmp))
                gr.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
            return bmp;
        }

        private ImageSource ConvertBitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                return ImageSource.FromStream(() => new MemoryStream(memory.ToArray()));
            }
        }

        private static System.Drawing.Image LoadImageWithExif(String filePath)
        {
            try
            {
                System.Drawing.Image image = System.Drawing.Image.FromFile(filePath);

                // Check if the image has EXIF orientation data
                if (image.PropertyIdList.Contains(0x0112))
                {
                    int orientation = image.GetPropertyItem(0x0112).Value[0];

                    switch (orientation)
                    {
                        case 1:
                            // Normal
                            break;
                        case 3:
                            // Rotate 180
                            image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                            break;
                        case 6:
                            // Rotate 90
                            image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                            break;
                        case 8:
                            // Rotate 270
                            image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                            break;
                        default:
                            // Do nothing
                            break;
                    }
                }

                return image;
            }
            catch (Exception e)
            {
                throw new Exception("Image null!");
            }
        }

        public async Task<FileResult> OpenImage1(PickOptions options)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    if (result.FileName.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) ||
                        result.FileName.EndsWith("png", StringComparison.OrdinalIgnoreCase))
                    {
                        Image1.Source = ImageSource.FromFile(result.FullPath);

                        // Read local image, convert it to byte array, and get image size
                        image_path1 = result.FullPath;

                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                // The user canceled or something went wrong
            }

            return null;
        }

        public async Task<FileResult> OpenImage2(PickOptions options)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    if (result.FileName.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) ||
                        result.FileName.EndsWith("png", StringComparison.OrdinalIgnoreCase))
                    {
                        Image2.Source = ImageSource.FromFile(result.FullPath);

                        // Read local image, convert it to byte array, and get image size
                        image_path2 = result.FullPath;

                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                // The user canceled or something went wrong
            }

            return null;
        }

        private async void OnClickedSelectFace1Btn(object sender, EventArgs e)
        {

            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.my.comic.extension" } }, // UTType values
                    { DevicePlatform.Android, new[] { "application/comics" } }, // MIME type
                    { DevicePlatform.WinUI, new[] { ".jpg", ".png" } }, // file extension
                    { DevicePlatform.Tizen, new[] { "*/*" } },
                    { DevicePlatform.macOS, new[] { "jpg", "png" } }, // UTType values
                });

            PickOptions options = new()
            {
                PickerTitle = "Please select a image file",
                FileTypes = customFileType,
            };

            try
            {
                await OpenImage1(options);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException("It was cancelled");
            }

        }

        private async void OnClickedSelectFace2Btn(object sender, EventArgs e)
        {

            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.my.comic.extension" } }, // UTType values
                    { DevicePlatform.Android, new[] { "application/comics" } }, // MIME type
                    { DevicePlatform.WinUI, new[] { ".jpg", ".png" } }, // file extension
                    { DevicePlatform.Tizen, new[] { "*/*" } },
                    { DevicePlatform.macOS, new[] { "jpg", "png" } }, // UTType values
                });

            PickOptions options = new()
            {
                PickerTitle = "Please select a image file",
                FileTypes = customFileType,
            };

            try
            {
                await OpenImage2(options);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException("It was cancelled");
            }

        }
        private int DetectFace(string image_path, DetectionResult[] detectionResult)
        {
            System.Drawing.Image image = null;
            try
            {
                image = LoadImageWithExif(image_path);
                if (image == null)
                    return 0;
            }
            catch (Exception)
            {
                DisplayAlert("test", "Unknown Format!", "ok");
            }

            Bitmap imgBmp = ConvertTo24bpp(image);
            BitmapData bitmapData = imgBmp.LockBits(new Rectangle(0, 0, imgBmp.Width, imgBmp.Height), ImageLockMode.ReadWrite, imgBmp.PixelFormat);

            int bytesPerPixel = Bitmap.GetPixelFormatSize(imgBmp.PixelFormat) / 8;
            int byteCount = bitmapData.Stride * imgBmp.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bitmapData.Scan0;
            Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);

            imgBmp.UnlockBits(bitmapData);

            FaceBox[] faceBoxes = new FaceBox[detectionResult.Length];
            int faceCount = faceSDK.DetectFace(pixels, imgBmp.Width, imgBmp.Height, bitmapData.Stride, faceBoxes, detectionResult.Length);

            if (faceCount > 0)
            {
                if (detectionResult.Length < faceCount)
                    faceCount = detectionResult.Length;

                for (int i = 0; i < faceCount; i++)
                {
                    float[] feature = new float[128];
                    faceSDK.ExtractTemplate(pixels, imgBmp.Width, imgBmp.Height, bitmapData.Stride, faceBoxes[i].landmark_68, feature);
                    detectionResult[i].bbox = faceBoxes[i];
                    detectionResult[i].feature = feature;

                    // Crop the face from the original image
                    int x = (int)faceBoxes[i].x1;
                    int y = (int)faceBoxes[i].y1;
                    int width = (int)(faceBoxes[i].x2 - faceBoxes[i].x1);
                    int height = (int)(faceBoxes[i].y2 - faceBoxes[i].y1);

                    if (x < 0) x = 0;
                    if (y < 0) y = 0;
                    if (x + width > imgBmp.Width) width = imgBmp.Width - x;
                    if (y + height > imgBmp.Height) height = imgBmp.Height - y;

                    Rectangle cropArea = new Rectangle(x, y, width, height);
                    detectionResult[i].bitmap = imgBmp.Clone(cropArea, imgBmp.PixelFormat);
                }
                return faceCount;
            }
            
            return 0;
        }

        private async void OnClickedCompareBtn(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                await Task.Delay(100);
            }

            try
            {
                CompareResult.Source = ImageSource.FromFile("blank.png");
                Face1.Source = ImageSource.FromFile("face.jpg");
                Face2.Source = ImageSource.FromFile("face.jpg");
                ResultEditor.Text = "";
                await Task.Delay(100);

                if (activateResult != 0)
                {
                    await DisplayAlert("error", "SDK Activation Failed!", "ok");
                    return;
                }

                if (image_path1 == null || image_path2 == null)
                {
                    await DisplayAlert("error", "Select Image Files!", "ok");
                    return;
                }

                DetectionResult[] detectionResult1 = new DetectionResult[1];
                int cntFace1 = DetectFace(image_path1, detectionResult1);

                DetectionResult[] detectionResult2 = new DetectionResult[1];
                int cntFace2 = DetectFace(image_path2, detectionResult2);

                if (cntFace1 == 0 && cntFace2 == 0)
                    await DisplayAlert("error", "Can't detect faces", "ok");
                else if (cntFace1 == 0)
                    await DisplayAlert("error", "Can't detect face in Image1", "ok");
                else if (cntFace2 == 0)
                    await DisplayAlert("error", "Can't detect face in Image2", "ok");
                else
                {
                    var face1 = detectionResult1[0];
                    var face2 = detectionResult2[0];
                    float similarity = faceSDK.CalculateSimilarity(face1.feature, face2.feature, 128);

                    Face1.Source = ConvertBitmapToImageSource(face1.bitmap);
                    Face2.Source = ConvertBitmapToImageSource(face2.bitmap);
                    if (similarity >= 80)
                        CompareResult.Source = ImageSource.FromFile("same.png");
                    else
                        CompareResult.Source = ImageSource.FromFile("different.png");

                    var face1Result = new
                    {
                        box = new int[] { (int)face1.bbox.x1, (int)face1.bbox.y1, (int)face1.bbox.x2, (int)face1.bbox.y2 },
                        liveness = face1.bbox.liveness,
                        pitch = face1.bbox.pitch,
                        yaw = face1.bbox.yaw,
                        roll = face1.bbox.roll
                    };

                    var face2Result = new
                    {
                        box = new int[] { (int)face2.bbox.x1, (int)face2.bbox.y1, (int)face2.bbox.x2, (int)face2.bbox.y2 },
                        liveness = face2.bbox.liveness,
                        pitch = face2.bbox.pitch,
                        yaw = face2.bbox.yaw,
                        roll = face2.bbox.roll
                    };

                    var result = new
                    {
                        similarity = similarity,
                        face1 = face1Result,
                        face2 = face2Result
                    };

                    string jsonString = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

                    ResultEditor.Text = jsonString;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }

            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                }
            }
        }
    }
}
