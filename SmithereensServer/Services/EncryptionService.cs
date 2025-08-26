using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SmithereensServer.Services
{
    public class EncryptionService
    {
        private const int RegisterLength = 29; // Длина регистра, как в LSFR.cs
        private readonly string _registerState = "11111001110111010001011011111"; // Начальное состояние регистра

        private BitArray GenerateKeyStream(int bitLength)
        {
            bool[] register = new bool[RegisterLength];
            for (int i = 0; i < RegisterLength; i++)
            {
                register[i] = _registerState[i] == '1';
            }

            BitArray keyStream = new BitArray(bitLength);
            for (int i = 0; i < bitLength; i++)
            {
                bool keyBit = register[0];
                keyStream[i] = keyBit;

                // Новый бит вычисляется как XOR второго бита (индекс 0) и бита с индексом 27 (x^29 + x^2 + 1)
                bool newBit = register[0] ^ register[27];
                for (int j = 0; j < RegisterLength - 1; j++)
                    register[j] = register[j + 1];
                register[RegisterLength - 1] = newBit;
            }
            return keyStream;
        }
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException(nameof(plainText));

            // Преобразуем строку в байты
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            BitArray plainBits = new BitArray(plainBytes);

            // Генерируем ключевой поток длиной, равной количеству бит входных данных
            BitArray keyStream = GenerateKeyStream(plainBits.Length);

            // Выполняем XOR между входными битами и ключевым потоком
            BitArray encryptedBits = plainBits.Xor(keyStream);

            // Преобразуем результат обратно в байты
            byte[] encryptedBytes = new byte[(encryptedBits.Length + 7) / 8];
            encryptedBits.CopyTo(encryptedBytes, 0);

            // Возвращаем зашифрованные данные в формате Base64
            return Convert.ToBase64String(encryptedBytes);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentNullException(nameof(cipherText));

            try
            {
                // Преобразуем Base64-строку в байты
                byte[] encryptedBytes = Convert.FromBase64String(cipherText);
                BitArray encryptedBits = new BitArray(encryptedBytes);

                // Генерируем тот же ключевой поток
                BitArray keyStream = GenerateKeyStream(encryptedBits.Length);

                // Выполняем XOR для дешифрования (идентично шифрованию)
                BitArray decryptedBits = encryptedBits.Xor(keyStream);

                // Преобразуем биты обратно в байты
                byte[] decryptedBytes = new byte[(decryptedBits.Length + 7) / 8];
                decryptedBits.CopyTo(decryptedBytes, 0);

                // Возвращаем строку
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (FormatException ex)
            {
                throw new CryptographicException("Некорректный формат зашифрованных данных.", ex);
            }
        }
    }
}