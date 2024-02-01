using System;
using System.IO;
using QRCoder;
using System.Diagnostics;

namespace ShtrihM.SbpPoint.Examples.Gateway;

public abstract class BaseExamples
{
    private readonly string m_tempPath = Path.GetTempPath();

    protected void DeleteQrImage()
    {
        var fileNmae = Path.Combine(m_tempPath, "QR.png");
        if (File.Exists(fileNmae))
        {
            File.Delete(fileNmae);
        }
    }

    protected void ShowQrImage(string data)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeAsPngByteArr = qrCode.GetGraphic(20);
        var fileNmae = Path.Combine(m_tempPath, "QR.png");
        if (File.Exists(fileNmae))
        {
            File.Delete(fileNmae);
        }

        File.WriteAllBytes(fileNmae, qrCodeAsPngByteArr);
        Console.WriteLine(fileNmae);
        Process.Start("explorer.exe", fileNmae);
    }
}