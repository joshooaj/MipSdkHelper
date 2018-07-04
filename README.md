# MipSdkHelper

This repo is a personal project started out of a desire to wrap some of the complexities of Milestone MIP SDK usage into a package that is easier (for me) to use. Nothing in this project is necessary - everything here can simply be done on your own in your MIP SDK integrations. But if you're like me, you create numerous projects and want to minimize the amount of boilerplate used to do things like logging in, or creating ConfigApi WCF proxy clients.

## Getting Started

Before this library can be any use to you, you'll need to register with Milestone Systems as a Milestone Solution Partner and gain access to the MIP SDK (Milestone Integration Platform Software Development Kit). Once you have access to the MIP SDK, and just as important - the MIP SDK documentation, you will need to add references at a minimum:
- Nuget Package "ConfigApiSharp" version 1.1.0 or greater (automatically added if you add the MipSdkHelper nuget)
- C:\Program Files\Milestone\MIPSDK\Bin\VideoOS.Platform
- C:\Program Files\Milestone\MIPSDK\Bin\VideoOS.Platform.SDK

Below is a sample console application where you can supply the login parameters as arguments and simply print the Management Server information to console.

```C#
using MipSdkHelper;
using System;

namespace SampleConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var client = new MipSdkClient(new Uri("http://localhost")))
            {
                var loginResult = client.Login();
                if (!loginResult.Success) throw loginResult.Exception;

                var properties = client.ConfigApiClient.GetItem("/").Properties;
                foreach (var kvp in properties)
                {
                    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to continue. . .");
            Console.ReadKey();
        }
    }
}
```
