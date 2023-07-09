using System.Security.Cryptography;
using System.Text;

string guid;
string token;

if (args.Length < 2)
{
    Console.Write("GUID: ");
    guid = Console.ReadLine() ?? "";
    Console.Write("Token: ");
    token = Console.ReadLine() ?? "";
} 
else
{
    guid = args[0];
    token = args[1];
}

if(!Guid.TryParse(guid, out _))
{
    Console.WriteLine("The provided GUID is not valid!");
    return;
}

var catBytes = Encoding.UTF8.GetBytes(guid + token);
var hash = MD5.HashData(catBytes);
var sig = Convert.ToBase64String(hash);

Console.WriteLine("Signature: " + sig);
