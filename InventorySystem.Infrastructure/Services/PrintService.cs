using System;
using System.Drawing;
using System.Drawing.Printing;

namespace InventorySystem.Infrastructure.Services
{
    public class PrintService
    {
        // Now accepts printerName and copies as arguments
        public void PrintReceipt(string receiptId, string content, string printerName, int copies)
        {
            // 1. Validation
            if (string.IsNullOrWhiteSpace(printerName))
            {
                throw new Exception("No printer configured. Go to Settings page.");
            }

            // 2. Platform Check (Windows Only)
            if (OperatingSystem.IsWindows() && !IsPrinterAvailable(printerName))
            {
                throw new Exception($"Printer '{printerName}' is not found or offline.");
            }

            // 3. Execute Print Job
            for (int i = 0; i < copies; i++)
            {
                // Ensure System.Drawing.Common NuGet is installed
                PrintDocument pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = printerName;

                pd.PrintPage += (sender, e) =>
                {
                    // Basic Thermal Print Layout
                    Font font = new Font("Courier New", 10);
                    float yPos = 10;
                    int count = 0;
                    float leftMargin = 0;

                    if (e.Graphics != null)
                    {
                        foreach (string line in content.Split('\n'))
                        {
                            yPos = 10 + (count * font.GetHeight(e.Graphics));
                            e.Graphics.DrawString(line, font, Brushes.Black, leftMargin, yPos);
                            count++;
                        }
                    }
                };

                pd.Print();
            }
        }

        private bool IsPrinterAvailable(string printerName)
        {
            if (!OperatingSystem.IsWindows()) return false;

            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                if (printer.Equals(printerName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}