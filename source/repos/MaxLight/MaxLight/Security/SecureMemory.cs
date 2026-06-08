using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace MaxLight.Security
{
    public class SecureMemory : IDisposable
    {
        private SecureString _secureData;
        private byte[] _encryptedData;
        private byte[] _encryptionKey;
        private bool _disposed = false;

        public SecureMemory()
        {
            GenerateEncryptionKey();
        }

        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        private const uint PAGE_NOACCESS = 0x01;

        public void StoreData(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            _secureData = new SecureString();
            foreach (char c in data)
            {
                _secureData.AppendChar(c);
            }
            _secureData.MakeReadOnly();

            EncryptDataInMemory(data);
        }

        public string RetrieveData()
        {
            if (_secureData == null && _encryptedData == null) return null;

            if (_secureData != null)
            {
                IntPtr ptr = Marshal.SecureStringToBSTR(_secureData);
                try
                {
                    return Marshal.PtrToStringBSTR(ptr);
                }
                finally
                {
                    Marshal.ZeroFreeBSTR(ptr);
                }
            }

            return DecryptDataFromMemory();
        }

        public void Clear()
        {
            if (_secureData != null)
            {
                _secureData.Clear();
                _secureData.Dispose();
                _secureData = null;
            }

            if (_encryptedData != null)
            {
                Array.Clear(_encryptedData, 0, _encryptedData.Length);
                _encryptedData = null;
            }

            if (_encryptionKey != null)
            {
                Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
                _encryptionKey = null;
            }
        }

        private void GenerateEncryptionKey()
        {
            using (var aes = Aes.Create())
            {
                aes.GenerateKey();
                _encryptionKey = aes.Key;
            }
        }

        private void EncryptDataInMemory(string data)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                aes.GenerateIV();

                byte[] plaintext = Encoding.UTF8.GetBytes(data);
                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
                    _encryptedData = aes.IV.Concat(ciphertext).ToArray();
                }
            }
        }

        private string DecryptDataFromMemory()
        {
            if (_encryptedData == null || _encryptionKey == null) return null;

            using (var aes = Aes.Create())
            {
                aes.Key = _encryptionKey;
                byte[] iv = _encryptedData.Take(16).ToArray();
                byte[] ciphertext = _encryptedData.Skip(16).ToArray();
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
                    return Encoding.UTF8.GetString(plaintext);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }
    }

    public static class SecureStorage
    {
        private static SecureMemory _instance;

        public static void Store(string key, string data)
        {
            GetInstance().StoreData(data);
        }

        public static string Retrieve(string key)
        {
            return GetInstance().RetrieveData();
        }

        public static void Clear(string key)
        {
            GetInstance().Clear();
            _instance = null;
        }

        private static SecureMemory GetInstance()
        {
            if (_instance == null)
                _instance = new SecureMemory();
            return _instance;
        }
    }
}