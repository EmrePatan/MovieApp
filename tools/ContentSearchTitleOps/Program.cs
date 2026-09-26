using MovieApp.ContentSearchTitleOps;

try
{
    Environment.ExitCode = await ContentSearchTitleOpsCli.RunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
