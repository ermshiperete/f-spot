//
// main.cs
//
// Author:
//   Ruben Vermeersch <ruben@savanne.be>
//   Paul Lange <palango@gmx.de>
//   Evan Briones <erbriones@gmail.com>
//   Stephane Delcroix <stephane@delcroix.org>
//
// Copyright (C) 2006-2010 Novell, Inc.
// Copyright (C) 2010 Ruben Vermeersch
// Copyright (C) 2010 Paul Lange
// Copyright (C) 2010 Evan Briones
// Copyright (C) 2006-2009 Stephane Delcroix
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;

using Mono.Unix;
using Mono.Addins;
using Mono.Addins.Setup;

using FSpot.Core;
using FSpot.Utils;

using Hyena;
using Hyena.CommandLine;
using Hyena.Gui;

namespace FSpot
{
	public static class Driver
	{
		private static void ShowVersion ()
		{
			Console.WriteLine ("F-Spot {0}", Defines.VERSION);
			Console.WriteLine ("http://f-spot.org");
			Console.WriteLine ("\t(c)2003-2009, Novell Inc");
			Console.WriteLine ("\t(c)2009 Stephane Delcroix");
			Console.WriteLine("Personal photo management for the GNOME Desktop");
		}

		private static void ShowAssemblyVersions ()
		{
			ShowVersion ();
			Console.WriteLine ();
			Console.WriteLine ("Mono/.NET Version: " + Environment.Version.ToString ());
			Console.WriteLine (String.Format ("{0}Assembly Version Information:", Environment.NewLine));

			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies ())
			{
				AssemblyName name = asm.GetName ();
				Console.WriteLine ("\t" + name.Name + " (" + name.Version.ToString () + ")");
			}
		}

		private static void ShowHelp ()
		{
			Console.WriteLine ("Usage: f-spot [options...] [files|URIs...]");
			Console.WriteLine ();

			Hyena.CommandLine.Layout commands = new Hyena.CommandLine.Layout (
				new LayoutGroup ("help", "Help Options",
					new LayoutOption ("help", "Show this help"),
					new LayoutOption ("help-options", "Show command line options"),
					new LayoutOption ("help-all", "Show all options"),
					new LayoutOption ("version", "Show version information"),
					new LayoutOption ("versions", "Show detailed version information")),
				new LayoutGroup ("options", "General options",
					new LayoutOption ("basedir=DIR", "Path to the photo database folder"),
					new LayoutOption ("import=URI", "Import from the given uri"),
					new LayoutOption ("photodir=DIR", "Default import folder"),
					new LayoutOption ("view ITEM", "View file(s) or directories"),
					new LayoutOption ("shutdown", "Shut down a running instance of F-Spot"),
					new LayoutOption ("slideshow", "Display a slideshow"),
					new LayoutOption ("debug", "Run in debug mode")));

			if (ApplicationContext.CommandLine.Contains ("help-all")) {
				Console.WriteLine (commands);
				return;
			}

			List<string> errors = null;
			foreach (KeyValuePair<string, string> argument in ApplicationContext.CommandLine.Arguments) {
				switch (argument.Key) {
					case "help": Console.WriteLine (commands.ToString ("help")); break;
					case "help-options": Console.WriteLine (commands.ToString ("options")); break;
					default:
						if (argument.Key.StartsWith ("help")) {
							(errors ?? (errors = new List<string> ())).Add (argument.Key);
						}
						break;
				}
			}

			if (errors != null) {
				Console.WriteLine (commands.LayoutLine (String.Format (
					"The following help arguments are invalid: {0}",
					Hyena.Collections.CollectionExtensions.Join (errors, "--", null, ", "))));
			}
		}

		static string [] FixArgs (string [] args)
		{
			// Makes sure command line arguments are parsed backwards compatible.
			var outargs = new List<string> ();
			for (int i = 0; i < args.Length; i++) {
				switch (args [i]) {
					case "-h": case "-help": case "-usage":
						outargs.Add ("--help");
						break;
					case "-V": case "-version":
						outargs.Add ("--version");
						break;
					case "-versions":
						outargs.Add ("--versions");
						break;
					case "-shutdown":
						outargs.Add ("--shutdown");
						break;
					case "-b": case "-basedir": case "--basedir":
						outargs.Add ("--basedir=" + (i + 1 == args.Length ? String.Empty : args [++i]));
						break;
					case "-p": case "-photodir": case "--photodir":
						outargs.Add ("--photodir=" + (i + 1 == args.Length ? String.Empty : args [++i]));
						break;
					case "-i": case "-import": case "--import":
						outargs.Add ("--import=" + (i + 1 == args.Length ? String.Empty : args [++i]));
						break;
					case "-v": case "-view":
						outargs.Add ("--view");
						break;
					case "-slideshow":
						outargs.Add ("--slideshow");
						break;
					default:
						outargs.Add (args [i]);
						break;
				}
			}
			return outargs.ToArray ();
		}

		static int Main (string [] args)
		{
			args = FixArgs (args);

			ApplicationContext.ApplicationName = "F-Spot";
			ApplicationContext.TrySetProcessName (Defines.PACKAGE);

			Paths.ApplicationName = "f-spot";
			ThreadAssist.InitializeMainThread ();
			ThreadAssist.ProxyToMainHandler = RunIdle;

			// Options and Option parsing
			bool shutdown = false;
			bool view = false;
			bool slideshow = false;
			bool import = false;

			GLib.GType.Init ();
			Catalog.Init ("f-spot", Defines.LOCALE_DIR);

			FSpot.Core.Global.PhotoUri = new SafeUri (Preferences.Get<string> (Preferences.STORAGE_PATH));

			ApplicationContext.CommandLine = new CommandLineParser (args, 0);

			if (ApplicationContext.CommandLine.ContainsStart ("help")) {
				ShowHelp ();
				return 0;
			}

			if (ApplicationContext.CommandLine.Contains ("version")) {
				ShowVersion ();
				return 0;
			}

			if (ApplicationContext.CommandLine.Contains ("versions")) {
				ShowAssemblyVersions ();
				return 0;
			}

			if (ApplicationContext.CommandLine.Contains ("shutdown")) {
				Log.Information ("Shutting down existing F-Spot server...");
				shutdown = true;
			}

			if (ApplicationContext.CommandLine.Contains ("slideshow")) {
				Log.Information ("Running F-Spot in slideshow mode.");
				slideshow = true;
			}

			if (ApplicationContext.CommandLine.Contains ("basedir")) {
				string dir = ApplicationContext.CommandLine ["basedir"];

				if (!string.IsNullOrEmpty (dir))
				{
					FSpot.Core.Global.BaseDirectory = dir;
					Log.InformationFormat ("BaseDirectory is now {0}", dir);
				} else {
					Log.Error ("f-spot: -basedir option takes one argument");
					return 1;
				}
			}

			if (ApplicationContext.CommandLine.Contains ("photodir")) {
				string dir = ApplicationContext.CommandLine ["photodir"];

				if (!string.IsNullOrEmpty (dir))
				{
					FSpot.Core.Global.PhotoUri = new SafeUri (dir);
					Log.InformationFormat ("PhotoDirectory is now {0}", dir);
				} else {
					Log.Error ("f-spot: -photodir option takes one argument");
					return 1;
				}
			}

			if (ApplicationContext.CommandLine.Contains ("import"))
				import = true;

			if (ApplicationContext.CommandLine.Contains ("view"))
				view = true;

			if (ApplicationContext.CommandLine.Contains ("debug")) {
				Log.Debugging = true;
				// Debug GdkPixbuf critical warnings
				GLib.LogFunc logFunc = new GLib.LogFunc (GLib.Log.PrintTraceLogFunction);
				GLib.Log.SetLogHandler ("GdkPixbuf", GLib.LogLevelFlags.Critical, logFunc);

				// Debug Gtk critical warnings
				GLib.Log.SetLogHandler ("Gtk", GLib.LogLevelFlags.Critical, logFunc);

				// Debug GLib critical warnings
				GLib.Log.SetLogHandler ("GLib", GLib.LogLevelFlags.Critical, logFunc);

				//Debug GLib-GObject critical warnings
				GLib.Log.SetLogHandler ("GLib-GObject", GLib.LogLevelFlags.Critical, logFunc);

				GLib.Log.SetLogHandler ("GLib-GIO", GLib.LogLevelFlags.Critical, logFunc);
			}

			// Validate command line options
			if ((import && (view || shutdown || slideshow)) ||
				(view && (shutdown || slideshow)) ||
				(shutdown && slideshow))
			{
				Log.Error ("Can't mix -import, -view, -shutdown or -slideshow");
				return 1;
			}

			InitializeAddins ();

			// Gtk initialization
			Gtk.Application.Init (Defines.PACKAGE, ref args);
			// Maybe we'll add this at a future date
			//Xwt.Application.InitializeAsGuest (Xwt.ToolkitType.Gtk);

			// init web proxy globally
			Platform.WebProxy.Init ();

			if (File.Exists (Preferences.Get<string> (Preferences.GTK_RC))) {
				if (File.Exists (Path.Combine (FSpot.Core.Global.BaseDirectory, "gtkrc")))
					Gtk.Rc.AddDefaultFile (Path.Combine (FSpot.Core.Global.BaseDirectory, "gtkrc"));

				FSpot.Core.Global.DefaultRcFiles = Gtk.Rc.DefaultFiles;
				Gtk.Rc.AddDefaultFile (Preferences.Get<string> (Preferences.GTK_RC));
				Gtk.Rc.ReparseAllForSettings (Gtk.Settings.Default, true);
			}

			try {
				Gtk.Window.DefaultIconList = new Gdk.Pixbuf [] {
					GtkUtil.TryLoadIcon (FSpot.Core.Global.IconTheme, "f-spot", 16, (Gtk.IconLookupFlags)0),
					GtkUtil.TryLoadIcon (FSpot.Core.Global.IconTheme, "f-spot", 22, (Gtk.IconLookupFlags)0),
					GtkUtil.TryLoadIcon (FSpot.Core.Global.IconTheme, "f-spot", 32, (Gtk.IconLookupFlags)0),
					GtkUtil.TryLoadIcon (FSpot.Core.Global.IconTheme, "f-spot", 48, (Gtk.IconLookupFlags)0)
				};
			} catch {}

			CleanRoomStartup.Startup (Startup);

			// Running threads are preventing the application from quitting
			// we force it for now until this is fixed
			System.Environment.Exit (0);
			return 0;
		}

		static void InitializeAddins ()
		{
			uint timer = Log.InformationTimerStart ("Initializing Mono.Addins");
			try {
				UpdatePlugins ();
			} catch (Exception) {
				Log.Debug ("Failed to initialize plugins, will remove addin-db and try again.");
				ResetPluginDb ();
			}
			SetupService setupService = new SetupService (AddinManager.Registry);
			foreach (AddinRepository repo in setupService.Repositories.GetRepositories ()) {
				if (repo.Url.StartsWith ("http://addins.f-spot.org/")) {
					Log.InformationFormat ("Unregistering {0}", repo.Url);
					setupService.Repositories.RemoveRepository (repo.Url);
				}
			}
			Log.DebugTimerPrint (timer, "Mono.Addins Initialization took {0}");
		}

		static void UpdatePlugins ()
		{
			AddinManager.Initialize (FSpot.Core.Global.BaseDirectory);
			AddinManager.Registry.Update (null);
		}

		static void ResetPluginDb ()
		{
			// Nuke addin-db
			var directory = GLib.FileFactory.NewForUri (new SafeUri (FSpot.Core.Global.BaseDirectory));
			var list = directory.EnumerateChildren ("standard::name", GLib.FileQueryInfoFlags.None, null);
			foreach (GLib.FileInfo info in list) {
				if (info.Name.StartsWith ("addin-db-")) {
					var file = GLib.FileFactory.NewForPath (Path.Combine (directory.Path, info.Name));
					file.DeleteRecursive ();
				}
			}

			// Try again
			UpdatePlugins ();
		}

		static void Startup ()
		{
			if (ApplicationContext.CommandLine.Contains ("slideshow"))
				App.Instance.Slideshow (null);
			else if (ApplicationContext.CommandLine.Contains ("shutdown"))
				App.Instance.Shutdown ();
			else if (ApplicationContext.CommandLine.Contains ("view")) {
				if (ApplicationContext.CommandLine.Files.Count == 0) {
					Log.Error ("f-spot: -view option takes at least one argument");
					System.Environment.Exit (1);
				}

				var list = new UriList ();

				foreach (var f in ApplicationContext.CommandLine.Files)
					list.AddUnknown (f);

				if (list.Count == 0) {
					ShowHelp ();
					System.Environment.Exit (1);
				}

				App.Instance.View (list);
			} else if (ApplicationContext.CommandLine.Contains ("import")) {
				string dir = ApplicationContext.CommandLine ["import"];

				if (string.IsNullOrEmpty (dir)) {
					Log.Error ("f-spot: -import option takes one argument");
					System.Environment.Exit (1);
				}

				App.Instance.Import (dir);
			} else
				App.Instance.Organize ();

			if (!App.Instance.IsRunning)
				Gtk.Application.Run ();
		}

		public static void RunIdle (InvokeHandler handler)
		{
			GLib.Idle.Add (delegate { handler (); return false; });
		}
	}
}
