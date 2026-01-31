using System;
using System.Text;

namespace InventorySystem.Infrastructure.Services
{
    public static class BarcodeService
    {
        private static readonly Random _random = new Random();

        // 1. GENERATE UNIQUE BARCODE (8 Digits)
        public static string GenerateNewBarcode()
        {
            // Generates a random 8-digit number (Simple EAN-8 style)
            return _random.Next(10000000, 99999999).ToString();
        }

        // 2. GENERATE PRICE CIPHER (The Secret Code)
        // Logic: 0=X, 1=A, 2=B, 3=C, 4=D, 5=E, 6=F, 7=G, 8=H, 9=I
        public static string GeneratePriceCipher(decimal price)
        {
            if (price == 0) return "-";

            // The Secret Key Mapping
            char[] key = { 'X', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I' };

            // Convert price to integer (ignore cents for the cipher)
            string priceStr = ((int)price).ToString();
            StringBuilder cipher = new StringBuilder();

            foreach (char c in priceStr)
            {
                if (char.IsDigit(c))
                {
                    int digit = int.Parse(c.ToString());
                    cipher.Append(key[digit]);
                }
            }
            return cipher.ToString();
        }

        // 3. GENERATE DISCOUNT CIPHER
        // Example: 10% -> "D10"
        public static string GenerateDiscountCipher(double discount)
        {
            if (discount <= 0) return "N"; // Net (No discount)
            return $"D{discount}";
        }
    }
}