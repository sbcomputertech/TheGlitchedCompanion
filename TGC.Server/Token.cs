using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TGC.Server
{
    public class Token
    {
        public int Length { get; private set; }
        public string Value { get; private set; }

        public static Token Generate(int length)
        {
            return new Token
            {
                Length = length,
                Value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(length))
            };
        }
    }
}
