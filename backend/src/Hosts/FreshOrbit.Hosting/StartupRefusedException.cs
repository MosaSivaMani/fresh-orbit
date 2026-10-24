namespace FreshOrbit.Hosting;

/// <summary>
/// Thrown by a hosted service's start when the host must not run (rule 7), for example the Demo Api on a database
/// the demo did not create. <see cref="HostRunner"/> turns it into one Critical log line and exit code 1.
/// </summary>
public sealed class StartupRefusedException : Exception
{
    public StartupRefusedException()
    {
    }

    public StartupRefusedException(string message)
        : base(message)
    {
    }

    public StartupRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
