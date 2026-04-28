using System;
using System.Linq;
using System.Reflection;
using Azure.AI.Extensions.OpenAI;

var t = typeof(AgentReference);
Console.WriteLine($"TYPE: {t.FullName}");
foreach (var c in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderBy(c => c.GetParameters().Length))
{
    Console.WriteLine("ctor (" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
}
