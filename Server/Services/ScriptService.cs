using FileFlows.Plugin;
using FileFlows.Server.Controllers;
using FileFlows.ServerShared.Services;
using FileFlows.Shared.Models;

namespace FileFlows.Server.Services;

/// <summary>
/// A service used to get script data from the FileFlows server
/// </summary>
public class ScriptService:IScriptService
{
    /// <summary>
    /// Get all scripts
    /// </summary>
    /// <returns>a collection of scripts</returns>
    public Task<IEnumerable<Script>> GetScripts() => new ScriptController().GetAll();
    
    /// <summary>
    /// Get a script
    /// </summary>
    /// <param name="name">The name of the script</param>
    /// <returns>the script</returns>
    public Task<Script> Get(string name) => new ScriptController().Get(name);

    /// <summary>
    /// Gets or sets a function used to load new instances of the service
    /// </summary>
    /// <param name="name">The name of the script</param>
    /// <returns>the script code</returns>
    public Task<string> GetCode(string name) => new ScriptController().GetCode(name);
}