public class Globals 
{
    public const string MajorVersion = "0.5.4";
    public static int BuildNumber { get; set; }

    public static string Version => MajorVersion + "." + BuildNumber;

    public static string Copyright => $"Copyright ${DateTime.Now.Year} - John Andrews";
}