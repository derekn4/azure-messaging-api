// Quick script to test Cosmos DB emulator connectivity
using System.Net.Http;

var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
};
var client = new HttpClient(handler);

try
{
    var response = await client.GetAsync("https://localhost:8081/");
    Console.WriteLine($"Status: {response.StatusCode}");
    var body = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Body: {body.Substring(0, Math.Min(200, body.Length))}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
