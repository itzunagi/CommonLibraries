using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Amazon.S3;
using Common.FileProvider.Abstractions;
using Common.FileProvider.S3;
using Microsoft.Extensions.Configuration;

namespace Example;

public class Program
{
    private static readonly IServiceProvider ServiceProvider;
    private static readonly IConfiguration Configuration;

    static Program()
    {
        Configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly)
            .Build();

        ServiceProvider = new ServiceCollection()
            .AddDefaultAWSOptions(new AWSOptions
            {
                Credentials = new BasicAWSCredentials(
                    accessKey: Configuration["AWS:AccessKey"],
                    secretKey: Configuration["AWS:SecretKey"]),
                Region = RegionEndpoint.USEast1,
                DefaultClientConfig =
                {
                    RegionEndpoint = RegionEndpoint.USEast1
                },
                DefaultConfigurationMode = DefaultConfigurationMode.Standard,
                Profile = Configuration["AWS:Profile"]
            })
            .AddAWSService<IAmazonS3>()
            .AddS3FileProviders(new Dictionary<string, S3BucketOptions>
            {
                {
                    Configuration["AWS:BucketName"],
                    new S3BucketOptions
                    {
                        BucketName = Configuration["AWS:BucketName"]
                    }
                }
            })
            .BuildServiceProvider();
    }

    public static async Task Main(string[] args)
    {
        var packetBytes = new byte[] { 0x1, 0xFF, 0x2, 0xFF, 0x3, 0xFF, 0x4 };
        var s3FileProviderResult = ServiceProvider.GetRequiredService<INamedFileProviderFactory>()
            .CreateNamedFileProvider(Configuration["AWS:BucketName"]);
            var s3FileProvider = s3FileProviderResult.Value;
        await using Stream packetByteStream = new MemoryStream(packetBytes);
        var saveResult = await s3FileProvider.SaveFileAsync("requestmission", "testfile.bin", packetByteStream);
        if (!saveResult.IsSuccessful())
        {
            Console.WriteLine($"Failed to save file: {saveResult.Errors.First()}");
        }

        var getResult = await s3FileProvider.GetFileAsync("requestmission", "testfile.bin");
        if (!getResult.IsSuccessful())
        {
            Console.WriteLine($"Failed to get file: {getResult.Errors.First()}");
        }
        else
        {
            var newByteArray = getResult.Value.ToArray();
            for (var i = 0; i < packetBytes.Length; i++)
            {
                if (packetBytes[i] != newByteArray[i])
                {
                    Console.WriteLine($"Byte {i} is different");
                }
            }

            if (packetBytes.Length != newByteArray.Length)
            {
                Console.WriteLine("Byte arrays are different sizes");
            }
        }
        
        Console.WriteLine("done");
        Console.ReadLine();
    }
}
