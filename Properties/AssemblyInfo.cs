using Interstellar;
using System.Reflection;

[assembly: AssemblyCompany("Interstellar Software")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyFileVersion(PluginInfo.Version + ".0")]
[assembly: AssemblyInformationalVersion(PluginInfo.Version)]
[assembly: AssemblyVersion(PluginInfo.Version + ".0")]

#if DEBUG
[assembly: AssemblyProduct(PluginInfo.Name + " [Debug]")]
[assembly: AssemblyTitle(PluginInfo.Name + " [Debug]")]
#else
[assembly: AssemblyProduct(PluginInfo.Name)]
[assembly: AssemblyTitle(PluginInfo.Name)]
#endif

[assembly: AssemblyDescription(PluginInfo.Description)]
