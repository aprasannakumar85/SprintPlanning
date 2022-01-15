using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SprintPlanning
{
    public static class HelperUtil
    {
        public static string DecryptHeaderDataPk(string headerData)
        {
            string output = string.Empty;
            for (int i = 0; i < headerData.Length; i += 2)
            {
                var twoNumbers = headerData.Substring(i, 2);
                var oneChar = Convert.ToChar(Convert.ToInt32(twoNumbers));
                output = $"{output}{oneChar}";
            }
            return output;
        }

        public static string EncryptHeaderDataPk(string headerData)
        {
            string finalValue = string.Empty;
            int value;
            foreach (char c in headerData)
            {
                value = Convert.ToInt32(c);
                finalValue += value.ToString();
            }

            return finalValue;
        }

        public static string DecryptStringAES(string cipherText, string keyString)
        {
            if (string.IsNullOrWhiteSpace(keyString))
            {
                throw new ArgumentException("key string missing to decrypt!");
            }

            cipherText = cipherText.Trim().Replace("vnaa", "/");

            byte[] keybytes = Encoding.UTF8.GetBytes(keyString);
            byte[] iv = Encoding.UTF8.GetBytes(keyString);

            var encrypted = Convert.FromBase64String(Uri.UnescapeDataString(cipherText));

            var decryptedFromJavascript = DecryptStringFromBytes(encrypted, keybytes, iv);
            return decryptedFromJavascript.Trim('\"');
        }

        private static string DecryptStringFromBytes(byte[] cipherText, byte[] key, byte[] iv)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
            {
                throw new ArgumentNullException(nameof(cipherText));
            }
            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (iv == null || iv.Length <= 0)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // Declare the string used to hold
            // the decrypted text.
            string plaintext;

            // Create an RijndaelManaged object
            // with the specified key and IV.
            using var rijAlg = new RijndaelManaged
            {
                //Settings
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                FeedbackSize = 128,
                Key = key,
                IV = iv
            };


            // Create a decryptor to perform the stream transform.
            var decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

            try
            {
                // Create the streams used for decryption.
                using var msDecrypt = new MemoryStream(cipherText);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);
                // Read the decrypted bytes from the decrypting stream
                // and place them in a string.
                plaintext = srDecrypt.ReadToEnd();
            }
            catch
            {
                plaintext = "keyError";
            }

            return plaintext;
        }

        public static string EncryptStringAES(string plainText, string keyString)
        {
            if (string.IsNullOrWhiteSpace(keyString))
            {
                throw new ArgumentException("key string missing to encrypt!");
            }

            var keybytes = Encoding.UTF8.GetBytes(keyString);
            var iv = Encoding.UTF8.GetBytes(keyString);

            var encryoFromJavascript = EncryptStringToBytes(plainText, keybytes, iv);
            return Convert.ToBase64String(encryoFromJavascript);
        }

        private static byte[] EncryptStringToBytes(string plainText, byte[] key, byte[] iv)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
            {
                throw new ArgumentNullException(nameof(plainText));
            }
            if (key == null || key.Length <= 0)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (iv == null || iv.Length <= 0)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // Create a RijndaelManaged object
            // with the specified key and IV.
            using var rijAlg = new RijndaelManaged
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                FeedbackSize = 128,
                Key = key,
                IV = iv
            };


            // Create a decryptor to perform the stream transform.
            var encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);

            // Create the streams used for encryption.
            using var msEncrypt = new MemoryStream();
            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                //Write all data to the stream.
                swEncrypt.Write(plainText);
            }
            var encrypted = msEncrypt.ToArray();

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }
    }
}
