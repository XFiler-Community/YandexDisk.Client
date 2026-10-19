using CommandLine;
using CommandLine.Text;
using System;
using System.Threading.Tasks;
using XFiler.YandexDisk.Client.Http;

namespace YandexDisk.Client.CLI
{
    public static class YandexDiskCliProgram
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                ParserResult<object> parseResult = Parser.Default.ParseArguments<UploadOptions, DownloadOptions>(args);

                await parseResult.WithParsedAsync<DownloadOptions>(async options =>
                {
                    using DiskHttpApi disk = CreateDiskClient(options);
                    await new Download(disk).ExecuteAsync(options);
                });

                await parseResult.WithParsedAsync<UploadOptions>(async options =>
                {
                    using DiskHttpApi disk = CreateDiskClient(options);
                    await new Upload(disk).ExecuteAsync(options);
                });

                if (parseResult.Tag == ParserResultType.NotParsed)
                {
                    Console.WriteLine(HelpText.AutoBuild(parseResult));
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return -1;
            }
        }

        private static DiskHttpApi CreateDiskClient(OptionsBase options)
        {
            if (string.IsNullOrWhiteSpace(options.AccessToken))
            {
                throw new ArgumentException("AccessToken is not defined.");
            }

            return new DiskHttpApi(options.AccessToken);
        }
    }
}
