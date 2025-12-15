using System;
using System.Runtime.InteropServices;

public class GammaManager
{
    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public UInt16[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public UInt16[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public UInt16[] Blue;
    }

    public static void SetGamma(double gamma, double brightness, double contrast, string screenDeviceName)
    {
        RAMP ramp = new RAMP();
        ramp.Red = new UInt16[256];
        ramp.Green = new UInt16[256];
        ramp.Blue = new UInt16[256];

        for (int i = 0; i < 256; i++)
        {
            double iVal = i / 255.0;
            iVal = (iVal - 0.5) * contrast + 0.5;
            if (iVal < 0) iVal = 0;
            if (iVal > 1) iVal = 1;

            iVal = iVal * brightness;

            if (gamma > 0) iVal = Math.Pow(iVal, 1.0 / gamma);

            double finalVal = iVal * 65535.0;
            if (finalVal > 65535) finalVal = 65535;
            if (finalVal < 0) finalVal = 0;

            ramp.Red[i] = (UInt16)finalVal;
            ramp.Green[i] = (UInt16)finalVal;
            ramp.Blue[i] = (UInt16)finalVal;
        }


        IntPtr hDC = CreateDC(null, screenDeviceName, null, IntPtr.Zero);

        if (hDC != IntPtr.Zero)
        {
            SetDeviceGammaRamp(hDC, ref ramp);
            DeleteDC(hDC);
        }
    }
}