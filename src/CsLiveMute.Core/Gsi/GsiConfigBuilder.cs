using System.Text;
using CsLiveMute.Core.Models;

namespace CsLiveMute.Core.Gsi;

public static class GsiConfigBuilder
{
    public static string Build(AppSettings settings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("\"cs_live_mute\"");
        builder.AppendLine("{");
        builder.AppendLine($"  \"uri\"           \"http://127.0.0.1:{settings.Port}/gsi\"");
        builder.AppendLine("  \"timeout\"       \"5.0\"");
        builder.AppendLine("  \"buffer\"        \"0.1\"");
        builder.AppendLine("  \"throttle\"      \"0.1\"");
        builder.AppendLine("  \"heartbeat\"     \"10.0\"");
        builder.AppendLine("  \"auth\"");
        builder.AppendLine("  {");
        builder.AppendLine($"    \"token\"       \"{settings.AuthToken}\"");
        builder.AppendLine("  }");
        builder.AppendLine("  \"data\"");
        builder.AppendLine("  {");
        builder.AppendLine("    \"provider\"    \"1\"");
        builder.AppendLine("    \"map\"         \"1\"");
        builder.AppendLine("    \"round\"       \"1\"");
        builder.AppendLine("  }");
        builder.AppendLine("}");
        return builder.ToString();
    }
}
