using System;
using System.Security.Cryptography;

namespace TGC.Client;

public class Token
{
    public int Length { get; set; }
    public string Value { get; set; }

    public static Token Generate(int length)
    {
        var rng = RandomNumberGenerator.Create();
        var buffer = new byte[length];
        rng.GetBytes(buffer);
        return new Token
        {
            Length = length,
            Value = Convert.ToBase64String(buffer)
        };
    }
}