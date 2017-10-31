﻿using System.IO;
using Microsoft.Owin.Security.DataProtection;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Security;

namespace OwinAuthServer
{
    /// <summary>
    /// IDataProtector implementation that uses AES:
    /// </summary>
    internal class AesDataProtectorProvider : IDataProtector
    {
        // Fields
        private byte[] key;

        // Constructor
        public AesDataProtectorProvider(string key)
        {
            using (var sha1 = new SHA256Managed())
            {
                this.key = sha1.ComputeHash(Encoding.UTF8.GetBytes(key));
            }
        }

        // IDataProtector Methods
        public byte[] Protect(byte[] data)
        {
            byte[] dataHash;
            using (var sha = new SHA256Managed())
            {
                dataHash = sha.ComputeHash(data);
            }

            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Key = this.key;
                aesAlg.GenerateIV();

                using (var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                using (var msEncrypt = new MemoryStream())
                {
                    msEncrypt.Write(aesAlg.IV, 0, 16);

                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (var bwEncrypt = new BinaryWriter(csEncrypt))
                    {
                        bwEncrypt.Write(dataHash);
                        bwEncrypt.Write(data.Length);
                        bwEncrypt.Write(data);
                    }
                    var protectedData = msEncrypt.ToArray();
                    return protectedData;
                }
            }
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Key = this.key;

                using (var msDecrypt = new MemoryStream(protectedData))
                {
                    byte[] iv = new byte[16];
                    msDecrypt.Read(iv, 0, 16);

                    aesAlg.IV = iv;

                    using (var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var brDecrypt = new BinaryReader(csDecrypt))
                    {
                        var signature = brDecrypt.ReadBytes(32);
                        var len = brDecrypt.ReadInt32();
                        var data = brDecrypt.ReadBytes(len);

                        byte[] dataHash;
                        using (var sha = new SHA256Managed())
                        {
                            dataHash = sha.ComputeHash(data);
                        }

                        if (!dataHash.SequenceEqual(signature))
                            throw new SecurityException("Signature does not match the computed hash");

                        return data;
                    }
                }
            }
        }
    }
}
