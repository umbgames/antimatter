using Newtonsoft.Json.Linq;

namespace Antimatter
{
    internal static class ConsoleToolImplementations
    {
        internal static AgentToolResult ReadConsoleErrors(JObject args)
        {
            int maxCount = args["maxCount"]?.Value<int>() ?? 30;
            var errors = ProjectObserver.GetConsoleErrors(maxCount);

            if (errors.Count == 0)
                return AgentToolResult.Ok("No console errors found.");

            return AgentToolResult.Ok($"{errors.Count} errors:\n" + string.Join("\n", errors));
        }
    }
}
