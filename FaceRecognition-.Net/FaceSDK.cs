using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FaceSDK
{
    enum SDK_STATUS
    {
        SDK_SUCCESS = 0,
        SDK_LICENSE_KEY_ERROR = -1,
        SDK_LICENSE_APPID_ERROR = -2,
        SDK_LICENSE_EXPIRED = -3,
        SDK_NO_ACTIVATED = -4,
        SDK_INIT_ERROR = -5,
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct FaceBox
    {
        public float x1, y1, x2, y2;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 68 * 2)]
        public float[] landmark_68; // Array of 136 floats
        public float liveness;
        public float yaw, roll, pitch;
        public float face_occlusion;
        public float left_eye, right_eye;
        public float face_quality;

        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2048)]

        public FaceBox()
        {
            x1 = x2 = y1 = y2 = 0;
            landmark_68 = new float[68 * 2];
            liveness = 0;
            yaw = roll = pitch = 0;
            face_occlusion = 0;
            left_eye = right_eye = 0;
            face_quality = 0;
        }
    };

    public class FaceEngineClass
    {
        public FaceEngineClass()
        {

        }

        [DllImport("ttvfaceengine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ttv_get_hwid();

        public String GetHardwareId()
        {
            try
            {
                IntPtr machineCode = ttv_get_hwid();
                if (machineCode == null)
                    throw new Exception("Failed to retrieve machine code.");

                string strMachineCode = Marshal.PtrToStringAnsi(machineCode);
                return strMachineCode;
            }
            catch (Exception e)
            {
                return e.ToString();
            }
        }

        [DllImport("ttvfaceengine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ttv_set_activation(IntPtr license);

        public int Activate(String license)
        {
            IntPtr ptr = Marshal.StringToHGlobalAnsi(license);
            try
            {
                return ttv_set_activation(ptr);
            }
            finally
            {
                // Free the unmanaged memory when done
                Marshal.FreeHGlobal(ptr);
            }
        }

        [DllImport("ttvfaceengine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ttv_init(IntPtr modelPath);

        public int Init(string modelPath)
        {
            return ttv_init(Marshal.StringToHGlobalAnsi(modelPath));
        }

        [DllImport("ttvfaceengine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ttv_detect_face_c_sharp(
            IntPtr rgbData, // Pointer to the RGB data
            int width,      // Width of the image
            int height,     // Height of the image
            int stride,     // Stride of the image
            [In, Out] FaceBox[] faceBoxes, // Array of FaceBox
            int maxCount, 
            bool check_liveness,
            bool check_eye_closeness, 
            bool check_face_occlusion
        );

        public int DetectFace(byte[] rgbData, int width, int height, int stride, [In, Out] FaceBox[] faceBoxes, int faceBoxCount, bool check_liveness, bool check_eye_closeness, bool check_face_occlusion)
        {
            IntPtr imgPtr = Marshal.AllocHGlobal(rgbData.Length);
            Marshal.Copy(rgbData, 0, imgPtr, rgbData.Length);

            try
            {
                int ret = ttv_detect_face_c_sharp(imgPtr, width, height, stride, faceBoxes, faceBoxCount, check_liveness, check_eye_closeness, check_face_occlusion);
                return ret;
            }
            finally
            {
                Marshal.FreeHGlobal(imgPtr);
            }
        }

        [DllImport("ttvfaceengine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ttv_feature_extract_c_sharp(
            IntPtr rgbData, // Pointer to the RGB data
            int width,      // Width of the image
            int height,     // Height of the image
            int stride,     // Stride of the image
            float[] landmark, // Array of Landmark
            float[] feature
        );

        public int ExtractTemplate(byte[] rgbData, int width, int height, int stride, float[] landmark, float[] template)
        {
            IntPtr imgPtr = Marshal.AllocHGlobal(rgbData.Length);
            Marshal.Copy(rgbData, 0, imgPtr, rgbData.Length);

            try
            {
                int ret = ttv_feature_extract_c_sharp(imgPtr, width, height, stride, landmark, template);
                return ret;
            }
            finally
            {
                Marshal.FreeHGlobal(imgPtr);
            }
        }

        [DllImport("ttvfaceengine.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern float ttv_compare_feature_c_sharp(
            float[] feature1,
            float[] feature2,
            int featureLength
        );
        
        public float CalculateSimilarity(float[] feature1, float[] feature2, int featureLength)
        {
            return ttv_compare_feature_c_sharp(feature1, feature2, featureLength);
        }
    }
}
