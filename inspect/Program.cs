// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
using System; using System.Reflection; class P{static void Main(){var t=Type.GetType( Radzen.TreeItemContextMenuEventArgs, Radzen.Blazor"); if(t==null){Console.WriteLine("type null); return;} foreach(var p in t.GetProperties()){Console.WriteLine(p.Name+ : +p.PropertyType);} } }
