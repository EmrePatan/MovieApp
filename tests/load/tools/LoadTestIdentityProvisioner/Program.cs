using MovieApp.LoadTestIdentityProvisioner;

try
{
    Environment.ExitCode = await ProvisionerCli.RunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
