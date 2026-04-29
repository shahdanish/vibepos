namespace POSApp.Core.Services
{
    public interface IBarcodeService
    {
        byte[] GenerateBarcode(string text, int width = 300, int height = 100);
        byte[] GenerateQRCode(string text, int width = 200, int height = 200);
    }
}
