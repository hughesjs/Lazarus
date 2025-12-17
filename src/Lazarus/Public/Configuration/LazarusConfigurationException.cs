namespace Lazarus.Public.Configuration;

/// <summary>
/// Exception thrown when there is an error in the Lazarus service configuration.
/// </summary>
/// <remarks>
/// This exception is typically thrown during service registration when invalid
/// configuration is detected, such as attempting to register duplicate services.
/// </remarks>
public class LazarusConfigurationException: Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LazarusConfigurationException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the configuration error.</param>
    public  LazarusConfigurationException(string message) : base(message) {}
}
