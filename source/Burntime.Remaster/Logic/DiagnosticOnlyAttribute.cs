using System;

namespace Burntime.Remaster.Logic
{
    /// <summary>
    /// Marks a property intended only for diagnostics such as debugging and telemetry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class DiagnosticOnlyAttribute : Attribute
    {
    }
}
