// LLM maintained.
//
// Assembly-level attributes.
//
// SupportedOSPlatform for the same reason the backend carries it, and it is worth stating separately
// here because the smoke test's relationship to the claim is different: the backend's calls into
// TerraFX *are* its implementation, while this project only drives them - and it already gates the
// whole of Main behind OperatingSystem.IsWindows. Without the attribute every one of those calls
// raises CA1416, so the warnings would be a hundred lines of noise in a project whose entire output
// is a pass/fail report.
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]
