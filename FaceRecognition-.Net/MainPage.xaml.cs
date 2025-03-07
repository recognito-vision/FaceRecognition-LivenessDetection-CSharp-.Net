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
using System.Data.SQLite;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Microsoft.Maui.ApplicationModel;
using System.Data.Entity.Core.Metadata.Edm;

enum IMAGE_INDEX
{
    MATCH_1 = 0,
    MATCH_2 = 1,
    IDENTIFY = 2
}

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
        private int activateResult = (int)SDK_STATUS.SDK_LICENSE_KEY_ERROR;
        private string[] imagePath = new string[3]; // 0: Image1 for 1to1, 1: Image2 for 1to1, 2: Image3 for 1toN
        private FaceEngineClass faceSDK = new FaceEngineClass();
        private DBManager dbManager = new DBManager();
        public ObservableCollection<User> Users { get; set; }

        private int matchThreshold = 80;
        public MainPage()
        {
            InitializeComponent();

            HWIDEntry.Text = faceSDK.GetHardwareId();
            
            string licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.txt");

            if (File.Exists(licensePath))
            {
                string licenseText = File.ReadAllText(licensePath);
                activateResult = faceSDK.Activate(licenseText);
            }
            else
                ActivationStatusEntry.Text = "Can't find license file!";

            if (activateResult == (int)SDK_STATUS.SDK_SUCCESS)
            {
                var dictPath = $"{AppDomain.CurrentDomain.BaseDirectory}assets";
                int ret = faceSDK.Init(dictPath);
                if (ret != (int)SDK_STATUS.SDK_SUCCESS)
                    ActivationStatusEntry.Text = "Failed to init SDK";
                else
                    ActivationStatusEntry.Text = "Activation Success!";
            }
            else
                ActivationStatusEntry.Text = "Activation failed! Status code: " + activateResult.ToString();


            dbManager.Create();
            Users = dbManager.Load();

            BindingContext = this;
        }

        public async Task<FileResult> OpenImage(PickOptions options, int index)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    if (result.FileName.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) ||
                        result.FileName.EndsWith("png", StringComparison.OrdinalIgnoreCase))
                    {
                        switch (index)
                        {
                            case (int)IMAGE_INDEX.MATCH_1:
                                Image1.Source = ImageSource.FromFile(result.FullPath);
                                break;
                            case (int)IMAGE_INDEX.MATCH_2:
                                Image2.Source = ImageSource.FromFile(result.FullPath);
                                break;
                            case (int)IMAGE_INDEX.IDENTIFY:
                                Image3.Source = ImageSource.FromFile(result.FullPath);
                                break;
                            default:
                                return null;
                        }

                        imagePath[index] = result.FullPath;
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

        private int DetectFace(string imagePath, DetectionResult[] detectionResult)
        {
            System.Drawing.Image image = null;
            try
            {
                image = ImageProcess.LoadImageWithExif(imagePath);
                if (image == null)
                    return 0;
            }
            catch (Exception)
            {
                DisplayAlert("Error", "Unknown Format!", "Ok");
            }

            Bitmap imgBmp = ImageProcess.ConvertTo24bpp(image);
            BitmapData bitmapData = imgBmp.LockBits(new Rectangle(0, 0, imgBmp.Width, imgBmp.Height), ImageLockMode.ReadWrite, imgBmp.PixelFormat);

            int bytesPerPixel = Bitmap.GetPixelFormatSize(imgBmp.PixelFormat) / 8;
            int byteCount = bitmapData.Stride * imgBmp.Height;
            byte[] pixels = new byte[byteCount];
            IntPtr ptrFirstPixel = bitmapData.Scan0;
            Marshal.Copy(ptrFirstPixel, pixels, 0, pixels.Length);

            imgBmp.UnlockBits(bitmapData);

            FaceBox[] faceBoxes = new FaceBox[detectionResult.Length];
            int faceCount = faceSDK.DetectFace(pixels, imgBmp.Width, imgBmp.Height, bitmapData.Stride, faceBoxes, detectionResult.Length, false);

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
                    Bitmap croppedBitmap = imgBmp.Clone(cropArea, imgBmp.PixelFormat);
                    detectionResult[i].bitmap = ImageProcess.ResizeImage(croppedBitmap, 200, 200);
                }
                return faceCount;
            }

            return 0;
        }

        private async void OnOpenImage(object sender, EventArgs e)
        {
            int index = -1;
            if (sender is Button button && button.CommandParameter is string param)
                index = int.Parse(param);

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
                await OpenImage(options, index);
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException("It was cancelled");
            }
        }


        private async void OnCompareFace(object sender, EventArgs e)
        {
            CompareBtn.IsEnabled = false;

            try
            {
                CompareResult.Source = ImageSource.FromFile("blank.png");
                Face1.Source = ImageSource.FromFile("face.jpg");
                Face2.Source = ImageSource.FromFile("face.jpg");
                ResultEditor.Text = "";
                await Task.Delay(100);

                if (activateResult != 0)
                {
                    await DisplayAlert("Error", "SDK Activation Failed!", "Ok");
                    return;
                }

                string path1 = imagePath[(int)IMAGE_INDEX.MATCH_1];
                string path2 = imagePath[(int)IMAGE_INDEX.MATCH_2];
                if (path1 == null || path2 == null)
                {
                    await DisplayAlert("Error", "Select Image Files!", "Ok");
                    return;
                }

                DetectionResult[] detectionResult1 = new DetectionResult[1];
                int cntFace1 = DetectFace(path1, detectionResult1);

                DetectionResult[] detectionResult2 = new DetectionResult[1];
                int cntFace2 = DetectFace(path2, detectionResult2);

                if (cntFace1 == 0 && cntFace2 == 0)
                    await DisplayAlert("Error", "Can't detect faces", "Ok");
                else if (cntFace1 == 0)
                    await DisplayAlert("Error", "Can't detect face in Image1", "Ok");
                else if (cntFace2 == 0)
                    await DisplayAlert("Error", "Can't detect face in Image2", "Ok");
                else
                {
                    var face1 = detectionResult1[0];
                    var face2 = detectionResult2[0];
                    float similarity = faceSDK.CalculateSimilarity(face1.feature, face2.feature, 128);

                    Face1.Source = ImageProcess.ConvertBitmapToImageSource(face1.bitmap);
                    Face2.Source = ImageProcess.ConvertBitmapToImageSource(face2.bitmap);
                    if (similarity >= matchThreshold)
                        CompareResult.Source = ImageSource.FromFile("same.png");
                    else
                        CompareResult.Source = ImageSource.FromFile("different.png");

                    var face1Result = new
                    {
                        box = string.Join(",", new int[] { (int)face1.bbox.x1, (int)face1.bbox.y1, (int)face1.bbox.x2, (int)face1.bbox.y2 }),
                        liveness = face1.bbox.liveness,
                        pitch = face1.bbox.pitch,
                        yaw = face1.bbox.yaw,
                        roll = face1.bbox.roll
                    };

                    var face2Result = new
                    {
                        box = string.Join(",", new int[] { (int)face2.bbox.x1, (int)face2.bbox.y1, (int)face2.bbox.x2, (int)face2.bbox.y2 }),
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
                    await Task.Delay(100);
                    ResultEditor.CursorPosition = 0;
                    ResultEditor.SelectionLength = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "Ok");
            }

            finally
            {
                CompareBtn.IsEnabled = true;
            }
        }

        public static byte[] FloatArrayToByteArray(float[] floatArray)
        {
            byte[] byteArray = new byte[floatArray.Length * sizeof(float)];
            Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
            return byteArray;
        }

        private async void OnEnroll(object sender, EventArgs e)
        {
            EnrollBtn.IsEnabled = false;
            IdentifyBtn.IsEnabled = false;
            await Task.Delay(100);
            
            try
            {
                if (activateResult != 0)
                {
                    await DisplayAlert("Error", "SDK Activation Failed!", "Ok");
                    return;
                }

                string path = imagePath[(int)IMAGE_INDEX.IDENTIFY];
                if (path == null)
                {
                    await DisplayAlert("Error", "Select Image File!", "Ok");
                    return;
                }

                DetectionResult[] detectionResult = new DetectionResult[10];
                int cntFace = DetectFace(path, detectionResult);

                if (cntFace == 0)
                    await DisplayAlert("Error", "Can't detect faces", "Ok");
                else
                {
                    for (int i = 0; i < cntFace; i++)
                    {
                        int randomNumber = RandomNumberGenerator.GetInt32(10000000, 99999999);
                        User user = new User
                        {
                            name = "User " + randomNumber.ToString(),
                            face = detectionResult[i].bitmap,  // Convert the byte array back to an image (implement ConvertToImage)
                            image = ImageProcess.ConvertBitmapToImageSource(detectionResult[i].bitmap),
                            templates = FloatArrayToByteArray(detectionResult[i].feature) // Assuming the template is stored as a byte array
                        };

                        Users.Add(user);
                        dbManager.Insert(user);
                    }
                    await DisplayAlert("Info", $"Enrolled {cntFace} Users!", "Ok");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "Ok");
            }

            finally
            {
                EnrollBtn.IsEnabled = true;
                IdentifyBtn.IsEnabled = true;
            }
        }

        public static float[] ByteArrayToFloatArray(byte[] byteArray)
        {
            if (byteArray == null || byteArray.Length % 4 != 0)
                throw new ArgumentException("Invalid byte array length for float conversion");

            int floatCount = byteArray.Length / 4;
            float[] floatArray = new float[floatCount];

            Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);
            return floatArray;

        }
        private async void OnIdentify(object sender, EventArgs e)
        {
            EnrollBtn.IsEnabled = false;
            IdentifyBtn.IsEnabled = false;


            try
            {
                DetectedFace.Source = ImageSource.FromFile("face.jpg");
                IdentifiedFace.Source = ImageSource.FromFile("unknown.jpg");
                IdentificationResultEditor.Text = "";
                await Task.Delay(100);

                if (activateResult != 0)
                {
                    await DisplayAlert("Error", "SDK Activation Failed!", "Ok");
                    return;
                }

                string path = imagePath[(int)IMAGE_INDEX.IDENTIFY];
                if (path == null)
                {
                    await DisplayAlert("Error", "Select Image File!", "Ok");
                    return;
                }

                DetectionResult[] detectionResult = new DetectionResult[1];
                int cntFace = DetectFace(path, detectionResult);

                if (cntFace == 0)
                    await DisplayAlert("Error", "Can't detect face", "Ok");
                else
                {
                    var face = detectionResult[0];
                    float maxSimiarlity = 0;
                    User maxSimiarlityUser = null;
                    DetectedFace.Source = ImageProcess.ConvertBitmapToImageSource(face.bitmap);
                    foreach (User user in Users)
                    {
                        float similarity = faceSDK.CalculateSimilarity(face.feature, ByteArrayToFloatArray(user.templates), 128);

                        if (similarity > maxSimiarlity)
                        {
                            maxSimiarlity = similarity;
                            maxSimiarlityUser = user;
                        }
                    }

                    if (maxSimiarlity >= matchThreshold)
                    {
                        IdentifiedFace.Source = maxSimiarlityUser.image;
                        IdentificationResultEditor.Text = $"User Name: {maxSimiarlityUser.name}\nScore: {maxSimiarlity}";
                    }
                    else
                        IdentificationResultEditor.Text = "Can't find matched user.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "Ok");
            }

            finally
            {
                EnrollBtn.IsEnabled = true;
                IdentifyBtn.IsEnabled = true;
            }
        }

        private async void OnDeleteUser(object sender, EventArgs e)
        {
            var imageButton = (ImageButton)sender;
            imageButton.IsEnabled = false;
            await Task.Delay(100);
            var user = imageButton.CommandParameter as User;
            if (user != null)
            {
                int index = Users.IndexOf(user);
                if (index < 0) 
                {
                    imageButton.IsEnabled = true;
                    return;
                }
                    
                Users.RemoveAt(index);
                dbManager.Delete(user.name);
            }
            imageButton.IsEnabled = true;
        }

        private async void OnDeleteAll(object sender, EventArgs e)
        {
            ResetBtn.IsEnabled = false;
            await Task.Delay(100);
            dbManager.DeleteAll();
            Users.Clear();
            await DisplayAlert("Info", "All Users are removed!", "Ok");
            ResetBtn.IsEnabled = true;
        }
    }
}
