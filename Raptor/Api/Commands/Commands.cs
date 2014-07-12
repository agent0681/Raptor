﻿//  Raptor - a client API for Terraria
//  Copyright (C) 2013-2014 MarioE
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json;
using Terraria;

namespace Raptor.Api.Commands
{
	/// <summary>
	/// Contains raptor's commands.
	/// </summary>
	public static class Commands
	{
		static List<Command> ChatCommands = new List<Command>();
		static List<Command> LuaCommands = new List<Command>();

		/// <summary>
		/// Deregisters a command.
		/// </summary>
		/// <param name="commandName">The command name.</param>
		public static void Deregister(string commandName)
		{
			ChatCommands.RemoveAll(c => String.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));
		}
		/// <summary>
		/// Executes a string as a command.
		/// </summary>
		/// <param name="text">The command text.</param>
		public static void Execute(string text)
		{
			var args = new CommandEventArgs(ParseParameters(text));

			string commandName = args[-1].ToLowerInvariant();
			Command command = ChatCommands.Concat(LuaCommands).FirstOrDefault(c => c.Names.Contains(commandName));
			if (command != null)
				command.Invoke(args);
			else
				Utils.ErrorMessage("Invalid command.");
		}
		/// <summary>
		/// Finds a command.
		/// </summary>
		/// <param name="commandName">The command name.</param>
		/// <returns>The command.</returns>
		public static Command Find(string commandName)
		{
			return ChatCommands.FirstOrDefault(c => String.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));
		}
		internal static void Initialize()
		{
			ChatCommands.Add(new Command(Aliases, "aliases")
			{
				HelpText = new[]
				{
					"Syntax: /aliases <command name>",
					"Prints a list of a command's aliases.",
				}
			});
			ChatCommands.Add(new Command(Help, "help", "?")
			{
				HelpText = new[]
				{
					"Syntax: /help (command name)",
					"Prints a list of commands or help about a specific command."
				}
			});
			ChatCommands.Add(new Command(Keybind, "keybind", "kb")
			{
				HelpText = new[]
				{
					"Syntax: /keybind <add | clr | del | list> [arguments...]",
					"Manages key bindings, which are keys that, when pressed, execute a command.",
					"To add a key binding, use /kb add <key> <command>, without the first /.",
					"To clear all key bindings, use /kb clr.",
					"To delete a key binding, use /kb del <key>.",
					"To list all key bindings, use /kb list.",
				}
			});
			ChatCommands.Add(new Command(Reload, "reload")
			{
				HelpText = new[]
				{
					"Syntax: /reload",
					"Reloads various objects such as the configuration file and Lua script commands."
				}
			});
			ChatCommands.Add(new Command(Say, "say")
			{
				HelpText = new[]
				{
					"Syntax: /say <message>",
					"Sends a message to the server."
				}
			});
			ChatCommands.Add(new Command(Set, "set")
			{
				HelpText = new[]
				{
					"Syntax: /set OR /set <option> <value>",
					"To show the config file's options, use /set.",
					"To edit the config file's options, use /set <option> <value>."
				}
			});

			LoadLuaCommands();
		}
		static void LoadLuaCommands()
		{
			foreach (string path in Directory.EnumerateFiles("Scripts", "*.lua"))
			{
				if (path.EndsWith("startup.lua", StringComparison.OrdinalIgnoreCase))
					continue;

				List<string> lines = File.ReadAllLines(path).ToList();
				var names = new List<string> { Path.GetFileNameWithoutExtension(path) };

				var aliases = from s in lines
							  where s.StartsWith("-- Aliases: ")
							  select s.Substring(12);
				if (aliases.Any())
					names.AddRange(aliases.ElementAt(0).Split(','));

				var command = new Command(LuaCommand, names.ToArray());

				var helpTexts = from s in lines
								where s.StartsWith("-- HelpText: ")
								select s.Substring(13);
				if (helpTexts.Any())
					command.HelpText = helpTexts.ToArray();

				LuaCommands.Add(command);
			}
		}
		/// <summary>
		/// Parses parameters from an input string.
		/// </summary>
		/// <param name="str">The input.</param>
		/// <returns>The parsed parameters.</returns>
		public static List<string> ParseParameters(string str)
		{
			var parameters = new List<string>();
			var sb = new StringBuilder();

			bool quote = false;
			for (int i = 0; i < str.Length; i++)
			{
				char c = str[i];

				if (c == '\\' && ++i < str.Length)
				{
					if (str[i] != '"' && str[i] != ' ' && str[i] != '\\')
						sb.Append('\\');
					sb.Append(str[i]);
				}
				else if (c == '"')
				{
					quote = !quote;
					if (!quote || sb.Length > 0)
					{
						parameters.Add(sb.ToString());
						sb.Clear();
					}
				}
				else if (Char.IsWhiteSpace(c) && !quote)
				{
					if (sb.Length > 0)
					{
						parameters.Add(sb.ToString());
						sb.Clear();
					}
				}
				else
					sb.Append(c);
			}
			if (sb.Length > 0)
				parameters.Add(sb.ToString());
			return parameters;
		}
		/// <summary>
		/// Registers a command.
		/// </summary>
		/// <param name="command">The command.</param>
		public static void Register(Command command)
		{
			if (command == null)
				throw new ArgumentNullException("command");
			if (ChatCommands.Any(c => String.Equals(c.Name, command.Name, StringComparison.OrdinalIgnoreCase)))
				throw new ArgumentException("Can't register another command with the same name.");

			ChatCommands.Add(command);
		}

		static void Aliases(object o, CommandEventArgs e)
		{
			if (e.Length == 0)
			{
				Utils.ErrorMessage("Syntax: /{0} <command name>", e[-1]);
				return;
			}

			string commandName = e[0].ToLowerInvariant();
			Command command = ChatCommands.Concat(LuaCommands).FirstOrDefault(c => c.Names.Contains(commandName));

			if (command != null)
			{
				Utils.SuccessMessage("/{0} aliases:", command.Name);
				Utils.InfoMessage(String.Join(", ", command.Names.Select(s => "/" + s)));
			}
			else
				Utils.ErrorMessage("Invalid command \"{0}\".", commandName);
		}
		static void Help(object o, CommandEventArgs e)
		{
			if (e.Length == 0)
			{
				Utils.SuccessMessage("Raptor commands: ");
				Utils.InfoMessage(String.Join(", ", ChatCommands.Concat(LuaCommands).Select(c => "/" + c.Name)));
				return;
			}

			string commandName = e[0].ToLowerInvariant();
			Command command = ChatCommands.Concat(LuaCommands).FirstOrDefault(c => c.Names.Contains(commandName));

			if (command != null)
			{
				Utils.SuccessMessage("/{0} help:", command.Name);
				foreach (string line in command.HelpText)
					Utils.InfoMessage("{0}", line);
			}
			else
				Utils.ErrorMessage("Invalid command \"{0}\".", commandName);
		}
		static void Keybind(object o, CommandEventArgs e)
		{
			string subcommand = e.Length > 0 ? e[0].ToLowerInvariant() : "help";

			switch (subcommand)
			{
				case "add":
					{
						if (e.Length < 3)
						{
							Utils.ErrorMessage("Syntax: /{0} {1} <key combination> <command>", e[-1], e[0]);
							return;
						}

						string key = e[1];
						var keybind = new Input.Keybind();
						while (!Utils.TryParseXNAKey(key, out keybind.Key))
						{
							if (String.IsNullOrEmpty(key))
							{
								Utils.ErrorMessage("Invalid keybind '{0}'!", e[0]);
								return;
							}

							switch (key[0])
							{
								case '!':
									keybind.Alt = true;
									break;
								case '^':
									keybind.Control = true;
									break;
								case '+':
									keybind.Shift = true;
									break;
								default:
									Utils.ErrorMessage("Invalid keybind modifier '{0}'!", key[0]);
									return;
							}
							key = key.Substring(1);
						}

						if (Raptor.Config.Keybinds.ContainsKey(keybind))
							Raptor.Config.Keybinds[keybind].Add(e.Eol(2));
						else
							Raptor.Config.Keybinds.Add(keybind, new List<string> { e.Eol(2) });
						string configPath = "raptor.config";
						File.WriteAllText(configPath, JsonConvert.SerializeObject(Raptor.Config, Formatting.Indented));
						Utils.SuccessMessage("Bound the key '{0}' to '{1}'.", e.Eol(2), e[1]);
					}
					return;
				case "clr":
				case "clear":
					{
						Raptor.Config.Keybinds.Clear();
						string configPath = "raptor.config";
						File.WriteAllText(configPath, JsonConvert.SerializeObject(Raptor.Config, Formatting.Indented));
						Utils.SuccessMessage("Cleared all key bindings.");
					}
					return;
				case "del":
				case "delete":
				case "remove":
					{
						if (e.Length == 1)
						{
							Utils.ErrorMessage("Syntax: /{0} {1} <key>", e[-1], e[0]);
							return;
						}

						string key = e[1];
						var keybind = new Input.Keybind();
						while (!Utils.TryParseXNAKey(key, out keybind.Key))
						{
							if (String.IsNullOrEmpty(key))
							{
								Utils.ErrorMessage("Invalid keybind '{0}'!", e[0]);
								return;
							}

							switch (key[0])
							{
								case '!':
									keybind.Alt = true;
									break;
								case '^':
									keybind.Control = true;
									break;
								case '+':
									keybind.Shift = true;
									break;
								default:
									Utils.ErrorMessage("Invalid keybind modifier '{0}'!", key[0]);
									return;
							}
							key = key.Substring(1);
						}

						Raptor.Config.Keybinds.Remove(keybind);
						string configPath = "raptor.config";
						File.WriteAllText(configPath, JsonConvert.SerializeObject(Raptor.Config, Formatting.Indented));
						Utils.SuccessMessage("Removed all bindings to '{0}'.", e[1]);
					}
					return;
				case "list":
					Utils.SuccessMessage("Key Bindings:");
					foreach (var kv in Raptor.Config.Keybinds)
					{
						Utils.InfoMessage("Keybind '{0}':", Utils.ConvertToString(kv.Key.Key));
						foreach (var command in kv.Value)
							Utils.InfoMessage("  {0}", command);
					}
					return;
				case "help":
				default:
					Utils.SuccessMessage("/{0} help:", e[-1]);
					Utils.InfoMessage("/{0} add <key combination> <command> - Adds to a keybind.", e[-1]);
					Utils.InfoMessage("/{0} clr/clear - Clears all keybinds.", e[-1]);
					Utils.InfoMessage("/{0} del/delete/remove <key combination> - Deletes a keybind.", e[-1]);
					Utils.InfoMessage("/{0} list - Lists all keybinds.", e[-1]);
					return;
			}
		}
		static void LuaCommand(object o, CommandEventArgs e)
		{
			Raptor.Lua["args"] = e;
			ThreadPool.QueueUserWorkItem(c =>
			{
				try
				{
					Raptor.Lua.DoFile(Path.Combine("Scripts", ((Command)o).Name + ".lua"));
				}
				catch (Exception ex)
				{
					Utils.ErrorMessage(ex.ToString());
				}
			});
		}
		static void Reload(object o, CommandEventArgs e)
		{
			string configPath = "raptor.config";
			Raptor.Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));

			LuaCommands.Clear();
			LoadLuaCommands();

			Utils.SuccessMessage("Reloaded configuration file & scripts.");
		}
		static void Say(object o, CommandEventArgs e)
		{
			if (e.Length == 0)
			{
				Utils.SuccessMessage("Syntax: /{0} <message>", e[-1]);
				return;
			}

			if (Main.netMode == 1)
				NetMessage.SendData(25, -1, -1, e.Eol(0));
			else
				Main.NewText(String.Format("<{0}> {1}", Main.player[Main.myPlayer].name, e.Eol(0)));
		}
		static void Set(object o, CommandEventArgs e)
		{
			if (e.Length == 0 || String.Equals(e[0], "list", StringComparison.OrdinalIgnoreCase))
			{
				Utils.SuccessMessage("Config options:");
				foreach (FieldInfo fi in typeof(Config).GetFields(BindingFlags.Instance | BindingFlags.Public))
				{
					Type t = fi.FieldType;
					if (t != typeof(bool) && t != typeof(int) && t != typeof(string))
						continue;
					
					Utils.InfoMessage("{0}: {1} ({2})",
						fi.Name, fi.GetValue(Raptor.Config), ((DescriptionAttribute)fi.GetCustomAttributes(false)[0]).Description);
				}
			}
			else
			{
				FieldInfo fi = typeof(Config).GetField(e[0], BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

				if (fi == null)
				{
					Utils.ErrorMessage("Invalid option \"{0}\".", e[0]);
					return;
				}

				Type t = fi.FieldType;
				if (t != typeof(bool) && t != typeof(int) && t != typeof(string))
				{
					Utils.ErrorMessage("Invalid option \"{0}\".", e[0]);
					return;
				}

				if (e.Length == 1)
				{
					if (t == typeof(bool))
						Utils.ErrorMessage("Syntax: /{0} {1} <boolean>", e[-1], fi.Name);
					else if (t == typeof(int))
						Utils.ErrorMessage("Syntax: /{0} {1} <integer>", e[-1], fi.Name);
					else
						Utils.ErrorMessage("Syntax: /{0} {1} <string>", e[-1], fi.Name);
					return;
				}

				if (t == typeof(bool))
				{
					bool value;
					if (!bool.TryParse(e[1], out value))
					{
						Utils.ErrorMessage("Invalid value for option \"{0}\".", fi.Name);
						return;
					}

					fi.SetValue(Raptor.Config, value);
					Utils.SuccessMessage("Set option \"{0}\" to value \"{1}\".", fi.Name, value);
				}
				else if (t == typeof(int))
				{
					int value;
					if (!int.TryParse(e[1], out value) || value <= 0)
					{
						Utils.ErrorMessage("Invalid value for option \"{0}\".", fi.Name);
						return;
					}

					fi.SetValue(Raptor.Config, value);
					Utils.SuccessMessage("Set option \"{0}\" to value \"{1}\".", fi.Name, value);
				}
				else
				{
					string value = e.Eol(1);
					fi.SetValue(Raptor.Config, value);
					Utils.SuccessMessage("Set option \"{0}\" to value \"{1}\".", fi.Name, value);
				}

				string configPath = "raptor.config";
				File.WriteAllText(configPath, JsonConvert.SerializeObject(Raptor.Config, Formatting.Indented));
			}
		}
	}
}
