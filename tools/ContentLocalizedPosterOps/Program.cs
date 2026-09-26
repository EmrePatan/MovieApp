using MovieApp.ContentLocalizedPosterOps;

try
{
    Environment.ExitCode = await ContentLocalizedPosterOpsCli.RunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
