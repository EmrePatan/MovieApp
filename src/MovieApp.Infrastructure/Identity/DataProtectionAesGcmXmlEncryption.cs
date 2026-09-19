using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;

namespace MovieApp.Infrastructure.Identity;

internal sealed class MovieAppDataProtectionKeyMaterial(byte[] key)
{
    public byte[] Key { get; } = key;
}

internal sealed class DataProtectionAesGcmXmlEncryptor(byte[] key) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        using var plainStream = new MemoryStream();
        plaintextElement.Save(plainStream, SaveOptions.DisableFormatting);
        var plainBytes = plainStream.ToArray();
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var encryptedElement = new XElement(
            "encryptedKey",
            new XElement("nonce", Convert.ToBase64String(nonce)),
            new XElement("cipher", Convert.ToBase64String(cipher)),
            new XElement("tag", Convert.ToBase64String(tag)));

        return new EncryptedXmlInfo(encryptedElement, typeof(DataProtectionAesGcmXmlDecryptor));
    }
}

internal sealed class DataProtectionAesGcmXmlDecryptor : IXmlDecryptor
{
    private readonly byte[] _key;

    public DataProtectionAesGcmXmlDecryptor(IServiceProvider serviceProvider)
    {
        _key = serviceProvider.GetRequiredService<MovieAppDataProtectionKeyMaterial>().Key;
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        var nonce = Convert.FromBase64String(encryptedElement.Element("nonce")!.Value);
        var cipher = Convert.FromBase64String(encryptedElement.Element("cipher")!.Value);
        var tag = Convert.FromBase64String(encryptedElement.Element("tag")!.Value);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plain);

        return XElement.Load(new MemoryStream(plain));
    }
}
