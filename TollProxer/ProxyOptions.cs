namespace TollProxer;

public class ProxyOptions
{
    public string PassHeaders { get; set; } = "";
    
    public string AllowedDestinationHosts { get; set; } = "";

    private HashSet<string>? passHeaderSet;

    public HashSet<string> PassHeaderSet =>
        passHeaderSet ??= new HashSet<string>(
            PassHeaders.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase
        );


    private HashSet<string>? allowedDestinationHostSet;

    public HashSet<string> AllowedDestinationHostSet =>
        allowedDestinationHostSet ??= new HashSet<string>(
            AllowedDestinationHosts.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase
        );
}