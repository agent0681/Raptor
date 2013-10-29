﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;

namespace Raptor.Api
{
	/// <summary>
	/// The Raptor API.
	/// </summary>
	public static class ClientApi
	{
		/// <summary>
		/// The Raptor version.
		/// </summary>
		public static readonly Version ApiVersion = new Version(1, 0);
		/// <summary>
		/// Gets the Terraria.Main instance.
		/// </summary>
		public static Main Main
		{
			get;
			internal set;
		}
		static List<TerrariaPlugin> plugins = new List<TerrariaPlugin>();

		/// <summary>
		/// Gets the list of loaded plugins.
		/// </summary>
		public static ReadOnlyCollection<TerrariaPlugin> Plugins
		{
			get { return new ReadOnlyCollection<TerrariaPlugin>(plugins); }
		}

		internal static void DeInitialize()
		{
			Raptor.DeInitialize();
			Log.LogNotice("Raptor v{0} stopped.\n", ApiVersion);

			#region Dispose plugins
			foreach (TerrariaPlugin plugin in plugins)
			{
				try
				{
					plugin.Dispose();
				}
				catch (Exception ex)
				{
					Log.LogError("Plugin \"{0}\" failed to dispose:", plugin.Name, ex);
					Log.LogError(ex.ToString());
				}
			}
			#endregion
		}
		internal static void Initialize()
		{
			Log.LogNotice("Raptor v{0} started.", ApiVersion);

			#region Load plugins
			var loadedAssemblies = new Dictionary<string, Assembly>();
			foreach (string path in Directory.EnumerateFiles("Plugins", "*.dll"))
			{
				string fileName = Path.GetFileNameWithoutExtension(path);

				try
				{
					Assembly assembly;
					if (!loadedAssemblies.TryGetValue(fileName, out assembly))
					{
						assembly = Assembly.Load(File.ReadAllBytes(path));
						loadedAssemblies.Add(fileName, assembly);
					}

					foreach (Type type in assembly.GetExportedTypes())
					{
						if (!type.IsSubclassOf(typeof(TerrariaPlugin)) || !type.IsPublic || type.IsAbstract)
							continue;

						object[] customAttributes = type.GetCustomAttributes(typeof(ApiVersionAttribute), false);
						if (customAttributes.Length == 0)
						{
							Log.LogError("Plugin \"{0}\" has no API version and was ignored.", type.FullName);
							continue;
						}

						Version apiVersion = ((ApiVersionAttribute)customAttributes[0]).ApiVersion;
						if (apiVersion.Major != ApiVersion.Major || apiVersion.Minor != ApiVersion.Minor)
						{
							Log.LogError("Plugin \"{0}\" is designed for a different API version ({1}) and was ignored.",
								type.FullName, apiVersion.ToString(2));
							continue;
						}

						try
						{
							plugins.Add((TerrariaPlugin)Activator.CreateInstance(type));
						}
						catch (Exception ex)
						{
							Log.LogError("Could not create an instance of plugin \"{0}\":", type.FullName);
							Log.LogError(ex.ToString());
						}
					}
				}
				catch (Exception ex)
				{
					Log.LogError("Failed to load assembly \"{0}\":", fileName);
					Log.LogError(ex.ToString());
				}
			}
			#endregion
			#region Initialize plugins
			var orderedPluginSelector =
				from p in Plugins
				orderby p.Order, p.Name
				select p;

			foreach (TerrariaPlugin plugin in orderedPluginSelector)
			{
				try
				{
					plugin.Initialize();
					Log.LogNotice("Plugin \"{0}\" v{1} (by {2}) initiated.", plugin.Name, plugin.Version, plugin.Author);
				}
				catch (Exception ex)
				{
					Log.LogError("Plugin \"{0}\" failed to initialize:", plugin.Name);
					Log.LogError(ex.ToString());
				}
			}
			#endregion
		}
	}
}