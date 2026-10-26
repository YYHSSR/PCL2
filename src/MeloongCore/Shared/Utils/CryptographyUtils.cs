using System.Security;
using System.Security.Cryptography;

namespace MeloongCore;
public static class CryptographyUtils {

    #region Hash

    public enum HashMethod {
        /// <summary>
        /// Uses the <see cref="MD5"/> algorithm. The hex hash is 32 characters long.
        /// </summary>
        Md5,
        /// <summary>
        /// Uses the <see cref="SHA1"/> algorithm. The hex hash is 40 characters long.
        /// </summary>
        Sha1,
        /// <summary>
        /// Uses the <see cref="SHA256"/> algorithm. The hex hash is 64 characters long.
        /// </summary>
        Sha256,
        /// <summary>
        /// Uses the <see cref="SHA512"/> algorithm. The hex hash is 128 characters long.
        /// </summary>
        Sha512
    }
    private static HashAlgorithm GetHashAlgorithm(HashMethod method) => method switch {
        HashMethod.Md5 => MD5.Create(),
        HashMethod.Sha1 => SHA1.Create(),
        HashMethod.Sha256 => SHA256.Create(),
        HashMethod.Sha512 => SHA512.Create(),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

    /// <summary>
    /// Computes the hash of a file. Returns a lower-case hex string.
    /// </summary>
    public static string ComputeFileHash(string filePath, HashMethod method = HashMethod.Md5) {
        using HashAlgorithm hashImpl = GetHashAlgorithm(method);
        Logger.Trace($"计算文件 {method}：{filePath}");
        using var file = FileUtils.ReadAsStream(filePath);
        return BitConverter.ToString(hashImpl.ComputeHash(file)).Replace("-", "").Lower();
    }

    /// <summary>
    /// Computes the hash of a byte array. Returns a lower-case hex string.
    /// </summary>
    public static string ComputeHash(byte[] input, HashMethod method = HashMethod.Md5) {
        using HashAlgorithm hashImpl = GetHashAlgorithm(method);
        return BitConverter.ToString(hashImpl.ComputeHash(input)).Replace("-", "").Lower();
    }
    /// <summary>
    /// Computes the hash of a string. Returns a lower-case hex string.
    /// <para/> The string is converted to bytes using UTF-8.
    /// </summary>
    public static string ComputeHash(string input, HashMethod method = HashMethod.Md5) 
        => ComputeHash(Encoding.UTF8.GetBytes(input), method);

    #endregion

    #region AES symmetric encryption

    private const byte AesDataVersion = 1;

    /// <summary>
    /// Encrypts a string with AES-256-CBC.
    /// </summary>
    /// <returns>The encrypted Base64 string. An empty or null input is returned as is.</returns>
    public static string? AesEncrypt(string? sourceString, string key = "EncryptKey") {
        if (string.IsNullOrEmpty(sourceString)) return sourceString; // Return empty string or null as is
        byte[] iv = new byte[AesIvSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(iv);
        byte[] cipherBytes;
        using var sha256 = SHA256.Create();
        byte[] aesKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        byte[] hmacKey = sha256.ComputeHash(Encoding.UTF8.GetBytes("AES-HMAC|" + key));
        using Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using ICryptoTransform encryptor = aes.CreateEncryptor(aesKey, iv);
        byte[] sourceBytes = Encoding.UTF8.GetBytes(sourceString);
        cipherBytes = encryptor.TransformFinalBlock(sourceBytes, 0, sourceBytes.Length);
        // Format: version [1 byte] | IV [16 bytes] | ciphertext [N bytes] | checksum [4 bytes]
        byte[] encryptedBytes = new byte[1 + iv.Length + cipherBytes.Length + AesCheckSize];
        encryptedBytes[0] = AesDataVersion;
        Array.Copy(iv, 0, encryptedBytes, 1, iv.Length);
        Array.Copy(cipherBytes, 0, encryptedBytes, 1 + iv.Length, cipherBytes.Length);
        using var hmac = new HMACSHA256(hmacKey);
        byte[] checkBytes = hmac.ComputeHash(encryptedBytes, 0, 1 + iv.Length + cipherBytes.Length);
        Array.Copy(checkBytes, 0, encryptedBytes, 1 + iv.Length + cipherBytes.Length, AesCheckSize);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypts a string with AES-256-CBC.
    /// Guaranteed to throw <see cref="CryptographicException"/> if the input data is invalid.
    /// </summary>
    /// <returns>The decrypted string. An empty or null input is returned as is.</returns>
    public static string? AesDecrypt(string? encryptedString, string key = "EncryptKey") {
        if (string.IsNullOrEmpty(encryptedString)) return encryptedString; // Return empty string or null as is
        byte[] encryptedBytes;
        try {
            encryptedBytes = Convert.FromBase64String(encryptedString);
        } catch (FormatException ex) {
            throw new CryptographicException($"数据不是一个有效的 Base64 字符串（{encryptedString}）", ex);
        }
        if (encryptedBytes.Length == 0) throw new CryptographicException($"对称加密数据长度有误（{encryptedString}）");
        int cipherSize = encryptedBytes.Length - 1 - AesIvSize - AesCheckSize;
        if (encryptedBytes[0] != AesDataVersion || cipherSize < 16 || cipherSize % 16 != 0)
            throw new CryptographicException($"对称加密数据长度有误（{encryptedString}）");
        using var sha256 = SHA256.Create();
        byte[] hmacKey = sha256.ComputeHash(Encoding.UTF8.GetBytes("AES-HMAC|" + key));
        using var hmac = new HMACSHA256(hmacKey);
        byte[] checkBytes = hmac.ComputeHash(encryptedBytes, 0, 1 + AesIvSize + cipherSize);
        int checkOffset = 1 + AesIvSize + cipherSize;
        int checkDiff = 0;
        for (int i = 0; i < AesCheckSize; i++)
            checkDiff |= encryptedBytes[checkOffset + i] ^ checkBytes[i];
        if (checkDiff != 0) throw new CryptographicException($"对称加密数据校验失败（{encryptedString}）");
        byte[] aesKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        byte[] iv = new byte[AesIvSize];
        Array.Copy(encryptedBytes, 1, iv, 0, iv.Length);
        using Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using ICryptoTransform decryptor = aes.CreateDecryptor(aesKey, iv);
        byte[] output = decryptor.TransformFinalBlock(encryptedBytes, 1 + iv.Length, cipherSize);
        return Encoding.UTF8.GetString(output);
    }

    private const int AesIvSize = 16;
    private const int AesCheckSize = 4;

    #endregion

    #region ECDSA asymmetric signatures

    /// <summary>
    /// Verifies with ECDSA that the signature of a string was generated with the private key.
    /// Throws <see cref="CryptographicException"/> if verification fails.
    /// </summary>
    public static void EcdsaVerify(string sourceString, string sign, string publicKey) {
        using var sha256 = GetHashAlgorithm(HashMethod.Sha256);
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceString));
        IntPtr algorithmHandle = IntPtr.Zero;
        IntPtr keyHandle = IntPtr.Zero;
        // Call the DLL directly to avoid problems with the system services the .NET API depends on
        try {
            int status = BCryptOpenAlgorithmProvider(out algorithmHandle, "ECDSA_P256", null, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptOpenAlgorithmProvider)} 失败，错误码 {status}");
            status = BCryptImportKeyPair(algorithmHandle, IntPtr.Zero, "ECCPUBLICBLOB", out keyHandle, Convert.FromBase64String(publicKey), 72, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptImportKeyPair)} 失败，错误码 {status}");
            status = BCryptVerifySignature(keyHandle, IntPtr.Zero, hash, hash.Length, Convert.FromBase64String(sign), 64, 0);
            if (status == unchecked((int) 0xC000A000)) throw new SecurityException("签名验证失败");
            if (status < 0) throw new CryptographicException($"{nameof(BCryptVerifySignature)} 失败，错误码 {status}");
        } finally {
            if (keyHandle != IntPtr.Zero) BCryptDestroyKey(keyHandle);
            if (algorithmHandle != IntPtr.Zero) BCryptCloseAlgorithmProvider(algorithmHandle, 0);
        }
    }

    /// <summary>
    /// Generates an ECDSA signature for a string known to both parties.
    /// </summary>
    public static string EcdsaSign(string sourceString, string privateKey) {
        using var sha256 = GetHashAlgorithm(HashMethod.Sha256);
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceString));
        byte[] privateKeyBytes = Convert.FromBase64String(privateKey);
        IntPtr algorithmHandle = IntPtr.Zero;
        IntPtr keyHandle = IntPtr.Zero;
        // Call the DLL directly to avoid problems with the system services the .NET API depends on
        try {
            int status = BCryptOpenAlgorithmProvider(out algorithmHandle, "ECDSA_P256", null, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptOpenAlgorithmProvider)} 失败，错误码 {status}");
            status = BCryptImportKeyPair(algorithmHandle, IntPtr.Zero, "ECCPRIVATEBLOB", out keyHandle, privateKeyBytes, privateKeyBytes.Length, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptImportKeyPair)} 失败，错误码 {status}");
            status = BCryptSignHash(keyHandle, IntPtr.Zero, hash, hash.Length, null, 0, out int signSize, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptSignHash)} 失败，错误码 {status}");
            byte[] sign = new byte[signSize];
            status = BCryptSignHash(keyHandle, IntPtr.Zero, hash, hash.Length, sign, sign.Length, out int actualSignSize, 0);
            if (status < 0) throw new CryptographicException($"{nameof(BCryptSignHash)} 失败，错误码 {status}");
            if (actualSignSize != sign.Length) Array.Resize(ref sign, actualSignSize);
            return Convert.ToBase64String(sign);
        } finally {
            if (keyHandle != IntPtr.Zero) BCryptDestroyKey(keyHandle);
            if (algorithmHandle != IntPtr.Zero) BCryptCloseAlgorithmProvider(algorithmHandle, 0);
        }
    }

    [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)] private static extern int BCryptOpenAlgorithmProvider(out IntPtr algorithmHandle, string algorithmId, string? implementation, int flags);
    [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)] private static extern int BCryptImportKeyPair(IntPtr algorithmHandle, IntPtr importKey, string blobType, out IntPtr keyHandle, byte[] input, int inputSize, int flags);
    [DllImport("bcrypt.dll")] private static extern int BCryptVerifySignature(IntPtr keyHandle, IntPtr paddingInfo, byte[] hash, int hashSize, byte[] signature, int signatureSize, int flags);
    [DllImport("bcrypt.dll")] private static extern int BCryptDestroyKey(IntPtr keyHandle);
    [DllImport("bcrypt.dll")] private static extern int BCryptCloseAlgorithmProvider(IntPtr algorithmHandle, int flags);
    [DllImport("bcrypt.dll")] private static extern int BCryptSignHash(IntPtr keyHandle, IntPtr paddingInfo, byte[] hash, int hashSize, byte[]? signature, int signatureSize, out int resultSize, int flags);

    #endregion

}
